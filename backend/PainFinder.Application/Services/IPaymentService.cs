using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(Guid userId, string email, string planType, string billingCycle, CancellationToken cancellationToken = default);
    Task<string> CreatePortalSessionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task HandleWebhookAsync(string json, string signature, CancellationToken cancellationToken = default);
}
