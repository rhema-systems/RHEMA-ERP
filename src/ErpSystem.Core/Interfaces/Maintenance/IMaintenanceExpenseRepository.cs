using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceExpenseRepository : IGenericRepository<MaintenanceExpense>
{
    Task<IEnumerable<MaintenanceExpense>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<MaintenanceExpense>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<MaintenanceExpense>> GetByScheduleIdAsync(Guid scheduleId);
    Task<IEnumerable<MaintenanceExpense>> GetByStatusAsync(string status);
    Task<IEnumerable<MaintenanceExpense>> GetPendingExpensesAsync();
    Task<MaintenanceExpense?> GetByIdWithDetailsAsync(Guid id);
    Task<decimal> GetTotalExpensesByWorkOrderIdAsync(Guid workOrderId);
}
