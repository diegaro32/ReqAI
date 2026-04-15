using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// G2 Reviews connector. Scrapes product search results and review pages.
/// G2 may block automated requests — connector handles 403/429 gracefully.
/// </summary>
public class G2Connector(HttpClient httpClient, ILogger<G2Connector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.G2;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var searchUrl = $"https://www.g2.com/search?query={Uri.EscapeDataString(keyword)}";
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            if (html is null) return documents;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract product links from search results
            var productLinks = doc.DocumentNode
                .SelectNodes("//a[contains(@href, '/products/')]/@href")
                ?.Select(n => n.GetAttributeValue("href", ""))
                .Where(h => h.Contains("/products/") && h.Contains("reviews"))
                .Distinct()
                .Take(3)
                .ToList() ?? [];

            // If no direct review links, build them from product links
            if (productLinks.Count == 0)
            {
                productLinks = doc.DocumentNode
                    .SelectNodes("//a[contains(@href, '/products/')]/@href")
                    ?.Select(n => n.GetAttributeValue("href", ""))
                    .Where(h => h.Contains("/products/"))
                    .Distinct()
                    .Take(3)
                    .Select(h => h.TrimEnd('/') + "/reviews")
                    .ToList() ?? [];
            }

            foreach (var link in productLinks)
            {
                var reviewUrl = link.StartsWith("http") ? link : $"https://www.g2.com{link}";
                var reviewDocs = await ScrapeReviewPageAsync(reviewUrl, from, to, cancellationToken);
                documents.AddRange(reviewDocs);
                await Task.Delay(2000, cancellationToken); // Respect rate limits
            }

            logger.LogInformation("G2: Collected {Count} reviews for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "G2: Failed to scrape for '{Keyword}'", keyword);
        }

        return documents;
    }

    private async Task<List<RawDocument>> ScrapeReviewPageAsync(
        string url, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var documents = new List<RawDocument>();
        var html = await FetchHtmlAsync(url, cancellationToken);
        if (html is null) return documents;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // G2 reviews are in div elements with review content
        var reviewNodes = doc.DocumentNode.SelectNodes(
            "//div[contains(@class, 'review')]//div[contains(@class, 'text')]" +
            " | //div[@itemprop='reviewBody']" +
            " | //div[contains(@class, 'review-content')]");

        if (reviewNodes is null) return documents;

        var productName = doc.DocumentNode
            .SelectSingleNode("//h1")?.InnerText?.Trim() ?? "Unknown Product";

        foreach (var node in reviewNodes.Take(15))
        {
            var content = HtmlEntity.DeEntitize(node.InnerText?.Trim() ?? string.Empty);
            if (content.Length < 20) continue;

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[G2 Review] {productName}",
                Content = content,
                Author = "g2-reviewer",
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
                logger.LogWarning("G2: HTTP {StatusCode} for {Url}", (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "G2: Request failed for {Url}", url);
            return null;
        }
    }
}
