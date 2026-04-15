using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Google Play Store Reviews connector.
/// Scrapes app search results and extracts review content from app detail pages.
/// </summary>
public class GooglePlayConnector(HttpClient httpClient, ILogger<GooglePlayConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.GooglePlay;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            // Search for apps
            var searchUrl = $"https://play.google.com/store/search?q={Uri.EscapeDataString(keyword)}&c=apps&hl=en";
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            if (html is null) return documents;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract app detail links
            var appLinks = doc.DocumentNode
                .SelectNodes("//a[contains(@href, '/store/apps/details')]/@href")
                ?.Select(n => n.GetAttributeValue("href", ""))
                .Where(h => h.Contains("id="))
                .Distinct()
                .Take(5)
                .ToList() ?? [];

            foreach (var link in appLinks)
            {
                var appUrl = link.StartsWith("http") ? link : $"https://play.google.com{link}";
                if (!appUrl.Contains("hl="))
                    appUrl += "&hl=en";

                var reviewDocs = await ScrapeAppPageAsync(appUrl, cancellationToken);
                documents.AddRange(reviewDocs);
                await Task.Delay(1500, cancellationToken);
            }

            logger.LogInformation("GooglePlay: Collected {Count} reviews for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GooglePlay: Failed to scrape for '{Keyword}'", keyword);
        }

        return documents;
    }

    private async Task<List<RawDocument>> ScrapeAppPageAsync(string url, CancellationToken cancellationToken)
    {
        var documents = new List<RawDocument>();
        var html = await FetchHtmlAsync(url, cancellationToken);
        if (html is null) return documents;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var appName = doc.DocumentNode
            .SelectSingleNode("//h1")?.InnerText?.Trim() ?? "Unknown App";

        // Google Play reviews in structured nodes
        var reviewNodes = doc.DocumentNode.SelectNodes(
            "//div[contains(@class, 'review')]//span[contains(@class, 'review-body')]" +
            " | //div[@jscontroller]//div[contains(@class, 'body')]//span" +
            " | //div[contains(@class, 'user-review')]//span");

        if (reviewNodes is null) return documents;

        foreach (var node in reviewNodes.Take(15))
        {
            var content = HtmlEntity.DeEntitize(node.InnerText?.Trim() ?? string.Empty);
            if (content.Length < 20) continue;

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[GooglePlay Review] {appName}",
                Content = content,
                Author = "play-reviewer",
                Url = url,
                CreatedAt = DateTime.UtcNow,
                CollectedAt = DateTime.UtcNow
            });
        }

        return documents;
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GooglePlay: HTTP {StatusCode} for {Url}", (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "GooglePlay: Request failed for {Url}", url);
            return null;
        }
    }
}
