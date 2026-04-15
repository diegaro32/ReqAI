using PainFinder.Domain.Entities;

namespace PainFinder.Domain.Interfaces;

public interface IOpportunityGenerationService
{
    Task<IReadOnlyList<Opportunity>> GenerateOpportunitiesAsync(IReadOnlyList<PainCluster> clusters, CancellationToken cancellationToken = default);
}
