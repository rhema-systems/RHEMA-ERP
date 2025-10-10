using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Work Order Management Repository Implementations

public class WorkOrderRepository : GenericRepository<WorkOrder>, IWorkOrderRepository
{
    public WorkOrderRepository(ApplicationDbContext context) : base(context) { }

    public async Task<WorkOrder?> GetByWorkOrderNumberAsync(string workOrderNumber)
    {
        if (string.IsNullOrWhiteSpace(workOrderNumber))
            throw new ArgumentException("Work order number cannot be null or empty", nameof(workOrderNumber));
            
        return await _dbSet
            .Where(wo => wo.WorkOrderNumber == workOrderNumber && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .Include(wo => wo.WorkOrderType)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsWorkOrderNumberUniqueAsync(string workOrderNumber, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(workOrderNumber))
            return false;
            
        var query = _dbSet.Where(wo => wo.WorkOrderNumber == workOrderNumber && !wo.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(wo => wo.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetByAssetIdAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new ArgumentException("Asset ID cannot be empty", nameof(assetId));
            
        return await _dbSet
            .Where(wo => wo.AssetId == assetId && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .Include(wo => wo.WorkOrderType)
            .OrderByDescending(wo => wo.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
            
        return await _dbSet
            .Where(wo => wo.Status == status && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetByPriorityLevelAsync(Guid priorityLevelId)
    {
        if (priorityLevelId == Guid.Empty)
            throw new ArgumentException("Priority Level ID cannot be empty", nameof(priorityLevelId));
            
        return await _dbSet
            .Where(wo => wo.PriorityLevelId == priorityLevelId && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetByAssignedTechnicianAsync(Guid technicianId)
    {
        if (technicianId == Guid.Empty)
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
            
        return await _dbSet
            .Where(wo => wo.AssignedTechnicianId == technicianId && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetByAssignedTeamAsync(Guid teamId)
    {
        if (teamId == Guid.Empty)
            throw new ArgumentException("Team ID cannot be empty", nameof(teamId));
            
        return await _dbSet
            .Where(wo => wo.AssignedTeamId == teamId && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetOverdueWorkOrdersAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(wo => !wo.IsDeleted &&
                        wo.Status != "Completed" &&
                        wo.Status != "Cancelled" &&
                        wo.RequestedStartDate < today)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetWorkOrdersDueInDaysAsync(int days)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(days);
        return await _dbSet
            .Where(wo => !wo.IsDeleted &&
                        wo.Status != "Completed" &&
                        wo.Status != "Cancelled" &&
                        wo.RequestedStartDate <= targetDate)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetByMaintenanceScheduleIdAsync(Guid scheduleId)
    {
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("Schedule ID cannot be empty", nameof(scheduleId));
            
        return await _dbSet
            .Where(wo => wo.MaintenanceScheduleId == scheduleId && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderByDescending(wo => wo.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetChildWorkOrdersAsync(Guid parentWorkOrderId)
    {
        if (parentWorkOrderId == Guid.Empty)
            throw new ArgumentException("Parent Work Order ID cannot be empty", nameof(parentWorkOrderId));
            
        return await _dbSet
            .Where(wo => wo.ParentWorkOrderId == parentWorkOrderId && !wo.IsDeleted)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkOrder>> GetWorkOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            throw new ArgumentException("Start date must be before end date");
            
        return await _dbSet
            .Where(wo => !wo.IsDeleted &&
                        wo.RequestedStartDate >= startDate &&
                        wo.RequestedStartDate <= endDate)
            .Include(wo => wo.Asset)
            .Include(wo => wo.PriorityLevel)
            .Include(wo => wo.MaintenanceType)
            .OrderBy(wo => wo.RequestedStartDate)
            .ToListAsync();
    }

    public async Task<string> GenerateWorkOrderNumberAsync()
    {
        var currentYear = DateTime.UtcNow.Year;
        var yearPrefix = currentYear.ToString().Substring(2); // Last 2 digits of year
        
        var lastWorkOrder = await _dbSet
            .Where(wo => wo.WorkOrderNumber.StartsWith($"WO{yearPrefix}") && !wo.IsDeleted)
            .OrderByDescending(wo => wo.WorkOrderNumber)
            .FirstOrDefaultAsync();
            
        int nextSequence = 1;
        if (lastWorkOrder != null)
        {
            var sequencePart = lastWorkOrder.WorkOrderNumber.Substring(4); // Remove "WO" + year prefix
            if (int.TryParse(sequencePart, out int lastSequence))
            {
                nextSequence = lastSequence + 1;
            }
        }
        
        return $"WO{yearPrefix}{nextSequence:D4}"; // Format as WO24NNNN
    }

    public async Task<decimal> GetTotalCostByAssetAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(wo => wo.AssetId == assetId && !wo.IsDeleted)
            .SumAsync(wo => wo.ActualCost);
    }

    public async Task<decimal> GetTotalCostByPeriodAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(wo => !wo.IsDeleted &&
                        wo.RequestedStartDate >= startDate &&
                        wo.RequestedStartDate <= endDate)
            .SumAsync(wo => wo.ActualCost);
    }

    public async Task<double> GetTotalLaborHoursByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        if (technicianId == Guid.Empty)
            return 0;
            
        return await _context.Set<WorkOrderLabor>()
            .Where(wol => wol.TechnicianId == technicianId && !wol.IsDeleted &&
                         wol.StartTime >= startDate && wol.StartTime <= endDate)
            .SumAsync(wol => wol.Hours);
    }
}

public class WorkOrderTypeRepository : GenericRepository<WorkOrderType>, IWorkOrderTypeRepository
{
    public WorkOrderTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<WorkOrderType?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
            
        return await _dbSet
            .Where(wot => wot.Code == code && !wot.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
            
        var query = _dbSet.Where(wot => wot.Code == code && !wot.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(wot => wot.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<WorkOrderType>> GetActiveAsync()
    {
        return await _dbSet
            .Where(wot => wot.IsActive && !wot.IsDeleted)
            .OrderBy(wot => wot.Name)
            .ToListAsync();
    }

    public async Task<int> GetWorkOrderCountByTypeAsync(Guid workOrderTypeId)
    {
        if (workOrderTypeId == Guid.Empty)
            return 0;
            
        return await _context.Set<WorkOrder>()
            .Where(wo => wo.WorkOrderTypeId == workOrderTypeId && !wo.IsDeleted)
            .CountAsync();
    }
}

public class MaintenanceTypeRepository : GenericRepository<MaintenanceType>, IMaintenanceTypeRepository
{
    public MaintenanceTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MaintenanceType?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
            
        return await _dbSet
            .Where(mt => mt.Code == code && !mt.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
            
        var query = _dbSet.Where(mt => mt.Code == code && !mt.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(mt => mt.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<MaintenanceType>> GetActiveAsync()
    {
        return await _dbSet
            .Where(mt => mt.IsActive && !mt.IsDeleted)
            .OrderBy(mt => mt.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceType>> GetByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be null or empty", nameof(category));
            
        return await _dbSet
            .Where(mt => mt.Category == category && !mt.IsDeleted)
            .OrderBy(mt => mt.Name)
            .ToListAsync();
    }

    public async Task<int> GetWorkOrderCountByMaintenanceTypeAsync(Guid maintenanceTypeId)
    {
        if (maintenanceTypeId == Guid.Empty)
            return 0;
            
        return await _context.Set<WorkOrder>()
            .Where(wo => wo.MaintenanceTypeId == maintenanceTypeId && !wo.IsDeleted)
            .CountAsync();
    }
}

public class PriorityLevelRepository : GenericRepository<PriorityLevel>, IPriorityLevelRepository
{
    public PriorityLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PriorityLevel>> GetActiveAsync()
    {
        return await _dbSet
            .Where(pl => pl.IsActive && !pl.IsDeleted)
            .OrderBy(pl => pl.Level)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriorityLevel>> GetOrderedByLevelAsync()
    {
        return await _dbSet
            .Where(pl => !pl.IsDeleted)
            .OrderBy(pl => pl.Level)
            .ToListAsync();
    }

    public async Task<PriorityLevel?> GetByLevelAsync(int level)
    {
        return await _dbSet
            .Where(pl => pl.Level == level && !pl.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsLevelUniqueAsync(int level, Guid? excludeId = null)
    {
        var query = _dbSet.Where(pl => pl.Level == level && !pl.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(pl => pl.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<int> GetWorkOrderCountByPriorityAsync(Guid priorityLevelId)
    {
        if (priorityLevelId == Guid.Empty)
            return 0;
            
        return await _context.Set<WorkOrder>()
            .Where(wo => wo.PriorityLevelId == priorityLevelId && !wo.IsDeleted)
            .CountAsync();
    }
}

#endregion