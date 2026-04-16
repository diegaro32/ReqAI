using Microsoft.EntityFrameworkCore;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Infrastructure.Services;

public class ProjectService(PainFinderDbContext dbContext) : IProjectService
{
    public async Task<List<ProjectDto>> GetUserProjectsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Projects
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectDto(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                p.Generations.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectDto> CreateProjectAsync(Guid userId, CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, 0);
    }

    public async Task<ProjectDto?> GetProjectByIdAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Projects
            .Where(p => p.UserId == userId && p.Id == projectId)
            .Select(p => new ProjectDto(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                p.Generations.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == projectId, cancellationToken);

        if (project is null) return;

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
