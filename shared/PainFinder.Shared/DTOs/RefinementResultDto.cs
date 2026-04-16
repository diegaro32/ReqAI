namespace PainFinder.Shared.DTOs;

public record RefinementResultDto(
    Guid Id,
    Guid GenerationId,
    string Instruction,
    string RefinedOutput,
    DateTime CreatedAt);
