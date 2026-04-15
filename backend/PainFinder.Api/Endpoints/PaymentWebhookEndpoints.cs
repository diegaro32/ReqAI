using PainFinder.Application.Services;

namespace PainFinder.Api.Endpoints;

public static class PaymentWebhookEndpoints
{
    public static WebApplication MapPaymentWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhooks/lemonsqueezy", async (HttpRequest request, IPaymentService payment, CancellationToken cancellationToken) =>
        {
            var json = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
            var signature = request.Headers["X-Signature"].ToString();

            try
            {
                await payment.HandleWebhookAsync(json, signature, cancellationToken);
                return Results.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .AllowAnonymous()
        .WithTags("Payment");

        return app;
    }
}
