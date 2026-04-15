using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.PlanType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.BillingCycle).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.PaymentCustomerId).HasColumnName("StripeCustomerId").HasMaxLength(255);
        builder.Property(s => s.PaymentSubscriptionId).HasColumnName("StripeSubscriptionId").HasMaxLength(255);
        builder.Property(s => s.PaymentVariantId).HasColumnName("StripePriceId").HasMaxLength(255);
        builder.HasOne(s => s.User).WithOne().HasForeignKey<SubscriptionPlan>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.UserId).IsUnique();

        builder.Ignore(s => s.MonthlyPrice);
        builder.Ignore(s => s.AnnualPrice);
        builder.Ignore(s => s.CurrentPrice);
        builder.Ignore(s => s.EffectiveMonthlyPrice);
        builder.Ignore(s => s.AnnualDiscountPercent);
        builder.Ignore(s => s.BillingPeriodDays);
        builder.Ignore(s => s.NextBillingDate);
        builder.Ignore(s => s.IsActive);
        builder.Ignore(s => s.IsCancelled);
        builder.Ignore(s => s.DaysRemaining);
        builder.Ignore(s => s.MaxSearchesPerMonth);
        builder.Ignore(s => s.MaxMvpsPerMonth);
        builder.Ignore(s => s.MaxRadarMonitors);
        builder.Ignore(s => s.MaxPainsPerSearch);
        builder.Ignore(s => s.CanExport);
        builder.Ignore(s => s.CanSearch);
        builder.Ignore(s => s.CanGenerateMvp);
    }
}
