using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Shared.DTOs;

namespace PainFinder.Infrastructure.Services;

/// <summary>
/// Deep opportunity analysis engine.
/// Transforms a pain cluster into a specific, actionable SaaS opportunity with:
/// wedge definition, operative context, imperfect current solution, validation test and red flag check.
/// Uses Flash + thinking:1024 for strategic reasoning quality.
/// Results are persisted to the database so they survive page reloads.
/// </summary>
public class AiDeepOpportunityService(
    IChatClient chatClient,
    PainFinderDbContext dbContext,
    ILogger<AiDeepOpportunityService> logger) : IDeepOpportunityService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<DeepOpportunityAnalysisDto> AnalyzeAsync(
        OpportunityDto opportunity,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("DeepAnalysis: Analyzing '{Title}'...", opportunity.Title);

        var evidenceBlock = opportunity.Evidence.Count > 0
            ? string.Join("\n", opportunity.Evidence.Select(e => $"  - \"{e.Quote}\" ({e.Source}, {e.UserContext})"))
            : "  (sin citas de evidencia)";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, BuildPrompt(opportunity, evidenceBlock))
        };

        var responseText = "{}";
        try
        {
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            responseText = response.Messages[^1].Text?.Trim() ?? "{}";

            var fence = new string('`', 3);
            if (responseText.StartsWith(fence))
                responseText = responseText.Replace(fence + "json", "").Replace(fence, "").Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DeepAnalysis: Gemini call failed for '{Title}'", opportunity.Title);
            throw new InvalidOperationException("Error al generar el análisis profundo. Inténtalo de nuevo.", ex);
        }

        var parsed = JsonSerializer.Deserialize<DeepAnalysisResponse>(responseText, JsonOpts)
            ?? throw new InvalidOperationException("Respuesta inválida del modelo de IA.");

        logger.LogInformation("DeepAnalysis: Completed for '{Title}'", opportunity.Title);

        return new DeepOpportunityAnalysisDto(
            SpecificPain:      parsed.SpecificPain ?? "N/A",
            OperativeContext:  Map(parsed.OperativeContext),
            CurrentSolution:   Map(parsed.CurrentSolution),
            OpportunityGap:    parsed.OpportunityGap ?? "N/A",
            Wedge:             Map(parsed.Wedge),
            ValueProposition:  parsed.ValueProposition ?? "N/A",
            ImmediateAction:   Map(parsed.ImmediateAction),
            ValidationTest:    Map(parsed.ValidationTest),
            MvpDefinition:     Map(parsed.MvpDefinition),
            PaymentTrigger:    parsed.PaymentTrigger ?? "N/A",
            RedFlag:           Map(parsed.RedFlag));
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    public async Task SaveDeepAnalysisAsync(
        Guid opportunityId,
        DeepOpportunityAnalysisDto dto,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.DeepAnalyses
            .FirstOrDefaultAsync(d => d.OpportunityId == opportunityId, cancellationToken);

        if (existing is null)
        {
            dbContext.DeepAnalyses.Add(ToEntity(opportunityId, dto));
        }
        else
        {
            UpdateEntity(existing, dto);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DeepAnalysis: Saved for OpportunityId={Id}", opportunityId);
    }

    public async Task<DeepOpportunityAnalysisDto?> GetDeepAnalysisAsync(
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.DeepAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.OpportunityId == opportunityId, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<Dictionary<Guid, DeepOpportunityAnalysisDto>> GetDeepAnalysesForOpportunitiesAsync(
        IEnumerable<Guid> opportunityIds,
        CancellationToken cancellationToken = default)
    {
        var ids = opportunityIds.ToList();
        if (ids.Count == 0) return [];

        var entities = await dbContext.DeepAnalyses
            .AsNoTracking()
            .Where(d => ids.Contains(d.OpportunityId))
            .ToListAsync(cancellationToken);

        return entities.ToDictionary(e => e.OpportunityId, ToDto);
    }

    private static DeepAnalysis ToEntity(Guid opportunityId, DeepOpportunityAnalysisDto dto) =>
        new()
        {
            Id             = Guid.NewGuid(),
            OpportunityId  = opportunityId,
            SpecificPain   = dto.SpecificPain,
            Channel        = dto.OperativeContext.Channel,
            Timing         = dto.OperativeContext.Timing,
            AffectedUser   = dto.OperativeContext.AffectedUser,
            Frequency      = dto.OperativeContext.Frequency,
            DirectConsequence = dto.OperativeContext.DirectConsequence,
            ToolsUsedJson  = JsonSerializer.Serialize(dto.CurrentSolution.ToolsUsed),
            WhatWorks      = dto.CurrentSolution.WhatWorks,
            WhatFails      = dto.CurrentSolution.WhatFails,
            OpportunityGap = dto.OpportunityGap,
            WedgeStatement = dto.Wedge.Statement,
            WedgeJustification = dto.Wedge.Justification,
            ValueProposition = dto.ValueProposition,
            WhoToContact   = dto.ImmediateAction.WhoToContact,
            WhereToFind    = dto.ImmediateAction.WhereToFind,
            WhatToSay      = dto.ImmediateAction.WhatToSay,
            ManualService  = dto.ValidationTest.ManualService,
            ResultToDeliver = dto.ValidationTest.ResultToDeliver,
            WhatToMeasure  = dto.ValidationTest.WhatToMeasure,
            MvpFeature1    = dto.MvpDefinition.Feature1,
            MvpFeature2    = dto.MvpDefinition.Feature2,
            PaymentTrigger = dto.PaymentTrigger,
            IsGenericRisk  = dto.RedFlag.IsGenericRisk,
            RedFlagExplanation = dto.RedFlag.Explanation,
            HowToMakeSpecific  = dto.RedFlag.HowToMakeSpecific,
        };

    private static void UpdateEntity(DeepAnalysis e, DeepOpportunityAnalysisDto dto)
    {
        e.SpecificPain      = dto.SpecificPain;
        e.Channel           = dto.OperativeContext.Channel;
        e.Timing            = dto.OperativeContext.Timing;
        e.AffectedUser      = dto.OperativeContext.AffectedUser;
        e.Frequency         = dto.OperativeContext.Frequency;
        e.DirectConsequence = dto.OperativeContext.DirectConsequence;
        e.ToolsUsedJson     = JsonSerializer.Serialize(dto.CurrentSolution.ToolsUsed);
        e.WhatWorks         = dto.CurrentSolution.WhatWorks;
        e.WhatFails         = dto.CurrentSolution.WhatFails;
        e.OpportunityGap    = dto.OpportunityGap;
        e.WedgeStatement    = dto.Wedge.Statement;
        e.WedgeJustification = dto.Wedge.Justification;
        e.ValueProposition  = dto.ValueProposition;
        e.WhoToContact      = dto.ImmediateAction.WhoToContact;
        e.WhereToFind       = dto.ImmediateAction.WhereToFind;
        e.WhatToSay         = dto.ImmediateAction.WhatToSay;
        e.ManualService     = dto.ValidationTest.ManualService;
        e.ResultToDeliver   = dto.ValidationTest.ResultToDeliver;
        e.WhatToMeasure     = dto.ValidationTest.WhatToMeasure;
        e.MvpFeature1       = dto.MvpDefinition.Feature1;
        e.MvpFeature2       = dto.MvpDefinition.Feature2;
        e.PaymentTrigger    = dto.PaymentTrigger;
        e.IsGenericRisk     = dto.RedFlag.IsGenericRisk;
        e.RedFlagExplanation = dto.RedFlag.Explanation;
        e.HowToMakeSpecific = dto.RedFlag.HowToMakeSpecific;
        e.CreatedAt         = DateTime.UtcNow;
    }

    private static DeepOpportunityAnalysisDto ToDto(DeepAnalysis e)
    {
        List<string> tools = [];
        try { tools = JsonSerializer.Deserialize<List<string>>(e.ToolsUsedJson) ?? []; } catch { }

        return new DeepOpportunityAnalysisDto(
            SpecificPain:      e.SpecificPain,
            OperativeContext:  new OperativeContextDto(e.Channel, e.Timing, e.AffectedUser, e.Frequency, e.DirectConsequence),
            CurrentSolution:   new CurrentSolutionDto(tools, e.WhatWorks, e.WhatFails),
            OpportunityGap:    e.OpportunityGap,
            Wedge:             new WedgeDto(e.WedgeStatement, e.WedgeJustification),
            ValueProposition:  e.ValueProposition,
            ImmediateAction:   new ImmediateActionDto(e.WhoToContact, e.WhereToFind, e.WhatToSay),
            ValidationTest:    new ValidationTestDto(e.ManualService, e.ResultToDeliver, e.WhatToMeasure),
            MvpDefinition:     new MvpDefinitionDto(e.MvpFeature1, e.MvpFeature2),
            PaymentTrigger:    e.PaymentTrigger,
            RedFlag:           new RedFlagDto(e.IsGenericRisk, e.RedFlagExplanation, e.HowToMakeSpecific));
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static OperativeContextDto Map(RawOperativeContext? r) => new(
        r?.Channel ?? "N/A", r?.Timing ?? "N/A",
        r?.AffectedUser ?? "N/A", r?.Frequency ?? "N/A", r?.DirectConsequence ?? "N/A");

    private static CurrentSolutionDto Map(RawCurrentSolution? r) => new(
        r?.ToolsUsed ?? [], r?.WhatWorks ?? "N/A", r?.WhatFails ?? "N/A");

    private static WedgeDto Map(RawWedge? r) => new(r?.Statement ?? "N/A", r?.Justification ?? "N/A");

    private static ImmediateActionDto Map(RawImmediateAction? r) => new(
        r?.WhoToContact ?? "N/A", r?.WhereToFind ?? "N/A", r?.WhatToSay ?? "N/A");

    private static ValidationTestDto Map(RawValidationTest? r) => new(
        r?.ManualService ?? "N/A", r?.ResultToDeliver ?? "N/A", r?.WhatToMeasure ?? "N/A");

    private static MvpDefinitionDto Map(RawMvpDefinition? r) => new(r?.Feature1 ?? "N/A", r?.Feature2 ?? "N/A");

    private static RedFlagDto Map(RawRedFlag? r) => new(
        r?.IsGenericRisk ?? false, r?.Explanation ?? "N/A", r?.HowToMakeSpecific ?? "N/A");

    // ── Raw deserialization records ──────────────────────────────────────────

    private sealed record DeepAnalysisResponse(
        [property: JsonPropertyName("specificPain")]      string?              SpecificPain,
        [property: JsonPropertyName("operativeContext")]  RawOperativeContext? OperativeContext,
        [property: JsonPropertyName("currentSolution")]   RawCurrentSolution?  CurrentSolution,
        [property: JsonPropertyName("opportunityGap")]    string?              OpportunityGap,
        [property: JsonPropertyName("wedge")]             RawWedge?            Wedge,
        [property: JsonPropertyName("valueProposition")]  string?              ValueProposition,
        [property: JsonPropertyName("immediateAction")]   RawImmediateAction?  ImmediateAction,
        [property: JsonPropertyName("validationTest")]    RawValidationTest?   ValidationTest,
        [property: JsonPropertyName("mvpDefinition")]     RawMvpDefinition?    MvpDefinition,
        [property: JsonPropertyName("paymentTrigger")]    string?              PaymentTrigger,
        [property: JsonPropertyName("redFlag")]           RawRedFlag?          RedFlag);

    private sealed record RawOperativeContext(
        [property: JsonPropertyName("channel")]           string? Channel,
        [property: JsonPropertyName("timing")]            string? Timing,
        [property: JsonPropertyName("affectedUser")]      string? AffectedUser,
        [property: JsonPropertyName("frequency")]         string? Frequency,
        [property: JsonPropertyName("directConsequence")] string? DirectConsequence);

    private sealed record RawCurrentSolution(
        [property: JsonPropertyName("toolsUsed")]         List<string>? ToolsUsed,
        [property: JsonPropertyName("whatWorks")]         string? WhatWorks,
        [property: JsonPropertyName("whatFails")]         string? WhatFails);

    private sealed record RawWedge(
        [property: JsonPropertyName("statement")]         string? Statement,
        [property: JsonPropertyName("justification")]     string? Justification);

    private sealed record RawImmediateAction(
        [property: JsonPropertyName("whoToContact")]      string? WhoToContact,
        [property: JsonPropertyName("whereToFind")]       string? WhereToFind,
        [property: JsonPropertyName("whatToSay")]         string? WhatToSay);

    private sealed record RawValidationTest(
        [property: JsonPropertyName("manualService")]     string? ManualService,
        [property: JsonPropertyName("resultToDeliver")]   string? ResultToDeliver,
        [property: JsonPropertyName("whatToMeasure")]     string? WhatToMeasure);

    private sealed record RawMvpDefinition(
        [property: JsonPropertyName("feature1")]          string? Feature1,
        [property: JsonPropertyName("feature2")]          string? Feature2);

    private sealed record RawRedFlag(
        [property: JsonPropertyName("isGenericRisk")]     bool    IsGenericRisk,
        [property: JsonPropertyName("explanation")]       string? Explanation,
        [property: JsonPropertyName("howToMakeSpecific")] string? HowToMakeSpecific);

    // ── Prompts ──────────────────────────────────────────────────────────────

    private const string SystemPrompt = """
        Eres un experto en descubrimiento de oportunidades SaaS en etapas tempranas,
        con enfoque en mercados de LATAM (Colombia, Mexico, Argentina).

        Tu objetivo NO es generar ideas genericas, sino identificar oportunidades
        altamente accionables, especificas y con potencial real de monetizacion inmediata.

        REGLAS CRITICAS:
        - PROHIBIDO generar ideas genericas tipo "plataforma de gestion", "app todo en uno".
        - DEBES basarte en comportamientos reales detectables.
        - DEBES identificar como el usuario actualmente intenta resolver el problema.
        - DEBES proponer un angulo de entrada (wedge) especifico, pequeno y vendible.
        - TODO en espanol LATAM. TODO debe ser concreto, no abstracto.
        - Si el output suena como algo que cualquier SaaS podria hacer, esta MAL.

        Devuelve UNICAMENTE JSON valido. Sin markdown, sin explicaciones.
        """;

    private static string BuildPrompt(OpportunityDto opp, string evidenceBlock) => $$"""
        CLUSTER DE DOLOR: {{opp.ClusterTitle}}
        TITULO DE OPORTUNIDAD: {{opp.Title}}
        PROBLEMA: {{opp.ProblemSummary}}
        SOLUCION SUGERIDA: {{opp.SuggestedSolution}}
        ICP: {{opp.Icp.Role}} — {{opp.Icp.Context}}
        HERRAMIENTAS DETECTADAS: {{string.Join(", ", opp.Competition.ToolsDetected)}}
        BRECHA: {{opp.Competition.GapAnalysis}}
        MARKET REALITY: {{(opp.MarketRealityScore * 100):F0}}%
        DECISION: {{opp.BuildDecision}}

        SENALES DE DOLOR (evidencia real):
        {{evidenceBlock}}

        Analiza esta oportunidad y devuelve el siguiente JSON con TODOS los campos completos:

        {
          "specificPain": "1 frase concreta que suene como algo que alguien diria frustrado",
          "operativeContext": {
            "channel": "herramienta principal donde ocurre el dolor (WhatsApp, Excel, email, etc.)",
            "timing": "cuando ocurre (diario, cierre de mes, entregas, etc.)",
            "affectedUser": "rol especifico afectado",
            "frequency": "con que frecuencia ocurre el problema",
            "directConsequence": "consecuencia directa (perdida de tiempo, dinero, clientes, errores)"
          },
          "currentSolution": {
            "toolsUsed": ["herramienta1", "herramienta2"],
            "whatWorks": "que parte del workaround actual funciona",
            "whatFails": "que parte falla (AQUI esta la oportunidad)"
          },
          "opportunityGap": "tension especifica no resuelta. Ej: Comunican bien (WhatsApp) pero no tienen seguimiento estructurado (Excel manual)",
          "wedge": {
            "statement": "[accion especifica] sin [dolor actual]. Ej: Convierte mensajes de WhatsApp en tareas con seguimiento automatico",
            "justification": "por que este wedge especifico, no otro"
          },
          "valueProposition": "1 linea clara orientada a resultado. El usuario debe entenderla en 5 segundos",
          "immediateAction": {
            "whoToContact": "persona hiper-especifica a contactar (rol, tipo de empresa, tamano)",
            "whereToFind": "donde encontrarla exactamente (comunidad, red social, grupo)",
            "whatToSay": "mensaje enfocado en el dolor, no en el producto (menos de 60 palabras)"
          },
          "validationTest": {
            "manualService": "que servicio manual ofrecer para validar SIN codigo",
            "resultToDeliver": "que resultado concreto entregar al usuario",
            "whatToMeasure": "que medir para confirmar validacion (pago, interes, uso)"
          },
          "mvpDefinition": {
            "feature1": "unica funcionalidad core que representa el wedge",
            "feature2": "segunda funcionalidad que cierra el loop de valor (opcional pero ideal)"
          },
          "paymentTrigger": "por que alguien pagaria: dolor urgente + costo que evita + beneficio tangible",
          "redFlag": {
            "isGenericRisk": true or false,
            "explanation": "si hay riesgo de genericidad, explicar por que",
            "howToMakeSpecific": "como hacerla mas especifica si aplica"
          }
        }
        """;
}
