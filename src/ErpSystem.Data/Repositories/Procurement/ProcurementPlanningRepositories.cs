using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using CommonPagedResult = ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Data.Repositories.Procurement;

#region Procurement Plan Repository

public class ProcurementPlanRepository : GenericRepository<ProcurementPlan>, IProcurementPlanRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProcurementPlanRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    private IQueryable<ProcurementPlan> GetTenantFilteredQuery()
    {
        var tenantId = _currentUserProvider.TenantId;
        return _dbSet.Where(p => !p.IsDeleted && p.TenantId == tenantId);
    }

    public async Task<ProcurementPlan?> GetByPlanNumberAsync(string planNumber)
    {
        return await GetTenantFilteredQuery()
            .Where(p => p.PlanNumber == planNumber)
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ProcurementPlan>> GetByDepartmentAsync(Guid departmentId)
    {
        return await GetTenantFilteredQuery()
            .Where(p => p.OrganizationUnitId == departmentId)
            .OrderByDescending(p => p.FiscalYear)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlan>> GetByFiscalYearAsync(int fiscalYear)
    {
        return await GetTenantFilteredQuery()
            .Where(p => p.FiscalYear == fiscalYear)
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .OrderBy(p => p.OrganizationUnit != null ? p.OrganizationUnit.Name : p.Department!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlan>> GetByStatusAsync(string status)
    {
        return await GetTenantFilteredQuery()
            .Where(p => p.Status == status)
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlan>> GetActivePlansAsync()
    {
        var today = DateTime.UtcNow;
        return await GetTenantFilteredQuery()
            .Where(p => (p.Status == "Active" || p.Status == "Approved") && p.PlanStartDate <= today && p.PlanEndDate >= today)
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .OrderBy(p => p.OrganizationUnit != null ? p.OrganizationUnit.Name : p.Department!.Name)
            .ToListAsync();
    }

    public async Task<decimal> GetPlannedBudgetExposureAsync(Guid budgetId, Guid? excludePlanId = null)
    {
        return await GetTenantFilteredQuery()
            .Where(plan =>
                plan.BudgetId == budgetId &&
                (!excludePlanId.HasValue || plan.Id != excludePlanId.Value) &&
                plan.Status != "Rejected" &&
                plan.Status != "Cancelled" &&
                plan.Status != "Completed")
            .SumAsync(plan => plan.TotalEstimatedBudget);
    }

    public async Task<ProcurementPlan?> GetWithItemsAsync(Guid id)
    {
        return await GetTenantFilteredQuery()
            .Where(p => p.Id == id)
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<ProcurementPlan?> GetWithFullDetailsAsync(Guid id)
    {
        return await GetTenantFilteredQuery()
            .Where(p => p.Id == id)
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.InventoryItem)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.MarketAnalysis)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ItemSuppliers.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.BusinessPartner)
            .Include(p => p.Budget)
            .Include(p => p.Budgets.Where(b => !b.IsDeleted))
            .Include(p => p.Schedules.Where(s => !s.IsDeleted))
            .Include(p => p.PreparedBy)
            .Include(p => p.ReviewedBy)
            .Include(p => p.ApprovedBy)
            .Include(p => p.PublishedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<CommonPagedResult.PagedResult<ProcurementPlan>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null)
    {
        var query = GetTenantFilteredQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.PlanNumber.Contains(search) || p.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(p => p.OrganizationUnitId == departmentId.Value);
        }

        if (fiscalYear.HasValue)
        {
            query = query.Where(p => p.FiscalYear == fiscalYear.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(p => p.Department)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .Include(p => p.PreparedBy)
            .OrderByDescending(p => p.FiscalYear)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonPagedResult.PagedResult<ProcurementPlan>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GeneratePlanNumberAsync(int fiscalYear)
    {
        var prefix = $"PP-{fiscalYear}-";
        var existingNumbers = await _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(plan =>
                plan.FiscalYear == fiscalYear &&
                plan.PlanNumber.StartsWith(prefix))
            .Select(plan => plan.PlanNumber)
            .ToListAsync();

        // PlanNumber is globally unique in the current schema. Count + 1 is
        // unsafe when a record was soft deleted or a sequence has a gap, and
        // tenant-local counting can collide with another tenant. Never reuse a
        // number: retain deleted rows in the calculation and advance from the
        // highest valid numeric suffix across the table.
        var highestSequence = existingNumbers
            .Select(number => number[prefix.Length..])
            .Select(suffix => int.TryParse(suffix, out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{highestSequence + 1:D4}";
    }

    public async Task<bool> PlanNumberExistsAsync(string planNumber)
    {
        return await GetTenantFilteredQuery().AnyAsync(p => p.PlanNumber == planNumber);
    }
}

#endregion

#region Procurement Plan Item Repository

public class ProcurementPlanItemRepository : GenericRepository<ProcurementPlanItem>, IProcurementPlanItemRepository
{
    public ProcurementPlanItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProcurementPlanItem>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .Include(i => i.MarketAnalysis)
            .Include(i => i.ItemSuppliers.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.BusinessPartner)
            .OrderBy(i => i.Priority)
            .ThenBy(i => i.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlanItem>> GetByPlanIdAndStatusAsync(Guid planId, string status)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && i.Status == status && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .Include(i => i.MarketAnalysis)
            .Include(i => i.ItemSuppliers.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.BusinessPartner)
            .OrderBy(i => i.RequiredDate)
            .ToListAsync();
    }

    public async Task<decimal> GetPlannedBudgetExposureByAllocationAsync(Guid allocationId, Guid? excludeItemId = null)
    {
        return await _dbSet
            .Where(item =>
                item.ProcurementBudgetAllocationId == allocationId &&
                (!excludeItemId.HasValue || item.Id != excludeItemId.Value) &&
                !item.IsDeleted &&
                !item.ProcurementPlan.IsDeleted &&
                item.ProcurementPlan.Status != "Rejected" &&
                item.ProcurementPlan.Status != "Cancelled" &&
                item.ProcurementPlan.Status != "Completed")
            .SumAsync(item => item.ApprovedBudgetAmount ?? item.EstimatedTotalCost);
    }

    public async Task<IEnumerable<ProcurementPlanItem>> GetCriticalItemsAsync(Guid planId)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && i.IsCritical && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .Include(i => i.MarketAnalysis)
            .Include(i => i.ItemSuppliers.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.BusinessPartner)
            .OrderBy(i => i.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlanItem>> GetByQuarterAsync(Guid planId, string quarter)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && i.PlannedQuarter == quarter && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .OrderBy(i => i.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlanItem>> GetByMonthAsync(Guid planId, int month)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && i.PlannedProcurementMonth == month && !i.IsDeleted)
            .OrderBy(i => i.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementPlanItem>> GetByPriorityAsync(Guid planId, string priority)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && i.Priority == priority && !i.IsDeleted)
            .OrderBy(i => i.RequiredDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalEstimatedCostAsync(Guid planId)
    {
        return await _dbSet
            .Where(i => i.ProcurementPlanId == planId && !i.IsDeleted)
            .SumAsync(i => i.EstimatedTotalCost);
    }
}

#endregion

#region Procurement Plan Item Supplier Repository

public class ProcurementPlanItemSupplierRepository : GenericRepository<ProcurementPlanItemSupplier>, IProcurementPlanItemSupplierRepository
{
    public ProcurementPlanItemSupplierRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProcurementPlanItemSupplier>> GetByItemIdAsync(Guid itemId)
    {
        return await _dbSet
            .Where(s => s.ProcurementPlanItemId == itemId && !s.IsDeleted)
            .Include(s => s.BusinessPartner)
            .OrderBy(s => s.Priority)
            .ToListAsync();
    }

    public async Task<ProcurementPlanItemSupplier?> GetByItemAndSupplierAsync(Guid itemId, Guid supplierId)
    {
        return await _dbSet
            .Where(s => s.ProcurementPlanItemId == itemId && s.SupplierId == supplierId && !s.IsDeleted)
            .Include(s => s.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteByItemIdAsync(Guid itemId)
    {
        // Hard delete to avoid unique constraint violations when re-adding the same supplier
        var suppliers = await _dbSet
            .Where(s => s.ProcurementPlanItemId == itemId)
            .ToListAsync();

        if (suppliers.Any())
        {
            await HardDeleteRangeAsync(suppliers);
        }
    }
}

#endregion

#region Procurement Budget Repository

public class ProcurementBudgetRepository : GenericRepository<ProcurementBudget>, IProcurementBudgetRepository
{
    public ProcurementBudgetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ProcurementBudget?> GetByBudgetCodeAsync(string budgetCode)
    {
        return await _dbSet
            .Where(b => b.BudgetCode == budgetCode && !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ProcurementBudget>> GetByDepartmentAsync(Guid departmentId)
    {
        return await _dbSet
            .Where(b => b.OrganizationUnitId == departmentId && !b.IsDeleted)
            .OrderByDescending(b => b.FiscalYear)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementBudget>> GetByFiscalYearAsync(int fiscalYear)
    {
        return await _dbSet
            .Where(b => b.FiscalYear == fiscalYear && !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .OrderBy(b => b.OrganizationUnit != null ? b.OrganizationUnit.Name : b.Department!.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementBudget>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(b => b.Status == status && !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementBudget>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Where(b => b.ProcurementPlanId == planId && !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementBudget>> GetActiveBudgetsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Where(b => b.Status == "Approved" &&
                   (!b.EffectiveDate.HasValue || b.EffectiveDate <= today) &&
                   (!b.ExpiryDate.HasValue || b.ExpiryDate >= today) &&
                   !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .OrderBy(b => b.OrganizationUnit != null ? b.OrganizationUnit.Name : b.Department!.Name)
            .ToListAsync();
    }

    public async Task<ProcurementBudget?> GetWithAllocationsAsync(Guid id)
    {
        return await _dbSet
            .Where(b => b.Id == id && !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.Allocations.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<ProcurementBudget?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Where(b => b.Id == id && !b.IsDeleted)
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.ProcurementPlan)
            .Include(b => b.Allocations.Where(a => !a.IsDeleted))
            .Include(b => b.Revisions.Where(r => !r.IsDeleted))
            .Include(b => b.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<CommonPagedResult.PagedResult<ProcurementBudget>> GetBudgetsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null)
    {
        var query = _dbSet.Where(b => !b.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(b => b.BudgetCode.Contains(search) || b.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(b => b.OrganizationUnitId == departmentId.Value);
        }

        if (fiscalYear.HasValue)
        {
            query = query.Where(b => b.FiscalYear == fiscalYear.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(b => b.Department)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.Allocations.Where(a => !a.IsDeleted))
            .OrderByDescending(b => b.FiscalYear)
            .ThenByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonPagedResult.PagedResult<ProcurementBudget>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GenerateBudgetCodeAsync(int fiscalYear, Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to generate a procurement budget code.");

        var prefix = $"PB-{fiscalYear}-";
        var existingCodes = await _dbSet
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.BudgetCode.StartsWith(prefix))
            .Select(b => b.BudgetCode)
            .ToListAsync();

        var highestSequence = existingCodes
            .Select(code => code[prefix.Length..])
            .Select(value => int.TryParse(value, out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highestSequence + 1):D4}";
    }

    public async Task<bool> BudgetCodeExistsAsync(string budgetCode)
    {
        return await _dbSet.AnyAsync(b => b.BudgetCode == budgetCode && !b.IsDeleted);
    }

    public async Task<decimal> GetTotalAllocatedBudgetAsync(Guid departmentId, int fiscalYear)
    {
        return await _dbSet
            .Where(b => b.OrganizationUnitId == departmentId && b.FiscalYear == fiscalYear && b.Status == "Approved" && !b.IsDeleted)
            .SumAsync(b => b.AllocatedAmount);
    }

    public async Task<decimal> GetTotalUtilizedBudgetAsync(Guid departmentId, int fiscalYear)
    {
        return await _dbSet
            .Where(b => b.OrganizationUnitId == departmentId && b.FiscalYear == fiscalYear && b.Status == "Approved" && !b.IsDeleted)
            .SumAsync(b => b.UtilizedAmount);
    }
}

#endregion

#region Procurement Budget Allocation Repository

public class ProcurementBudgetAllocationRepository : GenericRepository<ProcurementBudgetAllocation>, IProcurementBudgetAllocationRepository
{
    public ProcurementBudgetAllocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProcurementBudgetAllocation>> GetByBudgetIdAsync(Guid budgetId)
    {
        return await _dbSet
            .Where(a => a.ProcurementBudgetId == budgetId && !a.IsDeleted)
            .OrderBy(a => a.CategoryName)
            .ToListAsync();
    }

    public async Task<ProcurementBudgetAllocation?> GetByCategoryAsync(Guid budgetId, string categoryName)
    {
        return await _dbSet
            .Where(a => a.ProcurementBudgetId == budgetId && a.CategoryName == categoryName && !a.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<decimal> GetTotalAllocatedAsync(Guid budgetId)
    {
        return await _dbSet
            .Where(a => a.ProcurementBudgetId == budgetId && !a.IsDeleted)
            .SumAsync(a => a.AllocatedAmount);
    }
}

#endregion

#region Procurement Budget Revision Repository

public class ProcurementBudgetRevisionRepository : GenericRepository<ProcurementBudgetRevision>, IProcurementBudgetRevisionRepository
{
    public ProcurementBudgetRevisionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProcurementBudgetRevision>> GetByBudgetIdAsync(Guid budgetId)
    {
        return await _dbSet
            .Where(r => r.ProcurementBudgetId == budgetId && !r.IsDeleted)
            .Include(r => r.ApprovedBy)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementBudgetRevision>> GetPendingRevisionsAsync()
    {
        return await _dbSet
            .Where(r => r.Status == "Pending" && !r.IsDeleted)
            .Include(r => r.ProcurementBudget)
                .ThenInclude(b => b!.Department)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetNextRevisionNumberAsync(Guid budgetId)
    {
        var maxRevision = await _dbSet
            .Where(r => r.ProcurementBudgetId == budgetId && !r.IsDeleted)
            .MaxAsync(r => (int?)r.RevisionNumber) ?? 0;
        return maxRevision + 1;
    }
}

#endregion

#region Procurement Schedule Repository

public class ProcurementScheduleRepository : GenericRepository<ProcurementSchedule>, IProcurementScheduleRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProcurementScheduleRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    private IQueryable<ProcurementSchedule> GetTenantFilteredQuery()
    {
        var tenantId = _currentUserProvider.TenantId;
        return _dbSet.Where(s => !s.IsDeleted && s.TenantId == tenantId);
    }

    public async Task<ProcurementSchedule?> GetByScheduleCodeAsync(string scheduleCode)
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.ScheduleCode == scheduleCode)
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetByPlanIdAsync(Guid planId)
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.ProcurementPlanId == planId)
            .OrderBy(s => s.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetByDepartmentAsync(Guid departmentId)
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.OrganizationUnitId == departmentId)
            .OrderBy(s => s.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetByStatusAsync(string status)
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.Status == status)
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .OrderBy(s => s.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.PlannedStartDate >= startDate && s.PlannedEndDate <= endDate)
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .OrderBy(s => s.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetUpcomingSchedulesAsync(int daysAhead = 30)
    {
        var today = DateTime.UtcNow;
        var futureDate = today.AddDays(daysAhead);
        return await GetTenantFilteredQuery()
            .Where(s => s.PlannedStartDate >= today && s.PlannedStartDate <= futureDate && s.Status == "Planned")
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .OrderBy(s => s.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<ProcurementSchedule?> GetWithFullDetailsAsync(Guid id)
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.Id == id)
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .Include(s => s.ProcurementPlan)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetOverdueSchedulesAsync()
    {
        var today = DateTime.UtcNow;
        return await GetTenantFilteredQuery()
            .Where(s => s.PlannedEndDate < today && s.Status != "Completed" && s.Status != "Cancelled")
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .OrderBy(s => s.PlannedEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProcurementSchedule>> GetConsolidationOpportunitiesAsync()
    {
        return await GetTenantFilteredQuery()
            .Where(s => s.ConsolidationOpportunity && s.Status == "Planned")
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .OrderBy(s => s.PlannedStartDate)
            .ToListAsync();
    }

    public async Task<CommonPagedResult.PagedResult<ProcurementSchedule>> GetSchedulesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        Guid? planId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = GetTenantFilteredQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.ScheduleCode.Contains(search) || s.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(s => s.OrganizationUnitId == departmentId.Value);
        }

        if (planId.HasValue)
        {
            query = query.Where(s => s.ProcurementPlanId == planId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.PlannedEndDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.PlannedStartDate <= endDate.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(s => s.Department)
            .Include(s => s.OrganizationUnit)
            .Include(s => s.ProcurementPlan)
            .OrderBy(s => s.PlannedStartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonPagedResult.PagedResult<ProcurementSchedule>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GenerateScheduleCodeAsync()
    {
        var tenantId = _currentUserProvider.TenantId;
        var count = await _dbSet.Where(s => s.TenantId == tenantId).CountAsync();
        return $"PS-{DateTime.UtcNow:yyyyMM}-{(count + 1):D4}";
    }
}

#endregion

#region Market Analysis Repository

public class MarketAnalysisRepository : GenericRepository<MarketAnalysis>, IMarketAnalysisRepository
{
    public MarketAnalysisRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MarketAnalysis?> GetByAnalysisCodeAsync(string analysisCode)
    {
        return await _dbSet
            .Where(m => m.AnalysisCode == analysisCode && !m.IsDeleted)
            .Include(m => m.PreparedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<MarketAnalysis>> GetByItemCategoryAsync(string itemCategory)
    {
        return await _dbSet
            .Where(m => m.ItemCategory == itemCategory && !m.IsDeleted)
            .OrderByDescending(m => m.AnalysisPeriodEnd)
            .ToListAsync();
    }

    public async Task<IEnumerable<MarketAnalysis>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(m => m.Status == status && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<MarketAnalysis?> GetWithPriceHistoriesAsync(Guid id)
    {
        return await _dbSet
            .Where(m => m.Id == id && !m.IsDeleted)
            .Include(m => m.PreparedBy)
            .Include(m => m.PriceHistories.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<CommonPagedResult.PagedResult<MarketAnalysis>> GetAnalysesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? itemCategory = null)
    {
        var query = _dbSet.Where(m => !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.AnalysisCode.Contains(search) || m.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(m => m.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(itemCategory))
        {
            query = query.Where(m => m.ItemCategory == itemCategory);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(m => m.PreparedBy)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonPagedResult.PagedResult<MarketAnalysis>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GenerateAnalysisCodeAsync()
    {
        var count = await _dbSet.CountAsync();
        return $"MA-{DateTime.UtcNow:yyyyMM}-{(count + 1):D4}";
    }
}

#endregion

#region Price History Repository

public class PriceHistoryRepository : GenericRepository<PriceHistory>, IPriceHistoryRepository
{
    public PriceHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PriceHistory>> GetByMarketAnalysisIdAsync(Guid marketAnalysisId)
    {
        return await _dbSet
            .Where(p => p.MarketAnalysisId == marketAnalysisId && !p.IsDeleted)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.PriceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceHistory>> GetByItemCategoryAsync(string itemCategory)
    {
        return await _dbSet
            .Where(p => p.ItemCategory == itemCategory && !p.IsDeleted)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.PriceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceHistory>> GetBySupplierAsync(Guid supplierId)
    {
        return await _dbSet
            .Where(p => p.SupplierId == supplierId && !p.IsDeleted)
            .OrderByDescending(p => p.PriceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(p => p.PriceDate >= startDate && p.PriceDate <= endDate && !p.IsDeleted)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.PriceDate)
            .ToListAsync();
    }

    public async Task<decimal> GetAveragePriceAsync(string itemCategory, DateTime startDate, DateTime endDate)
    {
        var prices = await _dbSet
            .Where(p => p.ItemCategory == itemCategory && p.PriceDate >= startDate && p.PriceDate <= endDate && !p.IsDeleted)
            .Select(p => p.UnitPrice)
            .ToListAsync();

        return prices.Any() ? prices.Average() : 0;
    }
}

#endregion

#region Supplier Consolidation Repository

public class SupplierConsolidationRepository : GenericRepository<SupplierConsolidation>, ISupplierConsolidationRepository
{
    public SupplierConsolidationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SupplierConsolidation?> GetByConsolidationCodeAsync(string consolidationCode)
    {
        return await _dbSet
            .Where(s => s.ConsolidationCode == consolidationCode && !s.IsDeleted)
            .Include(s => s.PreparedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SupplierConsolidation>> GetByItemCategoryAsync(string itemCategory)
    {
        return await _dbSet
            .Where(s => s.ItemCategory == itemCategory && !s.IsDeleted)
            .OrderByDescending(s => s.AnalysisPeriodEnd)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierConsolidation>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(s => s.Status == status && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierConsolidation>> GetByOpportunityLevelAsync(string opportunityLevel)
    {
        return await _dbSet
            .Where(s => s.OpportunityLevel == opportunityLevel && !s.IsDeleted)
            .OrderByDescending(s => s.PotentialSavings)
            .ToListAsync();
    }

    public async Task<CommonPagedResult.PagedResult<SupplierConsolidation>> GetConsolidationsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? itemCategory = null)
    {
        var query = _dbSet.Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.ConsolidationCode.Contains(search) || s.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(itemCategory))
        {
            query = query.Where(s => s.ItemCategory == itemCategory);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(s => s.PreparedBy)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonPagedResult.PagedResult<SupplierConsolidation>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GenerateConsolidationCodeAsync()
    {
        var count = await _dbSet.CountAsync();
        return $"SC-{DateTime.UtcNow:yyyyMM}-{(count + 1):D4}";
    }
}

#endregion

#region Emergency Procurement Plan Repository

public class EmergencyProcurementPlanRepository : GenericRepository<EmergencyProcurementPlan>, IEmergencyProcurementPlanRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public EmergencyProcurementPlanRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public async Task<EmergencyProcurementPlan?> GetByPlanCodeAsync(string planCode)
    {
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.PlanCode == planCode && !e.IsDeleted)
            .Include(e => e.Department)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementPlan>> GetByDepartmentAsync(Guid departmentId)
    {
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.DepartmentId == departmentId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementPlan>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.Status == status && !e.IsDeleted)
            .Include(e => e.Department)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementPlan>> GetActivePlansAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.Status == "Approved" &&
                   (!e.EffectiveDate.HasValue || e.EffectiveDate <= today) &&
                   (!e.ExpiryDate.HasValue || e.ExpiryDate >= today) &&
                   !e.IsDeleted)
            .Include(e => e.Department)
            .OrderBy(e => e.CriticalityLevel)
            .ToListAsync();
    }

    public async Task<EmergencyProcurementPlan?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.Id == id && !e.IsDeleted)
            .Include(e => e.Department)
            .Include(e => e.CriticalItems.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<EmergencyProcurementPlan?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.Id == id && !e.IsDeleted)
            .Include(e => e.Department)
            .Include(e => e.CriticalItems.Where(i => !i.IsDeleted))
            .Include(e => e.EmergencySuppliers.Where(s => !s.IsDeleted))
            .Include(e => e.ApprovedBy)
            .Include(e => e.PurchaseRequisition)
            .Include(e => e.ExceptionRule)
            .Include(e => e.WorkflowInstance)
            .Include(e => e.CentralDocumentVersion).ThenInclude(e => e!.DocumentRecord)
            .Include(e => e.PostAwardCentralDocumentVersion).ThenInclude(e => e!.DocumentRecord)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementPlan>> GetByEmergencyTypeAsync(string emergencyType)
    {
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.EmergencyType == emergencyType && !e.IsDeleted)
            .Include(e => e.Department)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementPlan>> GetPlansRequiringReviewAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Where(e => e.TenantId == _currentUserProvider.TenantId && e.NextReviewDate.HasValue && e.NextReviewDate <= today && e.Status == "Approved" && !e.IsDeleted)
            .Include(e => e.Department)
            .OrderBy(e => e.NextReviewDate)
            .ToListAsync();
    }

    public async Task<CommonPagedResult.PagedResult<EmergencyProcurementPlan>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        string? emergencyType = null)
    {
        var query = _dbSet.Where(e => e.TenantId == _currentUserProvider.TenantId && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.PlanCode.Contains(search) || e.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(emergencyType))
        {
            query = query.Where(e => e.EmergencyType == emergencyType);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(e => e.Department)
            .Include(e => e.CriticalItems.Where(i => !i.IsDeleted))
            .Include(e => e.EmergencySuppliers.Where(s => !s.IsDeleted))
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonPagedResult.PagedResult<EmergencyProcurementPlan>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GeneratePlanCodeAsync()
    {
        var prefix = $"EP-{DateTime.UtcNow:yyyyMM}-";
        var codes = await _dbSet.IgnoreQueryFilters()
            .Where(item => item.TenantId == _currentUserProvider.TenantId && item.PlanCode.StartsWith(prefix))
            .Select(item => item.PlanCode)
            .ToListAsync();
        var sequence = codes.Select(code => int.TryParse(code[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty().Max() + 1;
        return $"{prefix}{sequence:D4}";
    }
}

#endregion

#region Emergency Procurement Item Repository

public class EmergencyProcurementItemRepository : GenericRepository<EmergencyProcurementItem>, IEmergencyProcurementItemRepository
{
    public EmergencyProcurementItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmergencyProcurementItem>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Where(i => i.EmergencyProcurementPlanId == planId && !i.IsDeleted)
            .OrderBy(i => i.CriticalityLevel)
            .ThenBy(i => i.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementItem>> GetCriticalItemsAsync(Guid planId)
    {
        return await _dbSet
            .Where(i => i.EmergencyProcurementPlanId == planId && i.CriticalityLevel == "Critical" && !i.IsDeleted)
            .OrderBy(i => i.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementItem>> GetLowStockItemsAsync(Guid planId)
    {
        return await _dbSet
            .Where(i => i.EmergencyProcurementPlanId == planId && i.CurrentStockLevel < i.MinimumStockLevel && !i.IsDeleted)
            .OrderBy(i => i.CriticalityLevel)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencyProcurementItem>> GetByCriticalityLevelAsync(Guid planId, string criticalityLevel)
    {
        return await _dbSet
            .Where(i => i.EmergencyProcurementPlanId == planId && i.CriticalityLevel == criticalityLevel && !i.IsDeleted)
            .OrderBy(i => i.ItemDescription)
            .ToListAsync();
    }
}

#endregion

#region Emergency Supplier Repository

public class EmergencySupplierRepository : GenericRepository<EmergencySupplier>, IEmergencySupplierRepository
{
    public EmergencySupplierRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmergencySupplier>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Where(s => s.EmergencyProcurementPlanId == planId && !s.IsDeleted)
            .Include(s => s.Supplier)
            .OrderBy(s => s.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencySupplier>> GetActiveSuppliersByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Where(s => s.EmergencyProcurementPlanId == planId && s.IsActive && !s.IsDeleted)
            .Include(s => s.Supplier)
            .OrderBy(s => s.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencySupplier>> GetByPriorityAsync(Guid planId, int priority)
    {
        return await _dbSet
            .Where(s => s.EmergencyProcurementPlanId == planId && s.Priority == priority && !s.IsDeleted)
            .Include(s => s.Supplier)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencySupplier>> GetWithExpiringContractsAsync(int daysAhead = 30)
    {
        var futureDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(s => s.HasEmergencyContract && s.ContractExpiryDate.HasValue && s.ContractExpiryDate <= futureDate && s.IsActive && !s.IsDeleted)
            .Include(s => s.Supplier)
            .Include(s => s.EmergencyProcurementPlan)
            .OrderBy(s => s.ContractExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmergencySupplier>> GetRequiringVerificationAsync(int daysSinceLastVerification = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastVerification);
        return await _dbSet
            .Where(s => (!s.LastVerifiedDate.HasValue || s.LastVerifiedDate < cutoffDate) && s.IsActive && !s.IsDeleted)
            .Include(s => s.Supplier)
            .Include(s => s.EmergencyProcurementPlan)
            .OrderBy(s => s.LastVerifiedDate)
            .ToListAsync();
    }
}

#endregion
