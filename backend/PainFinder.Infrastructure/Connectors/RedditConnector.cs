using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Reddit connector using the public JSON API (no OAuth).
/// Uses AI-generated subreddits based on the search keyword for maximum relevance.
/// Falls back to a default set if the AI suggestion service is unavailable.
/// </summary>
public partial class RedditConnector(HttpClient httpClient, ILogger<RedditConnector> logger, ISubredditSuggestionService subredditSuggestion) : ISourceConnector
{
    public SourceType SourceType => SourceType.Reddit;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Instance-level cache: subreddits are resolved once per scope (one scope = one search run).
    // All batches for the same search share the same subreddits → 1 Gemini call instead of 3.
    private IReadOnlyList<string>? _resolvedSubreddits;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();
        var hasDateFilter = from.HasValue || to.HasValue;
        var sort = hasDateFilter ? "new" : "relevance";
        var timeFilter = GetTimeFilter(from);
        var maxPages = hasDateFilter ? 3 : 1;

        logger.LogInformation("Reddit: dateFrom={From}, dateTo={To}, sort={Sort}, t={T}, maxPages={Pages}",
            from?.ToString("yyyy-MM-dd HH:mm") ?? "null",
            to?.ToString("yyyy-MM-dd HH:mm") ?? "null",
            sort, timeFilter, maxPages);

        try
        {
            // Resolve subreddits once per scope — first batch triggers the AI call,
            // subsequent batches reuse the cached result (same search = same subreddits)
            _resolvedSubreddits ??= await subredditSuggestion.SuggestSubredditsAsync(keyword, cancellationToken);
            var subredditList = string.Join("+", _resolvedSubreddits);

            logger.LogInformation("Reddit: Using subreddits for '{Keyword}': [{Subs}]",
                keyword, string.Join(", ", _resolvedSubreddits));

            var query = Uri.EscapeDataString(keyword);
            string? after = null;

            for (var page = 0; page < maxPages; page++)
            {
                var url = $"https://www.reddit.com/r/{subredditList}/search.json?q={query}&sort={sort}&t={timeFilter}&limit=100&restrict_sr=on";
                if (after is not null)
                    url += $"&after={after}";

                logger.LogInformation("Reddit: → GET page {Page} {Url}", page + 1, url);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PainFinder/1.0 (.NET; +https://painfinder.app)");
                request.Headers.Accept.ParseAdd("application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogWarning("Reddit: REJECTED {StatusCode} — {Body}",
                        (int)response.StatusCode, errorBody[..Math.Min(300, errorBody.Length)]);
                    break;
                }

                var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var listing = JsonSerializer.Deserialize<RedditListing>(rawJson, JsonOptions);

                if (listing?.Data?.Children is null || listing.Data.Children.Count == 0)
                    break;

                var passedBefore = false;

                foreach (var child in listing.Data.Children)
                {
                    var post = child.Data;
                    if (post is null || string.IsNullOrWhiteSpace(post.Title))
                        continue;

                    var content = string.IsNullOrWhiteSpace(post.Selftext) ? post.Title : post.Selftext;
                    var createdAt = DateTimeOffset.FromUnixTimeSeconds((long)post.CreatedUtc).UtcDateTime;

                    if (to.HasValue && createdAt > to.Value)
                        continue;

                    if (from.HasValue && createdAt < from.Value)
                    {
                        passedBefore = true;
                        continue;
                    }

                    documents.Add(new RawDocument
                    {
                        Id = Guid.NewGuid(),
                        Title = $"[Reddit r/{post.Subreddit}] {StripHtml(post.Title)}",
                        Content = StripHtml(content),
                        Author = post.Author ?? "anonymous",
                        Url = $"https://www.reddit.com{post.Permalink}",
                        CreatedAt = createdAt,
                        CollectedAt = DateTime.UtcNow
                    });
                }

                after = listing.Data.After;

                // Stop paginating if: no more pages, or all posts are before our date range
                if (after is null || passedBefore)
                    break;

                // Brief delay between pages to avoid Reddit rate limiting
                await Task.Delay(1000, cancellationToken);
            }

            logger.LogInformation("Reddit: ✓ {Count} posts after date filter for '{Keyword}'",
                documents.Count, keyword);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Reddit: NETWORK ERROR for '{Keyword}'", keyword);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Reddit: JSON PARSE ERROR for '{Keyword}'", keyword);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reddit: TIMEOUT for '{Keyword}'", keyword);
        }

        return documents;
    }

    // Use a WIDER time window than the exact date to ensure Reddit returns enough posts.
    // The precise date filtering happens client-side (lines 89-90 above).
    private static string GetTimeFilter(DateTime? from) => from switch
    {
        null => "month",
        _ when from.Value > DateTime.UtcNow.AddDays(-7) => "month",
        _ when from.Value > DateTime.UtcNow.AddMonths(-1) => "year",
        _ => "all"
    };

    private static string StripHtml(string input) => HtmlTagRegex().Replace(input, string.Empty).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    // Inner model classes — use case-insensitive deserialization, no [JsonPropertyName] needed
    private sealed class RedditListing
    {
        public RedditListingData? Data { get; set; }
    }

    private sealed class RedditListingData
    {
        public List<RedditChild>? Children { get; set; }
        public string? After { get; set; }
    }

    private sealed class RedditChild
    {
        public RedditPost? Data { get; set; }
    }

    private sealed class RedditPost
    {
        public string? Title { get; set; }
        public string? Selftext { get; set; }
        public string? Permalink { get; set; }
        public string? Author { get; set; }
        public string? Subreddit { get; set; }

        [JsonPropertyName("created_utc")]
        public double CreatedUtc { get; set; }

        public int Score { get; set; }

        [JsonPropertyName("num_comments")]
        public int NumComments { get; set; }
    }
}
