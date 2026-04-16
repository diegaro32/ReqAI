namespace PainFinder.Shared.DTOs;

public record RequirementGenerationDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string OriginalInput,
    string FunctionalRequirements,
    string NonFunctionalRequirements,
    string Ambiguities,
    string SuggestedFeatures,
    DateTime CreatedAt,
    List<RefinementResultDto> Refinements);
