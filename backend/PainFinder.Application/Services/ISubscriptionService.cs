using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface ISubscriptionService
{
    Task<SubscriptionPlanDto> GetUserPlanAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> UpgradeAsync(Guid userId, string planType, string billingCycle, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> CancelAsync(Guid userId, CancellationToken cancellationToken = default);
    Task IncrementSearchUsageAsync(Guid userId, CancellationToken cancellationToken = default);
    Task IncrementMvpUsageAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUserSearchAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUserGenerateMvpAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUserCreateMonitorAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EnsurePlanExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    List<PlanPricingDto> GetAllPlanPricing();
}
