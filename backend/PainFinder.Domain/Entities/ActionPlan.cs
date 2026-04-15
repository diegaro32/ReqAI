namespace PainFinder.Domain.Entities;

public class ActionPlan
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;

    public string ProblemStatement { get; set; } = string.Empty;
    public string TargetUsers { get; set; } = string.Empty;
    public string CoreFeaturesJson { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;
    public string ValidationStrategy { get; set; } = string.Empty;
    public string FirstStep { get; set; } = string.Empty;
    public string EstimatedTimeline { get; set; } = string.Empty;

    // Action plan specific fields
    public string ExactIcp { get; set; } = string.Empty;
    public string ValueProposition { get; set; } = string.Empty;
    public string OutreachMessage { get; set; } = string.Empty;
    public string ValidationTest { get; set; } = string.Empty;
    public string FirstStepTomorrow { get; set; } = string.Empty;
    public string MonetizationStrategiesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
