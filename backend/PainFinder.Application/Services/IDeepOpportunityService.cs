using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IDeepOpportunityService
{
    Task<DeepOpportunityAnalysisDto> AnalyzeAsync(
        OpportunityDto opportunity,
        CancellationToken cancellationToken = default);

    Task SaveDeepAnalysisAsync(
        Guid opportunityId,
        DeepOpportunityAnalysisDto analysis,
        CancellationToken cancellationToken = default);

    Task<DeepOpportunityAnalysisDto?> GetDeepAnalysisAsync(
        Guid opportunityId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, DeepOpportunityAnalysisDto>> GetDeepAnalysesForOpportunitiesAsync(
        IEnumerable<Guid> opportunityIds,
        CancellationToken cancellationToken = default);
}
