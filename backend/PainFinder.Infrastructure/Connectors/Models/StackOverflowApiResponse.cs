using System.Text.Json.Serialization;

namespace PainFinder.Infrastructure.Connectors.Models;

public class StackOverflowApiResponse
{
    [JsonPropertyName("items")]
    public List<StackOverflowQuestion> Items { get; set; } = [];

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("quota_remaining")]
    public int QuotaRemaining { get; set; }

    // API error fields — populated when the request fails (throttle, bad key, etc.)
    [JsonPropertyName("error_id")]
    public int? ErrorId { get; set; }

    [JsonPropertyName("error_name")]
    public string? ErrorName { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public class StackOverflowQuestion
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; set; }

    [JsonPropertyName("question_id")]
    public long QuestionId { get; set; }

    [JsonPropertyName("owner")]
    public StackOverflowOwner? Owner { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("creation_date")]
    public long CreationDate { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("answer_count")]
    public int AnswerCount { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

public class StackOverflowOwner
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}
