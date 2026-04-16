using System.Security.Claims;
using PainFinder.Application.Services;
using PainFinder.Shared.Contracts;

namespace PainFinder.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/projects").WithTags("Projects");

        group.MapGet("/", async (
            ClaimsPrincipal user,
            IProjectService projectService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var projects = await projectService.GetUserProjectsAsync(userId.Value, cancellationToken);
            return Results.Ok(projects);
        });

        group.MapPost("/", async (
            CreateProjectRequest request,
            ClaimsPrincipal user,
            IProjectService projectService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "El nombre del proyecto es obligatorio." });

            var project = await projectService.CreateProjectAsync(userId.Value, request, cancellationToken);
            return Results.Created($"/projects/{project.Id}", project);
        });

        group.MapGet("/{projectId:guid}", async (
            Guid projectId,
            ClaimsPrincipal user,
            IProjectService projectService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            var project = await projectService.GetProjectByIdAsync(userId.Value, projectId, cancellationToken);
            return project is not null ? Results.Ok(project) : Results.NotFound();
        });

        group.MapDelete("/{projectId:guid}", async (
            Guid projectId,
            ClaimsPrincipal user,
            IProjectService projectService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            await projectService.DeleteProjectAsync(userId.Value, projectId, cancellationToken);
            return Results.NoContent();
        });

        return group;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
