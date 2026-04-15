using System.Security.Claims;
using PainFinder.Application.Services;
using PainFinder.Shared.Contracts;

namespace PainFinder.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await authService.RegisterAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await authService.LoginAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).AllowAnonymous();

        group.MapPost("/google", async (
            GoogleLoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await authService.GoogleLoginAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).AllowAnonymous();

        group.MapGet("/me", async (
            ClaimsPrincipal user,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            if (userId is null || !Guid.TryParse(userId, out var id))
                return Results.Unauthorized();

            var userDto = await authService.GetUserByIdAsync(id, cancellationToken);
            return userDto is not null ? Results.Ok(userDto) : Results.NotFound();
        }).RequireAuthorization();

        return group;
    }
}
