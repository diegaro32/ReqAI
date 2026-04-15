using System.Text;
using PainFinder.Shared.DTOs;

namespace PainFinder.Web.Services;

public static class CsvExportService
{
    public static string ExportDocuments(IEnumerable<RawDocumentDto> docs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Source,Title,Author,URL,Created,Collected");
        foreach (var d in docs)
        {
            sb.AppendLine($"{Escape(d.SourceName)},{Escape(d.Title)},{Escape(d.Author)},{Escape(d.Url)},{d.CreatedAt:yyyy-MM-dd HH:mm},{d.CollectedAt:yyyy-MM-dd HH:mm}");
        }
        return sb.ToString();
    }

    public static string ExportPainSignals(IEnumerable<PainSignalDto> pains)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Pain Phrase,Category,Pain Score,Sentiment Score,Source Document,Detected At");
        foreach (var p in pains)
        {
            sb.AppendLine($"{Escape(p.PainPhrase)},{Escape(p.PainCategory)},{p.PainScore:F2},{p.SentimentScore:F2},{Escape(p.DocumentTitle)},{p.DetectedAt:yyyy-MM-dd HH:mm}");
        }
        return sb.ToString();
    }

    public static string ExportClusters(IEnumerable<PainClusterDto> clusters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Title,Description,Category,Severity Score,Document Count");
        foreach (var c in clusters)
        {
            sb.AppendLine($"{Escape(c.Title)},{Escape(c.Description)},{Escape(c.Category)},{c.SeverityScore:F2},{c.DocumentCount}");
        }
        return sb.ToString();
    }

    public static string ExportOpportunities(IEnumerable<OpportunityDto> opportunities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Title,Market Category,Confidence,Market Reality Score,Build Decision,Problem Summary,Suggested Solution,ICP Role,ICP Context,Competition,Gap Analysis");
        foreach (var o in opportunities)
        {
            var tools = string.Join(" | ", o.Competition.ToolsDetected);
            sb.AppendLine($"{Escape(o.Title)},{Escape(o.MarketCategory)},{o.ConfidenceScore:F2},{o.MarketRealityScore:F2},{Escape(o.BuildDecision)},{Escape(o.ProblemSummary)},{Escape(o.SuggestedSolution)},{Escape(o.Icp.Role)},{Escape(o.Icp.Context)},{Escape(tools)},{Escape(o.Competition.GapAnalysis)}");
        }
        return sb.ToString();
    }

    public static string ExportAll(string keyword, IEnumerable<RawDocumentDto> docs, IEnumerable<PainSignalDto> pains, IEnumerable<PainClusterDto> clusters, IEnumerable<OpportunityDto> opportunities)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# QueryRadar Export — \"{keyword}\"");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        sb.AppendLine("## OPPORTUNITIES");
        sb.Append(ExportOpportunities(opportunities));
        sb.AppendLine();

        sb.AppendLine("## PAIN SIGNALS");
        sb.Append(ExportPainSignals(pains));
        sb.AppendLine();

        sb.AppendLine("## CLUSTERS");
        sb.Append(ExportClusters(clusters));
        sb.AppendLine();

        sb.AppendLine("## DOCUMENTS");
        sb.Append(ExportDocuments(docs));

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
