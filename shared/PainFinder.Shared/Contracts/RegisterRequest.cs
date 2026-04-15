namespace PainFinder.Shared.Contracts;

public record RegisterRequest(
    string FullName,
    string Email,
    string Password);
