using Microsoft.EntityFrameworkCore;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Persistence;

namespace PainFinder.Api.BackgroundServices;

/// <summary>
/// Unified background service: scrapes sources, analyzes pains, generates opportunities.
/// Single service = no handoff between services, no stuck runs, no race conditions.
/// </summary>
public class ScraperBackgroundService(
    ILogger<ScraperBackgroundService> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scraper background service started (unified: scrape + analyze)");

        await ResetStaleRunsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var pendingRun = await dbContext.SearchRuns
                    .FirstOrDefaultAsync(r => r.Status == SearchRunStatus.Pending, stoppingToken);

                if (pendingRun is not null)
                {
                    var connectors = scope.ServiceProvider.GetRequiredService<IEnumerable<ISourceConnector>>();
                    var sourceRepo = scope.ServiceProvider.GetRequiredService<IRepository<Source>>();
                    var keywordExpansion = scope.ServiceProvider.GetRequiredService<IKeywordExpansionService>();
                    var painDetection = scope.ServiceProvider.GetRequiredService<IPainDetectionService>();
                    var painClustering = scope.ServiceProvider.GetRequiredService<IPainClusteringService>();
                    var opportunityGeneration = scope.ServiceProvider.GetRequiredService<IOpportunityGenerationService>();

                    await ProcessRunAsync(
                        pendingRun, dbContext, connectors, unitOfWork, sourceRepo,
                        keywordExpansion, painDetection, painClustering, opportunityGeneration,
                        stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scraper: Unhandled error in main loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessRunAsync(
        SearchRun run,
        PainFinderDbContext dbContext,
        IEnumerable<ISourceConnector> connectors,
        IUnitOfWork unitOfWork,
        IRepository<Source> sourceRepo,
        IKeywordExpansionService keywordExpansion,
        IPainDetectionService painDetection,
        IPainClusteringService painClustering,
        IOpportunityGenerationService opportunityGeneration,
        CancellationToken ct)
    {
        run.Status = SearchRunStatus.Expanding;
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            // ── Phase 1: Keyword expansion ───────────────────────────────────
            var batches = await keywordExpansion.BuildSearchBatchesAsync(run.Keyword, ct);
            var expandedPhrases = await keywordExpansion.ExpandKeywordAsync(run.Keyword, ct);

            run.ExpandedKeywords = string.Join("|", expandedPhrases);

            logger.LogInformation("Scraper: Run {RunId} '{Keyword}' → {Count} batches, {PhraseCount} phrases: [{Phrases}], dateFrom={From}, dateTo={To}",
                run.Id, run.Keyword, batches.Count, expandedPhrases.Count, string.Join(", ", expandedPhrases),
                run.DateRangeFrom?.ToString("yyyy-MM-dd") ?? "null",
                run.DateRangeTo?.ToString("yyyy-MM-dd") ?? "null");

            // ── Phase 2: Scrape ──────────────────────────────────────────────
            run.Status = SearchRunStatus.Scraping;
            await unitOfWork.SaveChangesAsync(ct);

            var requestedSources = run.Sources
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Fix date-only values from the UI:
            // "2025-03-13" arrives as midnight → adjust "to" to end of day so today's posts aren't excluded
            var dateFrom = run.DateRangeFrom;
            var dateTo = run.DateRangeTo.HasValue && run.DateRangeTo.Value.TimeOfDay == TimeSpan.Zero
                ? run.DateRangeTo.Value.Date.AddDays(1).AddSeconds(-1)  // 23:59:59
                : run.DateRangeTo;

            var totalDocuments = 0;
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var connector in connectors)
            {
                if (requestedSources.Length > 0 &&
                    !requestedSources.Contains(connector.SourceType.ToString(), StringComparer.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var source = await GetOrCreateSourceAsync(
                        dbContext, sourceRepo, unitOfWork, connector.SourceType, ct);

                    var connectorDocs = 0;

                    foreach (var batch in batches)
                    {
                        // Per-request timeout — allow more time for connectors with pagination
                        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        requestCts.CancelAfter(TimeSpan.FromSeconds(45));

                        try
                        {
                            var documents = await connector.FetchDocumentsAsync(
                                batch, dateFrom, dateTo, requestCts.Token);

                            foreach (var doc in documents)
                            {
                                if (!seenUrls.Add(doc.Url))
                                    continue;

                                doc.SourceId = source.Id;
                                doc.SearchRunId = run.Id;
                                await dbContext.RawDocuments.AddAsync(doc, ct);
                                connectorDocs++;
                            }
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            logger.LogWarning("Scraper: {Source} batch TIMEOUT for '{Batch}'",
                                connector.SourceType, batch[..Math.Min(batch.Length, 60)]);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "Scraper: {Source} batch FAILED for '{Batch}' — {Error}",
                                connector.SourceType, batch[..Math.Min(batch.Length, 60)], ex.Message);
                        }
                    }

                    totalDocuments += connectorDocs;

                    if (connectorDocs > 0)
                        logger.LogInformation("Scraper: ✓ {Count} docs from {Source}",
                            connectorDocs, connector.SourceType);
                    else
                        logger.LogWarning("Scraper: ⚠ 0 docs from {Source} ({BatchCount} batches tried)",
                            connector.SourceType, batches.Count);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Scraper: ✗ {Source} failed — {Error}",
                        connector.SourceType, ex.Message);
                }
            }

            // Save docs + ExpandedKeywords after ALL connectors are done
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation("Scraper: Phase 2 done — {Total} docs for '{Keyword}'",
                totalDocuments, run.Keyword);

            // ── Phase 3: Analyze ─────────────────────────────────────────────
            run.Status = SearchRunStatus.Analyzing;
            await unitOfWork.SaveChangesAsync(ct);

            var runDocs = await dbContext.RawDocuments
                .Where(d => d.SearchRunId == run.Id)
                .ToListAsync(ct);

            var allSignals = (await painDetection.DetectPainsBatchAsync(runDocs, ct)).ToList();

            if (allSignals.Count > 0)
            {
                await dbContext.PainSignals.AddRangeAsync(allSignals, ct);
                await unitOfWork.SaveChangesAsync(ct);

                // Throttle: respect Gemini rate limit before clustering call
                await Task.Delay(TimeSpan.FromSeconds(6), ct);

                var clusters = await painClustering.ClusterPainsAsync(allSignals, ct);
                await dbContext.PainClusters.AddRangeAsync(clusters, ct);
                await unitOfWork.SaveChangesAsync(ct);

                var opportunities = await opportunityGeneration.GenerateOpportunitiesAsync(clusters, ct);
                await dbContext.Opportunities.AddRangeAsync(opportunities, ct);
            }

            // ── Done ────────────────────────────────────────────────────────
            run.DocumentsCollected = totalDocuments;
            run.PainsDetected = allSignals.Count;
            run.Status = SearchRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation(
                "Scraper: Run {RunId} COMPLETE — {Docs} docs, {Pains} pains for '{Keyword}'",
                run.Id, totalDocuments, allSignals.Count, run.Keyword);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scraper: Run {RunId} FAILED for '{Keyword}'", run.Id, run.Keyword);
            run.Status = SearchRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;

            try { await unitOfWork.SaveChangesAsync(ct); }
            catch { /* best effort */ }
        }
    }

    private async Task ResetStaleRunsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var staleRuns = await dbContext.SearchRuns
                .Where(r => r.Status == SearchRunStatus.Expanding
                         || r.Status == SearchRunStatus.Scraping
                         || r.Status == SearchRunStatus.Analyzing)
                .ToListAsync(ct);

            if (staleRuns.Count == 0) return;

            foreach (var run in staleRuns)
            {
                run.Status = SearchRunStatus.Pending;
                logger.LogWarning("Scraper: Reset stale run {RunId} '{Keyword}' → Pending",
                    run.Id, run.Keyword);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scraper: Failed to reset stale runs");
        }
    }

    private static async Task<Source> GetOrCreateSourceAsync(
        PainFinderDbContext dbContext, IRepository<Source> sourceRepo,
        IUnitOfWork unitOfWork, SourceType sourceType, CancellationToken ct)
    {
        var source = await dbContext.Sources
            .FirstOrDefaultAsync(s => s.Type == sourceType, ct);

        if (source is not null)
            return source;

        source = new Source
        {
            Id = Guid.NewGuid(),
            Name = sourceType.ToString(),
            Type = sourceType,
            BaseUrl = $"https://{sourceType.ToString().ToLowerInvariant()}.com",
            IsActive = true
        };
        await sourceRepo.AddAsync(source, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return source;
    }
}
