using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IWorkOrderToolRepository : IGenericRepository<WorkOrderTool>
{
    Task<IEnumerable<WorkOrderTool>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<WorkOrderTool?> GetByWorkOrderAndToolIdAsync(Guid workOrderId, Guid toolId);
    Task<IEnumerable<WorkOrderTool>> GetCheckedOutToolsAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderTool>> GetOverdueToolsAsync(Guid workOrderId);
    Task<bool> IsToolAllocatedToWorkOrderAsync(Guid toolId, Guid workOrderId);
}
