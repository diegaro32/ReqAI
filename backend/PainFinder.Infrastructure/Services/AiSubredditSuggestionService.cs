using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Services;

/// <summary>
/// AI-powered subreddit suggestion service using Gemini.
/// Given a natural language query, returns the most relevant subreddit names for scraping.
/// Results are cached per keyword to avoid redundant AI calls.
/// </summary>
public class AiSubredditSuggestionService(
    IChatClient chatClient,
    ILogger<AiSubredditSuggestionService> logger) : ISubredditSuggestionService
{
    private static readonly string[] FallbackSubreddits =
    [
        "Colombia", "mexico", "argentina",
        "emprendedores", "EmpreLatam", "Entrepreneur",
        "freelance", "startups", "SaaS",
        "rant", "smallbusiness"
    ];

    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<string>> SuggestSubredditsAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var normalized = keyword.Trim().ToLowerInvariant();

        if (_cache.TryGetValue(normalized, out var cached))
        {
            logger.LogInformation("SubredditSuggestion: Cache hit for '{Keyword}' ({Count} subreddits)", keyword, cached.Count);
            return cached;
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, """
                    You are a Reddit expert for PainFinder, a tool that discovers real frustrations and pain points
                    from online communities. The target market is LATIN AMERICA, specifically Colombia, Mexico and Argentina.

                    Given a user's natural language description of what they're searching for, return the most relevant
                    subreddit names where people in those countries discuss that topic, complain, or share frustrations.

                    Rules:
                    - Return ONLY a valid JSON array of strings (subreddit names WITHOUT the "r/" prefix).
                    - Return between 5 and 15 subreddits, ordered by relevance.
                    - ALWAYS include at least 3 country-specific subreddits: Colombia, mexico, argentina.
                    - Include Spanish-language subreddits (emprendedores, EmpreLatam, Colombia, mexico, argentina, es).
                    - Also include English subreddits that LATAM professionals use (Entrepreneur, startups, SaaS, freelance).
                    - Include industry/topic-specific subreddits relevant to the keyword.
                    - Only suggest subreddits that actually exist and are active on Reddit.
                    - No markdown, no explanation, ONLY the JSON array.
                    """),
                new(ChatRole.User, $"""
                    The user is searching for: "{keyword}"
                    Target countries: Colombia, Mexico, Argentina (Spanish-speaking LATAM market).

                    Suggest the best subreddits where people in Colombia, Mexico and Argentina
                    would discuss, complain about, or share pain points related to this topic.
                    Always include: Colombia, mexico, argentina, emprendedores.
                    Return ONLY a JSON array of subreddit names.
                    """)
            };

            logger.LogInformation("SubredditSuggestion: Calling Gemini for '{Keyword}'...", keyword);

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseText = response.Messages[^1].Text?.Trim() ?? "[]";

            // Clean markdown code fences if present
            var codeFence = new string('`', 3);
            if (responseText.StartsWith(codeFence))
            {
                responseText = responseText
                    .Replace(codeFence + "json", "")
                    .Replace(codeFence, "")
                    .Trim();
            }

            var subreddits = JsonSerializer.Deserialize<string[]>(responseText) ?? [];

            var result = subreddits
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().TrimStart('/').Replace("r/", ""))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(15)
                .ToList()
                .AsReadOnly();

            if (result.Count == 0)
            {
                logger.LogWarning("SubredditSuggestion: Gemini returned 0 valid subreddits for '{Keyword}', using fallback", keyword);
                _cache[normalized] = FallbackSubreddits;
                return FallbackSubreddits;
            }

            _cache[normalized] = result;

            logger.LogInformation("SubredditSuggestion: Generated {Count} subreddits for '{Keyword}': [{Subs}]",
                result.Count, keyword, string.Join(", ", result));

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SubredditSuggestion: AI call failed for '{Keyword}', using fallback subreddits", keyword);
            IReadOnlyList<string> fallback = FallbackSubreddits;
            _cache[normalized] = fallback;
            return fallback;
        }
    }
}
