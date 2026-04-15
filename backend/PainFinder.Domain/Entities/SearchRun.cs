namespace PainFinder.Domain.Entities;

public class SearchRun
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SearchRunStatus Status { get; set; } = SearchRunStatus.Pending;
    public string Sources { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string? ExpandedKeywords { get; set; }
    public DateTime? DateRangeFrom { get; set; }
    public DateTime? DateRangeTo { get; set; }
    public int DocumentsCollected { get; set; }
    public int PainsDetected { get; set; }
}
