namespace PainFinder.Shared.DTOs;

public record SearchRunDto(
    Guid Id,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    string Sources,
    string Keyword,
    string? ExpandedKeywords,
    DateTime? DateRangeFrom,
    DateTime? DateRangeTo,
    int DocumentsCollected,
    int PainsDetected);
