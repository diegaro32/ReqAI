using System.Security.Claims;
using PainFinder.Application.Services;
using PainFinder.Shared.Contracts;

namespace PainFinder.Api.Endpoints;

public static class RequirementsEndpoints
{
    public static RouteGroupBuilder MapRequirementsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/requirements").WithTags("Requirements");

        group.MapPost("/generate", async (
            GenerateRequirementsRequest request,
            ClaimsPrincipal user,
            IRequirementsService requirementsService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.ConversationInput))
                return Results.BadRequest(new { error = "La conversación no puede estar vacía." });

            try
            {
                var result = await requirementsService.GenerateAsync(userId.Value, request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/refine", async (
            RefineRequirementsRequest request,
            ClaimsPrincipal user,
            IRequirementsService requirementsService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Instruction))
                return Results.BadRequest(new { error = "La instrucción de refinamiento no puede estar vacía." });

            try
            {
                var result = await requirementsService.RefineAsync(userId.Value, request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/project/{projectId:guid}", async (
            Guid projectId,
            ClaimsPrincipal user,
            IRequirementsService requirementsService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var generations = await requirementsService.GetProjectGenerationsAsync(userId.Value, projectId, cancellationToken);
            return Results.Ok(generations);
        });

        group.MapGet("/{generationId:guid}", async (
            Guid generationId,
            ClaimsPrincipal user,
            IRequirementsService requirementsService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var generation = await requirementsService.GetGenerationByIdAsync(userId.Value, generationId, cancellationToken);
            return generation is not null ? Results.Ok(generation) : Results.NotFound();
        });

        group.MapGet("/history", async (
            ClaimsPrincipal user,
            IRequirementsService requirementsService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var history = await requirementsService.GetUserHistoryAsync(userId.Value, cancellationToken);
            return Results.Ok(history);
        });

        group.MapGet("/limits", async (
            ClaimsPrincipal user,
            IRequirementsService requirementsService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var (used, max) = await requirementsService.GetGenerationLimitsAsync(userId.Value, cancellationToken);
            return Results.Ok(new { used, max });
        });

        return group;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
