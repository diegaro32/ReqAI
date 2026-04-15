namespace PainFinder.Shared.Contracts;

public record CreateRadarMonitorRequest(
    string Name,
    string Keyword,
    List<string> Sources);
