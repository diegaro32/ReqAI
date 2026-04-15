using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Services;

/// <summary>
/// AI-powered pain detection using Gemini 2.5 Flash via Microsoft.Extensions.AI.
/// Sends documents in batched prompts to minimize API calls.
/// Includes rate-limit throttling (6s between calls) for Gemini Free tier (10 RPM).
/// On HTTP 429 the chunk is retried up to 3 times with exponential back-off (10s / 20s / 30s).
/// </summary>
public partial class AiPainDetectionService(
    IChatClient chatClient,
    ILogger<AiPainDetectionService> logger) : IPainDetectionService
{
    private const int BatchSize = 25;
    private const int MaxSnippetLength = 600;
    private static readonly TimeSpan DelayBetweenChunks = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)];
    private const int MinContentLength = 60;

    // Words that signal potential pain — if a document contains NONE of these, skip it
    private static readonly string[] PainIndicators =
    [
        // Explicit frustration (English)
        "frustrat", "annoying", "terrible", "horrible", "awful", "worst", "hate", "sucks",
        "broken", "unusable", "painful", "nightmare", "disaster", "garbage", "trash",
        // Implicit pain (English)
        "struggling", "difficult", "confusing", "complicated", "unclear", "slow",
        "expensive", "overpriced", "costly", "waste", "bloated",
        // Feature gaps & workarounds (English)
        "wish", "missing", "lack", "no way to", "can't", "cannot", "doesn't support",
        "workaround", "alternative", "switch", "migrate", "replace",
        // Help-seeking (English)
        "help me", "how do i", "anyone know", "is there a", "looking for",
        // Negative outcomes (English)
        "failed", "error", "bug", "crash", "broke", "lost", "timeout",
        "downtime", "outage", "unreliable", "inconsistent",
        // Cost/value concerns (English)
        "pricing", "price", "cost", "fee", "charge", "subscription", "pay",
        "worth", "value", "refund", "cancel",

        // ── Spanish LATAM pain indicators ──────────────────────────────────
        // Frustración explícita
        "odio", "horrible", "terrible", "pesimo", "pésimo", "fatal", "detesto",
        "un caos", "desastre", "basura", "inutil", "inútil",
        // Dolor implícito
        "dificil", "difícil", "confuso", "lento", "tardado", "complicado",
        "caro", "costoso", "desperdicio", "engorroso",
        // Procesos manuales / ineficiencia
        "a mano", "manual", "toca hacer", "hay que hacer", "pierde tiempo",
        "pierdo tiempo", "tarda", "demora", "no automatiza", "no sirve",
        // Búsqueda de alternativas
        "alternativa", "reemplazar", "cambiar", "migrar", "busco algo",
        "necesito algo", "alguien sabe", "como hago", "como puedo",
        // Resultados negativos
        "falla", "fallo", "error", "se rompe", "no funciona", "se cae",
        "no carga", "perdimos", "perdí", "no encontro", "no encuentro",
        // Costo / valor
        "precio", "cobran", "cobra", "cuesta", "muy caro", "no vale",
        "cancelar", "cancelé", "devolucion", "devolución",
        // Escalabilidad / crecimiento
        "no escala", "cuando crece", "se rompe todo", "no aguanta",
        // Desorganización
        "desordenado", "regado", "por todos lados", "nadie sabe",
        "no hay orden", "sin control"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<PainSignal>> DetectPainsAsync(
        RawDocument document, CancellationToken cancellationToken = default)
    {
        return await DetectPainsBatchAsync([document], cancellationToken);
    }

    public async Task<IReadOnlyList<PainSignal>> DetectPainsBatchAsync(
        IReadOnlyList<RawDocument> documents, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return [];

        // ── Pre-filter: discard docs unlikely to contain pain ──
        var candidates = PreFilterDocuments(documents);

        logger.LogInformation(
            "PainDetection: Pre-filter kept {Kept}/{Total} documents ({Pct}%)",
            candidates.Count, documents.Count,
            documents.Count > 0 ? candidates.Count * 100 / documents.Count : 0);

        if (candidates.Count == 0)
            return [];

        var allSignals = new List<PainSignal>();

        var chunks = candidates
            .Select((doc, idx) => (doc, idx))
            .GroupBy(x => x.idx / BatchSize)
            .Select(g => g.Select(x => x.doc).ToList())
            .ToList();

        logger.LogInformation("PainDetection: Analyzing {Total} candidates in {Chunks} chunk(s)",
            candidates.Count, chunks.Count);

        var successCount = 0;
        var failCount = 0;

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var chunkSucceeded = false;

            for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
            {
                try
                {
                    var signals = await AnalyzeChunkAsync(chunk, i + 1, chunks.Count, cancellationToken);
                    allSignals.AddRange(signals);
                    successCount++;
                    chunkSucceeded = true;
                    break;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("PainDetection: Chunk {N}/{Total} TIMEOUT ({Count} docs)",
                        i + 1, chunks.Count, chunk.Count);
                    break; // timeout is not retriable
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    if (attempt < RetryDelays.Length)
                    {
                        var wait = RetryDelays[attempt];
                        logger.LogWarning("PainDetection: Chunk {N}/{Total} rate-limited (429) — retrying in {Wait}s (attempt {A}/{Max})",
                            i + 1, chunks.Count, wait.TotalSeconds, attempt + 1, RetryDelays.Length);
                        await Task.Delay(wait, cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning(ex, "PainDetection: Chunk {N}/{Total} FAILED after {Max} retries ({Count} docs) — {Error}",
                            i + 1, chunks.Count, RetryDelays.Length, chunk.Count, ex.Message);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "PainDetection: Chunk {N}/{Total} FAILED ({Count} docs) — {Error}",
                        i + 1, chunks.Count, chunk.Count, ex.Message);
                    break; // non-retriable error
                }
            }

            if (!chunkSucceeded)
                failCount++;

            // Throttle to stay under Gemini Free tier rate limit (10 RPM ~= 6s between calls)
            if (i < chunks.Count - 1)
                await Task.Delay(DelayBetweenChunks, cancellationToken);
        }

        logger.LogInformation(
            "PainDetection: Done — {Signals} pain signals from {Docs} documents ({Success} chunks OK, {Fail} failed)",
            allSignals.Count, documents.Count, successCount, failCount);

        return allSignals;
    }

    private async Task<List<PainSignal>> AnalyzeChunkAsync(
        List<RawDocument> chunk, int chunkNum, int totalChunks, CancellationToken cancellationToken)
    {
        var docEntries = chunk.Select((doc, i) =>
        {
            var snippet = doc.Content.Length > MaxSnippetLength
                ? doc.Content[..MaxSnippetLength]
                : doc.Content;
            return $"[{i}] {snippet}";
        });

        var docsBlock = string.Join("\n\n", docEntries);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are a pain-point analyst for PainFinder.
                Detect real frustrations in user-generated content.
                Content may be in English OR Spanish (LATAM — Colombia, Mexico, Argentina).

                Be generous detecting pain. Include:
                - Explicit complaints, implicit pain, feature gaps, confusion, cost concerns

                Spanish LATAM pain signals to recognize:
                - "toca hacerlo a mano", "se rompe todo", "un caos", "no funciona"
                - "pierdo tiempo", "no entiendo", "es un desastre", "me hace perder"
                - "muy lento", "siempre falla", "no escala", "demasiado complicado"
                - "nadie sabe", "todo desordenado", "cuesta demasiado", "no sirve"

                RULES:
                - Return ONLY 1 pain per document (the strongest one). Skip neutral/positive docs.
                - Return ONLY a valid JSON array. No markdown, no explanation, no trailing commas.
                - Keep "painPhrase" under 100 characters.
                - Write "painPhrase" in the SAME language as the source document.
                Each element: {"docIndex": N, "painPhrase": "...", "painCategory": "...", "painScore": 0.0-1.0, "sentimentScore": -1.0-0.0}
                Categories: Pricing, UX/Usability, Performance, Support, Documentation, Feature Gap, Reliability, Integration, Security, Workflow
                If NO pain found, return: []
                """),
            new(ChatRole.User, $"Analyze these {chunk.Count} documents for pain points:\n\n{docsBlock}")
        };

        logger.LogInformation("PainDetection: Chunk {N}/{Total} — sending {Count} docs to Gemini...",
            chunkNum, totalChunks, chunk.Count);

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var responseText = response.Messages[^1].Text?.Trim() ?? "[]";

        // Clean markdown code fences if present
        var codeFence = new string('`', 3);
        if (responseText.StartsWith(codeFence))
        {
            responseText = responseText
                .Replace(codeFence + "json", "")
                .Replace(codeFence, "")
                .Trim();
        }

        // Fix malformed JSON numbers from Gemini: -.8 → -0.8, .5 → 0.5
        responseText = LeadingZeroRegex().Replace(responseText, ": -0.$1");
        responseText = PositiveLeadingZeroRegex().Replace(responseText, ": 0.$1");

        List<AiPainResult> parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<AiPainResult>>(responseText, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            // Gemini truncated the JSON — salvage complete objects before the cut
            parsed = RepairTruncatedJson(responseText);
            if (parsed.Count > 0)
                logger.LogWarning("PainDetection: Chunk {N}/{Total} — repaired truncated JSON, salvaged {Count} signals",
                    chunkNum, totalChunks, parsed.Count);
            else
                throw; // re-throw if nothing was salvageable
        }

        logger.LogInformation("PainDetection: Chunk {N}/{Total} — Gemini returned {Count} pain signals",
            chunkNum, totalChunks, parsed.Count);

        var signals = new List<PainSignal>();
        foreach (var result in parsed)
        {
            if (result.DocIndex < 0 || result.DocIndex >= chunk.Count)
                continue;

            var doc = chunk[result.DocIndex];
            signals.Add(new PainSignal
            {
                Id = Guid.NewGuid(),
                RawDocumentId = doc.Id,
                PainPhrase = result.PainPhrase.Length > 100 ? result.PainPhrase[..100] : result.PainPhrase,
                PainCategory = result.PainCategory,
                SentimentScore = Math.Clamp(result.SentimentScore, -1.0, 0.0),
                PainScore = Math.Clamp(result.PainScore, 0.0, 1.0),
                DetectedAt = DateTime.UtcNow
            });
        }

        return signals;
    }

    /// <summary>
    /// When Gemini truncates its JSON output mid-array, find the last complete object
    /// and parse everything before it. Example: [{...}, {...}, {"painPhr  ← cut here
    /// We find the last '}' followed by the array pattern and close it with ']'.
    /// </summary>
    private static List<AiPainResult> RepairTruncatedJson(string json)
    {
        // Find the position of the last complete JSON object (ending with })
        var lastClose = json.LastIndexOf('}');
        if (lastClose <= 0)
            return [];

        var repaired = json[..(lastClose + 1)].TrimEnd();

        // Ensure it ends as a valid JSON array
        if (!repaired.EndsWith(']'))
            repaired += "]";

        // Fix leading-zero issues in the repaired string too
        repaired = LeadingZeroRegex().Replace(repaired, ": -0.$1");
        repaired = PositiveLeadingZeroRegex().Replace(repaired, ": 0.$1");

        try
        {
            return JsonSerializer.Deserialize<List<AiPainResult>>(repaired, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class AiPainResult
    {
        public int DocIndex { get; set; }
        public string PainPhrase { get; set; } = "";
        public string PainCategory { get; set; } = "";
        public double PainScore { get; set; }
        public double SentimentScore { get; set; }
    }

    /// <summary>
    /// Local pre-filter that discards documents unlikely to contain pain:
    /// 1. Too short (&lt;60 chars) — no meaningful content
    /// 2. Near-duplicates — same content already seen
    /// 3. No pain indicators — purely neutral/positive/technical content
    /// This reduces Gemini calls by ~60-70% without losing real pain signals.
    /// </summary>
    private List<RawDocument> PreFilterDocuments(IReadOnlyList<RawDocument> documents)
    {
        var candidates = new List<RawDocument>();
        var seenContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in documents)
        {
            // 1. Skip very short content
            if (doc.Content.Length < MinContentLength)
                continue;

            // 2. Skip near-duplicates (first 150 chars as fingerprint)
            var fingerprint = doc.Content.Length > 150
                ? doc.Content[..150]
                : doc.Content;
            if (!seenContent.Add(fingerprint))
                continue;

            // 3. Skip if no pain indicator found
            var contentLower = doc.Content.ToLowerInvariant();
            var hasPainSignal = false;
            foreach (var indicator in PainIndicators)
            {
                if (contentLower.Contains(indicator))
                {
                    hasPainSignal = true;
                    break;
                }
            }

            if (!hasPainSignal)
                continue;

            candidates.Add(doc);
        }

        return candidates;
    }

    // Gemini sometimes outputs -.8 instead of -0.8 (invalid JSON)
    [System.Text.RegularExpressions.GeneratedRegex(@":\s*-\.(\d)")]
    private static partial System.Text.RegularExpressions.Regex LeadingZeroRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@":\s*\.(\d)")]
    private static partial System.Text.RegularExpressions.Regex PositiveLeadingZeroRegex();
}
