using System.Security.Claims;
using PainFinder.Application.Services;

namespace PainFinder.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static RouteGroupBuilder MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/subscription").WithTags("Subscription");

        group.MapGet("/pricing", (ISubscriptionService service) =>
            Results.Ok(service.GetAllPlanPricing())).AllowAnonymous();

        group.MapGet("/", async (ISubscriptionService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var plan = await service.GetUserPlanAsync(userId, cancellationToken);
            return Results.Ok(plan);
        }).RequireAuthorization();

        group.MapPost("/checkout", async (CheckoutRequest request, IPaymentService payment, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var email = user.FindFirstValue(ClaimTypes.Email) ?? "";
            try
            {
                var url = await payment.CreateCheckoutSessionAsync(userId, email, request.PlanType, request.BillingCycle, cancellationToken);
                return Results.Ok(new { url });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/portal", async (IPaymentService payment, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                var url = await payment.CreatePortalSessionAsync(userId, cancellationToken);
                return Results.Ok(new { url });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        group.MapPost("/cancel", async (ISubscriptionService service, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                var plan = await service.CancelAsync(userId, cancellationToken);
                return Results.Ok(plan);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        return group;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}

public record CheckoutRequest(string PlanType, string BillingCycle = "Monthly");
