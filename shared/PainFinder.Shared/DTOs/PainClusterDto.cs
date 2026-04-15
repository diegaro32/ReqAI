namespace PainFinder.Shared.DTOs;

public record PainClusterDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    double SeverityScore,
    int DocumentCount);
