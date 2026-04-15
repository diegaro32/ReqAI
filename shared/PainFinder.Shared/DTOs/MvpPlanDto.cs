namespace PainFinder.Shared.DTOs;

public record MvpPlanDto(
    string ProblemStatement,
    string TargetUsers,
    List<string> CoreFeatures,
    string TechStack,
    string ValidationStrategy,
    string FirstStep,
    string EstimatedTimeline)
{
    public ActionPlanDto? Action { get; init; }
    public List<MonetizationStrategyDto> MonetizationStrategies { get; init; } = [];
}

public record MonetizationStrategyDto(
    string Model,
    string Description,
    string PriceRange);

public record ActionPlanDto(
    string ExactIcp,
    string ValueProposition,
    string OutreachMessage,
    string ValidationTest,
    string FirstStepTomorrow);
