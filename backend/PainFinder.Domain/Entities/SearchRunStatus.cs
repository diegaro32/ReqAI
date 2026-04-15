namespace PainFinder.Domain.Entities;

public enum SearchRunStatus
{
    Pending,
    Expanding,   // Generating keyword expansion (AI call)
    Scraping,    // Fetching documents from connectors
    Analyzing,   // Running pain detection + clustering
    Completed,
    Failed
}
