namespace PainFinder.Domain.Entities;

public class RequirementGeneration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string ConversationInput { get; set; } = string.Empty;

    // Análisis estructurado
    public string SystemOverview { get; set; } = string.Empty;
    public string DomainModel { get; set; } = string.Empty;
    public string LifecycleModel { get; set; } = string.Empty;
    public string FunctionalRequirements { get; set; } = string.Empty;
    public string BusinessRules { get; set; } = string.Empty;
    public string Inconsistencies { get; set; } = string.Empty;
    public string NonFunctionalRequirements { get; set; } = string.Empty;
    public string Ambiguities { get; set; } = string.Empty;
    public string Prioritization { get; set; } = string.Empty;
    public string DecisionPoints { get; set; } = string.Empty;
    public string OwnershipActions { get; set; } = string.Empty;
    public string SystemInsights { get; set; } = string.Empty;
    public string SuggestedFeatures { get; set; } = string.Empty;
    public string ImplementationRisks { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public List<RefinementResult> Refinements { get; set; } = [];
}
