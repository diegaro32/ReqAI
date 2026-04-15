using PainFinder.Domain.Entities;

namespace PainFinder.Domain.Interfaces;

public interface IPainClusteringService
{
    Task<IReadOnlyList<PainCluster>> ClusterPainsAsync(IReadOnlyList<PainSignal> signals, CancellationToken cancellationToken = default);
}
