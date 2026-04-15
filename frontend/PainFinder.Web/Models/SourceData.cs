namespace PainFinder.Web.Models;

public record SourceInfo(string Name, string Tag);

public static class SourceData
{
    public static readonly List<SourceInfo> Sources =
    [
        new("Reddit", "API"),
        new("HackerNews", "API"),
    ];

    public static readonly List<string> AllSourceNames =
        Sources.Select(s => s.Name).ToList();
}
