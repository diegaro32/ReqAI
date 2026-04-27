namespace PainFinder.Shared.DTOs;

public record SubscriptionPlanDto(
    Guid Id,
    string PlanType,
    string BillingCycle,
    DateTime StartedAt,
    DateTime? ExpiresAt,
    DateTime? CancelledAt,
    bool IsActive,
    bool IsCancelled,
    int DaysRemaining,
    DateTime? NextBillingDate,
    decimal CurrentPrice,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    decimal EffectiveMonthlyPrice,
    int AnnualDiscountPercent,
    int AnalysesUsedThisMonth,
    int MaxAnalysesPerMonth,
    int RefinementsUsedThisMonth,
    int MaxRefinementsPerMonth,
    bool CanExport,
    bool CanAnalyze,
    bool CanRefine,
    DateTime CurrentPeriodStart);

public record PlanPricingDto(
    string PlanType,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    decimal EffectiveMonthlyPrice,
    int AnnualDiscountPercent,
    int MaxAnalysesPerMonth,
    int MaxRefinementsPerMonth,
    bool CanExport);
