using PainFinder.Shared.DTOs;

namespace PainFinder.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
