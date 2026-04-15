using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// X (Twitter) connector. Requires Twitter API v2 Bearer Token.
/// API docs: https://developer.twitter.com/en/docs/twitter-api
/// TODO: Register at https://developer.twitter.com/ (Basic plan: $100/month).
/// Use GET /2/tweets/search/recent?query={keyword} endpoint.
/// Free tier: 1 app, 1500 tweets/month read.
/// </summary>
public class TwitterConnector(HttpClient httpClient, ILogger<TwitterConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.Twitter;

    public Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Twitter/X connector not yet implemented — requires API Bearer Token ($100/mo). Keyword: '{Keyword}'", keyword);
        return Task.FromResult<IReadOnlyList<RawDocument>>([]);
    }
}
