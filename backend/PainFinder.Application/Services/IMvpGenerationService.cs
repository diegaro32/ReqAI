using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IMvpGenerationService
{
    Task<MvpPlanDto> GenerateMvpPlanAsync(OpportunityDto opportunity, CancellationToken cancellationToken = default);
    Task SaveActionPlanAsync(Guid opportunityId, MvpPlanDto plan, CancellationToken cancellationToken = default);
    Task<MvpPlanDto?> GetActionPlanAsync(Guid opportunityId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, MvpPlanDto>> GetActionPlansForOpportunitiesAsync(IEnumerable<Guid> opportunityIds, CancellationToken cancellationToken = default);
}
