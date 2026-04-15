using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Capterra Reviews connector. Scrapes product search results and review pages.
/// </summary>
public class CapterraConnector(HttpClient httpClient, ILogger<CapterraConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.Capterra;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var searchUrl = $"https://www.capterra.com/search/?query={Uri.EscapeDataString(keyword)}";
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            if (html is null) return documents;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract product review page links
            var reviewLinks = doc.DocumentNode
                .SelectNodes("//a[contains(@href, '/reviews/')]/@href")
                ?.Select(n => n.GetAttributeValue("href", ""))
                .Where(h => h.Contains("/reviews/"))
                .Distinct()
                .Take(3)
                .ToList() ?? [];

            foreach (var link in reviewLinks)
            {
                var reviewUrl = link.StartsWith("http") ? link : $"https://www.capterra.com{link}";
                var reviewDocs = await ScrapeReviewPageAsync(reviewUrl, cancellationToken);
                documents.AddRange(reviewDocs);
                await Task.Delay(2000, cancellationToken);
            }

            logger.LogInformation("Capterra: Collected {Count} reviews for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Capterra: Failed to scrape for '{Keyword}'", keyword);
        }

        return documents;
    }

    private async Task<List<RawDocument>> ScrapeReviewPageAsync(string url, CancellationToken cancellationToken)
    {
        var documents = new List<RawDocument>();
        var html = await FetchHtmlAsync(url, cancellationToken);
        if (html is null) return documents;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var productName = doc.DocumentNode
            .SelectSingleNode("//h1")?.InnerText?.Trim() ?? "Unknown Product";

        // Capterra reviews are in structured review cards
        var reviewNodes = doc.DocumentNode.SelectNodes(
            "//div[contains(@class, 'review')]//p" +
            " | //div[contains(@class, 'review-body')]" +
            " | //div[@itemprop='reviewBody']" +
            " | //span[@itemprop='description']");

        if (reviewNodes is null) return documents;

        foreach (var node in reviewNodes.Take(15))
        {
            var content = HtmlEntity.DeEntitize(node.InnerText?.Trim() ?? string.Empty);
            if (content.Length < 20) continue;

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[Capterra Review] {productName}",
                Content = content,
                Author = "capterra-reviewer",
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
                logger.LogWarning("Capterra: HTTP {StatusCode} for {Url}", (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Capterra: Request failed for {Url}", url);
            return null;
        }
    }
}
