using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Connectors.Models;

namespace PainFinder.Infrastructure.Connectors;

public class HackerNewsConnector(HttpClient httpClient, ILogger<HackerNewsConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.HackerNews;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            // Search stories
            var storyDocs = await SearchAsync(keyword, "story", from, to, cancellationToken);
            documents.AddRange(storyDocs);

            // Search comments (where real pain points live)
            var commentDocs = await SearchAsync(keyword, "comment", from, to, cancellationToken);
            documents.AddRange(commentDocs);

            logger.LogInformation("HackerNews: Total {Count} documents collected for '{Keyword}'", documents.Count, keyword);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HackerNews: Failed to fetch data for '{Keyword}'", keyword);
        }

        return documents;
    }

    private async Task<List<RawDocument>> SearchAsync(
        string keyword, string tag, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var documents = new List<RawDocument>();
        var url = $"https://hn.algolia.com/api/v1/search_by_date?query={Uri.EscapeDataString(keyword)}&tags={tag}&hitsPerPage=30";

        if (from.HasValue)
            url += $"&numericFilters=created_at_i>{new DateTimeOffset(from.Value).ToUnixTimeSeconds()}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);

        logger.LogInformation("HackerNews: HTTP {StatusCode} for '{Keyword}' ({Tag})",
            (int)response.StatusCode, keyword, tag);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("HackerNews: HTTP {StatusCode} for '{Keyword}'", (int)response.StatusCode, keyword);
            return documents;
        }

        var parsed = await response.Content.ReadFromJsonAsync<HackerNewsApiResponse>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (parsed?.Hits is null)
            return documents;

        foreach (var hit in parsed.Hits)
        {
            var content = hit.StoryText ?? hit.CommentText ?? hit.Title;
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var createdAt = DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtTimestamp).UtcDateTime;

            if (to.HasValue && createdAt > to.Value) continue;

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[HN {tag}] {hit.Title}",
                Content = content,
                Author = hit.Author,
                Url = hit.Url ?? $"https://news.ycombinator.com/item?id={hit.ObjectId}",
                CreatedAt = createdAt,
                CollectedAt = DateTime.UtcNow
            });
        }

        logger.LogInformation("HackerNews: Collected {Count} {Tag}s for '{Keyword}'", documents.Count, tag, keyword);
        return documents;
    }
}
