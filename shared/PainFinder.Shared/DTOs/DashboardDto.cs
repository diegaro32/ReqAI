namespace PainFinder.Shared.DTOs;

public record DashboardDto(
    int TotalDocuments,
    int TotalPainSignals,
    int TotalClusters,
    int TotalOpportunities,
    int ActiveSearchRuns,
    IReadOnlyList<PainClusterDto> TopClusters,
    IReadOnlyList<OpportunityDto> LatestOpportunities);
