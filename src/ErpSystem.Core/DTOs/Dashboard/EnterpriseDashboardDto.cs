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
    public List<EnterpriseDashboardModuleStatusDto> ModuleStatus { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class EnterpriseDashboardModuleStatusDto
{
    public string Module { get; set; } = string.Empty;
    public bool Available { get; set; }
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
