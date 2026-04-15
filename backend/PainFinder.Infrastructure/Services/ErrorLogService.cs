using Microsoft.Extensions.Logging;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;

namespace PainFinder.Infrastructure.Services;

public class ErrorLogService(PainFinderDbContext dbContext, ILogger<ErrorLogService> logger) : IErrorLogService
{
    public async Task LogErrorAsync(string source, Exception exception, string? requestPath = null, Guid? userId = null, int? statusCode = null)
    {
        try
        {
            var error = new AppError
            {
                Id = Guid.NewGuid(),
                Source = Truncate(source, 200),
                Message = Truncate(exception.Message, 4000),
                StackTrace = exception.StackTrace,
                ExceptionType = Truncate(exception.GetType().FullName ?? exception.GetType().Name, 500),
                RequestPath = Truncate(requestPath, 500),
                UserId = userId,
                StatusCode = statusCode,
                OccurredAt = DateTime.UtcNow
            };

            dbContext.AppErrors.Add(error);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist error log to database");
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
