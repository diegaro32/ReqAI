using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Trustpilot Reviews connector. Scrapes search results and review pages.
/// Trustpilot renders some content server-side, making basic scraping feasible.
/// </summary>
public class TrustpilotConnector(HttpClient httpClient, ILogger<TrustpilotConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.Trustpilot;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var searchUrl = $"https://www.trustpilot.com/search?query={Uri.EscapeDataString(keyword)}";
            var html = await FetchHtmlAsync(searchUrl, cancellationToken);
            if (html is null) return documents;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract company review page links
            var companyLinks = doc.DocumentNode
                .SelectNodes("//a[contains(@href, '/review/')]/@href")
                ?.Select(n => n.GetAttributeValue("href", ""))
                .Where(h => h.Contains("/review/"))
                .Distinct()
                .Take(3)
                .ToList() ?? [];

            foreach (var link in companyLinks)
            {
                var reviewUrl = link.StartsWith("http") ? link : $"https://www.trustpilot.com{link}";
                var reviewDocs = await ScrapeReviewPageAsync(reviewUrl, cancellationToken);
                documents.AddRange(reviewDocs);
                await Task.Delay(2000, cancellationToken);
            }

            logger.LogInformation("Trustpilot: Collected {Count} reviews for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Trustpilot: Failed to scrape for '{Keyword}'", keyword);
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

        var companyName = doc.DocumentNode
            .SelectSingleNode("//h1//span")?.InnerText?.Trim()
            ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim()
            ?? "Unknown Company";

        // Trustpilot review cards with structured data
        var reviewNodes = doc.DocumentNode.SelectNodes(
            "//p[@data-service-review-text-typography]" +
            " | //div[contains(@class, 'review-content')]//p" +
            " | //section[contains(@class, 'review')]//p");

        if (reviewNodes is null) return documents;

        foreach (var node in reviewNodes.Take(20))
        {
            var content = HtmlEntity.DeEntitize(node.InnerText?.Trim() ?? string.Empty);
            if (content.Length < 15) continue;

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[Trustpilot] {companyName}",
                Content = content,
                Author = "trustpilot-reviewer",
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
                logger.LogWarning("Trustpilot: HTTP {StatusCode} for {Url}", (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Trustpilot: Request failed for {Url}", url);
            return null;
        }
    }
}
