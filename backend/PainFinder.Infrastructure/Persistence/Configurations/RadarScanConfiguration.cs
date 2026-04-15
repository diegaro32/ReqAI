using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class RadarScanConfiguration : IEntityTypeConfiguration<RadarScan>
{
    public void Configure(EntityTypeBuilder<RadarScan> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.ExpandedQuery).HasMaxLength(4000);
        builder.HasMany(s => s.Documents).WithOne(d => d.RadarScan).HasForeignKey(d => d.RadarScanId);
    }
}
