using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

#region Procurement Plan Repositories

/// <summary>
/// Repository interface for procurement plans
/// </summary>
public interface IProcurementPlanRepository : IGenericRepository<ProcurementPlan>
{
    Task<ProcurementPlan?> GetByPlanNumberAsync(string planNumber);
    Task<IEnumerable<ProcurementPlan>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<ProcurementPlan>> GetByFiscalYearAsync(int fiscalYear);
    Task<IEnumerable<ProcurementPlan>> GetByStatusAsync(string status);
    Task<IEnumerable<ProcurementPlan>> GetActivePlansAsync();
    Task<ProcurementPlan?> GetWithItemsAsync(Guid id);
    Task<ProcurementPlan?> GetWithFullDetailsAsync(Guid id);
    Task<PagedResult<ProcurementPlan>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null);
    Task<string> GeneratePlanNumberAsync(int fiscalYear);
    Task<bool> PlanNumberExistsAsync(string planNumber);
    Task<decimal> GetPlannedBudgetExposureAsync(Guid budgetId, Guid? excludePlanId = null);
}

/// <summary>
/// Repository interface for procurement plan items
/// </summary>
public interface IProcurementPlanItemRepository : IGenericRepository<ProcurementPlanItem>
{
    Task<IEnumerable<ProcurementPlanItem>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<ProcurementPlanItem>> GetByPlanIdAndStatusAsync(Guid planId, string status);
    Task<IEnumerable<ProcurementPlanItem>> GetCriticalItemsAsync(Guid planId);
    Task<IEnumerable<ProcurementPlanItem>> GetByQuarterAsync(Guid planId, string quarter);
    Task<IEnumerable<ProcurementPlanItem>> GetByMonthAsync(Guid planId, int month);
    Task<IEnumerable<ProcurementPlanItem>> GetByPriorityAsync(Guid planId, string priority);
    Task<decimal> GetTotalEstimatedCostAsync(Guid planId);
}

/// <summary>
/// Repository interface for procurement plan item suppliers (many-to-many)
/// </summary>
public interface IProcurementPlanItemSupplierRepository : IGenericRepository<ProcurementPlanItemSupplier>
{
    Task<IEnumerable<ProcurementPlanItemSupplier>> GetByItemIdAsync(Guid itemId);
    Task<ProcurementPlanItemSupplier?> GetByItemAndSupplierAsync(Guid itemId, Guid supplierId);
    Task DeleteByItemIdAsync(Guid itemId);
}

#endregion

#region Procurement Budget Repositories

/// <summary>
/// Repository interface for procurement budgets
/// </summary>
public interface IProcurementBudgetRepository : IGenericRepository<ProcurementBudget>
{
    Task<ProcurementBudget?> GetByBudgetCodeAsync(string budgetCode);
    Task<IEnumerable<ProcurementBudget>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<ProcurementBudget>> GetByFiscalYearAsync(int fiscalYear);
    Task<IEnumerable<ProcurementBudget>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<ProcurementBudget>> GetByStatusAsync(string status);
    Task<IEnumerable<ProcurementBudget>> GetActiveBudgetsAsync();
    Task<ProcurementBudget?> GetWithAllocationsAsync(Guid id);
    Task<ProcurementBudget?> GetWithFullDetailsAsync(Guid id);
    Task<PagedResult<ProcurementBudget>> GetBudgetsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null);
    Task<string> GenerateBudgetCodeAsync(int fiscalYear, Guid tenantId);
    Task<bool> BudgetCodeExistsAsync(string budgetCode);
    Task<decimal> GetTotalAllocatedBudgetAsync(Guid departmentId, int fiscalYear);
    Task<decimal> GetTotalUtilizedBudgetAsync(Guid departmentId, int fiscalYear);
}

/// <summary>
/// Repository interface for procurement budget allocations
/// </summary>
public interface IProcurementBudgetAllocationRepository : IGenericRepository<ProcurementBudgetAllocation>
{
    Task<IEnumerable<ProcurementBudgetAllocation>> GetByBudgetIdAsync(Guid budgetId);
    Task<ProcurementBudgetAllocation?> GetByCategoryAsync(Guid budgetId, string categoryName);
    Task<decimal> GetTotalAllocatedAsync(Guid budgetId);
}

/// <summary>
/// Repository interface for procurement budget revisions
/// </summary>
public interface IProcurementBudgetRevisionRepository : IGenericRepository<ProcurementBudgetRevision>
{
    Task<IEnumerable<ProcurementBudgetRevision>> GetByBudgetIdAsync(Guid budgetId);
    Task<IEnumerable<ProcurementBudgetRevision>> GetPendingRevisionsAsync();
    Task<int> GetNextRevisionNumberAsync(Guid budgetId);
}

#endregion

#region Procurement Schedule Repositories

/// <summary>
/// Repository interface for procurement schedules
/// </summary>
public interface IProcurementScheduleRepository : IGenericRepository<ProcurementSchedule>
{
    Task<ProcurementSchedule?> GetByScheduleCodeAsync(string scheduleCode);
    Task<ProcurementSchedule?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<ProcurementSchedule>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<ProcurementSchedule>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<ProcurementSchedule>> GetByStatusAsync(string status);
    Task<IEnumerable<ProcurementSchedule>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ProcurementSchedule>> GetUpcomingSchedulesAsync(int daysAhead = 30);
    Task<IEnumerable<ProcurementSchedule>> GetOverdueSchedulesAsync();
    Task<IEnumerable<ProcurementSchedule>> GetConsolidationOpportunitiesAsync();
    Task<PagedResult<ProcurementSchedule>> GetSchedulesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        Guid? planId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);
    Task<string> GenerateScheduleCodeAsync();
}

#endregion

#region Market Analysis Repositories

/// <summary>
/// Repository interface for market analysis
/// </summary>
public interface IMarketAnalysisRepository : IGenericRepository<MarketAnalysis>
{
    Task<MarketAnalysis?> GetByAnalysisCodeAsync(string analysisCode);
    Task<IEnumerable<MarketAnalysis>> GetByItemCategoryAsync(string itemCategory);
    Task<IEnumerable<MarketAnalysis>> GetByStatusAsync(string status);
    Task<MarketAnalysis?> GetWithPriceHistoriesAsync(Guid id);
    Task<PagedResult<MarketAnalysis>> GetAnalysesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? itemCategory = null);
    Task<string> GenerateAnalysisCodeAsync();
}

/// <summary>
/// Repository interface for price histories
/// </summary>
public interface IPriceHistoryRepository : IGenericRepository<PriceHistory>
{
    Task<IEnumerable<PriceHistory>> GetByMarketAnalysisIdAsync(Guid marketAnalysisId);
    Task<IEnumerable<PriceHistory>> GetByItemCategoryAsync(string itemCategory);
    Task<IEnumerable<PriceHistory>> GetBySupplierAsync(Guid supplierId);
    Task<IEnumerable<PriceHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetAveragePriceAsync(string itemCategory, DateTime startDate, DateTime endDate);
}

#endregion

#region Supplier Consolidation Repositories

/// <summary>
/// Repository interface for supplier consolidation
/// </summary>
public interface ISupplierConsolidationRepository : IGenericRepository<SupplierConsolidation>
{
    Task<SupplierConsolidation?> GetByConsolidationCodeAsync(string consolidationCode);
    Task<IEnumerable<SupplierConsolidation>> GetByItemCategoryAsync(string itemCategory);
    Task<IEnumerable<SupplierConsolidation>> GetByStatusAsync(string status);
    Task<IEnumerable<SupplierConsolidation>> GetByOpportunityLevelAsync(string opportunityLevel);
    Task<PagedResult<SupplierConsolidation>> GetConsolidationsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? itemCategory = null);
    Task<string> GenerateConsolidationCodeAsync();
}

#endregion

#region Emergency Planning Repositories

/// <summary>
/// Repository interface for emergency procurement plans
/// </summary>
public interface IEmergencyProcurementPlanRepository : IGenericRepository<EmergencyProcurementPlan>
{
    Task<EmergencyProcurementPlan?> GetByPlanCodeAsync(string planCode);
    Task<IEnumerable<EmergencyProcurementPlan>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<EmergencyProcurementPlan>> GetByStatusAsync(string status);
    Task<IEnumerable<EmergencyProcurementPlan>> GetActivePlansAsync();
    Task<EmergencyProcurementPlan?> GetWithItemsAsync(Guid id);
    Task<EmergencyProcurementPlan?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<EmergencyProcurementPlan>> GetByEmergencyTypeAsync(string emergencyType);
    Task<IEnumerable<EmergencyProcurementPlan>> GetPlansRequiringReviewAsync();
    Task<PagedResult<EmergencyProcurementPlan>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        string? emergencyType = null);
    Task<string> GeneratePlanCodeAsync();
}

/// <summary>
/// Repository interface for emergency procurement items
/// </summary>
public interface IEmergencyProcurementItemRepository : IGenericRepository<EmergencyProcurementItem>
{
    Task<IEnumerable<EmergencyProcurementItem>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<EmergencyProcurementItem>> GetCriticalItemsAsync(Guid planId);
    Task<IEnumerable<EmergencyProcurementItem>> GetLowStockItemsAsync(Guid planId);
    Task<IEnumerable<EmergencyProcurementItem>> GetByCriticalityLevelAsync(Guid planId, string criticalityLevel);
}

/// <summary>
/// Repository interface for emergency suppliers
/// </summary>
public interface IEmergencySupplierRepository : IGenericRepository<EmergencySupplier>
{
    Task<IEnumerable<EmergencySupplier>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<EmergencySupplier>> GetActiveSuppliersByPlanIdAsync(Guid planId);
    Task<IEnumerable<EmergencySupplier>> GetByPriorityAsync(Guid planId, int priority);
    Task<IEnumerable<EmergencySupplier>> GetWithExpiringContractsAsync(int daysAhead = 30);
    Task<IEnumerable<EmergencySupplier>> GetRequiringVerificationAsync(int daysSinceLastVerification = 90);
}

#endregion
