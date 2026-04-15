using System.Text.Json.Serialization;

namespace PainFinder.Infrastructure.Connectors.Models;

public class HackerNewsApiResponse
{
    [JsonPropertyName("hits")]
    public List<HackerNewsHit> Hits { get; set; } = [];

    [JsonPropertyName("nbHits")]
    public int TotalHits { get; set; }
}

public class HackerNewsHit
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("story_text")]
    public string? StoryText { get; set; }

    [JsonPropertyName("comment_text")]
    public string? CommentText { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("objectID")]
    public string ObjectId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("created_at_i")]
    public long CreatedAtTimestamp { get; set; }

    [JsonPropertyName("num_comments")]
    public int? NumComments { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }
}
