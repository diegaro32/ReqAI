using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class RawDocumentConfiguration : IEntityTypeConfiguration<RawDocument>
{
    public void Configure(EntityTypeBuilder<RawDocument> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Title).IsRequired().HasMaxLength(500);
        builder.Property(d => d.Content).IsRequired();
        builder.Property(d => d.Author).HasMaxLength(200);
        builder.Property(d => d.Url).HasMaxLength(1000);

        builder.HasMany(d => d.PainSignals)
            .WithOne(p => p.RawDocument)
            .HasForeignKey(p => p.RawDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
