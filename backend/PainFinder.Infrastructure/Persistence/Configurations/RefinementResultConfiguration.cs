using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class RefinementResultConfiguration : IEntityTypeConfiguration<RefinementResult>
{
    public void Configure(EntityTypeBuilder<RefinementResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Instruction)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.RefinedOutput)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
    }
}
