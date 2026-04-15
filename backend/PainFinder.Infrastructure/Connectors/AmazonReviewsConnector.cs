using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Amazon Reviews connector. Scrapes product search results and review pages.
/// Amazon has aggressive anti-bot measures — this connector handles blocks gracefully.
/// </summary>
public class AmazonReviewsConnector(HttpClient httpClient, ILogger<AmazonReviewsConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.AmazonReviews;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            // Search for products
            var searchUrl = $"https://www.amazon.com/s?k={Uri.EscapeDataString(keyword)}";
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            if (html is null) return documents;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract product ASINs from search results
            var productNodes = doc.DocumentNode
                .SelectNodes("//div[@data-asin]")
                ?.Where(n => !string.IsNullOrEmpty(n.GetAttributeValue("data-asin", "")))
                .Take(3)
                .ToList() ?? [];

            foreach (var productNode in productNodes)
            {
                var asin = productNode.GetAttributeValue("data-asin", "");
                if (string.IsNullOrEmpty(asin)) continue;

                var reviewUrl = $"https://www.amazon.com/product-reviews/{asin}/ref=cm_cr_dp_d_show_all_btm?ie=UTF8&reviewerType=all_reviews&sortBy=recent";
                var reviewDocs = await ScrapeReviewPageAsync(asin, reviewUrl, cancellationToken);
                documents.AddRange(reviewDocs);
                await Task.Delay(3000, cancellationToken); // Longer delay for Amazon
            }

            logger.LogInformation("Amazon: Collected {Count} reviews for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Amazon: Failed to scrape for '{Keyword}'", keyword);
        }

        return documents;
    }

    private async Task<List<RawDocument>> ScrapeReviewPageAsync(
        string asin, string url, CancellationToken cancellationToken)
    {
        var documents = new List<RawDocument>();
        var html = await FetchHtmlAsync(url, cancellationToken);
        if (html is null) return documents;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var productName = doc.DocumentNode
            .SelectSingleNode("//a[@data-hook='product-link']")?.InnerText?.Trim()
            ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim()
            ?? "Unknown Product";

        // Amazon review structure
        var reviewNodes = doc.DocumentNode.SelectNodes(
            "//span[@data-hook='review-body']//span" +
            " | //div[contains(@class, 'review-text')]//span");

        if (reviewNodes is null) return documents;

        var titleNodes = doc.DocumentNode.SelectNodes(
            "//a[@data-hook='review-title']//span[not(@class)]" +
            " | //span[@data-hook='review-title']//span");

        for (int i = 0; i < Math.Min(reviewNodes.Count, 15); i++)
        {
            var content = HtmlEntity.DeEntitize(reviewNodes[i].InnerText?.Trim() ?? string.Empty);
            if (content.Length < 20) continue;

            var title = titleNodes is not null && i < titleNodes.Count
                ? HtmlEntity.DeEntitize(titleNodes[i].InnerText?.Trim() ?? "")
                : "";

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[Amazon Review] {productName} — {title}",
                Content = content,
                Author = "amazon-reviewer",
                Url = $"https://www.amazon.com/dp/{asin}",
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
                logger.LogWarning("Amazon: HTTP {StatusCode} for {Url} — possible anti-bot block",
                    (int)response.StatusCode, url);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Detect CAPTCHA pages
            if (content.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("robot", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Amazon: CAPTCHA detected for {Url}", url);
                return null;
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Amazon: Request failed for {Url}", url);
            return null;
        }
    }
}
