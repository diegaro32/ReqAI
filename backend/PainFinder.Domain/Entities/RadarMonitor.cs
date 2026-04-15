namespace PainFinder.Domain.Entities;

public class RadarMonitor
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string Sources { get; set; } = string.Empty;
    public RadarMonitorStatus Status { get; set; } = RadarMonitorStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastScanAt { get; set; }
    public int TotalScans { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalPainsDetected { get; set; }

    public ICollection<RadarScan> Scans { get; set; } = [];
}

public enum RadarMonitorStatus
{
    Active,
    Paused,
    Archived
}
