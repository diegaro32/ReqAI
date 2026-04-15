using PainFinder.Domain.Entities;

namespace PainFinder.Domain.Interfaces;

public interface IPainDetectionService
{
    Task<IReadOnlyList<PainSignal>> DetectPainsAsync(RawDocument document, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PainSignal>> DetectPainsBatchAsync(IReadOnlyList<RawDocument> documents, CancellationToken cancellationToken = default);
}
