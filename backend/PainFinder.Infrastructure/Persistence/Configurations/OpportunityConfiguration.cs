using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence.Configurations;

public class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Title).IsRequired().HasMaxLength(300);
        builder.Property(o => o.Description).HasMaxLength(2000);
        builder.Property(o => o.ProblemSummary).HasMaxLength(2000);
        builder.Property(o => o.SuggestedSolution).HasMaxLength(2000);
        builder.Property(o => o.MarketCategory).HasMaxLength(200);

        builder.Property(o => o.IcpRole).HasMaxLength(300);
        builder.Property(o => o.IcpContext).HasMaxLength(500);
        builder.Property(o => o.ToolsDetected).HasMaxLength(1000);
        builder.Property(o => o.GapAnalysis).HasMaxLength(2000);
        builder.Property(o => o.BuildDecision).HasMaxLength(20);
        builder.Property(o => o.BuildReasoning).HasMaxLength(1000);
        builder.Property(o => o.EvidenceQuotesJson).HasMaxLength(4000);
    }
}
