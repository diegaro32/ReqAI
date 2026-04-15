using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Connectors.Models;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// GitHub connector that searches public issues via the GitHub Search API.
/// No authentication required (rate-limited to 10 requests/minute for unauthenticated).
/// Multi-word batches from the keyword expansion are converted to OR queries
/// because GitHub search treats spaces as AND by default.
/// </summary>
public class GitHubConnector(HttpClient httpClient, ILogger<GitHubConnector> logger) : ISourceConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SourceType SourceType => SourceType.GitHub;

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var issueDocs = await SearchIssuesAsync(keyword, from, to, cancellationToken);
            documents.AddRange(issueDocs);

            logger.LogInformation("GitHub: Total {Count} issues collected for '{Keyword}'",
                documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GitHub: Failed for '{Keyword}' — {Error}", keyword, ex.Message);
        }

        return documents;
    }

    private async Task<List<RawDocument>> SearchIssuesAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var documents = new List<RawDocument>();

        // Multi-word batches from keyword expansion need OR logic.
        // GitHub search treats spaces as AND, so we quote each term and join with OR.
        var searchTerms = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var termsPart = searchTerms.Length > 1
            ? string.Join(" OR ", searchTerms.Select(t => $"\"{t}\""))
            : keyword;

        var query = $"{termsPart} is:issue";

        if (from.HasValue)
            query += $" created:>={from.Value:yyyy-MM-dd}";
        if (to.HasValue)
            query += $" created:<={to.Value:yyyy-MM-dd}";

        var url = $"https://api.github.com/search/issues?q={Uri.EscapeDataString(query)}&sort=created&order=desc&per_page=100";

        logger.LogInformation("GitHub: Searching — q={Query}", query);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("PainFinder/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("GitHub: HTTP {StatusCode} — {Body}",
                (int)response.StatusCode, body[..Math.Min(body.Length, 300)]);
            return documents;
        }

        var parsed = await response.Content.ReadFromJsonAsync<GitHubSearchResponse>(JsonOptions, cancellationToken);

        if (parsed is null)
        {
            logger.LogWarning("GitHub: Deserialization returned null");
            return documents;
        }

        logger.LogInformation("GitHub: API returned {Total} total, {Items} items in page",
            parsed.TotalCount, parsed.Items.Count);

        foreach (var item in parsed.Items)
        {
            var content = item.Body ?? item.Title;
            if (string.IsNullOrWhiteSpace(content))
                continue;

            // Truncate very long issue bodies to keep storage reasonable
            if (content.Length > 2000)
                content = content[..2000];

            documents.Add(new RawDocument
            {
                Id = Guid.NewGuid(),
                Title = $"[GitHub] {item.Title}",
                Content = content,
                Author = item.User?.Login ?? "unknown",
                Url = item.HtmlUrl,
                CreatedAt = item.CreatedAt,
                CollectedAt = DateTime.UtcNow
            });
        }

        logger.LogInformation("GitHub: Collected {Count} issues for '{Keyword}'", documents.Count, keyword);
        return documents;
    }
}
