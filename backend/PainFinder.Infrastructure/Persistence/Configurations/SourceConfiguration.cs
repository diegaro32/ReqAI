using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.BaseUrl).HasMaxLength(500);
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(50);

        builder.HasMany(s => s.RawDocuments)
            .WithOne(d => d.Source)
            .HasForeignKey(d => d.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
