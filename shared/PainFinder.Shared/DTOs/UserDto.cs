namespace PainFinder.Shared.DTOs;

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    DateTime CreatedAt,
    string PlanType = "Free");
