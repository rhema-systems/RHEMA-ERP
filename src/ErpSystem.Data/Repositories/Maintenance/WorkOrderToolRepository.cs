using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class WorkOrderToolRepository : GenericRepository<WorkOrderTool>, IWorkOrderToolRepository
{
    public WorkOrderToolRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkOrderTool>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        return await _context.WorkOrderTools
            .Include(wt => wt.Tool)
            .Include(wt => wt.Checkout)
                .ThenInclude(c => c!.CheckedOutBy)
            .Where(wt => wt.WorkOrderId == workOrderId && !wt.IsDeleted)
            .OrderBy(wt => wt.Tool.Name)
            .ToListAsync();
    }

    public async Task<WorkOrderTool?> GetByWorkOrderAndToolIdAsync(Guid workOrderId, Guid toolId)
    {
        return await _context.WorkOrderTools
            .Include(wt => wt.Tool)
            .Include(wt => wt.Checkout)
            .FirstOrDefaultAsync(wt => wt.WorkOrderId == workOrderId && wt.ToolId == toolId && !wt.IsDeleted);
    }

    public async Task<IEnumerable<WorkOrderTool>> GetCheckedOutToolsAsync(Guid workOrderId)
    {
        return await _context.WorkOrderTools
            .Include(wt => wt.Tool)
            .Include(wt => wt.Checkout)
                .ThenInclude(c => c!.CheckedOutBy)
            .Where(wt => wt.WorkOrderId == workOrderId 
                && wt.CheckoutId.HasValue 
                && wt.Checkout!.ActualReturnDate == null
                && !wt.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderTool>> GetOverdueToolsAsync(Guid workOrderId)
    {
        var now = DateTime.UtcNow;
        return await _context.WorkOrderTools
            .Include(wt => wt.Tool)
            .Include(wt => wt.Checkout)
                .ThenInclude(c => c!.CheckedOutBy)
            .Where(wt => wt.WorkOrderId == workOrderId 
                && wt.CheckoutId.HasValue 
                && wt.Checkout!.ActualReturnDate == null
                && wt.Checkout!.ExpectedReturnDate.HasValue
                && wt.Checkout!.ExpectedReturnDate.Value < now
                && !wt.IsDeleted)
            .ToListAsync();
    }

    public async Task<bool> IsToolAllocatedToWorkOrderAsync(Guid toolId, Guid workOrderId)
    {
        return await _context.WorkOrderTools
            .AnyAsync(wt => wt.ToolId == toolId && wt.WorkOrderId == workOrderId && !wt.IsDeleted);
    }
}
