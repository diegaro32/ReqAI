namespace PainFinder.Domain.Entities;

public class PainCluster
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double SeverityScore { get; set; }
    public int DocumentCount { get; set; }

    public ICollection<PainSignal> PainSignals { get; set; } = [];
    public ICollection<Opportunity> Opportunities { get; set; } = [];
}
