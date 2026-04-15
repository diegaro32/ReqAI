namespace PainFinder.Domain.Interfaces;

/// <summary>
/// Uses AI to suggest relevant subreddits based on a natural language search query.
/// Results are cached per keyword to avoid redundant AI calls.
/// </summary>
public interface ISubredditSuggestionService
{
    /// <summary>
    /// Returns a list of subreddit names (without "r/" prefix) relevant to the given keyword or phrase.
    /// Falls back to a default set if the AI call fails.
    /// </summary>
    Task<IReadOnlyList<string>> SuggestSubredditsAsync(string keyword, CancellationToken cancellationToken = default);
}
