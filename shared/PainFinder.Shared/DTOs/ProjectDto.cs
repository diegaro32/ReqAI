namespace PainFinder.Shared.DTOs;

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int GenerationCount);
