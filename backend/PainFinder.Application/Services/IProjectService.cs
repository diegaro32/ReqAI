using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IProjectService
{
    Task<List<ProjectDto>> GetUserProjectsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProjectDto> CreateProjectAsync(Guid userId, CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ProjectDto?> GetProjectByIdAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
    Task DeleteProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
}
