namespace PainFinder.Domain.Entities;

public class PainSignal
{
    public Guid Id { get; set; }
    public Guid RawDocumentId { get; set; }
    public string PainPhrase { get; set; } = string.Empty;
    public string PainCategory { get; set; } = string.Empty;
    public double SentimentScore { get; set; }
    public double PainScore { get; set; }
    public DateTime DetectedAt { get; set; }

    public RawDocument RawDocument { get; set; } = null!;
    public Guid? PainClusterId { get; set; }
    public PainCluster? PainCluster { get; set; }
}
