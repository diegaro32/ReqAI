using PainFinder.Application.Mapping;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public class OpportunityQueryService(
    IRepository<Opportunity> opportunityRepository) : IOpportunityQueryService
{
    public async Task<IReadOnlyList<OpportunityDto>> GetAllOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        var opportunities = await opportunityRepository.GetAllAsync(cancellationToken, o => o.PainCluster);
        return opportunities.Select(o => o.ToDto()).ToList();
    }

    public Task<IReadOnlyList<OpportunityWithSearchDto>> GetAllOpportunitiesGroupedBySearchAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
