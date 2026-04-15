using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Connectors.Models;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Apple App Store Reviews connector.
/// Uses iTunes Search API to find apps, then fetches reviews via public RSS JSON feed.
/// No authentication required.
/// </summary>
public class AppStoreConnector(HttpClient httpClient, ILogger<AppStoreConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.AppStore;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            // Step 1: Search for apps matching the keyword
            var searchUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(keyword)}&entity=software&limit=5&country=us";
            var searchResult = await httpClient.GetFromJsonAsync<ItunesSearchResponse>(searchUrl, cancellationToken);

            if (searchResult?.Results is null || searchResult.Results.Count == 0)
            {
                logger.LogInformation("AppStore: No apps found for '{Keyword}'", keyword);
                return documents;
            }

            // Step 2: Fetch reviews for each app
            foreach (var app in searchResult.Results)
            {
                try
                {
                    var reviewUrl = $"https://itunes.apple.com/us/rss/customerreviews/id={app.TrackId}/sortBy=mostRecent/json";
                    var reviewFeed = await httpClient.GetFromJsonAsync<AppStoreReviewFeed>(reviewUrl, cancellationToken);

                    if (reviewFeed?.Feed?.Entry is null)
                        continue;

                    foreach (var entry in reviewFeed.Feed.Entry)
                    {
                        var title = entry.Title?.Label ?? string.Empty;
                        var content = entry.Content?.Label ?? string.Empty;
                        var author = entry.Author?.Name?.Label ?? "anonymous";
                        var rating = entry.Rating?.Label ?? "?";

                        if (string.IsNullOrWhiteSpace(content))
                            continue;

                        DateTime createdAt = DateTime.UtcNow;
                        if (entry.Updated?.Label is not null && DateTime.TryParse(entry.Updated.Label, out var parsed))
                            createdAt = parsed.ToUniversalTime();

                        if (to.HasValue && createdAt > to.Value) continue;
                        if (from.HasValue && createdAt < from.Value) continue;

                        documents.Add(new RawDocument
                        {
                            Id = Guid.NewGuid(),
                            Title = $"[AppStore {app.TrackName}] ★{rating} {title}",
                            Content = content,
                            Author = author,
                            Url = $"https://apps.apple.com/app/id{app.TrackId}",
                            CreatedAt = createdAt,
                            CollectedAt = DateTime.UtcNow
                        });
                    }

                    logger.LogInformation("AppStore: Collected reviews for '{AppName}' (id={AppId})",
                        app.TrackName, app.TrackId);

                    await Task.Delay(500, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "AppStore: Failed to fetch reviews for app {AppId}", app.TrackId);
                }
            }

            logger.LogInformation("AppStore: Total {Count} reviews collected for '{Keyword}'", documents.Count, keyword);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "AppStore: Failed to search apps for '{Keyword}'", keyword);
        }

        return documents;
    }
}
