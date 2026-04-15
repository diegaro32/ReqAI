namespace PainFinder.Domain.Interfaces;

public interface IKeywordExpansionService
{
    /// <summary>
    /// Expands a keyword into a list of 10 short pain-focused search phrases using AI.
    /// Results are cached per keyword.
    /// </summary>
    Task<IReadOnlyList<string>> ExpandKeywordAsync(string keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a search query string with OR operators for APIs that support it.
    /// </summary>
    Task<string> BuildExpandedQueryAsync(string keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds ready-to-use search batches for API connectors:
    /// [0] = original keyword, [1] = group A (5 phrases joined), [2] = group B (5 phrases joined).
    /// Each batch is a single query string safe for most search APIs.
    /// </summary>
    Task<IReadOnlyList<string>> BuildSearchBatchesAsync(string keyword, CancellationToken cancellationToken = default);
}
