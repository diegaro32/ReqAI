using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class PainClusterConfiguration : IEntityTypeConfiguration<PainCluster>
{
    public void Configure(EntityTypeBuilder<PainCluster> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(300);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Category).HasMaxLength(200);

        builder.HasMany(c => c.PainSignals)
            .WithOne(p => p.PainCluster)
            .HasForeignKey(p => p.PainClusterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Opportunities)
            .WithOne(o => o.PainCluster)
            .HasForeignKey(o => o.PainClusterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
