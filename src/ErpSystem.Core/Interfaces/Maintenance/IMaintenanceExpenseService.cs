using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceExpenseService
{
    Task<MaintenanceExpenseDto> CreateExpenseAsync(CreateMaintenanceExpenseDto createDto);
    Task<MaintenanceExpenseDto> UpdateExpenseAsync(Guid id, UpdateMaintenanceExpenseDto updateDto);
    Task DeleteExpenseAsync(Guid id);
    Task<MaintenanceExpenseDto?> GetExpenseByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceExpenseDto>> GetExpensesByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<MaintenanceExpenseDto>> GetExpensesByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<MaintenanceExpenseDto>> GetPendingExpensesAsync();
    Task<MaintenanceExpenseDto> ApproveExpenseAsync(Guid id, ApproveExpenseDto approveDto);
    Task<decimal> GetTotalExpensesByWorkOrderIdAsync(Guid workOrderId);
}
