using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Indie Hackers connector.
/// IndieHackers is a React/Ember SPA with no public API and DuckDuckGo blocks bots,
/// so this connector uses the public sitemap (hosted on Google Cloud Storage) to discover
/// post URLs, then fetches individual pages and extracts server-side rendered OG metadata.
/// </summary>
public partial class IndieHackersConnector(HttpClient httpClient, ILogger<IndieHackersConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.IndieHackers;

    private const int MaxPages = 30; // Max individual pages to fetch per search
    private const int MaxConcurrency = 5; // Parallel page fetches
    private static readonly string[] SitemapUrls =
    [
        "https://storage.googleapis.com/indie-hackers.appspot.com/sitemaps/ih-sitemap-1.xml",
        "https://storage.googleapis.com/indie-hackers.appspot.com/sitemaps/ih-sitemap-2.xml",
    ];

    // Cache sitemap URLs in memory (shared across requests, ~48K URLs per file)
    private static readonly ConcurrentDictionary<string, List<string>> SitemapCache = new();
    private static DateTime _cacheExpiry = DateTime.MinValue;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            // Step 1: Get all post URLs from sitemap (cached)
            var postUrls = await GetCachedPostUrlsAsync(cancellationToken);

            if (postUrls.Count == 0)
            {
                logger.LogWarning("IndieHackers: Sitemap returned 0 post URLs");
                return documents;
            }

            // Step 2: Filter URLs matching any keyword term in the slug
            var terms = keyword
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length >= 3)
                .Select(t => t.ToLowerInvariant())
                .ToList();

            var matchingUrls = postUrls
                .Where(url =>
                {
                    var slug = url.ToLowerInvariant();
                    return terms.Any(term => slug.Contains(term));
                })
                .Take(MaxPages)
                .ToList();

            logger.LogInformation(
                "IndieHackers: {Total} sitemap URLs, {Matching} match '{Keyword}' (terms: {Terms})",
                postUrls.Count, matchingUrls.Count, keyword, string.Join(", ", terms));

            if (matchingUrls.Count == 0)
                return documents;

            // Step 3: Fetch OG metadata from each matching page (concurrent, throttled)
            using var semaphore = new SemaphoreSlim(MaxConcurrency);
            var tasks = matchingUrls.Select(async url =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await FetchPageMetadataAsync(url, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var doc in results)
            {
                if (doc is not null)
                    documents.Add(doc);
            }

            logger.LogInformation("IndieHackers: ✓ {Count} documents for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "IndieHackers: Failed for '{Keyword}'", keyword);
        }

        return documents;
    }

    private async Task<List<string>> GetCachedPostUrlsAsync(CancellationToken cancellationToken)
    {
        // Return cached URLs if fresh (cache for 1 hour)
        if (_cacheExpiry > DateTime.UtcNow && SitemapCache.TryGetValue("posts", out var cached))
            return cached;

        var allUrls = new List<string>();

        foreach (var sitemapUrl in SitemapUrls)
        {
            try
            {
                logger.LogInformation("IndieHackers: Downloading sitemap {Url}", sitemapUrl);
                var xml = await httpClient.GetStringAsync(sitemapUrl, cancellationToken);

                var urls = PostUrlRegex().Matches(xml)
                    .Select(m => m.Groups[1].Value)
                    .ToList();

                allUrls.AddRange(urls);
                logger.LogInformation("IndieHackers: Sitemap has {Count} post URLs", urls.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "IndieHackers: Failed to download sitemap {Url}", sitemapUrl);
            }
        }

        SitemapCache["posts"] = allUrls;
        _cacheExpiry = DateTime.UtcNow.AddHours(1);

        return allUrls;
    }

    private async Task<RawDocument?> FetchPageMetadataAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")
                ?.GetAttributeValue("content", null);
            var description = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")
                ?.GetAttributeValue("content", null);

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
                return null;

            var content = description ?? title ?? string.Empty;
            content = HtmlEntity.DeEntitize(content);

            if (content.Length < 20)
                return null;

            return new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[IndieHackers] {HtmlEntity.DeEntitize(title ?? "Untitled")}",
                Content = content,
                Author = "indiehackers-user",
                Url = url,
                CreatedAt = DateTime.UtcNow, // OG tags don't include date
                CollectedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "IndieHackers: Failed to fetch {Url}", url);
            return null;
        }
    }

    [GeneratedRegex(@"<loc>(https://www\.indiehackers\.com/post/[^<]+)</loc>")]
    private static partial Regex PostUrlRegex();
}
