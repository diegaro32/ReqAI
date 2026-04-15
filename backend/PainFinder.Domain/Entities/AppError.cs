namespace PainFinder.Domain.Entities;

public class AppError
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? ExceptionType { get; set; }
    public string? RequestPath { get; set; }
    public Guid? UserId { get; set; }
    public int? StatusCode { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
