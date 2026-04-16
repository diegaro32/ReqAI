namespace PainFinder.Shared.DTOs;

public record RequirementGenerationDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string ConversationInput,
    string SystemOverview,
    string DomainModel,
    string LifecycleModel,
    string FunctionalRequirements,
    string BusinessRules,
    string Inconsistencies,
    string NonFunctionalRequirements,
    string Ambiguities,
    string Prioritization,
    string DecisionPoints,
    string OwnershipActions,
    string SystemInsights,
    string SuggestedFeatures,
    string ImplementationRisks,
    DateTime CreatedAt,
    List<RefinementResultDto> Refinements);
