using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceStaffScheduleService
{
    Task<MaintenanceStaffScheduleDto> CreateScheduleAsync(CreateMaintenanceStaffScheduleDto createDto);
    Task<MaintenanceStaffScheduleDto> UpdateScheduleAsync(Guid id, UpdateMaintenanceStaffScheduleDto updateDto);
    Task DeleteScheduleAsync(Guid id);
    Task<MaintenanceStaffScheduleDto?> GetScheduleByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceStaffScheduleDto>> GetSchedulesByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<MaintenanceStaffScheduleDto>> GetSchedulesByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<MaintenanceStaffScheduleDto>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<MaintenanceStaffScheduleDto> StartScheduleAsync(Guid id);
    Task<MaintenanceStaffScheduleDto> CompleteScheduleAsync(Guid id, int? actualTravelMinutes = null);
}
