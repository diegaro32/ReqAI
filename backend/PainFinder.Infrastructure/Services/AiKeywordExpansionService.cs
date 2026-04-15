using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Services;

/// <summary>
/// Multi-layer keyword expansion engine for pain discovery.
///
/// Layer 1  – Concept Normalization   : synonyms, category, industry context
/// Layer 2  – Human Language Expansion: how real LATAM users actually say it
/// Layer 3  – Problem Framing         : terms → concrete problems
/// Layer 4  – Pain Query Generation   : emotional, ready-to-scrape queries
/// Layer 5  – Intent Segmentation     : pain / solution / context
/// Layer 6  – Prioritization          : top queries by pain intensity + economic signal
///
/// Implemented as a single structured Gemini call (Chain-of-Thought prompt).
/// The model is forced to reason through all 6 layers before producing the final output.
/// </summary>
public class AiKeywordExpansionService(
    IChatClient chatClient,
    ILogger<AiKeywordExpansionService> logger) : IKeywordExpansionService
{
    private const int BatchSize = 8;
    private const int MaxTotal  = 40;
    private const int TopQueriesCount = 5;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Layer output records ─────────────────────────────────────────────────

    private sealed record Layer1(
        [property: JsonPropertyName("category")]        string?       Category,
        [property: JsonPropertyName("synonyms")]        List<string>? Synonyms,
        [property: JsonPropertyName("industryContext")] string?       IndustryContext);

    private sealed record Layer2(
        [property: JsonPropertyName("humanTerms")]      List<string>? HumanTerms,
        [property: JsonPropertyName("phrases")]         List<string>? Phrases);

    private sealed record Layer3(
        [property: JsonPropertyName("problemFrames")]   List<string>? ProblemFrames);

    private sealed record LayeredExpansion(
        [property: JsonPropertyName("layer1")]          Layer1?       Layer1,
        [property: JsonPropertyName("layer2")]          Layer2?       Layer2,
        [property: JsonPropertyName("layer3")]          Layer3?       Layer3,
        [property: JsonPropertyName("painQueries")]     List<string>? PainQueries,
        [property: JsonPropertyName("solutionQueries")] List<string>? SolutionQueries,
        [property: JsonPropertyName("contextQueries")]  List<string>? ContextQueries,
        [property: JsonPropertyName("topQueries")]      List<string>? TopQueries);

    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> ExpandKeywordAsync(
        string keyword, CancellationToken cancellationToken = default)
    {
        var normalized = keyword.Trim().ToLowerInvariant();

        if (_cache.TryGetValue(normalized, out var cached))
        {
            logger.LogInformation("KeywordExpansion: Cache hit for '{Keyword}' ({Count} queries)", keyword, cached.Count);
            return cached;
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, BuildPrompt(normalized))
            };

            logger.LogInformation("KeywordExpansion: Calling Gemini (6-layer engine) for '{Keyword}'...", keyword);

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseText = response.Messages[^1].Text?.Trim() ?? "{}";

            var fence = new string('`', 3);
            if (responseText.StartsWith(fence))
                responseText = responseText.Replace(fence + "json", "").Replace(fence, "").Trim();

            var expanded = JsonSerializer.Deserialize<LayeredExpansion>(responseText, JsonOpts);

            LogLayers(keyword, expanded);

            var result = FlattenAndRank(expanded, normalized)
                .ToList()
                .AsReadOnly();

            // Only cache non-empty results — empty means the AI returned nothing useful,
            // so we should retry on the next search rather than permanently silencing expansion
            if (result.Count > 0)
                _cache[normalized] = result;

            logger.LogInformation(
                "KeywordExpansion: {Count} total queries for '{Keyword}' (top:{Top} pain:{Pain} solution:{Sol} context:{Ctx})",
                result.Count, keyword,
                expanded?.TopQueries?.Count ?? 0,
                expanded?.PainQueries?.Count ?? 0,
                expanded?.SolutionQueries?.Count ?? 0,
                expanded?.ContextQueries?.Count ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "KeywordExpansion: Engine failed for '{Keyword}', returning empty", keyword);
            // Do NOT cache failures — next search should retry the API call
            return [];
        }
    }

    public async Task<string> BuildExpandedQueryAsync(
        string keyword, CancellationToken cancellationToken = default)
    {
        var terms = await ExpandKeywordAsync(keyword, cancellationToken);
        if (terms.Count == 0) return keyword;

        var parts = terms.Select(t => t.Contains(' ') ? $"\"{t}\"" : t);
        return $"{keyword} OR {string.Join(" OR ", parts)}";
    }

    public async Task<IReadOnlyList<string>> BuildSearchBatchesAsync(
        string keyword, CancellationToken cancellationToken = default)
    {
        var phrases = await ExpandKeywordAsync(keyword, cancellationToken);

        // Batch 0: original keyword (always first)
        // Batch 1: top-priority queries (Layer 6) — run early for best signal
        // Batch 2..N: remaining queries in groups of BatchSize
        var batches = new List<string> { keyword };

        if (phrases.Count == 0) return batches;

        // The first TopQueriesCount items are the prioritized ones (see FlattenAndRank)
        var top = phrases.Take(TopQueriesCount).ToList();
        if (top.Count > 0)
            batches.Add(string.Join(" ", top));

        var remaining = phrases.Skip(TopQueriesCount).ToList();
        var chunks = remaining
            .Select((phrase, i) => (phrase, i))
            .GroupBy(x => x.i / BatchSize)
            .Select(g => string.Join(" ", g.Select(x => x.phrase)));

        batches.AddRange(chunks);

        logger.LogInformation(
            "KeywordExpansion: {Count} batches for '{Keyword}' (top-priority batch included)",
            batches.Count, keyword);

        return batches;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Flattens all layers into a ranked list:
    /// topQueries first (Layer 6), then pain, solution, context (Layers 4-5).
    /// </summary>
    private static IEnumerable<string> FlattenAndRank(LayeredExpansion? e, string keyword)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
        var result = new List<string>();

        void Add(IEnumerable<string>? items)
        {
            if (items is null) return;
            foreach (var q in items)
            {
                if (!string.IsNullOrWhiteSpace(q) && seen.Add(q))
                    result.Add(q);
            }
        }

        Add(e?.TopQueries);      // Layer 6 — highest priority, runs in batch 1
        Add(e?.PainQueries);     // Layer 4+5
        Add(e?.SolutionQueries); // Layer 5
        Add(e?.ContextQueries);  // Layer 5

        return result.Take(MaxTotal);
    }

    private void LogLayers(string keyword, LayeredExpansion? e)
    {
        if (e is null) return;

        logger.LogInformation("KeywordExpansion L1 '{Keyword}': category={Cat} | synonyms=[{Syn}] | industry={Ind}",
            keyword,
            e.Layer1?.Category ?? "?",
            string.Join(", ", e.Layer1?.Synonyms ?? []),
            e.Layer1?.IndustryContext ?? "?");

        logger.LogInformation("KeywordExpansion L2 '{Keyword}': humanTerms=[{Terms}] | phrases=[{Ph}]",
            keyword,
            string.Join(", ", e.Layer2?.HumanTerms ?? []),
            string.Join(", ", e.Layer2?.Phrases ?? []));

        logger.LogInformation("KeywordExpansion L3 '{Keyword}': frames=[{Frames}]",
            keyword,
            string.Join(" | ", e.Layer3?.ProblemFrames ?? []));

        logger.LogInformation("KeywordExpansion L6 '{Keyword}' TOP queries: [{Top}]",
            keyword,
            string.Join(" | ", e.TopQueries ?? []));
    }

    // ── Prompts ──────────────────────────────────────────────────────────────

    private const string SystemPrompt = """
        Eres un motor de expansion de keywords especializado en pain discovery para LATAM
        (Colombia, Mexico, Argentina).

        Tu proceso interno sigue 6 capas de razonamiento antes de producir el output final.
        Debes completar TODAS las capas en orden — cada una alimenta a la siguiente.

        CAPA 1 — NORMALIZACION DEL CONCEPTO
        Interpreta la keyword: detecta su categoria (software, proceso, industria, rol),
        identifica sinonimos formales y determina el contexto de industria en LATAM.

        CAPA 2 — EXPANSION A LENGUAJE HUMANO
        Traduce el concepto a como habla la gente real en LATAM.
        Ejemplos de traduccion:
          documentos → archivos, papeles, carpetas, cosas del trabajo
          facturacion → cobrar, mandar facturas, bills, recibos
          gestion → manejar, organizar, llevar el control, admininstrar
        Evita completamente lenguaje tecnico o corporativo.

        CAPA 3 — PROBLEM FRAMING
        Convierte cada termino humano en un problema concreto.
        No: "documentos"
        Si: "no encuentro los documentos cuando los necesito"
            "perdemos archivos y nadie sabe donde estan"
            "tenemos carpetas por todos lados sin orden"

        CAPA 4 — GENERACION DE PAIN QUERIES
        Convierte los problem frames en queries listas para scraping.
        Usa lenguaje emocional: frustracion, confusion, cansancio, perdidas.
        REGLA CRITICA: Solo el 40-50% debe mencionar la keyword exacta.
        El resto describe el dolor SIN mencionarla (mas natural, mayor cobertura).

        CAPA 5 — SEGMENTACION DE INTENCIONES
        Clasifica las queries en:
          pain: quejas y frustraciones
          solution: personas buscando alternativas
          context: segmentos por pais, industria o rol en LATAM

        CAPA 6 — PRIORIZACION
        Selecciona las 5 queries con mayor potencial de encontrar dolores reales.
        Criterios: intensidad del dolor, claridad del problema, señal economica.
        Estas queries se ejecutan PRIMERO en el scraping.

        Devuelve UNICAMENTE un JSON valido con esta estructura. Sin markdown, sin explicaciones:
        {
          "layer1": {
            "category": "...",
            "synonyms": ["...", ...],
            "industryContext": "..."
          },
          "layer2": {
            "humanTerms": ["...", ...],
            "phrases": ["...", ...]
          },
          "layer3": {
            "problemFrames": ["...", ...]
          },
          "painQueries":     ["...", ...],
          "solutionQueries": ["...", ...],
          "contextQueries":  ["...", ...],
          "topQueries":      ["...", "...", "...", "...", "..."]
        }
        """;

    private static string BuildPrompt(string keyword) => $$"""
        Keyword de entrada: "{{keyword}}"
        Mercado objetivo: Colombia, Mexico, Argentina (LATAM).

        Ejecuta las 6 capas del motor de expansion:

        CAPA 1 – NORMALIZACION
        - Detecta la categoria de "{{keyword}}" (software, proceso, industria, rol, otro)
        - Lista 3-5 sinonimos formales
        - Describe el contexto de industria tipico en LATAM

        CAPA 2 – LENGUAJE HUMANO
        - Lista 4-6 formas en que la gente real nombra esto en LATAM
          (sin tecnicismos, como si lo dijera alguien en WhatsApp o en Reddit)
        - Lista 4-6 frases cortas del dia a dia relacionadas

        CAPA 3 – PROBLEM FRAMES
        - Genera 5-8 problemas concretos derivados de los terminos humanos de la Capa 2
        - Cada uno debe ser una situacion real, no una descripcion generica
        - Formato: "verbo + situacion especifica"
          Mal: "{{keyword}} es lento"
          Bien: "pierdo 30 minutos buscando un archivo que deberia estar en segundos"

        CAPA 4+5 – QUERIES POR INTENCION
        painQueries (15-20): quejas emocionales y especificas
          - 40-50% menciona "{{keyword}}", el resto describe el dolor sin usarla
          - Varia entre preguntas, afirmaciones, situaciones, experiencias
        solutionQueries (5-8): personas buscando alternativas o soluciones
        contextQueries (5-8): segmentos LATAM (Colombia, Mexico, Argentina, pymes, freelancers, startups)

        CAPA 6 – TOP 5
        topQueries: exactamente 5 queries con mayor potencial de encontrar dolores reales
          Criterios: intensidad del dolor > claridad > señal de pago potencial

        Reglas de calidad para todas las queries:
        - Lenguaje LATAM natural (no castellano de Espana)
        - Longitud: 5-12 palabras
        - Cada query debe sonar escrita por una persona real
        - No repetir estructuras identicas
        - Evitar lenguaje tecnico o corporativo
        """;
}
