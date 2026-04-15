namespace PainFinder.Shared.DTOs;

public record RadarScanDto(
    Guid Id,
    Guid RadarMonitorId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    int DocumentsCollected,
    int PainsDetected,
    string? ExpandedQuery);
