using Microsoft.EntityFrameworkCore;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Persistence;

namespace PainFinder.Api.BackgroundServices;

public class RadarScanBackgroundService(
    ILogger<RadarScanBackgroundService> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Radar scan background service started — scanning every 12 hours");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
                var connectors = scope.ServiceProvider.GetRequiredService<IEnumerable<ISourceConnector>>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var sourceRepo = scope.ServiceProvider.GetRequiredService<IRepository<Source>>();
                var keywordExpansion = scope.ServiceProvider.GetRequiredService<IKeywordExpansionService>();
                var painDetection = scope.ServiceProvider.GetRequiredService<IPainDetectionService>();
                var painClustering = scope.ServiceProvider.GetRequiredService<IPainClusteringService>();
                var opportunityGeneration = scope.ServiceProvider.GetRequiredService<IOpportunityGenerationService>();

                var activeMonitors = await dbContext.RadarMonitors
                    .Where(m => m.Status == RadarMonitorStatus.Active)
                    .ToListAsync(stoppingToken);

                if (activeMonitors.Count == 0)
                {
                    logger.LogInformation("Radar: No active monitors found");
                }

                foreach (var monitor in activeMonitors)
                {
                    try
                    {
                        await ProcessMonitorAsync(
                            monitor, dbContext, connectors, unitOfWork, sourceRepo,
                            keywordExpansion, painDetection, painClustering, opportunityGeneration,
                            stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Radar: Error processing monitor '{Name}' ({Id})", monitor.Name, monitor.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Radar: Error during scan cycle");
            }

            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }

    private async Task ProcessMonitorAsync(
        RadarMonitor monitor,
        PainFinderDbContext dbContext,
        IEnumerable<ISourceConnector> connectors,
        IUnitOfWork unitOfWork,
        IRepository<Source> sourceRepo,
        IKeywordExpansionService keywordExpansion,
        IPainDetectionService painDetection,
        IPainClusteringService painClustering,
        IOpportunityGenerationService opportunityGeneration,
        CancellationToken stoppingToken)
    {
        logger.LogInformation("Radar: Starting scan for monitor '{Name}' — keyword: '{Keyword}'",
            monitor.Name, monitor.Keyword);

        // Create scan record
        var scan = new RadarScan
        {
            Id = Guid.NewGuid(),
            RadarMonitorId = monitor.Id,
            StartedAt = DateTime.UtcNow,
            Status = RadarScanStatus.Scraping
        };
        dbContext.RadarScans.Add(scan);
        await unitOfWork.SaveChangesAsync(stoppingToken);

        // Phase 1: Build search batches with AI — [keyword, groupA, groupB]
        var batches = await keywordExpansion.BuildSearchBatchesAsync(monitor.Keyword, stoppingToken);
        scan.ExpandedQuery = string.Join(" | ", batches);

        logger.LogInformation("Radar [{Name}]: '{Keyword}' → {Count} batches: [{Batches}]",
            monitor.Name, monitor.Keyword, batches.Count, string.Join(" | ", batches));

        // Phase 2: Scrape sources — foreach batch, call connector, deduplicate by URL
        var requestedSources = monitor.Sources
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var totalDocuments = 0;
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var connector in connectors)
        {
            if (requestedSources.Length > 0 &&
                !requestedSources.Contains(connector.SourceType.ToString(), StringComparer.OrdinalIgnoreCase))
                continue;

            try
            {
                var source = await dbContext.Sources
                    .FirstOrDefaultAsync(s => s.Type == connector.SourceType, stoppingToken);

                if (source is null)
                {
                    source = new Source
                    {
                        Id = Guid.NewGuid(),
                        Name = connector.SourceType.ToString(),
                        Type = connector.SourceType,
                        BaseUrl = $"https://{connector.SourceType.ToString().ToLowerInvariant()}.com",
                        IsActive = true
                    };
                    await sourceRepo.AddAsync(source, stoppingToken);
                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }

                var connectorDocs = 0;
                foreach (var batch in batches)
                {
                    var documents = await connector.FetchDocumentsAsync(
                        batch, monitor.LastScanAt, null, stoppingToken);

                    var newDocs = new List<RawDocument>();
                    foreach (var doc in documents)
                    {
                        if (!seenUrls.Add(doc.Url))
                            continue;

                        doc.SourceId = source.Id;
                        doc.RadarScanId = scan.Id;
                        newDocs.Add(doc);
                    }

                    if (newDocs.Count > 0)
                        await dbContext.RawDocuments.AddRangeAsync(newDocs, stoppingToken);

                    connectorDocs += newDocs.Count;
                }

                totalDocuments += connectorDocs;
                logger.LogInformation("Radar [{Name}]: Collected {Count} unique docs from {Source}",
                    monitor.Name, connectorDocs, connector.SourceType);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Radar [{Name}]: Connector {Source} failed", monitor.Name, connector.SourceType);
            }
        }

        scan.DocumentsCollected = totalDocuments;
        await unitOfWork.SaveChangesAsync(stoppingToken);

        // Phase 3: Pain detection & analysis
        scan.Status = RadarScanStatus.Analyzing;
        await unitOfWork.SaveChangesAsync(stoppingToken);

        var scanDocs = await dbContext.RawDocuments
            .Where(d => d.RadarScanId == scan.Id)
            .ToListAsync(stoppingToken);

        var allSignals = (await painDetection.DetectPainsBatchAsync(scanDocs, stoppingToken)).ToList();

        if (allSignals.Count > 0)
        {
            await dbContext.PainSignals.AddRangeAsync(allSignals, stoppingToken);
            await unitOfWork.SaveChangesAsync(stoppingToken);

            // Throttle: respect Gemini rate limit before clustering call
            await Task.Delay(TimeSpan.FromSeconds(6), stoppingToken);

            var clusters = await painClustering.ClusterPainsAsync(allSignals, stoppingToken);
            await dbContext.PainClusters.AddRangeAsync(clusters, stoppingToken);
            await unitOfWork.SaveChangesAsync(stoppingToken);

            var opportunities = await opportunityGeneration.GenerateOpportunitiesAsync(clusters, stoppingToken);
            await dbContext.Opportunities.AddRangeAsync(opportunities, stoppingToken);
        }

        // Phase 4: Update scan & monitor stats
        scan.PainsDetected = allSignals.Count;
        scan.Status = RadarScanStatus.Completed;
        scan.CompletedAt = DateTime.UtcNow;

        monitor.LastScanAt = DateTime.UtcNow;
        monitor.TotalScans++;
        monitor.TotalDocuments += totalDocuments;
        monitor.TotalPainsDetected += allSignals.Count;

        await unitOfWork.SaveChangesAsync(stoppingToken);

        logger.LogInformation(
            "Radar [{Name}]: Scan complete — {Docs} docs, {Pains} pains detected (scan #{ScanNum})",
            monitor.Name, totalDocuments, allSignals.Count, monitor.TotalScans);
    }
}
