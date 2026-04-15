using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IOpportunityQueryService
{
    Task<IReadOnlyList<OpportunityDto>> GetAllOpportunitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpportunityWithSearchDto>> GetAllOpportunitiesGroupedBySearchAsync(Guid userId, CancellationToken cancellationToken = default);
}
