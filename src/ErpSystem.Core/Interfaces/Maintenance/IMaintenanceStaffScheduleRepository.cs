using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceStaffScheduleRepository : IGenericRepository<MaintenanceStaffSchedule>
{
    Task<IEnumerable<MaintenanceStaffSchedule>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<MaintenanceStaffSchedule>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<MaintenanceStaffSchedule>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<MaintenanceStaffSchedule>> GetByTechnicianAndDateRangeAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<MaintenanceStaffSchedule?> GetByIdWithDetailsAsync(Guid id);
}
