using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// LinkedIn connector. No public search API for posts.
/// LinkedIn restricts automated access heavily.
/// TODO: Consider using LinkedIn Marketing API (requires partner approval)
/// or a third-party service like Phantom Buster / Apify.
/// </summary>
public class LinkedInConnector(HttpClient httpClient, ILogger<LinkedInConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.LinkedIn;

    public Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("LinkedIn connector not yet implemented — requires partner API approval. Keyword: '{Keyword}'", keyword);
        return Task.FromResult<IReadOnlyList<RawDocument>>([]);
    }
}
