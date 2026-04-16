using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class RequirementGenerationConfiguration : IEntityTypeConfiguration<RequirementGeneration>
{
    public void Configure(EntityTypeBuilder<RequirementGeneration> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.OriginalInput)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(g => g.FunctionalRequirements)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(g => g.NonFunctionalRequirements)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(g => g.Ambiguities)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(g => g.SuggestedFeatures)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.HasMany(g => g.Refinements)
            .WithOne(r => r.Generation)
            .HasForeignKey(r => r.GenerationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
