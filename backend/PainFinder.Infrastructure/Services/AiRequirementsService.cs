using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Infrastructure.Services;

public class AiRequirementsService(
    IChatClient chatClient,
    PainFinderDbContext dbContext,
    ISubscriptionService subscriptionService,
    ILogger<AiRequirementsService> logger) : IRequirementsService
{
    private const int FreeGenerationLimit = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RequirementGenerationDto> GenerateAsync(Guid userId, GenerateRequirementsRequest request, CancellationToken cancellationToken = default)
    {
        var (used, max) = await GetGenerationLimitsAsync(userId, cancellationToken);

        if (max != -1 && used >= max)
            throw new InvalidOperationException($"Has alcanzado el límite de {max} generaciones del plan gratuito. Actualiza tu plan para continuar.");

        var project = await dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Proyecto no encontrado.");

        var normalizedInput = NormalizeWhitespace(request.ConversationInput);
        var projectContext = await BuildProjectContextAsync(request.ProjectId, cancellationToken);

        logger.LogInformation("Calling Gemini for project {ProjectId}, user {UserId}", request.ProjectId, userId);

        ChatResponse response;
        try
        {
            var prompt = BuildGenerationPrompt(normalizedInput, request.AnalysisMode, projectContext);
            response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gemini API call failed for project {ProjectId}. Message: {Message}", request.ProjectId, ex.Message);
            throw new InvalidOperationException($"Error al llamar a la IA: {ex.Message}", ex);
        }

        var jsonText = response.Messages[^1].Text ?? "{}";
        var output = ParseOutput(jsonText, nameof(GenerateAsync));

        var generation = new RequirementGeneration
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            ConversationInput = normalizedInput,
            SystemOverview = output.SystemOverview,
            DomainModel = JsonSerializer.Serialize(output.DomainModel),
            LifecycleModel = JsonSerializer.Serialize(output.LifecycleModel),
            FunctionalRequirements = JsonSerializer.Serialize(output.FunctionalRequirements),
            BusinessRules = JsonSerializer.Serialize(output.BusinessRules),
            Inconsistencies = JsonSerializer.Serialize(output.Inconsistencies),
            NonFunctionalRequirements = JsonSerializer.Serialize(output.NonFunctionalRequirements),
            Ambiguities = JsonSerializer.Serialize(output.Ambiguities),
            Prioritization = JsonSerializer.Serialize(output.Prioritization),
            DecisionPoints = JsonSerializer.Serialize(output.DecisionPoints),
            OwnershipActions = JsonSerializer.Serialize(output.OwnershipActions),
            SystemInsights = JsonSerializer.Serialize(output.SystemInsights),
            SuggestedFeatures = JsonSerializer.Serialize(output.SuggestedFeatures),
            ImplementationRisks = JsonSerializer.Serialize(output.ImplementationRisks),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.RequirementGenerations.Add(generation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(generation, project.Name);
    }

    public async Task<RefinementResultDto> RefineAsync(Guid userId, RefineRequirementsRequest request, CancellationToken cancellationToken = default)
    {
        var generation = await dbContext.RequirementGenerations
            .Include(g => g.Project)
            .FirstOrDefaultAsync(g => g.Id == request.GenerationId && g.Project.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Generación no encontrada.");

        var prompt = BuildRefinementPrompt(generation, request.Instruction);

        logger.LogInformation("Refining generation {GenerationId} for user {UserId}", request.GenerationId, userId);

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            cancellationToken: cancellationToken);

        var jsonText = response.Messages[^1].Text ?? "{}";
        var output = ParseOutput(jsonText, nameof(RefineAsync));

        var refinement = new RefinementResult
        {
            Id = Guid.NewGuid(),
            GenerationId = request.GenerationId,
            Instruction = request.Instruction,
            RefinedOutput = JsonSerializer.Serialize(output),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.RefinementResults.Add(refinement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RefinementResultDto(
            refinement.Id,
            refinement.GenerationId,
            refinement.Instruction,
            refinement.RefinedOutput,
            refinement.CreatedAt);
    }

    public async Task<List<RequirementGenerationDto>> GetProjectGenerationsAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId, cancellationToken);

        if (project is null) return [];

        var generations = await dbContext.RequirementGenerations
            .Include(g => g.Refinements)
            .Where(g => g.ProjectId == projectId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        return generations.Select(g => ToDto(g, project.Name)).ToList();
    }

    public async Task<RequirementGenerationDto?> GetGenerationByIdAsync(Guid userId, Guid generationId, CancellationToken cancellationToken = default)
    {
        var generation = await dbContext.RequirementGenerations
            .Include(g => g.Refinements)
            .Include(g => g.Project)
            .FirstOrDefaultAsync(g => g.Id == generationId && g.Project.UserId == userId, cancellationToken);

        return generation is null ? null : ToDto(generation, generation.Project.Name);
    }

    public async Task<List<RequirementGenerationDto>> GetUserHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var generations = await dbContext.RequirementGenerations
            .Include(g => g.Refinements)
            .Include(g => g.Project)
            .Where(g => g.Project.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        return generations.Select(g => ToDto(g, g.Project.Name)).ToList();
    }

    public async Task<(int Used, int Max)> GetGenerationLimitsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await subscriptionService.GetUserPlanAsync(userId, cancellationToken);

        var used = await dbContext.RequirementGenerations
            .CountAsync(g => g.Project.UserId == userId, cancellationToken);

        var max = plan.PlanType == "Free" ? FreeGenerationLimit : -1;
        return (used, max);
    }

    public async Task DeleteGenerationAsync(Guid userId, Guid generationId, CancellationToken cancellationToken = default)
    {
        var generation = await dbContext.RequirementGenerations
            .Include(g => g.Project)
            .FirstOrDefaultAsync(g => g.Id == generationId && g.Project.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Generación no encontrada.");

        dbContext.RequirementGenerations.Remove(generation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static RequirementGenerationDto ToDto(RequirementGeneration g, string projectName) =>
        new(g.Id,
            g.ProjectId,
            projectName,
            g.ConversationInput,
            g.SystemOverview,
            g.DomainModel,
            g.LifecycleModel,
            g.FunctionalRequirements,
            g.BusinessRules,
            g.Inconsistencies,
            g.NonFunctionalRequirements,
            g.Ambiguities,
            g.Prioritization,
            g.DecisionPoints,
            g.OwnershipActions,
            g.SystemInsights,
            g.SuggestedFeatures,
            g.ImplementationRisks,
            g.CreatedAt,
            g.Refinements
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RefinementResultDto(r.Id, r.GenerationId, r.Instruction, r.RefinedOutput, r.CreatedAt))
                .ToList());

    private RequirementsOutput ParseOutput(string jsonText, string context)
    {
        // Extraer JSON si viene envuelto en markdown code block
        var match = System.Text.RegularExpressions.Regex.Match(jsonText, @"```(?:json)?\s*([\s\S]*?)```");
        if (match.Success) jsonText = match.Groups[1].Value.Trim();

        try
        {
            return JsonSerializer.Deserialize<RequirementsOutput>(jsonText, JsonOptions) ?? new RequirementsOutput();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Context}] Failed to parse AI JSON response: {Json}", context, jsonText[..Math.Min(300, jsonText.Length)]);
            return new RequirementsOutput();
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        text = Regex.Replace(text, @"\b\d{1,2}:\d{2}(:\d{2})?(\s?(AM|PM|am|pm))?\b", string.Empty);
        text = Regex.Replace(text, @"\b\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4}\b", string.Empty);
        text = Regex.Replace(text, @"\b\d{4}[\/\-\.]\d{1,2}[\/\-\.]\d{1,2}\b", string.Empty);
        text = Regex.Replace(text, @"[\[\(]\s*[\]\)]", string.Empty);
        text = Regex.Replace(text, @"\n{2,}", "\n");
        text = string.Join("\n", text.Split('\n').Select(l => l.Trim()));
        text = Regex.Replace(text, @"\n{2,}", "\n");
        return text.Trim();
    }

    private async Task<string> BuildProjectContextAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var previousGenerations = await dbContext.RequirementGenerations
            .Where(g => g.ProjectId == projectId)
            .OrderByDescending(g => g.CreatedAt)
            .Take(3)
            .Select(g => new
            {
                g.CreatedAt,
                g.SystemOverview,
                g.FunctionalRequirements,
                g.BusinessRules,
                g.Inconsistencies,
                g.Ambiguities,
                g.Prioritization,
                g.ImplementationRisks
            })
            .ToListAsync(cancellationToken);

        if (previousGenerations.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var generation in previousGenerations.OrderBy(g => g.CreatedAt))
        {
            sb.AppendLine($"Generación previa ({generation.CreatedAt:yyyy-MM-dd HH:mm} UTC)");
            AppendContextField(sb, "Resumen", generation.SystemOverview);
            AppendContextField(sb, "Requerimientos funcionales", generation.FunctionalRequirements);
            AppendContextField(sb, "Reglas de negocio", generation.BusinessRules);
            AppendContextField(sb, "Inconsistencias", generation.Inconsistencies);
            AppendContextField(sb, "Ambigüedades", generation.Ambiguities);
            AppendContextField(sb, "Prioridad MVP", generation.Prioritization);
            AppendContextField(sb, "Riesgos", generation.ImplementationRisks);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendContextField(System.Text.StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "[]" or "{}")
            return;

        sb.AppendLine($"{label}: {Truncate(value, 1200)}");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static string BuildGenerationPrompt(string conversationInput, string analysisMode, string projectContext)
    {
        var isQuick = string.Equals(analysisMode, "Quick", StringComparison.OrdinalIgnoreCase);
        var hasProjectContext = !string.IsNullOrWhiteSpace(projectContext);
        var modeInstructions = isQuick
            ? """
              QUICK MODE:
              Produce only the 3 most useful sections for fast validation:
              - systemOverview
              - functionalRequirements
              - ambiguities

              For every other field in the JSON schema, return an empty object or empty array as appropriate.
              Keep it concise and practical.
              """
            : """
              DEEP MODE:
              Produce a compact but complete analysis using only these 7 sections:
              - systemOverview
              - functionalRequirements
              - businessRules
              - ambiguities
              - inconsistencies
              - prioritization
              - implementationRisks

              For every other field in the JSON schema, return an empty object or empty array as appropriate.
              Avoid architecture-heavy output unless it is directly needed to clarify the requirements.
              """;
        var contextInstructions = hasProjectContext
            ? $$"""
              PROJECT HISTORY CONTEXT:
              {{projectContext}}

              Use this history as context, not as absolute truth.
              The new conversation has priority.
              If the new conversation confirms something from history, mention it in systemInsights as "Confirmado: ...".
              If it introduces something new, mention it in systemInsights as "Nuevo: ...".
              If it changes a previous requirement, mention it in systemInsights as "Modificado: ...".
              If it contradicts previous history, mention it in systemInsights as "Contradicción: ...".
              Do not inherit previous assumptions if the new conversation does not support them.
              """
            : """
              PROJECT HISTORY CONTEXT:
              No previous generations exist for this project. Return systemInsights as an empty array unless a high-value change insight is explicitly needed.
              """;

        return $$"""
        You are a senior software architect operating in CRITICAL THINKING MODE.

        Your job is NOT to summarize. Produce clear, engineering-ready requirements from a client conversation.

        {{modeInstructions}}

        {{contextInstructions}}

        GENERAL RULES:
        - Reduce redundancy: high signal, low noise.
        - Do not invent details that are not supported by the conversation.
        - If something is unclear, put it in ambiguities as a question.
        - Detect inconsistencies and conflicts; do not silently resolve them.
        - Keep each item actionable and specific.

        CONVERSATION TO ANALYZE:
        {{conversationInput}}

        Respond with a JSON object using EXACTLY this structure (no markdown, no code blocks, just valid JSON):
        {
          "systemOverview": "Specific system type and core purpose in 2-3 sentences",
          "domainModel": {
            "entities": ["Entity1", "Entity2"],
            "relationships": ["Entity1 has many Entity2"]
          },
          "lifecycleModel": [
            {
              "entity": "EntityName",
              "states": ["State1", "State2", "State3"],
              "transitions": ["State1 → State2: trigger", "State2 → State3: trigger"]
            }
          ],
          "functionalRequirements": {
            "GroupName": ["The system shall...", "..."]
          },
          "businessRules": ["Rule: precise constraint or condition"],
          "inconsistencies": [
            {
              "conflict": "What was said vs what conflicts",
              "risk": "What risk this introduces if unresolved"
            }
          ],
          "nonFunctionalRequirements": ["Performance: ...", "Security: ..."],
          "ambiguities": ["Missing flow: ...", "Undefined ownership: ...", "Edge case: ..."],
          "prioritization": {
            "core": ["MVP-critical requirement"],
            "important": ["Next phase requirement"],
            "optional": ["Future enhancement"]
          },
          "decisionPoints": [
            {
              "decision": "What needs to be decided",
              "optionA": "First option",
              "optionB": "Second option (if applicable)",
              "impact": "What changes depending on the decision",
              "recommendation": "Best option based on context, or empty string if unclear"
            }
          ],
          "ownershipActions": [
            {
              "role": "Product Owner / Tech Lead / Backend Dev / etc.",
              "actions": ["Action 1", "Action 2"]
            }
          ],
          "systemInsights": ["History change note if project history exists, e.g. Nuevo/Modificado/Contradicción/Confirmado"],
          "suggestedFeatures": ["Feature clearly derived from conversation"],
          "implementationRisks": [
            {
              "area": "Area or module at risk",
              "risk": "Why it is risky or complex",
              "mitigation": "Suggested mitigation or design consideration"
            }
          ]
        }

        RULES:
        - The selected mode instructions are mandatory and override the example placeholders above.
        - In quick mode, only fill systemOverview, functionalRequirements and ambiguities.
        - In deep mode, only fill systemOverview, functionalRequirements, businessRules, ambiguities, inconsistencies, prioritization and implementationRisks.
        - If project history exists, systemInsights is allowed in both modes and must contain only changes versus history.
        - If project history does not exist, systemInsights must be [].
        - inconsistencies.risk: explain the implementation consequence if not resolved
        - For every field outside the selected mode, return an empty object or empty array as appropriate.
        - implementationRisks: focus on areas likely to break, needing careful design, or hidden complexity
        - Be strict with prioritization — if not MVP-critical, do not put it in core
        - All text MUST be in Spanish
        - Respond ONLY with valid JSON. No markdown, no code blocks, no extra text.
        """;
    }

    private static string BuildRefinementPrompt(RequirementGeneration generation, string instruction) => $$"""
        You are a senior software architect in CRITICAL THINKING MODE.
        Refine the following analysis based on the given instruction.

        CURRENT ANALYSIS:
        System Overview: {{generation.SystemOverview}}
        Domain Model: {{generation.DomainModel}}
        Lifecycle Model: {{generation.LifecycleModel}}
        Functional Requirements: {{generation.FunctionalRequirements}}
        Business Rules: {{generation.BusinessRules}}
        Inconsistencies: {{generation.Inconsistencies}}
        Non-Functional Requirements: {{generation.NonFunctionalRequirements}}
        Ambiguities: {{generation.Ambiguities}}
        Prioritization: {{generation.Prioritization}}
        Decision Points: {{generation.DecisionPoints}}
        Ownership & Actions: {{generation.OwnershipActions}}
        System Insights: {{generation.SystemInsights}}
        Suggested Features: {{generation.SuggestedFeatures}}
        Implementation Risks: {{generation.ImplementationRisks}}

        REFINEMENT INSTRUCTION: "{{instruction}}"

        Respond with a JSON object using EXACTLY this structure:
        {
          "systemOverview": "string",
          "domainModel": { "entities": [], "relationships": [] },
          "lifecycleModel": [{ "entity": "", "states": [], "transitions": [] }],
          "functionalRequirements": { "Group": [] },
          "businessRules": [],
          "inconsistencies": [{ "conflict": "", "risk": "" }],
          "nonFunctionalRequirements": [],
          "ambiguities": [],
          "prioritization": { "core": [], "important": [], "optional": [] },
          "decisionPoints": [{ "decision": "", "optionA": "", "optionB": "", "impact": "", "recommendation": "" }],
          "ownershipActions": [{ "role": "", "actions": [] }],
          "systemInsights": [],
          "suggestedFeatures": [],
          "implementationRisks": [{ "area": "", "risk": "", "mitigation": "" }]
        }

        Apply the instruction carefully. Maintain depth and precision.
        All text MUST be in Spanish.
        Respond ONLY with valid JSON. No markdown, no code blocks, no extra text.
        """;

    private sealed class DomainModelOutput
    {
        public List<string> Entities { get; set; } = [];
        public List<string> Relationships { get; set; } = [];
    }

    private sealed class LifecycleEntry
    {
        public string Entity { get; set; } = string.Empty;
        public List<string> States { get; set; } = [];
        public List<string> Transitions { get; set; } = [];
    }

    private sealed class InconsistencyEntry
    {
        public string Conflict { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
    }

    private sealed class PrioritizationOutput
    {
        public List<string> Core { get; set; } = [];
        public List<string> Important { get; set; } = [];
        public List<string> Optional { get; set; } = [];
    }

    private sealed class DecisionPointEntry
    {
        public string Decision { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    private sealed class OwnershipEntry
    {
        public string Role { get; set; } = string.Empty;
        public List<string> Actions { get; set; } = [];
    }

    private sealed class ImplementationRiskEntry
    {
        public string Area { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string Mitigation { get; set; } = string.Empty;
    }

    private sealed class RequirementsOutput
    {
        public string SystemOverview { get; set; } = string.Empty;
        public DomainModelOutput DomainModel { get; set; } = new();
        public List<LifecycleEntry> LifecycleModel { get; set; } = [];
        public Dictionary<string, List<string>> FunctionalRequirements { get; set; } = [];
        public List<string> BusinessRules { get; set; } = [];
        public List<InconsistencyEntry> Inconsistencies { get; set; } = [];
        public List<string> NonFunctionalRequirements { get; set; } = [];
        public List<string> Ambiguities { get; set; } = [];
        public PrioritizationOutput Prioritization { get; set; } = new();
        public List<DecisionPointEntry> DecisionPoints { get; set; } = [];
        public List<OwnershipEntry> OwnershipActions { get; set; } = [];
        public List<string> SystemInsights { get; set; } = [];
        public List<string> SuggestedFeatures { get; set; } = [];
        public List<ImplementationRiskEntry> ImplementationRisks { get; set; } = [];
    }
}
