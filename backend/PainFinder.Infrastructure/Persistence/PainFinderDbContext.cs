using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence;

public class PainFinderDbContext(DbContextOptions<PainFinderDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<RawDocument> RawDocuments => Set<RawDocument>();
    public DbSet<PainSignal> PainSignals => Set<PainSignal>();
    public DbSet<PainCluster> PainClusters => Set<PainCluster>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<ActionPlan> ActionPlans => Set<ActionPlan>();
    public DbSet<DeepAnalysis> DeepAnalyses => Set<DeepAnalysis>();
    public DbSet<SearchRun> SearchRuns => Set<SearchRun>();
    public DbSet<RadarMonitor> RadarMonitors => Set<RadarMonitor>();
    public DbSet<RadarScan> RadarScans => Set<RadarScan>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<AppError> AppErrors => Set<AppError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PainFinderDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
