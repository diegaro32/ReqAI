using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using PainFinder.Application.Services;

namespace PainFinder.Web.Services;

public class GlobalErrorHandler(IErrorLogService errorLogService, AuthService authService, ILogger<GlobalErrorHandler> logger) : IErrorBoundaryLogger
{
    public async ValueTask LogErrorAsync(Exception exception)
    {
        logger.LogError(exception, "Blazor ErrorBoundary caught: {Message}", exception.Message);

        try
        {
            var userId = authService.User?.Id;
            await errorLogService.LogErrorAsync("Blazor", exception, userId: userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist Blazor error to database");
        }
    }
}
