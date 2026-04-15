namespace PainFinder.Shared.DTOs;

public record PainSignalDto(
    Guid Id,
    string DocumentTitle,
    string PainPhrase,
    string PainCategory,
    double SentimentScore,
    double PainScore,
    DateTime DetectedAt);
