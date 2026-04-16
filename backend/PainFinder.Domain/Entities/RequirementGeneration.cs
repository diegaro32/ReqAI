namespace PainFinder.Domain.Entities;

public class RequirementGeneration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string OriginalInput { get; set; } = string.Empty;
    public string FunctionalRequirements { get; set; } = string.Empty;
    public string NonFunctionalRequirements { get; set; } = string.Empty;
    public string Ambiguities { get; set; } = string.Empty;
    public string SuggestedFeatures { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public List<RefinementResult> Refinements { get; set; } = [];
}
