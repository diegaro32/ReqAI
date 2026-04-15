namespace PainFinder.Shared.DTOs;

public record OpportunityDto(
    Guid Id,
    string Title,
    string Description,
    string ProblemSummary,
    string SuggestedSolution,
    string MarketCategory,
    double ConfidenceScore,
    string ClusterTitle,
    double MarketRealityScore,
    MarketRealityBreakdownDto MarketRealityBreakdown,
    IcpDto Icp,
    CompetitionContextDto Competition,
    string BuildDecision,
    string BuildReasoning,
    List<EvidenceQuoteDto> Evidence,
    bool IsGenericOpportunity = false,
    List<string>? SpecializationSuggestions = null);

public record MarketRealityBreakdownDto(
    double PainIntensity,
    double Frequency,
    double Urgency,
    double MonetizationIntent,
    double CompetitionDensity);

public record IcpDto(
    string Role,
    string Context);

public record CompetitionContextDto(
    List<string> ToolsDetected,
    string GapAnalysis);

public record EvidenceQuoteDto(
    string Quote,
    string Source,
    string UserContext);
