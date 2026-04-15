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

    // --- Pricing (USD) ---

    public static decimal GetMonthlyPrice(PlanType plan) => plan switch
    {
        PlanType.Free => 0m,
        PlanType.Starter => 5m,
        PlanType.Pro => 19m,
        PlanType.Agency => 59m,
        _ => 0m
    };

    public static decimal GetAnnualPrice(PlanType plan) => plan switch
    {
        PlanType.Free => 0m,
        PlanType.Starter => 48m,
        PlanType.Pro => 182m,
        PlanType.Agency => 566m,
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

    public int MaxSearchesPerMonth => PlanType switch
    {
        PlanType.Free => 1,
        PlanType.Starter => 15,
        PlanType.Pro => int.MaxValue,
        PlanType.Agency => int.MaxValue,
        _ => 1
    };

    public int MaxMvpsPerMonth => PlanType switch
    {
        PlanType.Free => 1,
        PlanType.Starter => 10,
        PlanType.Pro => 50,
        PlanType.Agency => int.MaxValue,
        _ => 1
    };

    public int MaxRadarMonitors => PlanType switch
    {
        PlanType.Free => 0,
        PlanType.Starter => 1,
        PlanType.Pro => 5,
        PlanType.Agency => 15,
        _ => 0
    };

    public int MaxPainsPerSearch => PlanType switch
    {
        PlanType.Free => 30,
        PlanType.Starter => int.MaxValue,
        PlanType.Pro => int.MaxValue,
        PlanType.Agency => int.MaxValue,
        _ => 30
    };

    public bool CanExport => PlanType is not PlanType.Free;

    public bool CanSearch => SearchesUsedThisMonth < MaxSearchesPerMonth;
    public bool CanGenerateMvp => MvpsUsedThisMonth < MaxMvpsPerMonth;
    public bool CanCreateMonitor(int currentMonitors) => currentMonitors < MaxRadarMonitors;
}
