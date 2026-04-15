namespace PainFinder.Shared.DTOs;

public record AuthResponseDto(
    string Token,
    DateTime ExpiresAt,
    UserDto User);
