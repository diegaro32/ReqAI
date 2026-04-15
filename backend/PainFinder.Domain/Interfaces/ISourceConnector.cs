using PainFinder.Domain.Entities;

namespace PainFinder.Domain.Interfaces;

public interface ISourceConnector
{
    SourceType SourceType { get; }
    Task<IReadOnlyList<RawDocument>> FetchDocumentsAsync(string keyword, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
