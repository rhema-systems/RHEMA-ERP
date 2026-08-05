using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>Aggregates the SHE KPI counts for the dashboard landing page.</summary>
public interface ISheDashboardService
{
    Task<SheDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
