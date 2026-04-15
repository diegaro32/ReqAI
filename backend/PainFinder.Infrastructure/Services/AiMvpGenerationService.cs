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
/// Generates execution-ready action plans using Gemini Flash with thinking.
/// </summary>
public class AiMvpGenerationService(
    IChatClient chatClient,
    PainFinderDbContext dbContext,
    ILogger<AiMvpGenerationService> logger) : IMvpGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<MvpPlanDto> GenerateMvpPlanAsync(
        OpportunityDto opportunity,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("ActionPlan: Generating for '{Title}' (decision: {Decision})",
            opportunity.Title, opportunity.BuildDecision);

        var evidenceBlock = opportunity.Evidence.Count > 0
            ? string.Join("\n", opportunity.Evidence.Select(e => $"  - \"{e.Quote}\" ({e.Source}, {e.UserContext})"))
            : "  (no evidence quotes available)";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                Eres un asesor de ejecucion de startups enfocado en el mercado de LATINOAMERICA
                (Colombia, Mexico y Argentina). Generas planes de accion ESPECIFICOS y LISTOS PARA COPIAR
                adaptados al contexto LATAM: plataformas locales, contacto en espanol,
                y estrategias de precios ajustadas al poder adquisitivo latinoamericano.
                Todo el contenido que generes debe estar en ESPANOL.
                Los nombres de campos JSON deben permanecer en ingles (camelCase) tal como se definen.
                Devuelve UNICAMENTE JSON valido. Sin markdown, sin explicaciones, sin comas finales.
                """),
            new(ChatRole.User, $$"""
                Mercado objetivo: Latinoamerica — Colombia, Mexico y Argentina.
                Oportunidad:
                - Titulo: {{opportunity.Title}}
                - Problema: {{opportunity.ProblemSummary}}
                - Solucion: {{opportunity.SuggestedSolution}}
                - ICP: {{opportunity.Icp.Role}} — {{opportunity.Icp.Context}}
                - Puntuacion de Realidad de Mercado: {{(opportunity.MarketRealityScore * 100):F0}}%
                - Decision: {{opportunity.BuildDecision}}
                - Competencia: {{string.Join(", ", opportunity.Competition.ToolsDetected)}}
                - Brecha: {{opportunity.Competition.GapAnalysis}}
                - Evidencia:
                {{evidenceBlock}}

                TODOS los textos del JSON deben estar en ESPANOL.
                Adapta el plan al contexto LATAM:
                - Mensajes de contacto en espanol, dirigidos al ICP de Colombia, Mexico o Argentina.
                - Menciona plataformas LATAM (LinkedIn, WhatsApp, grupos de Telegram, grupos de Facebook, comunidades locales) donde corresponda.
                - Los precios deben reflejar el poder adquisitivo de LATAM (indica equivalentes en USD y moneda local si es posible).

                Devuelve DOS secciones en UN SOLO JSON:

                {
                  "mvpPlan": {
                    "problemStatement": "1-2 oraciones ultra-especificas para el ICP",
                    "targetUsers": "persona exacta con contexto",
                    "coreFeatures": ["Funcionalidad 1 (1 semana)", "Funcionalidad 2 (1 semana)", "Funcionalidad 3 (2 semanas)"],
                    "techStack": "stack mas rapido para este problema especifico",
                    "validationStrategy": "como validar antes de construir",
                    "firstStep": "UNA accion para manana por la manana",
                    "estimatedTimeline": "semanas hasta el primer cliente de pago"
                  },
                  "actionPlan": {
                    "exactIcp": "Persona exacta a contactar: rol, tipo de empresa, tamano, donde encontrarla",
                    "valueProposition": "Una linea: Ayudamos a [ICP] a resolver [problema] mediante [solucion]",
                    "outreachMessage": "DM/email frio listo para enviar (menos de 100 palabras, personalizado para el ICP, en espanol)",
                    "validationTest": "Un test especifico: landing page, servicio manual o descripcion de demo",
                    "firstStepTomorrow": "La accion EXACTA a hacer manana a las 9am (especifica: plataforma, accion, objetivo)"
                  },
                  "monetizationStrategies": [
                    {
                      "model": "ej. Suscripcion SaaS, Freemium, Por uso, Licencia unica, Comision",
                      "description": "1 oracion explicando como funciona para este producto especifico",
                      "priceRange": "solo cifras en USD: ej. $9/mes, $49 unico, 5% por transaccion"
                    }
                  ]
                }

                Para monetizationStrategies, incluye 2-3 modelos de precios realistas ordenados por recomendacion.
                Cada uno debe ser especifico para ESTE producto y ICP, no consejos genericos.
                """)
        };

        var responseText = "{}";

        try
        {
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            responseText = response.Messages[^1].Text?.Trim() ?? "{}";

            var codeFence = new string('`', 3);
            if (responseText.StartsWith(codeFence))
            {
                responseText = responseText
                    .Replace(codeFence + "json", "")
                    .Replace(codeFence, "")
                    .Trim();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ActionPlan: Gemini call failed for '{Title}'", opportunity.Title);
            throw new InvalidOperationException("Failed to generate action plan. Please try again.", ex);
        }

        var parsed = JsonSerializer.Deserialize<CombinedPlanResponse>(responseText, JsonOptions);

        if (parsed is null)
            throw new InvalidOperationException("Invalid response from AI.");

        logger.LogInformation("ActionPlan: Generated for '{Title}'", opportunity.Title);

        var mvp = parsed.MvpPlan;
        var action = parsed.ActionPlan;

        return new MvpPlanDto(
            mvp?.ProblemStatement ?? "N/A",
            mvp?.TargetUsers ?? "N/A",
            mvp?.CoreFeatures ?? [],
            mvp?.TechStack ?? "N/A",
            mvp?.ValidationStrategy ?? "N/A",
            mvp?.FirstStep ?? "N/A",
            mvp?.EstimatedTimeline ?? "N/A")
        {
            Action = action is not null
                ? new ActionPlanDto(
                    action.ExactIcp ?? "N/A",
                    action.ValueProposition ?? "N/A",
                    action.OutreachMessage ?? "N/A",
                    action.ValidationTest ?? "N/A",
                    action.FirstStepTomorrow ?? "N/A")
                : null,
            MonetizationStrategies = parsed.MonetizationStrategies?
                .Where(m => m.Model is not null)
                .Select(m => new MonetizationStrategyDto(m.Model!, m.Description ?? "", m.PriceRange ?? ""))
                .ToList() ?? []
        };
    }

    public async Task SaveActionPlanAsync(Guid opportunityId, MvpPlanDto plan, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ActionPlans
            .FirstOrDefaultAsync(a => a.OpportunityId == opportunityId, cancellationToken);

        if (existing is not null)
            return;

        var entity = new ActionPlan
        {
            Id = Guid.NewGuid(),
            OpportunityId = opportunityId,
            ProblemStatement = plan.ProblemStatement,
            TargetUsers = plan.TargetUsers,
            CoreFeaturesJson = JsonSerializer.Serialize(plan.CoreFeatures, JsonOptions),
            TechStack = plan.TechStack,
            ValidationStrategy = plan.ValidationStrategy,
            FirstStep = plan.FirstStep,
            EstimatedTimeline = plan.EstimatedTimeline,
            ExactIcp = plan.Action?.ExactIcp ?? string.Empty,
            ValueProposition = plan.Action?.ValueProposition ?? string.Empty,
            OutreachMessage = plan.Action?.OutreachMessage ?? string.Empty,
            ValidationTest = plan.Action?.ValidationTest ?? string.Empty,
            FirstStepTomorrow = plan.Action?.FirstStepTomorrow ?? string.Empty,
            MonetizationStrategiesJson = JsonSerializer.Serialize(plan.MonetizationStrategies, JsonOptions),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ActionPlans.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MvpPlanDto?> GetActionPlanAsync(Guid opportunityId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ActionPlans
            .FirstOrDefaultAsync(a => a.OpportunityId == opportunityId, cancellationToken);

        return entity is not null ? ToDto(entity) : null;
    }

    public async Task<Dictionary<Guid, MvpPlanDto>> GetActionPlansForOpportunitiesAsync(
        IEnumerable<Guid> opportunityIds, CancellationToken cancellationToken = default)
    {
        var ids = opportunityIds.ToList();
        if (ids.Count == 0) return [];

        var plans = await dbContext.ActionPlans
            .Where(a => ids.Contains(a.OpportunityId))
            .ToListAsync(cancellationToken);

        return plans.ToDictionary(a => a.OpportunityId, ToDto);
    }

    private MvpPlanDto ToDto(ActionPlan entity)
    {
        var features = new List<string>();
        try
        {
            features = JsonSerializer.Deserialize<List<string>>(entity.CoreFeaturesJson, JsonOptions) ?? [];
        }
        catch { }

        return new MvpPlanDto(
            entity.ProblemStatement,
            entity.TargetUsers,
            features,
            entity.TechStack,
            entity.ValidationStrategy,
            entity.FirstStep,
            entity.EstimatedTimeline)
        {
            Action = !string.IsNullOrEmpty(entity.ExactIcp)
                ? new ActionPlanDto(
                    entity.ExactIcp,
                    entity.ValueProposition,
                    entity.OutreachMessage,
                    entity.ValidationTest,
                    entity.FirstStepTomorrow)
                : null,
            MonetizationStrategies = DeserializeMonetization(entity.MonetizationStrategiesJson)
        };
    }

    private static List<MonetizationStrategyDto> DeserializeMonetization(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json) || json == "[]") return [];
            return JsonSerializer.Deserialize<List<MonetizationStrategyDto>>(json, JsonOptions) ?? [];
        }
        catch { return []; }
    }

    private sealed class CombinedPlanResponse
    {
        public MvpPlanJson? MvpPlan { get; set; }
        public ActionPlanJson? ActionPlan { get; set; }
        public List<MonetizationJson>? MonetizationStrategies { get; set; }
    }

    private sealed record MvpPlanJson(
        [property: JsonPropertyName("problemStatement")] string? ProblemStatement,
        [property: JsonPropertyName("targetUsers")] string? TargetUsers,
        [property: JsonPropertyName("coreFeatures")] List<string>? CoreFeatures,
        [property: JsonPropertyName("techStack")] string? TechStack,
        [property: JsonPropertyName("validationStrategy")] string? ValidationStrategy,
        [property: JsonPropertyName("firstStep")] string? FirstStep,
        [property: JsonPropertyName("estimatedTimeline")] string? EstimatedTimeline);

    private sealed record ActionPlanJson(
        [property: JsonPropertyName("exactIcp")] string? ExactIcp,
        [property: JsonPropertyName("valueProposition")] string? ValueProposition,
        [property: JsonPropertyName("outreachMessage")] string? OutreachMessage,
        [property: JsonPropertyName("validationTest")] string? ValidationTest,
        [property: JsonPropertyName("firstStepTomorrow")] string? FirstStepTomorrow);

    private sealed record MonetizationJson(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("priceRange")] string? PriceRange);
}
