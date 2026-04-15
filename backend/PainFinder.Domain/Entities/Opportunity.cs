namespace PainFinder.Domain.Entities;

public class Opportunity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProblemSummary { get; set; } = string.Empty;
    public string SuggestedSolution { get; set; } = string.Empty;
    public string MarketCategory { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }

    // --- Decision Engine fields ---
    public double MarketRealityScore { get; set; }
    public double PainIntensityFactor { get; set; }
    public double FrequencyFactor { get; set; }
    public double UrgencyFactor { get; set; }
    public double MonetizationIntentFactor { get; set; }
    public double CompetitionDensityFactor { get; set; }

    public string IcpRole { get; set; } = string.Empty;
    public string IcpContext { get; set; } = string.Empty;

    public string ToolsDetected { get; set; } = string.Empty;
    public string GapAnalysis { get; set; } = string.Empty;

    public string BuildDecision { get; set; } = string.Empty;
    public string BuildReasoning { get; set; } = string.Empty;

    // Red Flag detection — set by AiInsightGenerationService during clustering
    public bool IsGenericOpportunity { get; set; }
    public string SpecializationSuggestionsJson { get; set; } = "[]"
;

    public string EvidenceQuotesJson { get; set; } = string.Empty;

    public Guid PainClusterId { get; set; }
    public PainCluster PainCluster { get; set; } = null!;

    public ActionPlan? ActionPlan { get; set; }
}
