namespace PainFinder.Domain.Entities;

public class RawDocument
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Guid? RadarScanId { get; set; }
    public Guid? SearchRunId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime CollectedAt { get; set; }

    public Source Source { get; set; } = null!;
    public RadarScan? RadarScan { get; set; }
    public SearchRun? SearchRun { get; set; }
    public ICollection<PainSignal> PainSignals { get; set; } = [];
}
