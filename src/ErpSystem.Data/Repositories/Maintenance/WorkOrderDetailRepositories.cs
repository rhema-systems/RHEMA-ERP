using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Work Order Detail Repository Implementations

public class WorkOrderTaskRepository : GenericRepository<WorkOrderTask>, IWorkOrderTaskRepository
{
    public WorkOrderTaskRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WorkOrderTask>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(wot => wot.WorkOrderId == workOrderId && !wot.IsDeleted)
            .Include(wot => wot.WorkOrder)
            .OrderBy(wot => wot.Sequence)
            .ThenBy(wot => wot.TaskName)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderTask>> GetByAssignedTechnicianAsync(Guid technicianId)
    {
        if (technicianId == Guid.Empty)
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
            
        return await _dbSet
            .Where(wot => wot.AssignedTechnicianId == technicianId && !wot.IsDeleted)
            .Include(wot => wot.WorkOrder)
            .OrderBy(wot => wot.StartedAt ?? DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderTask>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
            
        return await _dbSet
            .Where(wot => wot.Status == status && !wot.IsDeleted)
            .Include(wot => wot.WorkOrder)
            .OrderBy(wot => wot.StartedAt ?? DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderTask>> GetOverdueTasksAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(wot => !wot.IsDeleted &&
                         wot.Status != "Completed" &&
                         wot.Status != "Cancelled" &&
                         wot.CompletedAt < today)
            .Include(wot => wot.WorkOrder)
            .OrderBy(wot => wot.CompletedAt ?? DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task<double> GetTotalHoursByWorkOrderAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wot => wot.WorkOrderId == workOrderId && !wot.IsDeleted)
            .SumAsync(wot => wot.EstimatedHours);
    }

    public async Task<double> GetCompletionPercentageAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            return 0;
            
        var tasks = await _dbSet
            .Where(wot => wot.WorkOrderId == workOrderId && !wot.IsDeleted)
            .ToListAsync();
            
        if (!tasks.Any())
            return 0;
            
        var totalTasks = tasks.Count;
        var completedTasks = tasks.Count(t => t.Status == "Completed");
        
        return (double)completedTasks / totalTasks * 100;
    }
}

public class WorkOrderPartRepository : GenericRepository<WorkOrderPart>, IWorkOrderPartRepository
{
    public WorkOrderPartRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WorkOrderPart>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(wop => wop.WorkOrderId == workOrderId && !wop.IsDeleted)
            .Include(wop => wop.WorkOrder)
            .OrderBy(wop => wop.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderPart>> GetByPartNumberAsync(string partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
            throw new ArgumentException("Part number cannot be null or empty", nameof(partNumber));
            
        return await _dbSet
            .Where(wop => wop.ItemCode == partNumber && !wop.IsDeleted)
            .Include(wop => wop.WorkOrder)
            .OrderByDescending(wop => wop.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderPart>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
            
        return await _dbSet
            .Where(wop => wop.Status == status && !wop.IsDeleted)
            .Include(wop => wop.WorkOrder)
            .OrderBy(wop => wop.ItemCode)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalCostByWorkOrderAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wop => wop.WorkOrderId == workOrderId && !wop.IsDeleted)
            .SumAsync(wop => wop.QuantityUsed * wop.UnitCost);
    }

    public async Task<IEnumerable<WorkOrderPart>> GetPartsRequiringOrderAsync()
    {
        return await _dbSet
            .Where(wop => !wop.IsDeleted &&
                         (wop.Status == "Required" || wop.Status == "Ordered") &&
                         wop.QuantityRequired > 0)
            .Include(wop => wop.WorkOrder)
            .OrderBy(wop => wop.CreatedAt)
            .ToListAsync();
    }
}

public class WorkOrderLaborRepository : GenericRepository<WorkOrderLabor>, IWorkOrderLaborRepository
{
    public WorkOrderLaborRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WorkOrderLabor>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(wol => wol.WorkOrderId == workOrderId && !wol.IsDeleted)
            .Include(wol => wol.WorkOrder)
            .OrderBy(wol => wol.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderLabor>> GetByTechnicianIdAsync(Guid technicianId)
    {
        if (technicianId == Guid.Empty)
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
            
        return await _dbSet
            .Where(wol => wol.TechnicianId == technicianId && !wol.IsDeleted)
            .Include(wol => wol.WorkOrder)
            .OrderByDescending(wol => wol.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderLabor>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            throw new ArgumentException("Start date must be before end date");
            
        return await _dbSet
            .Where(wol => !wol.IsDeleted &&
                         wol.StartTime >= startDate &&
                         wol.StartTime <= endDate)
            .Include(wol => wol.WorkOrder)
            .OrderBy(wol => wol.StartTime)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalCostByWorkOrderAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wol => wol.WorkOrderId == workOrderId && !wol.IsDeleted)
            .SumAsync(wol => (decimal)wol.Hours * wol.HourlyRate);
    }

    public async Task<double> GetTotalHoursByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        if (technicianId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wol => wol.TechnicianId == technicianId && !wol.IsDeleted &&
                         wol.StartTime >= startDate && wol.StartTime <= endDate)
            .SumAsync(wol => wol.Hours);
    }

    public async Task<decimal> GetTotalLaborCostByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        if (technicianId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wol => wol.TechnicianId == technicianId && !wol.IsDeleted &&
                         wol.StartTime >= startDate && wol.StartTime <= endDate)
            .SumAsync(wol => (decimal)wol.Hours * wol.HourlyRate);
    }
}

public class WorkOrderDocumentRepository : GenericRepository<WorkOrderDocument>, IWorkOrderDocumentRepository
{
    public WorkOrderDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WorkOrderDocument>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(wod => wod.WorkOrderId == workOrderId && !wod.IsDeleted)
            .Include(wod => wod.WorkOrder)
            .OrderByDescending(wod => wod.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderDocument>> GetByDocumentTypeAsync(string documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("Document type cannot be null or empty", nameof(documentType));
            
        return await _dbSet
            .Where(wod => wod.DocumentType == documentType && !wod.IsDeleted)
            .Include(wod => wod.WorkOrder)
            .OrderByDescending(wod => wod.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderDocument>> GetByUploadedByAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
            
        return await _dbSet
            .Where(wod => wod.UploadedById == userId && !wod.IsDeleted)
            .Include(wod => wod.WorkOrder)
            .OrderByDescending(wod => wod.CreatedAt)
            .ToListAsync();
    }

    public async Task<long> GetTotalFileSizeByWorkOrderAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wod => wod.WorkOrderId == workOrderId && !wod.IsDeleted)
            .SumAsync(wod => wod.FileSize);
    }
}

public class WorkOrderCommentRepository : GenericRepository<WorkOrderComment>, IWorkOrderCommentRepository
{
    public WorkOrderCommentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WorkOrderComment>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(woc => woc.WorkOrderId == workOrderId && !woc.IsDeleted)
            .Include(woc => woc.WorkOrder)
            .OrderByDescending(woc => woc.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderComment>> GetByCommentTypeAsync(string commentType)
    {
        if (string.IsNullOrWhiteSpace(commentType))
            throw new ArgumentException("Comment type cannot be null or empty", nameof(commentType));
            
        return await _dbSet
            .Where(woc => woc.CommentType == commentType && !woc.IsDeleted)
            .Include(woc => woc.WorkOrder)
            .OrderByDescending(woc => woc.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderComment>> GetInternalCommentsAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(woc => woc.WorkOrderId == workOrderId && !woc.IsDeleted && woc.IsInternal)
            .Include(woc => woc.WorkOrder)
            .OrderByDescending(woc => woc.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrderComment>> GetExternalCommentsAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
            
        return await _dbSet
            .Where(woc => woc.WorkOrderId == workOrderId && !woc.IsDeleted && !woc.IsInternal)
            .Include(woc => woc.WorkOrder)
            .OrderByDescending(woc => woc.CreatedAt)
            .ToListAsync();
    }
}

#endregion