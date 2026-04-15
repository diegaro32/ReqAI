using PainFinder.Application.Mapping;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public class RadarMonitorService(
    IRepository<RadarMonitor> monitorRepository,
    IRepository<RadarScan> scanRepository,
    ISubscriptionService subscriptionService,
    IUnitOfWork unitOfWork) : IRadarMonitorService
{
    public async Task<RadarMonitorDto> CreateMonitorAsync(CreateRadarMonitorRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await subscriptionService.CanUserCreateMonitorAsync(userId, cancellationToken))
            throw new InvalidOperationException("You have reached your radar monitor limit. Upgrade your plan for more monitors.");
        var monitor = new RadarMonitor
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Keyword = request.Keyword,
            Sources = string.Join(",", request.Sources),
            Status = RadarMonitorStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await monitorRepository.AddAsync(monitor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return monitor.ToDto();
    }

    public async Task<IReadOnlyList<RadarMonitorDto>> GetAllMonitorsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var monitors = await monitorRepository.GetAllAsync(cancellationToken);
        return monitors
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.ToDto())
            .ToList();
    }

    public async Task<RadarMonitorDto?> GetMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var monitor = await monitorRepository.GetByIdAsync(id, cancellationToken);
        if (monitor is null || monitor.UserId != userId) return null;
        return monitor.ToDto();
    }

    public async Task<IReadOnlyList<RadarScanDto>> GetMonitorScansAsync(Guid monitorId, Guid userId, CancellationToken cancellationToken = default)
    {
        var monitor = await monitorRepository.GetByIdAsync(monitorId, cancellationToken);
        if (monitor is null || monitor.UserId != userId) return [];

        var scans = await scanRepository.GetAllAsync(cancellationToken);
        return scans
            .Where(s => s.RadarMonitorId == monitorId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => s.ToDto())
            .ToList();
    }

    public async Task<bool> PauseMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var monitor = await monitorRepository.GetByIdAsync(id, cancellationToken);
        if (monitor is null || monitor.UserId != userId || monitor.Status != RadarMonitorStatus.Active) return false;
        monitor.Status = RadarMonitorStatus.Paused;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResumeMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var monitor = await monitorRepository.GetByIdAsync(id, cancellationToken);
        if (monitor is null || monitor.UserId != userId || monitor.Status != RadarMonitorStatus.Paused) return false;
        monitor.Status = RadarMonitorStatus.Active;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ArchiveMonitorAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var monitor = await monitorRepository.GetByIdAsync(id, cancellationToken);
        if (monitor is null || monitor.UserId != userId || monitor.Status == RadarMonitorStatus.Archived) return false;
        monitor.Status = RadarMonitorStatus.Archived;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
