using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Projects;
using ProcurementPurchaseOrderSummaryDto = ErpSystem.Core.DTOs.Procurement.PurchaseOrderSummaryDto;
using ProcurementPurchaseRequisitionSummaryDto = ErpSystem.Core.DTOs.Procurement.PurchaseRequisitionSummaryDto;
using TenderDto = ErpSystem.Core.DTOs.Procurement.TenderDto;

namespace ErpSystem.Core.DTOs.Dashboard;

public class EnterpriseDashboardDto
{
    public CrmOverviewDto? CrmOverview { get; set; }
    public CrmReportingDto? CrmReporting { get; set; }
    public CrmConversionsDto? CrmConversions { get; set; }
    public ProjectDashboardDto? ProjectDashboard { get; set; }
    public List<ProcurementPurchaseRequisitionSummaryDto> PendingPurchaseRequisitions { get; set; } = new();
    public List<ProcurementPurchaseOrderSummaryDto> OpenPurchaseOrders { get; set; } = new();
    public List<InventoryRequisitionDto> PendingInventoryApprovals { get; set; } = new();
    public List<InventoryRequisitionDto> PendingInventoryIssues { get; set; } = new();
    public MaintenanceDashboardDto? MaintenanceOverview { get; set; }
    public WorkOrderMetricsDto? MaintenanceMetrics { get; set; }
    public WorkOrderTrendsDto? MaintenanceTrends { get; set; }
    public List<EnterpriseMaintenanceScheduleDto> UpcomingMaintenance { get; set; } = new();
    public List<TenderDto> Tenders { get; set; } = new();
    public ProcurementInventoryManagementDashboardDto? ProcurementInventoryManagement { get; set; }
    public List<EnterpriseDashboardModuleStatusDto> ModuleStatus { get; set; } = new();
    public DateTime RangeStartDate { get; set; }
    public DateTime RangeEndDate { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public sealed class ProcurementInventoryManagementDashboardDto
{
    public DateTime RangeStartDate { get; set; }
    public DateTime RangeEndDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public DateTime InventoryAsOfUtc { get; set; }
    public List<ManagementDashboardMoneyPointDto> SpendByCurrency { get; set; } = new();
    public List<ManagementDashboardMoneyPointDto> SpendByCategory { get; set; } = new();
    public List<ManagementDashboardMoneyPointDto> SpendByDepartment { get; set; } = new();
    public ManagementDashboardOpenPurchaseOrderDto OpenPurchaseOrders { get; set; } = new();
    public ManagementDashboardContractDto Contracts { get; set; } = new();
    public ManagementDashboardInventoryDto Inventory { get; set; } = new();
    public ManagementDashboardCycleTimeDto CycleTime { get; set; } = new();
    public ManagementDashboardServiceLevelDto ServiceLevel { get; set; } = new();
    public ManagementDashboardSupplierRiskDto SupplierRisk { get; set; } = new();
}

public sealed class ManagementDashboardMoneyPointDto
{
    public string Label { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public sealed class ManagementDashboardOpenPurchaseOrderDto
{
    public int Count { get; set; }
    public int OverdueCount { get; set; }
    public List<ManagementDashboardMoneyPointDto> OrderedValueByCurrency { get; set; } = new();
    public List<ManagementDashboardMoneyPointDto> RemainingValueByCurrency { get; set; } = new();
}

public sealed class ManagementDashboardContractDto
{
    public int ActiveCount { get; set; }
    public int ExpiringWithin90DaysCount { get; set; }
    public decimal AverageUtilizationPercent { get; set; }
    public List<ManagementDashboardMoneyPointDto> ContractValueByCurrency { get; set; } = new();
    public List<ManagementDashboardMoneyPointDto> UtilizedValueByCurrency { get; set; } = new();
    public List<ManagementDashboardContractExpiryDto> ExpiringContracts { get; set; } = new();
}

public sealed class ManagementDashboardContractExpiryDto
{
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime EndDate { get; set; }
    public int DaysToExpiry { get; set; }
    public decimal UtilizationPercent { get; set; }
}

public sealed class ManagementDashboardInventoryDto
{
    public decimal StockValue { get; set; }
    public decimal QuantityOnHand { get; set; }
    public int ItemLocationCount { get; set; }
    public int StockoutCount { get; set; }
    public List<ManagementDashboardInventoryValuePointDto> ValueByCategory { get; set; } = new();
    public List<ManagementDashboardInventoryValuePointDto> ValueByWarehouse { get; set; } = new();
}

public sealed class ManagementDashboardInventoryValuePointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal QuantityOnHand { get; set; }
    public int ItemLocationCount { get; set; }
}

public sealed class ManagementDashboardCycleTimeDto
{
    public int RequisitionToPurchaseOrderSampleCount { get; set; }
    public decimal? AverageRequisitionToPurchaseOrderDays { get; set; }
    public int PurchaseOrderToReceiptSampleCount { get; set; }
    public decimal? AveragePurchaseOrderToReceiptDays { get; set; }
}

public sealed class ManagementDashboardServiceLevelDto
{
    public int EligibleOrderCount { get; set; }
    public int OnTimeOrderCount { get; set; }
    public decimal? OnTimeDeliveryPercent { get; set; }
    public decimal? AcceptedFillRatePercent { get; set; }
}

public sealed class ManagementDashboardSupplierRiskDto
{
    public int AssessedSupplierCount { get; set; }
    public int HighOrCriticalSupplierCount { get; set; }
    public int AwardBlockedSupplierCount { get; set; }
    public int OpenAlertCount { get; set; }
    public int OverdueAssessmentCount { get; set; }
    public List<ManagementDashboardCountPointDto> ByRiskBand { get; set; } = new();
}

public sealed class ManagementDashboardCountPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class EnterpriseDashboardModuleStatusDto
{
    public string Module { get; set; } = string.Empty;
    public bool Available { get; set; }
    public bool AccessRestricted { get; set; }
    public string? Error { get; set; }
}

public class EnterpriseMaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string? MaintenanceTypeName { get; set; }
    public string? MaintenanceType { get; set; }
    public string? Name { get; set; }
    public DateTime? NextDue { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime? NextScheduledDate { get; set; }
    public string? AssignedTechnicianName { get; set; }
}
