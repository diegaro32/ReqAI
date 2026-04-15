using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.Connectors.Models;

namespace PainFinder.Infrastructure.Connectors;

public class StackOverflowConnectorOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public partial class StackOverflowConnector(HttpClient httpClient, ILogger<StackOverflowConnector> logger, StackOverflowConnectorOptions options) : ISourceConnector
{
    public SourceType SourceType => SourceType.StackOverflow;

    private const int PageSize = 100;  // StackOverflow API max per request
    private const int MaxResults = 500; // Target: 5 pages × 100
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();

        try
        {
            var encoded = Uri.EscapeDataString(keyword);

            // Build base URL — pagination is added per page
            var baseUrl = $"https://api.stackexchange.com/2.3/search/advanced?order=desc&sort=relevance&q={encoded}&site=stackoverflow&pagesize={PageSize}&filter=withbody";

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                baseUrl += $"&key={Uri.EscapeDataString(options.ApiKey)}";

            if (from.HasValue)
                baseUrl += $"&fromdate={new DateTimeOffset(from.Value, TimeSpan.Zero).ToUnixTimeSeconds()}";
            if (to.HasValue)
                baseUrl += $"&todate={new DateTimeOffset(to.Value, TimeSpan.Zero).ToUnixTimeSeconds()}";

            var page = 1;
            var hasMore = true;

            while (hasMore && documents.Count < MaxResults && !cancellationToken.IsCancellationRequested)
            {
                var url = $"{baseUrl}&page={page}";

                logger.LogInformation("StackOverflow: → GET page {Page} {Url}", page, url);

                var rawJson = await ReadGzipResponseAsync(url, cancellationToken);

                logger.LogInformation("StackOverflow: ← {Len} chars (page {Page}) for '{Keyword}'", rawJson.Length, page, keyword);

                var parsed = JsonSerializer.Deserialize<StackOverflowApiResponse>(rawJson, JsonOptions);

                // Check for API-level errors (throttle, invalid key, etc.)
                if (parsed?.ErrorId is not null)
                {
                    logger.LogWarning("StackOverflow: API error {ErrorId} ({ErrorName}): {ErrorMessage} for '{Keyword}'",
                        parsed.ErrorId, parsed.ErrorName, parsed.ErrorMessage, keyword);
                    break;
                }

                if (parsed?.Items is null || parsed.Items.Count == 0)
                {
                    if (page == 1)
                    {
                        logger.LogWarning("StackOverflow: No items for '{Keyword}' (quota: {Quota}) — raw preview: {Preview}",
                            keyword, parsed?.QuotaRemaining ?? -1,
                            rawJson[..Math.Min(500, rawJson.Length)]);
                    }
                    break;
                }

                foreach (var question in parsed.Items)
                {
                    if (documents.Count >= MaxResults)
                        break;

                    var content = question.Body ?? question.Excerpt ?? question.Title;
                    content = StripHtml(content);

                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    var createdAt = DateTimeOffset.FromUnixTimeSeconds(question.CreationDate).UtcDateTime;
                    var questionUrl = question.Link ?? $"https://stackoverflow.com/questions/{question.QuestionId}";

                    documents.Add(new RawDocument
                    {
                        Id = Guid.NewGuid(),
                        Title = $"[StackOverflow] {StripHtml(question.Title)}",
                        Content = content,
                        Author = question.Owner?.DisplayName ?? "anonymous",
                        Url = questionUrl,
                        CreatedAt = createdAt,
                        CollectedAt = DateTime.UtcNow
                    });
                }

                logger.LogInformation("StackOverflow: page {Page} → {PageCount} items, {Total} total (quota: {Quota})",
                    page, parsed.Items.Count, documents.Count, parsed.QuotaRemaining);

                hasMore = parsed.HasMore;
                page++;
            }

            logger.LogInformation("StackOverflow: ✓ {Count} results for '{Keyword}' across {Pages} page(s)",
                documents.Count, keyword, page - 1);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "StackOverflow: NETWORK ERROR for '{Keyword}'", keyword);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "StackOverflow: JSON PARSE ERROR for '{Keyword}'", keyword);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "StackOverflow: TIMEOUT for '{Keyword}'", keyword);
        }

        return documents;
    }

    /// <summary>
    /// Reads the response from StackOverflow API, handling gzip decompression manually.
    /// The API always returns gzip-compressed responses (Content-Encoding: gzip).
    /// If AutomaticDecompression is already active on the HttpClient, the Content-Encoding
    /// header is stripped and we read normally. Otherwise, we decompress manually.
    /// </summary>
    private async Task<string> ReadGzipResponseAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Ensure User-Agent is set — StackOverflow may return 403 without one.
        // The typed HttpClient config may be bypassed when resolved via ISourceConnector.
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
            request.Headers.TryAddWithoutValidation("User-Agent", "PainFinder/1.0 (.NET)");

        // Request gzip explicitly so the API knows we can handle it
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        request.Headers.AcceptEncoding.ParseAdd("deflate");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentEncoding = response.Content.Headers.ContentEncoding;

        if (contentEncoding.Contains("gzip"))
        {
            // Handler did NOT decompress — do it manually
            await using var compressed = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        // Handler already decompressed (or response wasn't compressed)
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string StripHtml(string input) => HtmlTagRegex().Replace(input, " ").Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
