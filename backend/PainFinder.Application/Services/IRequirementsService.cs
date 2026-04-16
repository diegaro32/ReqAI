using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IRequirementsService
{
    Task<RequirementGenerationDto> GenerateAsync(Guid userId, GenerateRequirementsRequest request, CancellationToken cancellationToken = default);
    Task<RefinementResultDto> RefineAsync(Guid userId, RefineRequirementsRequest request, CancellationToken cancellationToken = default);
    Task<List<RequirementGenerationDto>> GetProjectGenerationsAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
    Task<RequirementGenerationDto?> GetGenerationByIdAsync(Guid userId, Guid generationId, CancellationToken cancellationToken = default);
    Task<List<RequirementGenerationDto>> GetUserHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(int Used, int Max)> GetGenerationLimitsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteGenerationAsync(Guid userId, Guid generationId, CancellationToken cancellationToken = default);
}
