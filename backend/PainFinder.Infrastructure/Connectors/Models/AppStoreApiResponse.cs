using System.Text.Json.Serialization;

namespace PainFinder.Infrastructure.Connectors.Models;

public class ItunesSearchResponse
{
    [JsonPropertyName("results")]
    public List<ItunesApp> Results { get; set; } = [];
}

public class ItunesApp
{
    [JsonPropertyName("trackId")]
    public long TrackId { get; set; }

    [JsonPropertyName("trackName")]
    public string TrackName { get; set; } = string.Empty;

    [JsonPropertyName("bundleId")]
    public string BundleId { get; set; } = string.Empty;
}

public class AppStoreReviewFeed
{
    [JsonPropertyName("feed")]
    public AppStoreFeed? Feed { get; set; }
}

public class AppStoreFeed
{
    [JsonPropertyName("entry")]
    public List<AppStoreReviewEntry>? Entry { get; set; }
}

public class AppStoreReviewEntry
{
    [JsonPropertyName("author")]
    public AppStoreAuthor? Author { get; set; }

    [JsonPropertyName("title")]
    public AppStoreLabel? Title { get; set; }

    [JsonPropertyName("content")]
    public AppStoreLabel? Content { get; set; }

    [JsonPropertyName("im:rating")]
    public AppStoreLabel? Rating { get; set; }

    [JsonPropertyName("updated")]
    public AppStoreLabel? Updated { get; set; }
}

public class AppStoreAuthor
{
    [JsonPropertyName("name")]
    public AppStoreLabel? Name { get; set; }
}

public class AppStoreLabel
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}
