using PainFinder.Application.Mapping;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public class SearchRunService(
    IRepository<SearchRun> searchRunRepository,
    IRepository<RawDocument> documentRepository,
    IRepository<PainSignal> painSignalRepository,
    IRepository<PainCluster> painClusterRepository,
    IRepository<Opportunity> opportunityRepository,
    ISubscriptionService subscriptionService,
    IUnitOfWork unitOfWork) : ISearchRunService
{
    public async Task<SearchRunResponse> StartSearchRunAsync(SearchRunRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await subscriptionService.CanUserSearchAsync(userId, cancellationToken))
            throw new InvalidOperationException("You've reached the search limit on your free plan. Upgrade to keep discovering opportunities.");

        var searchRun = new SearchRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            Status = SearchRunStatus.Pending,
            Sources = string.Join(",", request.Sources),
            Keyword = request.Keyword,
            DateRangeFrom = request.DateFrom,
            DateRangeTo = request.DateTo
        };

        await searchRunRepository.AddAsync(searchRun, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await subscriptionService.IncrementSearchUsageAsync(userId, cancellationToken);

        return new SearchRunResponse(searchRun.Id, searchRun.Status.ToString(), searchRun.StartedAt);
    }

    public async Task<SearchRunDto?> GetSearchRunAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var run = await searchRunRepository.GetByIdAsync(id, cancellationToken);
        if (run is null || run.UserId != userId) return null;
        return run.ToDto();
    }

    public async Task<IReadOnlyList<SearchRunDto>> GetAllSearchRunsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var runs = await searchRunRepository.GetAllAsync(cancellationToken);
        return runs
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => r.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<RawDocumentDto>> GetSearchRunDocumentsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var run = await searchRunRepository.GetByIdAsync(id, cancellationToken);
        if (run is null || run.UserId != userId) return [];

        var allDocs = await documentRepository.GetAllAsync(cancellationToken, d => d.Source);
        return allDocs
            .Where(d => d.SearchRunId == id)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<PainSignalDto>> GetSearchRunPainsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var run = await searchRunRepository.GetByIdAsync(id, cancellationToken);
        if (run is null || run.UserId != userId) return [];

        var allDocs = await documentRepository.GetAllAsync(cancellationToken);
        var docIds = allDocs.Where(d => d.SearchRunId == id).Select(d => d.Id).ToHashSet();

        var allSignals = await painSignalRepository.GetAllAsync(cancellationToken, s => s.RawDocument);
        return allSignals
            .Where(s => docIds.Contains(s.RawDocumentId))
            .Select(s => s.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<PainClusterDto>> GetSearchRunClustersAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var run = await searchRunRepository.GetByIdAsync(id, cancellationToken);
        if (run is null || run.UserId != userId) return [];

        var allDocs = await documentRepository.GetAllAsync(cancellationToken);
        var docIds = allDocs.Where(d => d.SearchRunId == id).Select(d => d.Id).ToHashSet();

        var allSignals = await painSignalRepository.GetAllAsync(cancellationToken);
        var clusterIds = allSignals
            .Where(s => docIds.Contains(s.RawDocumentId) && s.PainClusterId.HasValue)
            .Select(s => s.PainClusterId!.Value)
            .Distinct()
            .ToHashSet();

        var allClusters = await painClusterRepository.GetAllAsync(cancellationToken);
        return allClusters
            .Where(c => clusterIds.Contains(c.Id))
            .OrderByDescending(c => c.SeverityScore)
            .Select(c => c.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<OpportunityDto>> GetSearchRunOpportunitiesAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var run = await searchRunRepository.GetByIdAsync(id, cancellationToken);
        if (run is null || run.UserId != userId) return [];

        var allDocs = await documentRepository.GetAllAsync(cancellationToken);
        var docIds = allDocs.Where(d => d.SearchRunId == id).Select(d => d.Id).ToHashSet();

        var allSignals = await painSignalRepository.GetAllAsync(cancellationToken);
        var clusterIds = allSignals
            .Where(s => docIds.Contains(s.RawDocumentId) && s.PainClusterId.HasValue)
            .Select(s => s.PainClusterId!.Value)
            .Distinct()
            .ToHashSet();

        var allOpportunities = await opportunityRepository.GetAllAsync(cancellationToken, o => o.PainCluster);
        return allOpportunities
            .Where(o => clusterIds.Contains(o.PainClusterId))
            .OrderByDescending(o => o.ConfidenceScore)
            .Select(o => o.ToDto())
            .ToList();
    }

    public async Task<bool> DeleteSearchRunAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var run = await searchRunRepository.GetByIdAsync(id, cancellationToken);
        if (run is null || run.UserId != userId) return false;

        var allDocs = await documentRepository.GetAllAsync(cancellationToken);
        var runDocs = allDocs.Where(d => d.SearchRunId == id).ToList();
        var docIds = runDocs.Select(d => d.Id).ToHashSet();

        var allSignals = await painSignalRepository.GetAllAsync(cancellationToken);
        var runSignals = allSignals.Where(s => docIds.Contains(s.RawDocumentId)).ToList();

        var clusterIds = runSignals
            .Where(s => s.PainClusterId.HasValue)
            .Select(s => s.PainClusterId!.Value)
            .ToHashSet();

        foreach (var signal in runSignals)
            await painSignalRepository.DeleteAsync(signal, cancellationToken);

        foreach (var doc in runDocs)
            await documentRepository.DeleteAsync(doc, cancellationToken);

        await searchRunRepository.DeleteAsync(run, cancellationToken);

        if (clusterIds.Count > 0)
        {
            var allClusters = await painClusterRepository.GetAllAsync(cancellationToken);
            var runClusters = allClusters.Where(c => clusterIds.Contains(c.Id)).ToList();
            foreach (var cluster in runClusters)
                await painClusterRepository.DeleteAsync(cluster, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
