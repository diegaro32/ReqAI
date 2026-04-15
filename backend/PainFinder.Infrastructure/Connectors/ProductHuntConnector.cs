using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Connectors;

/// <summary>
/// Product Hunt connector using the public Atom feed.
/// No API key required — the feed is publicly accessible at /feed?category={slug}.
/// The keyword is converted to a category slug (spaces → hyphens) and the feed is parsed
/// for product entries with title, tagline, author and published date.
/// Multiple slug variations are tried to maximize coverage.
/// </summary>
public partial class ProductHuntConnector(HttpClient httpClient, ILogger<ProductHuntConnector> logger) : ISourceConnector
{
    public SourceType SourceType => SourceType.ProductHunt;

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(
        string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var documents = new List<RawDocument>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Build category slugs from keyword: "password manager" → ["password-manager", "password", "manager"]
            var slugs = BuildCategorySlugs(keyword);

            logger.LogInformation("ProductHunt: Searching categories: [{Slugs}] for '{Keyword}'",
                string.Join(", ", slugs), keyword);

            foreach (var slug in slugs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var feedUrl = $"https://www.producthunt.com/feed?category={Uri.EscapeDataString(slug)}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
                    request.Headers.TryAddWithoutValidation("User-Agent", "PainFinder/1.0 (.NET)");
                    request.Headers.Add("Accept", "application/atom+xml, application/xml, text/xml");

                    using var response = await httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogDebug("ProductHunt: Feed {Slug} returned {Status}", slug, (int)response.StatusCode);
                        continue;
                    }

                    var xml = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(xml) || xml.Length < 100)
                        continue;

                    var entries = ParseAtomFeed(xml, from, to);

                    foreach (var doc in entries)
                    {
                        if (seenIds.Add(doc.Url))
                            documents.Add(doc);
                    }

                    logger.LogInformation("ProductHunt: category '{Slug}' → {Count} entries", slug, entries.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex, "ProductHunt: Failed to fetch category '{Slug}'", slug);
                }
            }

            logger.LogInformation("ProductHunt: ✓ {Count} documents for '{Keyword}'", documents.Count, keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "ProductHunt: Failed for '{Keyword}'", keyword);
        }

        return documents;
    }

    private List<RawDocument> ParseAtomFeed(string xml, DateTime? from, DateTime? to)
    {
        var results = new List<RawDocument>();

        try
        {
            var doc = XDocument.Parse(xml);
            var entries = doc.Descendants(Atom + "entry");

            foreach (var entry in entries)
            {
                var title = entry.Element(Atom + "title")?.Value?.Trim();
                var contentHtml = entry.Element(Atom + "content")?.Value?.Trim();
                var link = entry.Elements(Atom + "link")
                    .FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate")
                    ?.Attribute("href")?.Value;
                var publishedStr = entry.Element(Atom + "published")?.Value;
                var author = entry.Element(Atom + "author")?.Element(Atom + "name")?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(title))
                    continue;

                // Parse the tagline from HTML content: <p>tagline</p>
                var tagline = StripHtml(contentHtml ?? string.Empty);

                var content = string.IsNullOrWhiteSpace(tagline) ? title : $"{title} — {tagline}";

                if (content.Length < 10)
                    continue;

                var publishedDate = DateTime.UtcNow;
                if (DateTimeOffset.TryParse(publishedStr, out var parsed))
                    publishedDate = parsed.UtcDateTime;

                // Apply date filters
                if (from.HasValue && publishedDate < from.Value)
                    continue;
                if (to.HasValue && publishedDate > to.Value)
                    continue;

                results.Add(new RawDocument
                {
                    Id = Guid.NewGuid(),
                    Title = $"[ProductHunt] {title}",
                    Content = content,
                    Author = author ?? "producthunt-user",
                    Url = link ?? $"https://www.producthunt.com/search?q={Uri.EscapeDataString(title)}",
                    CreatedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception)
        {
            // XML parsing failure — return what we have
        }

        return results;
    }

    private static List<string> BuildCategorySlugs(string keyword)
    {
        var slugs = new List<string>();

        // Full keyword as slug: "password manager" → "password-manager"
        var fullSlug = keyword.Trim().ToLowerInvariant().Replace(' ', '-');
        fullSlug = SlugCleanRegex().Replace(fullSlug, "");
        slugs.Add(fullSlug);

        // Individual words (≥3 chars): "password manager" → ["password", "manager"]
        var words = keyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 3)
            .Select(w => w.ToLowerInvariant());

        foreach (var word in words)
        {
            var slug = SlugCleanRegex().Replace(word, "");
            if (!slugs.Contains(slug))
                slugs.Add(slug);
        }

        return slugs;
    }

    private static string StripHtml(string input) => HtmlTagRegex().Replace(input, " ").Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex SlugCleanRegex();
}
