using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class RadarMonitorConfiguration : IEntityTypeConfiguration<RadarMonitor>
{
    public void Configure(EntityTypeBuilder<RadarMonitor> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Keyword).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Sources).IsRequired().HasMaxLength(500);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(r => r.Scans).WithOne(s => s.RadarMonitor).HasForeignKey(s => s.RadarMonitorId);
    }
}
