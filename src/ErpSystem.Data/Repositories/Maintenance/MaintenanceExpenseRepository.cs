using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class MaintenanceExpenseRepository : GenericRepository<MaintenanceExpense>, IMaintenanceExpenseRepository
{
    public MaintenanceExpenseRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<MaintenanceExpense>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        return await _context.MaintenanceExpenses
            .Include(e => e.WorkOrder)
            .Include(e => e.Technician)
            .Include(e => e.Schedule)
            .Include(e => e.ApprovedBy)
            .Include(e => e.Vehicle)
            .Where(e => e.WorkOrderId == workOrderId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceExpense>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.MaintenanceExpenses
            .Include(e => e.WorkOrder)
            .Include(e => e.Technician)
            .Include(e => e.ApprovedBy)
            .Where(e => e.TechnicianId == technicianId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceExpense>> GetByScheduleIdAsync(Guid scheduleId)
    {
        return await _context.MaintenanceExpenses
            .Include(e => e.WorkOrder)
            .Include(e => e.Technician)
            .Include(e => e.Schedule)
            .Where(e => e.ScheduleId == scheduleId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceExpense>> GetByStatusAsync(string status)
    {
        return await _context.MaintenanceExpenses
            .Include(e => e.WorkOrder)
            .Include(e => e.Technician)
            .Include(e => e.ApprovedBy)
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceExpense>> GetPendingExpensesAsync()
    {
        return await GetByStatusAsync("Pending");
    }

    public async Task<MaintenanceExpense?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.MaintenanceExpenses
            .Include(e => e.WorkOrder)
            .Include(e => e.Technician)
            .Include(e => e.Schedule)
            .Include(e => e.ApprovedBy)
            .Include(e => e.Vehicle)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<decimal> GetTotalExpensesByWorkOrderIdAsync(Guid workOrderId)
    {
        return await _context.MaintenanceExpenses
            .Where(e => e.WorkOrderId == workOrderId && e.Status == "Approved")
            .SumAsync(e => e.Amount);
    }
}
