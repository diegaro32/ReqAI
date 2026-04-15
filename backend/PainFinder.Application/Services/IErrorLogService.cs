namespace PainFinder.Application.Services;

public interface IErrorLogService
{
    Task LogErrorAsync(string source, Exception exception, string? requestPath = null, Guid? userId = null, int? statusCode = null);
}
