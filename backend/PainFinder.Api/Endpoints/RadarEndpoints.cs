using System.Security.Claims;
using PainFinder.Application.Services;
using PainFinder.Shared.Contracts;

namespace PainFinder.Api.Endpoints;

public static class RadarEndpoints
{
    public static RouteGroupBuilder MapRadarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/radar").WithTags("Radar").RequireAuthorization();

        group.MapPost("/monitors", async (
            CreateRadarMonitorRequest request,
            IRadarMonitorService service,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var result = await service.CreateMonitorAsync(request, userId, cancellationToken);
            return Results.Created($"/radar/monitors/{result.Id}", result);
        });

        group.MapGet("/monitors", async (IRadarMonitorService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var monitors = await service.GetAllMonitorsAsync(userId, cancellationToken);
            return Results.Ok(monitors);
        });

        group.MapGet("/monitors/{id:guid}", async (Guid id, IRadarMonitorService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var monitor = await service.GetMonitorAsync(id, userId, cancellationToken);
            return monitor is not null ? Results.Ok(monitor) : Results.NotFound();
        });

        group.MapGet("/monitors/{id:guid}/scans", async (Guid id, IRadarMonitorService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var scans = await service.GetMonitorScansAsync(id, userId, cancellationToken);
            return Results.Ok(scans);
        });

        group.MapPost("/monitors/{id:guid}/pause", async (Guid id, IRadarMonitorService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var result = await service.PauseMonitorAsync(id, userId, cancellationToken);
            return result ? Results.Ok() : Results.NotFound();
        });

        group.MapPost("/monitors/{id:guid}/resume", async (Guid id, IRadarMonitorService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var result = await service.ResumeMonitorAsync(id, userId, cancellationToken);
            return result ? Results.Ok() : Results.NotFound();
        });

        group.MapPost("/monitors/{id:guid}/archive", async (Guid id, IRadarMonitorService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var result = await service.ArchiveMonitorAsync(id, userId, cancellationToken);
            return result ? Results.Ok() : Results.NotFound();
        });

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}
