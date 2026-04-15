using PainFinder.Application.Mapping;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public class DashboardService(
    IRepository<RawDocument> documentRepository,
    IRepository<PainSignal> painSignalRepository,
    IRepository<PainCluster> clusterRepository,
    IRepository<Opportunity> opportunityRepository,
    IRepository<SearchRun> searchRunRepository) : IDashboardService
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var documents = await documentRepository.GetAllAsync(cancellationToken);
        var painSignals = await painSignalRepository.GetAllAsync(cancellationToken);
        var clusters = await clusterRepository.GetAllAsync(cancellationToken);
        var opportunities = await opportunityRepository.GetAllAsync(cancellationToken);
        var searchRuns = await searchRunRepository.GetAllAsync(cancellationToken);

        var activeRuns = searchRuns.Count(r => r.Status == SearchRunStatus.Expanding || r.Status == SearchRunStatus.Scraping || r.Status == SearchRunStatus.Analyzing || r.Status == SearchRunStatus.Pending);

        var topClusters = clusters
            .OrderByDescending(c => c.SeverityScore)
            .Take(5)
            .Select(c => c.ToDto())
            .ToList();

        var latestOpportunities = opportunities
            .OrderByDescending(o => o.ConfidenceScore)
            .Take(5)
            .Select(o => o.ToDto())
            .ToList();

        return new DashboardDto(
            documents.Count,
            painSignals.Count,
            clusters.Count,
            opportunities.Count,
            activeRuns,
            topClusters,
            latestOpportunities);
    }
}
