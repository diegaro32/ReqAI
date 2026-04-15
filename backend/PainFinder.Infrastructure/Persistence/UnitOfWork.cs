using PainFinder.Domain.Interfaces;

namespace PainFinder.Infrastructure.Persistence;

public class UnitOfWork(PainFinderDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}
