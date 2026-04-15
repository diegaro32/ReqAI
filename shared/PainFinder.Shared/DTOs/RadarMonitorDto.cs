namespace PainFinder.Shared.DTOs;

public record RadarMonitorDto(
    Guid Id,
    string Name,
    string Keyword,
    string Sources,
    string Status,
    DateTime CreatedAt,
    DateTime? LastScanAt,
    int TotalScans,
    int TotalDocuments,
    int TotalPainsDetected);
