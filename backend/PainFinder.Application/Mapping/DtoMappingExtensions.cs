using System.Text.Json;
using PainFinder.Domain.Entities;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Mapping;

public static class DtoMappingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static RawDocumentDto ToDto(this RawDocument doc) =>
        new(doc.Id, doc.Source?.Name ?? string.Empty, doc.Title, doc.Content,
            doc.Author, doc.Url, doc.CreatedAt, doc.CollectedAt);

    public static PainSignalDto ToDto(this PainSignal signal) =>
        new(signal.Id, signal.RawDocument?.Title ?? string.Empty, signal.PainPhrase,
            signal.PainCategory, signal.SentimentScore, signal.PainScore, signal.DetectedAt);

    public static PainClusterDto ToDto(this PainCluster cluster) =>
        new(cluster.Id, cluster.Title, cluster.Description, cluster.Category,
            cluster.SeverityScore, cluster.DocumentCount);

    public static OpportunityDto ToDto(this Opportunity o)
    {
        var tools = string.IsNullOrEmpty(o.ToolsDetected)
            ? []
            : o.ToolsDetected.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        List<EvidenceQuoteDto> evidence;
        try
        {
            evidence = string.IsNullOrEmpty(o.EvidenceQuotesJson)
                ? []
                : JsonSerializer.Deserialize<List<EvidenceQuoteDto>>(o.EvidenceQuotesJson, JsonOptions) ?? [];
        }
        catch { evidence = []; }

        List<string> specialization;
        try
        {
            specialization = string.IsNullOrEmpty(o.SpecializationSuggestionsJson) || o.SpecializationSuggestionsJson == "[]"
                ? []
                : JsonSerializer.Deserialize<List<string>>(o.SpecializationSuggestionsJson, JsonOptions) ?? [];
        }
        catch { specialization = []; }

        return new OpportunityDto(
            o.Id, o.Title, o.Description,
            o.ProblemSummary, o.SuggestedSolution,
            o.MarketCategory, o.ConfidenceScore,
            o.PainCluster?.Title ?? string.Empty,
            o.MarketRealityScore,
            new MarketRealityBreakdownDto(
                o.PainIntensityFactor, o.FrequencyFactor,
                o.UrgencyFactor, o.MonetizationIntentFactor,
                o.CompetitionDensityFactor),
            new IcpDto(o.IcpRole, o.IcpContext),
            new CompetitionContextDto(tools, o.GapAnalysis),
            o.BuildDecision, o.BuildReasoning, evidence,
            o.IsGenericOpportunity, specialization);
    }

    public static SearchRunDto ToDto(this SearchRun run) =>
        new(run.Id, run.StartedAt, run.CompletedAt, run.Status.ToString(),
            run.Sources, run.Keyword, run.ExpandedKeywords, run.DateRangeFrom, run.DateRangeTo,
            run.DocumentsCollected, run.PainsDetected);

    public static RadarMonitorDto ToDto(this RadarMonitor monitor) =>
        new(monitor.Id, monitor.Name, monitor.Keyword, monitor.Sources,
            monitor.Status.ToString(), monitor.CreatedAt, monitor.LastScanAt,
            monitor.TotalScans, monitor.TotalDocuments, monitor.TotalPainsDetected);

    public static RadarScanDto ToDto(this RadarScan scan) =>
        new(scan.Id, scan.RadarMonitorId, scan.StartedAt, scan.CompletedAt,
            scan.Status.ToString(), scan.DocumentsCollected, scan.PainsDetected,
            scan.ExpandedQuery);
}
