using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class AppErrorConfiguration : IEntityTypeConfiguration<AppError>
{
    public void Configure(EntityTypeBuilder<AppError> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasMaxLength(200);
        builder.Property(e => e.Message).HasMaxLength(4000);
        builder.Property(e => e.ExceptionType).HasMaxLength(500);
        builder.Property(e => e.RequestPath).HasMaxLength(500);
        builder.HasIndex(e => e.OccurredAt);
    }
}
