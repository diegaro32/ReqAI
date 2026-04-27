namespace PainFinder.Domain.Entities;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public PlanType PlanType { get; set; } = PlanType.Free;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? PaymentCustomerId { get; set; }
    public string? PaymentSubscriptionId { get; set; }
    public string? PaymentVariantId { get; set; }
    public int SearchesUsedThisMonth { get; set; }
    public int MvpsUsedThisMonth { get; set; }
    public int MonitorsUsed { get; set; }
    public DateTime CurrentPeriodStart { get; set; }

    public int AnalysesUsedThisMonth
    {
        get => SearchesUsedThisMonth;
        set => SearchesUsedThisMonth = value;
    }

    public int RefinementsUsedThisMonth
    {
        get => MvpsUsedThisMonth;
        set => MvpsUsedThisMonth = value;
    }

    // --- Pricing (USD) ---

    public static decimal GetMonthlyPrice(PlanType plan) => plan switch
    {
        PlanType.Free => 0m,
        PlanType.Starter => 9m,
        PlanType.Pro => 19m,
        PlanType.Agency => 49m,
        _ => 0m
    };

    public static decimal GetAnnualPrice(PlanType plan) => plan switch
    {
        PlanType.Free => 0m,
        PlanType.Starter => 86m,
        PlanType.Pro => 182m,
        PlanType.Agency => 470m,
        _ => 0m
    };

    public decimal MonthlyPrice => GetMonthlyPrice(PlanType);
    public decimal AnnualPrice => GetAnnualPrice(PlanType);

    public decimal CurrentPrice => BillingCycle == BillingCycle.Annual ? AnnualPrice : MonthlyPrice;

    public decimal EffectiveMonthlyPrice => BillingCycle == BillingCycle.Annual
        ? Math.Round(AnnualPrice / 12m, 2)
        : MonthlyPrice;

    public int AnnualDiscountPercent => MonthlyPrice > 0
        ? (int)Math.Round((1m - AnnualPrice / (MonthlyPrice * 12m)) * 100m)
        : 0;

    public int BillingPeriodDays => BillingCycle == BillingCycle.Annual ? 365 : 30;

    public DateTime? NextBillingDate => PlanType == PlanType.Free || CancelledAt is not null
        ? null
        : ExpiresAt;

    public bool IsActive => PlanType == PlanType.Free
        || (ExpiresAt is not null && ExpiresAt > DateTime.UtcNow && CancelledAt is null);

    public bool IsCancelled => CancelledAt is not null;

    public int DaysRemaining => ExpiresAt is not null
        ? Math.Max(0, (int)(ExpiresAt.Value - DateTime.UtcNow).TotalDays)
        : 0;

    // --- Limits ---

    public int MaxAnalysesPerMonth => PlanType switch
    {
        PlanType.Free => 3,
        PlanType.Starter => 20,
        PlanType.Pro => int.MaxValue,
        PlanType.Agency => int.MaxValue,
        _ => 3
    };

    public int MaxRefinementsPerMonth => PlanType switch
    {
        PlanType.Free => 0,
        PlanType.Starter => int.MaxValue,
        PlanType.Pro => int.MaxValue,
        PlanType.Agency => int.MaxValue,
        _ => 0
    };

    public bool CanExport => PlanType is not PlanType.Free;

    public bool CanAnalyze => AnalysesUsedThisMonth < MaxAnalysesPerMonth;
    public bool CanRefine => RefinementsUsedThisMonth < MaxRefinementsPerMonth;
}
