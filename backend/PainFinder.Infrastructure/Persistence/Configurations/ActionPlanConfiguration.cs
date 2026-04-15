using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class ActionPlanConfiguration : IEntityTypeConfiguration<ActionPlan>
{
    public void Configure(EntityTypeBuilder<ActionPlan> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Opportunity)
            .WithOne(o => o.ActionPlan)
            .HasForeignKey<ActionPlan>(a => a.OpportunityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.ProblemStatement).HasMaxLength(2000);
        builder.Property(a => a.TargetUsers).HasMaxLength(1000);
        builder.Property(a => a.CoreFeaturesJson).HasMaxLength(4000);
        builder.Property(a => a.TechStack).HasMaxLength(1000);
        builder.Property(a => a.ValidationStrategy).HasMaxLength(2000);
        builder.Property(a => a.FirstStep).HasMaxLength(2000);
        builder.Property(a => a.EstimatedTimeline).HasMaxLength(500);
        builder.Property(a => a.ExactIcp).HasMaxLength(1000);
        builder.Property(a => a.ValueProposition).HasMaxLength(2000);
        builder.Property(a => a.OutreachMessage).HasMaxLength(4000);
        builder.Property(a => a.ValidationTest).HasMaxLength(2000);
        builder.Property(a => a.FirstStepTomorrow).HasMaxLength(2000);
        builder.Property(a => a.MonetizationStrategiesJson).HasMaxLength(4000).HasDefaultValue("[]");
    }
}
