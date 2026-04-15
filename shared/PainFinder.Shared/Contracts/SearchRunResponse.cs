namespace PainFinder.Shared.Contracts;

public record SearchRunResponse(
    Guid Id,
    string Status,
    DateTime StartedAt);
