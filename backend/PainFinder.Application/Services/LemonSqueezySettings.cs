namespace PainFinder.Application.Services;

public class LemonSqueezySettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public LemonSqueezyVariantIds VariantIds { get; set; } = new();
}

public class LemonSqueezyVariantIds
{
    public string StarterMonthly { get; set; } = string.Empty;
    public string StarterAnnual { get; set; } = string.Empty;
    public string ProMonthly { get; set; } = string.Empty;
    public string ProAnnual { get; set; } = string.Empty;
    public string AgencyMonthly { get; set; } = string.Empty;
    public string AgencyAnnual { get; set; } = string.Empty;
}
