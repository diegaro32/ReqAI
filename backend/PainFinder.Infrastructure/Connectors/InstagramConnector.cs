using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Instagram connector. Instagram has no public search API for posts.
/// Uses DuckDuckGo site search as proxy to find public Instagram posts.
/// For full access, register at https://developers.facebook.com/ and use Instagram Graph API.
/// </summary>
public class InstagramConnector(HttpClient httpClient, ILogger<InstagramConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.Instagram;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var query = $"site:instagram.com {keyword}";
            var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("Accept", "text/html");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Instagram: DuckDuckGo search returned HTTP {StatusCode}", (int)response.StatusCode);
                return documents;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'result')]");

            if (resultNodes is null)
            {
                logger.LogInformation("Instagram: No search results found for '{Keyword}'", keyword);
                return documents;
            }

            foreach (var resultNode in resultNodes.Take(15))
            {
                var titleNode = resultNode.SelectSingleNode(".//a[contains(@class, 'result__a')]");
                var snippetNode = resultNode.SelectSingleNode(".//a[contains(@class, 'result__snippet')]");

                var title = HtmlEntity.DeEntitize(titleNode?.InnerText?.Trim() ?? string.Empty);
                var snippet = HtmlEntity.DeEntitize(snippetNode?.InnerText?.Trim() ?? string.Empty);
                var resultUrl = titleNode?.GetAttributeValue("href", "") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(snippet) || snippet.Length < 20)
                    continue;

                if (!resultUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
                    continue;

                documents.Add(new RawDocument
                {
                    Id = Guid.NewGuid(),
                    Title = $"[Instagram] {title}",
                    Content = snippet,
                    Author = "instagram-user",
                    Url = resultUrl,
                    CreatedAt = DateTime.UtcNow,
                    CollectedAt = DateTime.UtcNow
                });
            }

            logger.LogInformation("Instagram: Collected {Count} results for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Instagram: Failed to search for '{Keyword}'", keyword);
        }

        return documents;
    }
}
