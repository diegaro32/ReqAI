using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class RequirementGenerationConfiguration : IEntityTypeConfiguration<RequirementGeneration>
{
    public void Configure(EntityTypeBuilder<RequirementGeneration> builder)
    {
        builder.HasKey(g => g.Id);

        foreach (var col in new[]
        {
            nameof(RequirementGeneration.ConversationInput),
            nameof(RequirementGeneration.SystemOverview),
            nameof(RequirementGeneration.DomainModel),
            nameof(RequirementGeneration.LifecycleModel),
            nameof(RequirementGeneration.FunctionalRequirements),
            nameof(RequirementGeneration.BusinessRules),
            nameof(RequirementGeneration.Inconsistencies),
            nameof(RequirementGeneration.NonFunctionalRequirements),
            nameof(RequirementGeneration.Ambiguities),
            nameof(RequirementGeneration.Prioritization),
            nameof(RequirementGeneration.DecisionPoints),
            nameof(RequirementGeneration.OwnershipActions),
            nameof(RequirementGeneration.SystemInsights),
            nameof(RequirementGeneration.SuggestedFeatures),
            nameof(RequirementGeneration.ImplementationRisks),
        })
        {
            builder.Property(col).IsRequired().HasColumnType("nvarchar(max)");
        }

        builder.HasMany(g => g.Refinements)
            .WithOne(r => r.Generation)
            .HasForeignKey(r => r.GenerationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
