namespace PainFinder.Shared.Contracts;

public record SearchRunRequest(
    List<string> Sources,
    string Keyword,
    DateTime? DateFrom,
    DateTime? DateTo);
