using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface ISearchRunService
{
    Task<SearchRunResponse> StartSearchRunAsync(SearchRunRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<SearchRunDto?> GetSearchRunAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchRunDto>> GetAllSearchRunsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawDocumentDto>> GetSearchRunDocumentsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PainSignalDto>> GetSearchRunPainsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PainClusterDto>> GetSearchRunClustersAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpportunityDto>> GetSearchRunOpportunitiesAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSearchRunAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
