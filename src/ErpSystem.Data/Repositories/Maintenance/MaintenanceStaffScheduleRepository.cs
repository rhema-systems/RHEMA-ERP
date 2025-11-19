using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class MaintenanceStaffScheduleRepository : GenericRepository<MaintenanceStaffSchedule>, IMaintenanceStaffScheduleRepository
{
    public MaintenanceStaffScheduleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<MaintenanceStaffSchedule>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.MaintenanceStaffSchedules
            .Include(s => s.WorkOrder)
            .Include(s => s.JobCard)
            .Include(s => s.Team)
            .Include(s => s.AssignedVehicle)
            .Where(s => s.TechnicianId == technicianId)
            .OrderByDescending(s => s.StartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceStaffSchedule>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        return await _context.MaintenanceStaffSchedules
            .Include(s => s.WorkOrder)
            .Include(s => s.AssignedVehicle)
            .Where(s => s.WorkOrderId == workOrderId)
            .OrderBy(s => s.StartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceStaffSchedule>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.MaintenanceStaffSchedules
            .Include(s => s.WorkOrder)
            .Include(s => s.AssignedVehicle)
            .Where(s => s.StartDateTime >= startDate && s.StartDateTime <= endDate)
            .OrderBy(s => s.StartDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceStaffSchedule>> GetByTechnicianAndDateRangeAsync(
        Guid technicianId, DateTime startDate, DateTime endDate)
    {
        return await _context.MaintenanceStaffSchedules
            .Include(s => s.WorkOrder)
            .Include(s => s.AssignedVehicle)
            .Where(s => s.TechnicianId == technicianId 
                && s.StartDateTime >= startDate 
                && s.StartDateTime <= endDate)
            .OrderBy(s => s.StartDateTime)
            .ToListAsync();
    }

    public async Task<MaintenanceStaffSchedule?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.MaintenanceStaffSchedules
            .Include(s => s.WorkOrder)
            .Include(s => s.JobCard)
            .Include(s => s.Team)
            .Include(s => s.AssignedVehicle)
            .Include(s => s.Expenses)
            .FirstOrDefaultAsync(s => s.Id == id);
    }
}
