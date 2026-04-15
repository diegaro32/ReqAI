using System.Security.Claims;
using System.Text.Json;
using PainFinder.Application.Services;

namespace PainFinder.Api.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Only handle API endpoint exceptions (not Blazor circuits)
            if (!context.Request.Path.StartsWithSegments("/_blazor") &&
                !context.Request.Path.StartsWithSegments("/_framework"))
            {
                await HandleExceptionAsync(context, ex);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        // Persist to database
        try
        {
            var errorService = context.RequestServices.GetRequiredService<IErrorLogService>();
            var userId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (Guid?)null;
            var statusCode = GetStatusCode(exception);
            await errorService.LogErrorAsync("API", exception, context.Request.Path, userId, statusCode);
        }
        catch (Exception logEx)
        {
            logger.LogError(logEx, "Failed to log error to database");
        }

        // Return user-friendly response
        var code = GetStatusCode(exception);
        context.Response.StatusCode = code;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = GetUserFriendlyMessage(exception),
            status = code
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        InvalidOperationException => StatusCodes.Status400BadRequest,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        HttpRequestException httpEx when httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests => StatusCodes.Status429TooManyRequests,
        HttpRequestException => StatusCodes.Status502BadGateway,
        TaskCanceledException => StatusCodes.Status408RequestTimeout,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetUserFriendlyMessage(Exception exception) => exception switch
    {
        InvalidOperationException ex => ex.Message,
        UnauthorizedAccessException => "You are not authorized to perform this action.",
        KeyNotFoundException => "The requested resource was not found.",
        HttpRequestException httpEx when httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            => "Our analysis agents are at capacity. Please wait a moment and try again.",
        HttpRequestException
            => "An external service is temporarily unavailable. Please try again in a few minutes.",
        TaskCanceledException
            => "The request took too long to complete. Please try again.",
        _ => "Something unexpected happened. Our team has been notified."
    };
}
