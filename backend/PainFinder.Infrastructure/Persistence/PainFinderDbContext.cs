using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PainFinder.Domain.Entities;

namespace PainFinder.Infrastructure.Persistence;

public class PainFinderDbContext(DbContextOptions<PainFinderDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<AppError> AppErrors => Set<AppError>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<RequirementGeneration> RequirementGenerations => Set<RequirementGeneration>();
    public DbSet<RefinementResult> RefinementResults => Set<RefinementResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PainFinderDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
