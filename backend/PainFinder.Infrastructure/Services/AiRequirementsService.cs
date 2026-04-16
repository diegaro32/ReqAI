using System.Text.Json;
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

        var prompt = BuildGenerationPrompt(request.ConversationInput);

        logger.LogInformation("Calling Gemini for project {ProjectId}, user {UserId}", request.ProjectId, userId);

        ChatResponse response;
        try
        {
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
            OriginalInput = request.ConversationInput,
            FunctionalRequirements = JsonSerializer.Serialize(output.FunctionalRequirements),
            NonFunctionalRequirements = JsonSerializer.Serialize(output.NonFunctionalRequirements),
            Ambiguities = JsonSerializer.Serialize(output.Ambiguities),
            SuggestedFeatures = JsonSerializer.Serialize(output.SuggestedFeatures),
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

    private static RequirementGenerationDto ToDto(RequirementGeneration g, string projectName) =>
        new(g.Id,
            g.ProjectId,
            projectName,
            g.OriginalInput,
            g.FunctionalRequirements,
            g.NonFunctionalRequirements,
            g.Ambiguities,
            g.SuggestedFeatures,
            g.CreatedAt,
            g.Refinements
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RefinementResultDto(r.Id, r.GenerationId, r.Instruction, r.RefinedOutput, r.CreatedAt))
                .ToList());

    private RequirementsOutput ParseOutput(string jsonText, string context)
    {
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

    private static string BuildGenerationPrompt(string conversationInput) => $$"""
        Eres un analista de software senior. Analiza la siguiente conversación con el cliente y extrae requerimientos de software estructurados.

        CONVERSACIÓN:
        {{conversationInput}}

        Responde con un objeto JSON usando exactamente esta estructura:
        {
          "functionalRequirements": ["El sistema deberá...", "..."],
          "nonFunctionalRequirements": ["El sistema deberá responder en menos de...", "..."],
          "ambiguities": ["No queda claro si...", "..."],
          "suggestedFeatures": ["Se sugiere agregar...", "..."]
        }

        Reglas:
        - functionalRequirements: Lo que el sistema debe HACER (comportamiento visible para el usuario, operaciones CRUD, flujos de trabajo)
        - nonFunctionalRequirements: Atributos de calidad — rendimiento, seguridad, escalabilidad, disponibilidad, usabilidad
        - ambiguities: Puntos poco claros, información faltante o contradicciones que requieren aclaración del cliente
        - suggestedFeatures: Mejoras opcionales más allá de lo solicitado explícitamente — incluir solo cuando sean claramente relevantes
        - Usa lenguaje imperativo y preciso. Mínimo 3 ítems por sección cuando el contenido lo permita.
        - Responde ÚNICAMENTE con JSON válido. Sin markdown, sin bloques de código, sin texto adicional.
        - Toda la respuesta debe estar en español.
        """;

    private static string BuildRefinementPrompt(RequirementGeneration generation, string instruction) => $$"""
        Eres un analista de software senior. Refina los siguientes requerimientos de software según la instrucción dada.

        REQUERIMIENTOS ACTUALES:
        Funcionales: {{generation.FunctionalRequirements}}
        No Funcionales: {{generation.NonFunctionalRequirements}}
        Ambigüedades: {{generation.Ambiguities}}
        Funcionalidades Sugeridas: {{generation.SuggestedFeatures}}

        INSTRUCCIÓN DE REFINAMIENTO: "{{instruction}}"

        Responde con un objeto JSON usando exactamente esta estructura:
        {
          "functionalRequirements": ["El sistema deberá...", "..."],
          "nonFunctionalRequirements": ["El sistema deberá responder en menos de...", "..."],
          "ambiguities": ["No queda claro si...", "..."],
          "suggestedFeatures": ["Se sugiere agregar...", "..."]
        }

        Aplica la instrucción con cuidado. Preserva la estructura y completitud.
        Responde ÚNICAMENTE con JSON válido. Sin markdown, sin bloques de código, sin texto adicional.
        Toda la respuesta debe estar en español.
        """;

    private sealed class RequirementsOutput
    {
        public List<string> FunctionalRequirements { get; set; } = [];
        public List<string> NonFunctionalRequirements { get; set; } = [];
        public List<string> Ambiguities { get; set; } = [];
        public List<string> SuggestedFeatures { get; set; } = [];
    }
}
