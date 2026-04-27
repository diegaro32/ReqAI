using Microsoft.EntityFrameworkCore;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Shared.DTOs;

namespace PainFinder.Infrastructure.Services;

public class SubscriptionService(PainFinderDbContext dbContext) : ISubscriptionService
{
    public async Task<SubscriptionPlanDto> GetUserPlanAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrCreatePlanAsync(userId, cancellationToken);
        ResetPeriodIfNeeded(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> UpgradeAsync(Guid userId, string planType, string billingCycle, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PlanType>(planType, true, out var newPlan))
            throw new InvalidOperationException($"Invalid plan type: {planType}");

        if (!Enum.TryParse<BillingCycle>(billingCycle, true, out var cycle))
            cycle = BillingCycle.Monthly;

        var plan = await GetOrCreatePlanAsync(userId, cancellationToken);
        plan.PlanType = newPlan;
        plan.BillingCycle = newPlan == PlanType.Free ? BillingCycle.Monthly : cycle;
        plan.StartedAt = DateTime.UtcNow;
        plan.CancelledAt = null;
        plan.ExpiresAt = newPlan == PlanType.Free ? null : DateTime.UtcNow.AddDays(plan.BillingPeriodDays);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> CancelAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOrCreatePlanAsync(userId, cancellationToken);

        if (plan.PlanType == PlanType.Free)
            throw new InvalidOperationException("Cannot cancel a free plan.");

        plan.CancelledAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(plan);
    }

    public async Task EnsurePlanExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await GetOrCreatePlanAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public List<PlanPricingDto> GetAllPlanPricing()
    {
        return Enum.GetValues<PlanType>().Select(pt =>
        {
            var plan = new SubscriptionPlan { PlanType = pt };
            return new PlanPricingDto(
                pt.ToString(),
                plan.MonthlyPrice,
                plan.AnnualPrice,
                plan.EffectiveMonthlyPrice,
                plan.AnnualDiscountPercent,
                plan.MaxAnalysesPerMonth == int.MaxValue ? -1 : plan.MaxAnalysesPerMonth,
                plan.MaxRefinementsPerMonth == int.MaxValue ? -1 : plan.MaxRefinementsPerMonth,
                plan.CanExport);
        }).ToList();
    }

    private async Task<SubscriptionPlan> GetOrCreatePlanAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (plan is not null) return plan;

        plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanType = PlanType.Free,
            BillingCycle = BillingCycle.Monthly,
            StartedAt = DateTime.UtcNow,
            CurrentPeriodStart = DateTime.UtcNow
        };

        dbContext.SubscriptionPlans.Add(plan);
        return plan;
    }

    private static void ResetPeriodIfNeeded(SubscriptionPlan plan)
    {
        if (plan.PlanType == PlanType.Free) return;

        var now = DateTime.UtcNow;
        if (now.Year != plan.CurrentPeriodStart.Year || now.Month != plan.CurrentPeriodStart.Month)
        {
            plan.CurrentPeriodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            plan.AnalysesUsedThisMonth = 0;
            plan.RefinementsUsedThisMonth = 0;
        }
    }

    private static SubscriptionPlanDto ToDto(SubscriptionPlan plan) => new(
        plan.Id,
        plan.PlanType.ToString(),
        plan.BillingCycle.ToString(),
        plan.StartedAt,
        plan.ExpiresAt,
        plan.CancelledAt,
        plan.IsActive,
        plan.IsCancelled,
        plan.DaysRemaining,
        plan.NextBillingDate,
        plan.CurrentPrice,
        plan.MonthlyPrice,
        plan.AnnualPrice,
        plan.EffectiveMonthlyPrice,
        plan.AnnualDiscountPercent,
        plan.AnalysesUsedThisMonth,
        plan.MaxAnalysesPerMonth == int.MaxValue ? -1 : plan.MaxAnalysesPerMonth,
        plan.RefinementsUsedThisMonth,
        plan.MaxRefinementsPerMonth == int.MaxValue ? -1 : plan.MaxRefinementsPerMonth,
        plan.CanExport,
        plan.CanAnalyze,
        plan.CanRefine,
        plan.CurrentPeriodStart);
}
