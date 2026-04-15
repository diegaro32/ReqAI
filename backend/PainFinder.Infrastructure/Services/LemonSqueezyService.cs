using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;

namespace PainFinder.Infrastructure.Services;

public class LemonSqueezyService(
    PainFinderDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<LemonSqueezySettings> settings,
    ILogger<LemonSqueezyService> logger) : IPaymentService
{
    private const string ApiBase = "https://api.lemonsqueezy.com/v1";
    private readonly LemonSqueezySettings _settings = settings.Value;

    public async Task<string> CreateCheckoutSessionAsync(
        Guid userId, string email, string planType, string billingCycle,
        CancellationToken cancellationToken = default)
    {
        var variantId = ResolveVariantId(planType, billingCycle);
        if (string.IsNullOrEmpty(variantId))
            throw new InvalidOperationException($"No Lemon Squeezy variant configured for {planType}/{billingCycle}");

        var client = CreateClient();

        var payload = new
        {
            data = new
            {
                type = "checkouts",
                attributes = new
                {
                    checkout_data = new
                    {
                        email,
                        custom = new Dictionary<string, string>
                        {
                            ["user_id"] = userId.ToString(),
                            ["plan_type"] = planType,
                            ["billing_cycle"] = billingCycle
                        }
                    },
                    product_options = new
                    {
                        redirect_url = _settings.SuccessUrl
                    }
                },
                relationships = new
                {
                    store = new { data = new { type = "stores", id = _settings.StoreId } },
                    variant = new { data = new { type = "variants", id = variantId } }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/vnd.api+json");

        var response = await client.PostAsync($"{ApiBase}/checkouts", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("LemonSqueezy: Checkout creation failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Failed to create checkout session.");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("url")
            .GetString();

        logger.LogInformation("LemonSqueezy: Checkout created for user {UserId} ({Plan}/{Cycle})", userId, planType, billingCycle);

        return url ?? throw new InvalidOperationException("Checkout URL not returned.");
    }

    public async Task<string> CreatePortalSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("No subscription found.");

        if (string.IsNullOrEmpty(plan.PaymentCustomerId))
            throw new InvalidOperationException("No payment customer found. Subscribe first.");

        // Lemon Squeezy customer portal URL pattern
        var portalUrl = $"https://app.lemonsqueezy.com/my-orders";

        // If we have a subscription ID, we can build a direct management URL
        if (!string.IsNullOrEmpty(plan.PaymentSubscriptionId))
        {
            var client = CreateClient();
            var response = await client.GetAsync(
                $"{ApiBase}/subscriptions/{plan.PaymentSubscriptionId}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var urls = doc.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("urls");
                if (urls.TryGetProperty("customer_portal", out var portal))
                {
                    portalUrl = portal.GetString() ?? portalUrl;
                }
            }
        }

        return portalUrl;
    }

    public async Task HandleWebhookAsync(string json, string signature, CancellationToken cancellationToken = default)
    {
        if (!VerifySignature(json, signature))
            throw new InvalidOperationException("Invalid webhook signature.");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var eventName = root.GetProperty("meta").GetProperty("event_name").GetString();
        logger.LogInformation("LemonSqueezy webhook: {Event}", eventName);

        switch (eventName)
        {
            case "subscription_created":
                await HandleSubscriptionCreated(root, cancellationToken);
                break;
            case "subscription_updated":
                await HandleSubscriptionUpdated(root, cancellationToken);
                break;
            case "subscription_cancelled":
                await HandleSubscriptionCancelled(root, cancellationToken);
                break;
            case "subscription_expired":
                await HandleSubscriptionExpired(root, cancellationToken);
                break;
            case "subscription_payment_success":
                await HandlePaymentSuccess(root, cancellationToken);
                break;
            case "subscription_payment_failed":
                await HandlePaymentFailed(root, cancellationToken);
                break;
        }
    }

    private async Task HandleSubscriptionCreated(JsonElement root, CancellationToken ct)
    {
        var meta = root.GetProperty("meta");
        var customData = meta.GetProperty("custom_data");
        var userId = Guid.Parse(customData.GetProperty("user_id").GetString()!);
        var planType = Enum.Parse<PlanType>(customData.GetProperty("plan_type").GetString()!, true);
        var billingCycle = Enum.Parse<BillingCycle>(customData.GetProperty("billing_cycle").GetString()!, true);

        var data = root.GetProperty("data");
        var attrs = data.GetProperty("attributes");
        var subscriptionId = data.GetProperty("id").GetString();
        var customerId = attrs.GetProperty("customer_id").GetInt64().ToString();
        var variantId = attrs.GetProperty("variant_id").GetInt64().ToString();

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (plan is null)
        {
            plan = new SubscriptionPlan { Id = Guid.NewGuid(), UserId = userId };
            dbContext.SubscriptionPlans.Add(plan);
        }

        plan.PlanType = planType;
        plan.BillingCycle = billingCycle;
        plan.PaymentCustomerId = customerId;
        plan.PaymentSubscriptionId = subscriptionId;
        plan.PaymentVariantId = variantId;
        plan.StartedAt = DateTime.UtcNow;
        plan.CancelledAt = null;
        plan.ExpiresAt = DateTime.UtcNow.AddDays(plan.BillingPeriodDays);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("LemonSqueezy: User {UserId} subscribed to {Plan}/{Cycle}", userId, planType, billingCycle);
    }

    private async Task HandleSubscriptionUpdated(JsonElement root, CancellationToken ct)
    {
        var data = root.GetProperty("data");
        var subscriptionId = data.GetProperty("id").GetString();
        var attrs = data.GetProperty("attributes");

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.PaymentSubscriptionId == subscriptionId, ct);
        if (plan is null) return;

        var status = attrs.GetProperty("status").GetString();
        if (status == "cancelled")
        {
            plan.CancelledAt ??= DateTime.UtcNow;
            logger.LogInformation("LemonSqueezy: Subscription {SubId} marked for cancellation", subscriptionId);
        }
        else if (status == "active")
        {
            plan.CancelledAt = null;
        }

        if (attrs.TryGetProperty("renews_at", out var renewsAt) && renewsAt.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(renewsAt.GetString(), out var renewDate))
                plan.ExpiresAt = renewDate;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionCancelled(JsonElement root, CancellationToken ct)
    {
        var data = root.GetProperty("data");
        var subscriptionId = data.GetProperty("id").GetString();

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.PaymentSubscriptionId == subscriptionId, ct);
        if (plan is null) return;

        plan.CancelledAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("LemonSqueezy: Subscription {SubId} cancelled", subscriptionId);
    }

    private async Task HandleSubscriptionExpired(JsonElement root, CancellationToken ct)
    {
        var data = root.GetProperty("data");
        var subscriptionId = data.GetProperty("id").GetString();

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.PaymentSubscriptionId == subscriptionId, ct);
        if (plan is null) return;

        plan.PlanType = PlanType.Free;
        plan.BillingCycle = BillingCycle.Monthly;
        plan.CancelledAt ??= DateTime.UtcNow;
        plan.PaymentSubscriptionId = null;
        plan.PaymentVariantId = null;

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("LemonSqueezy: Subscription expired — user downgraded to Free");
    }

    private async Task HandlePaymentSuccess(JsonElement root, CancellationToken ct)
    {
        var data = root.GetProperty("data");
        var attrs = data.GetProperty("attributes");

        var subscriptionId = attrs.TryGetProperty("subscription_id", out var subIdEl)
            ? subIdEl.GetInt64().ToString() : null;
        if (subscriptionId is null) return;

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.PaymentSubscriptionId == subscriptionId, ct);
        if (plan is null) return;

        plan.ExpiresAt = DateTime.UtcNow.AddDays(plan.BillingPeriodDays);
        plan.CancelledAt = null;

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("LemonSqueezy: Payment success for subscription {SubId} — renewed", subscriptionId);
    }

    private async Task HandlePaymentFailed(JsonElement root, CancellationToken ct)
    {
        var data = root.GetProperty("data");
        var attrs = data.GetProperty("attributes");

        var subscriptionId = attrs.TryGetProperty("subscription_id", out var subIdEl)
            ? subIdEl.GetInt64().ToString() : null;

        logger.LogWarning("LemonSqueezy: Payment FAILED for subscription {SubId}", subscriptionId);
    }

    private bool VerifySignature(string payload, string signature)
    {
        if (string.IsNullOrEmpty(_settings.WebhookSecret) || string.IsNullOrEmpty(signature))
            return false;

        var secret = Encoding.UTF8.GetBytes(_settings.WebhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(secret, payloadBytes);
        var computed = Convert.ToHexStringLower(hash);

        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveVariantId(string planType, string billingCycle)
    {
        var key = $"{planType}{billingCycle}";
        return key switch
        {
            "StarterMonthly" => _settings.VariantIds.StarterMonthly,
            "StarterAnnual" => _settings.VariantIds.StarterAnnual,
            "ProMonthly" => _settings.VariantIds.ProMonthly,
            "ProAnnual" => _settings.VariantIds.ProAnnual,
            "AgencyMonthly" => _settings.VariantIds.AgencyMonthly,
            "AgencyAnnual" => _settings.VariantIds.AgencyAnnual,
            _ => null
        };
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("LemonSqueezy");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        return client;
    }
}
