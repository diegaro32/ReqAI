using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class PainSignalConfiguration : IEntityTypeConfiguration<PainSignal>
{
    public void Configure(EntityTypeBuilder<PainSignal> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PainPhrase).IsRequired().HasMaxLength(500);
        builder.Property(p => p.PainCategory).HasMaxLength(200);
    }
}
