namespace PainFinder.Domain.Entities;

public class Source
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SourceType Type { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<RawDocument> RawDocuments { get; set; } = [];
}
