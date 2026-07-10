using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceOperationalReportsService
{
    Task<MaintenanceOperationalReportsDto> GetOperationalReportsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? assetId = null);
}
