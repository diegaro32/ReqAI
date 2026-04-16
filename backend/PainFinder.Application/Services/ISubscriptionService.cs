using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface ISubscriptionService
{
    Task<SubscriptionPlanDto> GetUserPlanAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> UpgradeAsync(Guid userId, string planType, string billingCycle, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> CancelAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EnsurePlanExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    List<PlanPricingDto> GetAllPlanPricing();
}
