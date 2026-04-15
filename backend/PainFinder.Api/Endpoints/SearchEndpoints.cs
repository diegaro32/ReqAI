using System.Security.Claims;
using FluentValidation;
using PainFinder.Application.Services;
using PainFinder.Shared.Contracts;

namespace PainFinder.Api.Endpoints;

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/search").WithTags("Search").RequireAuthorization();

        group.MapPost("/run", async (
            SearchRunRequest request,
            IValidator<SearchRunRequest> validator,
            ISearchRunService service,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await service.StartSearchRunAsync(request, userId, cancellationToken);
            return Results.Created($"/search/run/{result.Id}", result);
        });

        group.MapGet("/run", async (ISearchRunService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var runs = await service.GetAllSearchRunsAsync(userId, cancellationToken);
            return Results.Ok(runs);
        });

        group.MapGet("/run/{id:guid}", async (Guid id, ISearchRunService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var run = await service.GetSearchRunAsync(id, userId, cancellationToken);
            return run is not null ? Results.Ok(run) : Results.NotFound();
        });

        group.MapGet("/run/{id:guid}/documents", async (Guid id, ISearchRunService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var docs = await service.GetSearchRunDocumentsAsync(id, userId, cancellationToken);
            return Results.Ok(docs);
        });

        group.MapGet("/run/{id:guid}/pains", async (Guid id, ISearchRunService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var pains = await service.GetSearchRunPainsAsync(id, userId, cancellationToken);
            return Results.Ok(pains);
        });

        group.MapDelete("/run/{id:guid}", async (Guid id, ISearchRunService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var deleted = await service.DeleteSearchRunAsync(id, userId, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}
