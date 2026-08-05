using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

#region Procurement Plan Service

/// <summary>
/// Procurement plan service interface
/// </summary>
public interface IProcurementPlanService
{
    Task<ProcurementPlanDetailDto?> GetByIdAsync(Guid id);
    Task<ProcurementPlanDto?> GetByPlanNumberAsync(string planNumber);
    Task<PagedResult<ProcurementPlanDto>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null);
    Task<IEnumerable<ProcurementPlanDto>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<ProcurementPlanDto>> GetByFiscalYearAsync(int fiscalYear);
    Task<IEnumerable<ProcurementPlanDto>> GetActivePlansAsync();
    Task<ProcurementPlanDetailDto> CreateAsync(CreateProcurementPlanDto dto);
    Task<ProcurementPlanDetailDto> UpdateAsync(Guid id, UpdateProcurementPlanDto dto);
    Task<ProcurementPlanDetailDto> SubmitForApprovalAsync(Guid id, SubmitProcurementPlanDto dto);
    Task<ProcurementPlanDetailDto> ApproveAsync(Guid id, ApproveProcurementPlanDto dto);
    Task<ProcurementPlanDetailDto> PublishAsync(Guid id, PublishProcurementPlanDto dto);
    Task<ProcurementPlanDetailDto> CreateAmendmentAsync(Guid id, CreateProcurementPlanAmendmentDto dto);
    Task<IEnumerable<ProcurementPlanDto>> GetVersionHistoryAsync(Guid id);
    Task<IEnumerable<ProcurementPlanConsolidationOpportunityDto>> GetConsolidationOpportunitiesAsync(
        int? fiscalYear = null,
        string? planningQuarter = null,
        Guid? departmentId = null);
    Task<ProcurementPlanningDashboardDto> GetDashboardAsync(
        int? fiscalYear = null,
        string? planningQuarter = null,
        Guid? departmentId = null);
    Task<ProcurementPlanningReportDto> GetReportAsync(
        string reportType,
        int? fiscalYear = null,
        string? planningQuarter = null,
        Guid? departmentId = null);
    Task DeleteAsync(Guid id);

    // Plan Items
    Task<ProcurementPlanItemDto> AddItemAsync(Guid planId, CreateProcurementPlanItemDto dto);
    Task<ProcurementPlanItemDto> UpdateItemAsync(Guid itemId, UpdateProcurementPlanItemDto dto);
    Task DeleteItemAsync(Guid itemId);
    Task<IEnumerable<ProcurementPlanItemDto>> GetItemsByPlanIdAsync(Guid planId);
    Task<IEnumerable<ProcurementPlanItemDto>> GetCriticalItemsAsync(Guid planId);

    // Plan Item Conversion
    Task<PlanItemConversionResultDto> ConvertItemToTenderAsync(ConvertPlanItemToTenderDto dto);
    Task<PlanItemConversionResultDto> ConvertItemToRfqAsync(ConvertPlanItemToTenderDto dto);
    Task<PlanItemConversionResultDto> ConvertItemToPurchaseOrderAsync(ConvertPlanItemToPurchaseOrderDto dto);

    // Plan Item Status Updates
    Task<ProcurementPlanItemDto> UpdateItemStatusAsync(Guid itemId, string newStatus);
    Task CheckAndCompletePlanAsync(Guid planId);

    // Budget Validation
    Task<BudgetValidationResultDto> ValidateBudgetForItemAsync(Guid planItemId, decimal? amount = null);
}

#endregion

#region Procurement Budget Service

/// <summary>
/// Procurement budget service interface
/// </summary>
public interface IProcurementBudgetService
{
    Task<ProcurementBudgetDetailDto?> GetByIdAsync(Guid id);
    Task<ProcurementBudgetDto?> GetByBudgetCodeAsync(string budgetCode);
    Task<PagedResult<ProcurementBudgetDto>> GetBudgetsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        int? fiscalYear = null);
    Task<IEnumerable<ProcurementBudgetDto>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<ProcurementBudgetDto>> GetByFiscalYearAsync(int fiscalYear);
    Task<IEnumerable<ProcurementBudgetDto>> GetActiveBudgetsAsync();
    Task<ProcurementBudgetDetailDto> CreateAsync(CreateProcurementBudgetDto dto);
    Task<ProcurementBudgetDetailDto> UpdateAsync(Guid id, CreateProcurementBudgetDto dto);
    Task<ProcurementBudgetDetailDto> ApproveAsync(Guid id);
    Task DeleteAsync(Guid id);

    // Budget Allocations
    Task<ProcurementBudgetAllocationDto> AddAllocationAsync(Guid budgetId, CreateProcurementBudgetAllocationDto dto);
    Task<ProcurementBudgetAllocationDto> UpdateAllocationAsync(Guid allocationId, CreateProcurementBudgetAllocationDto dto);
    Task DeleteAllocationAsync(Guid allocationId);

    // Budget Revisions
    Task<ProcurementBudgetRevisionDto> CreateRevisionAsync(Guid budgetId, CreateProcurementBudgetRevisionDto dto);
    Task<ProcurementBudgetRevisionDto> ApproveRevisionAsync(Guid revisionId);
    Task<ProcurementBudgetRevisionDto> RejectRevisionAsync(Guid revisionId, string reason);
    Task<IEnumerable<ProcurementBudgetRevisionDto>> GetRevisionsAsync(Guid budgetId);

    // Budget Utilization
    Task<decimal> GetTotalAllocatedAsync(Guid departmentId, int fiscalYear);
    Task<decimal> GetTotalUtilizedAsync(Guid departmentId, int fiscalYear);
    Task RecordUtilizationAsync(Guid budgetId, decimal amount, string? category = null);

    /// <summary>
    /// Commit budget amount when PO is created. Updates CommittedAmount at total and category level.
    /// </summary>
    Task CommitBudgetAsync(Guid budgetId, decimal amount, string? category = null);

    /// <summary>
    /// Release committed budget (e.g., when PO is cancelled)
    /// </summary>
    Task ReleaseCommittedBudgetAsync(Guid budgetId, decimal amount, string? category = null);

    /// <summary>
    /// Move committed amount to utilized when PO is received/completed
    /// </summary>
    Task UtilizeCommittedBudgetAsync(Guid budgetId, decimal amount, string? category = null);

    /// <summary>
    /// Resolve the budget linked to a purchase order and move its committed amount to utilized.
    /// Returns false when the purchase order is not linked to an active/approved procurement budget.
    /// </summary>
    Task<bool> UtilizePurchaseOrderCommittedBudgetAsync(Guid purchaseOrderId, decimal amount);

    /// <summary>
    /// Get available budgets for linking (Active/Approved) for a department and fiscal year
    /// </summary>
    Task<IEnumerable<ProcurementBudgetDto>> GetAvailableBudgetsForLinkingAsync(Guid departmentId, int fiscalYear);

    /// <summary>
    /// Link a budget to a procurement plan
    /// </summary>
    Task LinkBudgetToPlanAsync(Guid budgetId, Guid planId);
}

#endregion

#region Procurement Schedule Service

/// <summary>
/// Procurement schedule service interface
/// </summary>
public interface IProcurementScheduleService
{
    Task<ProcurementScheduleDetailDto?> GetByIdAsync(Guid id);
    Task<ProcurementScheduleDto?> GetByScheduleCodeAsync(string scheduleCode);
    Task<PagedResult<ProcurementScheduleDto>> GetSchedulesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        Guid? departmentId = null,
        Guid? planId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);
    Task<IEnumerable<ProcurementScheduleDto>> GetByPlanIdAsync(Guid planId);
    Task<IEnumerable<ProcurementScheduleDto>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<ProcurementScheduleDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ProcurementScheduleDto>> GetUpcomingSchedulesAsync(int daysAhead = 30);
    Task<IEnumerable<ProcurementScheduleDto>> GetOverdueSchedulesAsync();
    Task<IEnumerable<ProcurementScheduleDto>> GetConsolidationOpportunitiesAsync();
    Task<ProcurementScheduleDetailDto> CreateAsync(CreateProcurementScheduleDto dto);
    Task<ProcurementScheduleDetailDto> UpdateAsync(Guid id, CreateProcurementScheduleDto dto);
    Task<ProcurementScheduleDetailDto> StartScheduleAsync(Guid id);
    Task<ProcurementScheduleDetailDto> CompleteScheduleAsync(Guid id);
    Task<ProcurementScheduleDetailDto> CancelScheduleAsync(Guid id, string reason);
    Task DeleteAsync(Guid id);
}

#endregion

#region Market Analysis Service

/// <summary>
/// Market analysis service interface
/// </summary>
public interface IMarketAnalysisService
{
    Task<MarketAnalysisDetailDto?> GetByIdAsync(Guid id);
    Task<MarketAnalysisDto?> GetByAnalysisCodeAsync(string analysisCode);
    Task<PagedResult<MarketAnalysisDto>> GetAnalysesAsync(
        int page,
        int pageSize,
        string? search = null,
        string? itemCategory = null,
        string? status = null);
    Task<IEnumerable<MarketAnalysisDto>> GetByCategoryAsync(string category);
    Task<IEnumerable<MarketAnalysisDto>> GetByItemAsync(string itemName);
    Task<IEnumerable<MarketAnalysisDto>> GetRecentAnalysesAsync(int count = 10);
    Task<MarketAnalysisDetailDto> CreateAsync(CreateMarketAnalysisDto dto);
    Task<MarketAnalysisDetailDto> UpdateAsync(Guid id, CreateMarketAnalysisDto dto);
    Task<MarketAnalysisDetailDto> PublishAsync(Guid id);
    Task DeleteAsync(Guid id);

    // Price History
    Task<PriceHistoryDto> AddPriceHistoryAsync(Guid analysisId, CreatePriceHistoryDto dto);
    Task DeletePriceHistoryAsync(Guid priceHistoryId);
    Task<IEnumerable<PriceHistoryDto>> GetPriceHistoryAsync(Guid analysisId);
    Task<PriceTrendDto> GetPriceTrendAsync(Guid analysisId, int months = 12);
    Task<MarketSurveySummaryDto> GetMarketSurveySummaryAsync(Guid analysisId);
    Task<decimal> GetAveragePriceAsync(string category);
}

#endregion

#region Supplier Consolidation Service

/// <summary>
/// Supplier consolidation service interface
/// </summary>
public interface ISupplierConsolidationService
{
    Task<SupplierConsolidationDetailDto?> GetByIdAsync(Guid id);
    Task<SupplierConsolidationDto?> GetByConsolidationCodeAsync(string consolidationCode);
    Task<PagedResult<SupplierConsolidationDto>> GetConsolidationsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null);
    Task<IEnumerable<SupplierConsolidationDto>> GetByCategoryAsync(string category);
    Task<IEnumerable<SupplierConsolidationDto>> GetBySupplierAsync(Guid supplierId);
    Task<IEnumerable<SupplierConsolidationDto>> GetPendingConsolidationsAsync();
    Task<IEnumerable<SupplierConsolidationDto>> GetImplementedConsolidationsAsync();
    Task<SupplierConsolidationDetailDto> CreateAsync(CreateSupplierConsolidationDto dto);
    Task<SupplierConsolidationDetailDto> UpdateAsync(Guid id, CreateSupplierConsolidationDto dto);
    Task<SupplierConsolidationDetailDto> ApproveAsync(Guid id);
    Task<SupplierConsolidationDetailDto> ImplementAsync(Guid id);
    Task RecordActualSavingsAsync(Guid id, decimal actualSavings);
    Task<decimal> GetTotalEstimatedSavingsAsync();
    Task<decimal> GetTotalActualSavingsAsync();
    Task DeleteAsync(Guid id);
}

#endregion

#region Emergency Procurement Plan Service

/// <summary>
/// Emergency procurement plan service interface
/// </summary>
public interface IEmergencyProcurementPlanService
{
    Task<EmergencyProcurementPlanDetailDto?> GetByIdAsync(Guid id);
    Task<EmergencyProcurementPlanDto?> GetByPlanNumberAsync(string planNumber);
    Task<PagedResult<EmergencyProcurementPlanDto>> GetPlansAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? emergencyType = null);
    Task<IEnumerable<EmergencyProcurementPlanDto>> GetByEmergencyTypeAsync(string emergencyType);
    Task<IEnumerable<EmergencyProcurementPlanDto>> GetActivePlansAsync();
    Task<IEnumerable<EmergencyProcurementPlanDto>> GetTriggeredPlansAsync();
    Task<EmergencyProcurementPlanDetailDto> CreateAsync(CreateEmergencyProcurementPlanDto dto);
    Task<EmergencyProcurementPlanDetailDto> UpdateAsync(Guid id, CreateEmergencyProcurementPlanDto dto);
    Task<EmergencyProcurementPlanDetailDto> ActivateAsync(Guid id);
    Task<EmergencyProcurementPlanDetailDto> TriggerAsync(Guid id);
    Task<EmergencyProcurementPlanDetailDto> DeactivateAsync(Guid id);
    Task DeleteAsync(Guid id);

    // Critical Items
    Task<EmergencyProcurementItemDto> AddItemAsync(Guid planId, CreateEmergencyProcurementItemDto dto);
    Task<EmergencyProcurementItemDto> UpdateItemAsync(Guid itemId, CreateEmergencyProcurementItemDto dto);
    Task DeleteItemAsync(Guid itemId);
    Task<IEnumerable<EmergencyProcurementItemDto>> GetCriticalItemsAsync(Guid planId);

    // Emergency Suppliers
    Task<EmergencySupplierDto> AddSupplierAsync(Guid planId, CreateEmergencySupplierDto dto);
    Task<EmergencySupplierDto> UpdateSupplierAsync(Guid supplierId, CreateEmergencySupplierDto dto);
    Task DeleteSupplierAsync(Guid supplierId);
    Task<IEnumerable<EmergencySupplierDto>> GetActiveSuppliersAsync(Guid planId);
    Task<IEnumerable<EmergencySupplierDto>> GetSuppliersByCategoryAsync(string category);
}

#endregion
