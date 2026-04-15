using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IRadarMonitorService
{
    Task<RadarMonitorDto> CreateMonitorAsync(CreateRadarMonitorRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RadarMonitorDto>> GetAllMonitorsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RadarMonitorDto?> GetMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RadarScanDto>> GetMonitorScansAsync(Guid monitorId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> PauseMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ResumeMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ArchiveMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
