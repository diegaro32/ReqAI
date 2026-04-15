using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Facebook connector. Facebook has no public search API for posts.
/// Uses DuckDuckGo site search as proxy to find public Facebook posts/groups.
/// For full access, register at https://developers.facebook.com/ and use Graph API.
/// </summary>
public class FacebookConnector(HttpClient httpClient, ILogger<FacebookConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.Facebook;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var query = $"site:facebook.com {keyword}";
            var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("Accept", "text/html");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Facebook: DuckDuckGo search returned HTTP {StatusCode}", (int)response.StatusCode);
                return documents;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'result')]");

            if (resultNodes is null)
            {
                logger.LogInformation("Facebook: No search results found for '{Keyword}'", keyword);
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

                if (!resultUrl.Contains("facebook.com", StringComparison.OrdinalIgnoreCase))
                    continue;

                documents.Add(new RawDocument
                {
                    Id = Guid.NewGuid(),
                    Title = $"[Facebook] {title}",
                    Content = snippet,
                    Author = "facebook-user",
                    Url = resultUrl,
                    CreatedAt = DateTime.UtcNow,
                    CollectedAt = DateTime.UtcNow
                });
            }

            logger.LogInformation("Facebook: Collected {Count} results for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Facebook: Failed to search for '{Keyword}'", keyword);
        }

        return documents;
    }
}
