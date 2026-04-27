namespace PainFinder.Shared.Contracts;

public record GenerateRequirementsRequest(
    Guid ProjectId,
    string ConversationInput,
    string AnalysisMode = "Deep");
