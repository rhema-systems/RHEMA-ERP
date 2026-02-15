using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceSettingsService
{
    Task<MaintenanceSettingsDto> GetSettingsAsync();
    Task<MaintenanceSettingsDto> UpdateSettingsAsync(UpdateMaintenanceSettingsDto dto);
}

