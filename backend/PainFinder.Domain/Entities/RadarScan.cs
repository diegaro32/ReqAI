namespace PainFinder.Domain.Entities;

public class RadarScan
{
    public Guid Id { get; set; }
    public Guid RadarMonitorId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public RadarScanStatus Status { get; set; } = RadarScanStatus.Pending;
    public int DocumentsCollected { get; set; }
    public int PainsDetected { get; set; }
    public string? ExpandedQuery { get; set; }

    public RadarMonitor RadarMonitor { get; set; } = null!;
    public ICollection<RawDocument> Documents { get; set; } = [];
}

public enum RadarScanStatus
{
    Pending,
    Scraping,
    Analyzing,
    Completed,
    Failed
}
