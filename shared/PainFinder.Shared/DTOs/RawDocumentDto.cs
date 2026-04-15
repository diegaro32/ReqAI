namespace PainFinder.Shared.DTOs;

public record RawDocumentDto(
    Guid Id,
    string SourceName,
    string Title,
    string Content,
    string Author,
    string Url,
    DateTime CreatedAt,
    DateTime CollectedAt);
