using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Entities.HR.Payroll;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Centralized resolver for workflow entity display info (numbers, names, and navigation URLs).
/// Keeps notification/audit UX consistent across modules.
/// </summary>
public class WorkflowEntityDisplayService : IWorkflowEntityDisplayService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPurchaseRequisitionRepository _purchaseRequisitionRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IJobCardRepository _jobCardRepository;
    private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryTransferRepository _inventoryTransferRepository;
    private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionRepository _inventoryRequisitionRepository;
    private readonly ILogger<WorkflowEntityDisplayService> _logger;

    public WorkflowEntityDisplayService(
        IUnitOfWork unitOfWork,
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        ITenderRepository tenderRepository,
        IProjectRepository projectRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IJobCardRepository jobCardRepository,
        ErpSystem.Core.Interfaces.Inventory.IInventoryTransferRepository inventoryTransferRepository,
        ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionRepository inventoryRequisitionRepository,
        ILogger<WorkflowEntityDisplayService> logger)
    {
        _unitOfWork = unitOfWork;
        _purchaseRequisitionRepository = purchaseRequisitionRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _tenderRepository = tenderRepository;
        _projectRepository = projectRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _jobCardRepository = jobCardRepository;
        _inventoryTransferRepository = inventoryTransferRepository;
        _inventoryRequisitionRepository = inventoryRequisitionRepository;
        _logger = logger;
    }

    public async Task<WorkflowEntityDisplayInfo> GetEntityDisplayInfoAsync(string entityType, Guid entityId)
    {
        var info = new WorkflowEntityDisplayInfo
        {
            EntityType = entityType ?? string.Empty,
            EntityId = entityId
        };

        if (string.IsNullOrWhiteSpace(entityType) || entityId == Guid.Empty)
        {
            return info;
        }

        // Normalize for matching (keep original EntityType for output unless we can canonicalize it).
        var key = Normalize(entityType);

        try
        {
            if (key == Normalize("PurchaseRequisition") || key == Normalize("PURCHASE_REQUISITION") || key == Normalize("PR"))
            {
                var pr = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(entityId);
                info.EntityType = "PurchaseRequisition";
                info.EntityNumber = pr?.RequisitionNumber;
                info.EntityName = pr?.Department;
                info.ActionUrl = $"/procurement/purchase-requisitions/{entityId}";
                return info;
            }

            if (key == Normalize("PurchaseOrder") || key == Normalize("PURCHASE_ORDER") || key == Normalize("PO"))
            {
                var po = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(entityId);
                info.EntityType = "PurchaseOrder";
                info.EntityNumber = po?.OrderNumber;
                if (po?.BusinessPartnerId != Guid.Empty)
                {
                    var bp = await _businessPartnerRepository.GetByIdAsync(po.BusinessPartnerId);
                    info.EntityName = bp?.PartnerName;
                }
                info.ActionUrl = $"/procurement/purchase-orders/{entityId}";
                return info;
            }

            if (key == Normalize("InventoryTransfer") || key == Normalize("INVENTORY_TRANSFER") || key == Normalize("Transfer"))
            {
                var transfer = await _inventoryTransferRepository.GetByIdAsync(entityId);
                info.EntityType = "InventoryTransfer";
                info.EntityNumber = transfer?.TransferNumber;
                info.ActionUrl = $"/inventory/transfers";
                return info;
            }

            if (key == Normalize("InventoryRequisition") || key == Normalize("INVENTORY_REQUISITION"))
            {
                var req = await _inventoryRequisitionRepository.GetByIdAsync(entityId);
                info.EntityType = "InventoryRequisition";
                info.EntityNumber = req?.RequisitionNumber;
                info.ActionUrl = $"/inventory/requisitions";
                return info;
            }

            if (key == Normalize("JobCard") || key == Normalize("JOB_CARD") || key == Normalize("Job Card"))
            {
                var jobCard = await _jobCardRepository.GetByIdAsync(entityId);
                info.EntityType = "JobCard";
                info.EntityNumber = jobCard?.JobCardNumber;
                info.ActionUrl = $"/maintenance/job-cards?id={entityId}";
                return info;
            }

            if (key == Normalize("PayrollRun") || key == Normalize("PAYROLL_RUN") || key == Normalize("Payroll Run"))
            {
                var run = await _unitOfWork.Repository<PayrollRun>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "PayrollRun";
                info.EntityNumber = run?.RunNumber;
                info.EntityName = run == null
                    ? null
                    : $"{run.PayPeriodFrom:MMM yyyy} - {run.PayPeriodTo:MMM yyyy}";
                info.ActionUrl = $"/hr/payroll?runId={entityId}";
                return info;
            }

            if (key == Normalize("FleetTrip") || key == Normalize("FLEET_TRIP") || key == Normalize("Fleet Trip"))
            {
                var trip = await _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTrip>()
                    .FirstOrDefaultAsync(t => t.Id == entityId, t => t.VehicleAsset);
                info.EntityType = "FleetTrip";
                info.EntityNumber = $"FT-{entityId.ToString()[..8].ToUpperInvariant()}";
                info.EntityName = trip?.VehicleAsset?.Name;
                info.ActionUrl = $"/maintenance/fleet/trips?id={entityId}";
                return info;
            }

            if (key == Normalize("Tender") || key == Normalize("TENDER") || key == Normalize("ProcurementTender"))
            {
                var tender = await _tenderRepository.GetByIdAsync(entityId);
                info.EntityType = "Tender";
                info.EntityNumber = tender?.TenderNumber;
                info.EntityName = tender?.Title;
                info.ActionUrl = $"/procurement/tenders/{entityId}";
                return info;
            }

            if (key == Normalize("Project") || key == Normalize("PROJECT"))
            {
                var project = await _projectRepository.GetByIdAsync(entityId);
                info.EntityType = "Project";
                info.EntityNumber = project?.ProjectCode;
                info.EntityName = project?.Title;
                info.ActionUrl = $"/development/projects/{entityId}";
                return info;
            }

            if (key == Normalize("ProjectDeliverable") || key == Normalize("PROJECT_DELIVERABLE") || key == Normalize("Project Deliverable"))
            {
                var deliverable = await _unitOfWork.Repository<ErpSystem.Core.Entities.Projects.ProjectDeliverable>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project);
                info.EntityType = "ProjectDeliverable";
                info.EntityNumber = deliverable?.Project?.ProjectCode;
                info.EntityName = deliverable == null
                    ? null
                    : $"{deliverable.Project?.Title ?? "Project"} / {deliverable.Title}";
                info.ActionUrl = deliverable == null ? null : $"/development/projects/{deliverable.ProjectId}";
                return info;
            }

            if (key == Normalize("ProjectClosure") || key == Normalize("PROJECT_CLOSURE") || key == Normalize("Project Closure"))
            {
                var closure = await _unitOfWork.Repository<ErpSystem.Core.Entities.Projects.ProjectClosure>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project);
                info.EntityType = "ProjectClosure";
                info.EntityNumber = closure?.Project?.ProjectCode;
                info.EntityName = closure == null
                    ? null
                    : $"{closure.Project?.Title ?? "Project"} / Closure";
                info.ActionUrl = closure == null ? null : $"/development/projects/{closure.ProjectId}";
                return info;
            }

            if (key == Normalize("BusinessPartner") || key == Normalize("BUSINESS_PARTNER") || key == Normalize("Business Partner") || key == Normalize("Supplier"))
            {
                var bp = await _businessPartnerRepository.GetByIdAsync(entityId);
                info.EntityType = "BusinessPartner";
                info.EntityNumber = bp?.PartnerCode;
                info.EntityName = bp?.PartnerName;
                info.ActionUrl = $"/procurement/business-partners/{entityId}";
                return info;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve entity display info for {EntityType} {EntityId}", entityType, entityId);
        }

        return info;
    }

    private static string Normalize(string s)
        => new string((s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
