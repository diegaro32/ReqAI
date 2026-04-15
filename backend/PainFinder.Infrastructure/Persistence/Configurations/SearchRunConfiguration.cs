using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class SearchRunConfiguration : IEntityTypeConfiguration<SearchRun>
{
    public void Configure(EntityTypeBuilder<SearchRun> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Keyword).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Sources).HasMaxLength(1000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
