using Microsoft.EntityFrameworkCore;
using PainFinder.Application.Mapping;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Shared.DTOs;

namespace PainFinder.Infrastructure.Services;

public class OpportunityQueryServiceImpl(PainFinderDbContext dbContext) : IOpportunityQueryService
{
    public async Task<IReadOnlyList<OpportunityDto>> GetAllOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        var opportunities = await dbContext.Opportunities
            .Include(o => o.PainCluster)
            .ToListAsync(cancellationToken);
        return opportunities.Select(o => o.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<OpportunityWithSearchDto>> GetAllOpportunitiesGroupedBySearchAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // Trace: SearchRun → RawDocument → PainSignal → PainCluster → Opportunity
        var results = await (
            from sr in dbContext.SearchRuns
            where sr.UserId == userId
            join doc in dbContext.RawDocuments on sr.Id equals doc.SearchRunId
            join ps in dbContext.PainSignals on doc.Id equals ps.RawDocumentId
            where ps.PainClusterId != null
            join cluster in dbContext.PainClusters on ps.PainClusterId equals cluster.Id
            join opp in dbContext.Opportunities on cluster.Id equals opp.PainClusterId
            select new { SearchRunId = sr.Id, sr.Keyword, OpportunityId = opp.Id }
        ).Distinct().ToListAsync(cancellationToken);

        if (results.Count == 0) return [];

        // Load full opportunity data
        var oppIds = results.Select(r => r.OpportunityId).Distinct().ToHashSet();
        var opportunities = await dbContext.Opportunities
            .Include(o => o.PainCluster)
            .Where(o => oppIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var oppLookup = opportunities.ToDictionary(o => o.Id, o => o.ToDto());

        var groups = results
            .Where(r => oppLookup.ContainsKey(r.OpportunityId))
            .GroupBy(r => new { r.SearchRunId, r.Keyword })
            .Select(g => new OpportunityWithSearchDto(
                g.Key.SearchRunId,
                g.Key.Keyword,
                g.Select(r => oppLookup[r.OpportunityId])
                    .DistinctBy(o => o.Id)
                    .OrderByDescending(o => o.ConfidenceScore)
                    .ToList()))
            .OrderByDescending(g => g.Opportunities.Max(o => o.ConfidenceScore))
            .ToList();

        return groups;
    }
}
