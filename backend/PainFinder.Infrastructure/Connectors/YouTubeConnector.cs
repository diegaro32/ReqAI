using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// YouTube Comments connector. Requires Google API Key.
/// API docs: https://developers.google.com/youtube/v3/docs/commentThreads/list
/// TODO: Create project at https://console.cloud.google.com/
/// Enable YouTube Data API v3 and create an API key.
/// Flow: Search videos → Get comment threads → Extract pain signals.
/// Free quota: 10,000 units/day.
/// </summary>
public class YouTubeConnector(HttpClient httpClient, ILogger<YouTubeConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.YouTube;

    public Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("YouTube connector not yet implemented — requires Google API key. Keyword: '{Keyword}'", keyword);
        return Task.FromResult<IReadOnlyList<RawDocument>>([]);
    }
}
