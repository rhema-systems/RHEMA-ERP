using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
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
    private readonly IProcurementPlanRepository _procurementPlanRepository;
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
        IProcurementPlanRepository procurementPlanRepository,
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
        _procurementPlanRepository = procurementPlanRepository;
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

            if (key == Normalize("FleetTripInspection") || key == Normalize("FLEET_TRIP_INSPECTION") || key == Normalize("Fleet Trip Inspection"))
            {
                var inspection = await _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTripInspection>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.VehicleAsset, x => x.InspectionTemplate);
                info.EntityType = "FleetTripInspection";
                info.EntityNumber = $"FI-{entityId.ToString()[..8].ToUpperInvariant()}";
                info.EntityName = inspection == null
                    ? null
                    : $"{inspection.VehicleAsset?.Name ?? "Asset"} / {inspection.InspectionTemplate?.Name ?? inspection.InspectionKind}";
                info.ActionUrl = inspection?.FleetTripId.HasValue == true
                    ? $"/maintenance/fleet/trips?id={inspection.FleetTripId}&inspectionId={entityId}"
                    : $"/maintenance/assets?id={inspection?.VehicleAssetId}&tab=inspections&inspectionId={entityId}";
                return info;
            }

            if (key == Normalize("ProcurementPlan") || key == Normalize("PROCUREMENT_PLAN") || key == Normalize("Procurement Plan"))
            {
                var plan = await _procurementPlanRepository.GetWithFullDetailsAsync(entityId);
                info.EntityType = "ProcurementPlan";
                info.EntityNumber = plan?.PlanNumber;
                info.EntityName = plan == null
                    ? null
                    : string.IsNullOrWhiteSpace(plan.Department?.Name)
                        ? plan.Title
                        : $"{plan.Title} ({plan.Department.Name})";
                info.ActionUrl = $"/procurement/planning/plans/{entityId}";
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

            // Finance approval notifications depend on these entity links, so keep them with the newer Sales workflow mappings below.
            if (key == Normalize("JournalEntry"))
            {
                var journal = await _unitOfWork.Repository<JournalEntry>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "JournalEntry";
                info.EntityNumber = journal?.JournalEntryNumber;
                info.EntityName = journal?.Description;
                info.ActionUrl = $"/finance/journal-entries/{entityId}";
                return info;
            }

            if (key == Normalize("FinancePurchaseOrder"))
            {
                var po = await _unitOfWork.Repository<FinancePurchaseOrder>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Vendor);
                info.EntityType = "FinancePurchaseOrder";
                info.EntityNumber = po?.OrderNumber;
                info.EntityName = po?.Vendor?.PartnerName;
                info.ActionUrl = $"/finance/ap/purchase-orders/{entityId}";
                return info;
            }

            if (key == Normalize("FinancePurchaseOrderReceipt"))
            {
                var receipt = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.FinancePurchaseOrder);
                info.EntityType = "FinancePurchaseOrderReceipt";
                info.EntityNumber = receipt?.ReceiptNumber;
                info.EntityName = receipt?.FinancePurchaseOrder?.OrderNumber;
                info.ActionUrl = $"/finance/ap/receipts/{entityId}";
                return info;
            }

            if (key == Normalize("VendorInvoice"))
            {
                var invoice = await _unitOfWork.Repository<VendorInvoice>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "VendorInvoice";
                info.EntityNumber = invoice?.InvoiceNumber;
                info.EntityName = invoice?.SupplierName;
                info.ActionUrl = $"/finance/ap/invoices/{entityId}";
                return info;
            }

            if (key == Normalize("VendorPayment"))
            {
                var payment = await _unitOfWork.Repository<VendorPayment>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Supplier);
                info.EntityType = "VendorPayment";
                info.EntityNumber = payment?.PaymentNumber;
                info.EntityName = payment?.Supplier?.Name;
                info.ActionUrl = $"/finance/ap/payments/{entityId}";
                return info;
            }

            if (key == Normalize("PaymentBatch"))
            {
                var batch = await _unitOfWork.Repository<PaymentBatch>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "PaymentBatch";
                info.EntityNumber = batch?.BatchNumber;
                info.EntityName = batch?.Description;
                info.ActionUrl = $"/finance/ap/payments";
                return info;
            }

            if (key == Normalize("PurchaseReturn"))
            {
                var purchaseReturn = await _unitOfWork.Repository<PurchaseReturn>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "PurchaseReturn";
                info.EntityNumber = purchaseReturn?.ReturnNumber;
                info.EntityName = purchaseReturn?.SupplierName;
                info.ActionUrl = $"/finance/ap/returns/{entityId}";
                return info;
            }

            if (key == Normalize("Quote"))
            {
                var quote = await _unitOfWork.Repository<Quote>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "Quote";
                info.EntityNumber = quote?.DocumentNumber;
                info.EntityName = quote?.QuoteName;
                info.ActionUrl = $"/sales/crm/quotes";
                return info;
            }

            if (key == Normalize("SalesOrder") || key == Normalize("SALES_ORDER") || key == Normalize("Sales Order"))
            {
                var order = await _unitOfWork.Repository<SalesOrder>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalesOrder";
                info.EntityNumber = order?.DocumentNumber;
                info.EntityName = order?.CustomerName;
                info.ActionUrl = $"/sales/orders/{entityId}";
                return info;
            }

            if (key == Normalize("DeliveryNote"))
            {
                var delivery = await _unitOfWork.Repository<DeliveryNote>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "DeliveryNote";
                info.EntityNumber = delivery?.DocumentNumber;
                info.EntityName = delivery?.CustomerName;
                info.ActionUrl = $"/sales/deliveries";
                return info;
            }

            if (key == Normalize("Invoice"))
            {
                var invoice = await _unitOfWork.Repository<Invoice>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "Invoice";
                info.EntityNumber = invoice?.InvoiceNumber;
                info.EntityName = invoice?.CustomerName;
                info.ActionUrl = $"/finance/ar/invoices/{entityId}";
                return info;
            }

            if (key == Normalize("ReturnOrder"))
            {
                var returnOrder = await _unitOfWork.Repository<ReturnOrder>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Customer);
                info.EntityType = "ReturnOrder";
                info.EntityNumber = returnOrder?.DocumentNumber;
                info.EntityName = returnOrder?.Customer?.CustomerName;
                info.ActionUrl = $"/sales/return-orders";
                return info;
            }

            if (key == Normalize("CreditNote"))
            {
                var creditNote = await _unitOfWork.Repository<CreditNote>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Customer);
                info.EntityType = "CreditNote";
                info.EntityNumber = creditNote?.DocumentNumber;
                info.EntityName = creditNote?.Customer?.CustomerName;
                info.ActionUrl = $"/sales/credit-notes";
                return info;
            }

            if (key == Normalize("CustomerPayment"))
            {
                var payment = await _unitOfWork.Repository<CustomerPayment>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Customer);
                info.EntityType = "CustomerPayment";
                info.EntityNumber = payment?.PaymentNumber;
                info.EntityName = payment?.Customer?.CustomerName;
                info.ActionUrl = $"/finance/ar/payments";
                return info;
            }

            if (key == Normalize("Refund"))
            {
                var refund = await _unitOfWork.Repository<Refund>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Customer);
                info.EntityType = "Refund";
                info.EntityNumber = refund?.DocumentNumber;
                info.EntityName = refund?.Customer?.CustomerName;
                info.ActionUrl = $"/sales/refunds";
                return info;
            }

            if (key == Normalize("BudgetScenario"))
            {
                var scenario = await _unitOfWork.Repository<BudgetScenario>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "BudgetScenario";
                info.EntityNumber = scenario?.Name;
                info.EntityName = scenario?.Status;
                info.ActionUrl = $"/finance/budgeting/scenarios/{entityId}";
                return info;
            }

            if (key == Normalize("BudgetReturn"))
            {
                var budgetReturn = await _unitOfWork.Repository<BudgetReturn>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BudgetScenario);
                info.EntityType = "BudgetReturn";
                info.EntityNumber = budgetReturn?.BudgetScenario?.Name ?? entityId.ToString()[..8].ToUpperInvariant();
                info.EntityName = budgetReturn?.Status;
                info.ActionUrl = $"/finance/budgeting/returns/{entityId}";
                return info;
            }

            if (key == Normalize("UnitJournalEntry"))
            {
                var entry = await _unitOfWork.Repository<UnitJournalEntry>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "UnitJournalEntry";
                info.EntityNumber = entry?.EntryNumber;
                info.EntityName = entry?.Description;
                info.ActionUrl = $"/finance/unit-journal-entries/{entityId}";
                return info;
            }

            if (key == Normalize("UnitAccountBudget"))
            {
                var budget = await _unitOfWork.Repository<UnitAccountBudget>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.UnitAccount);
                info.EntityType = "UnitAccountBudget";
                info.EntityNumber = budget?.BudgetVersion;
                info.EntityName = budget?.UnitAccount?.Name;
                info.ActionUrl = $"/finance/unit-budgets/{entityId}";
                return info;
            }

            if (key == Normalize("AllocationRule"))
            {
                var allocation = await _unitOfWork.Repository<AllocationRule>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AllocationRule";
                info.EntityNumber = allocation?.Code;
                info.EntityName = allocation?.Name;
                info.ActionUrl = $"/finance/allocations/{entityId}";
                return info;
            }

            if (key == Normalize("CashTransaction"))
            {
                var transaction = await _unitOfWork.Repository<CashTransaction>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "CashTransaction";
                info.EntityNumber = transaction?.TransactionNumber;
                info.EntityName = transaction?.Description ?? transaction?.PayeeOrPayer;
                info.ActionUrl = $"/finance/cash/transactions";
                return info;
            }

            if (key == Normalize("BankReconciliation"))
            {
                var reconciliation = await _unitOfWork.Repository<BankReconciliation>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BankAccount);
                info.EntityType = "BankReconciliation";
                info.EntityNumber = reconciliation == null ? null : $"REC-{reconciliation.ReconciliationDate:yyyyMMdd}";
                info.EntityName = reconciliation?.BankAccount?.AccountName;
                info.ActionUrl = $"/finance/cash/reconciliation";
                return info;
            }

            if (key == Normalize("Cheque"))
            {
                var cheque = await _unitOfWork.Repository<Cheque>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "Cheque";
                info.EntityNumber = cheque?.ChequeNumber;
                info.EntityName = cheque?.PayeeName;
                info.ActionUrl = $"/finance/cash/transactions";
                return info;
            }

            if (key == Normalize("FixedAsset"))
            {
                var asset = await _unitOfWork.Repository<FixedAsset>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "FixedAsset";
                info.EntityNumber = asset?.AssetCode;
                info.EntityName = asset?.Name;
                info.ActionUrl = $"/finance/fixed-assets/register/{entityId}";
                return info;
            }

            if (key == Normalize("AssetDepreciationSchedule"))
            {
                var schedule = await _unitOfWork.Repository<AssetDepreciationSchedule>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetDepreciationSchedule";
                info.EntityNumber = schedule?.FixedAsset?.AssetCode;
                info.EntityName = schedule == null ? null : $"Depreciation {schedule.DepreciationAmount:N2}";
                info.ActionUrl = $"/finance/fixed-assets/depreciation";
                return info;
            }

            if (key == Normalize("AssetValuation"))
            {
                var valuation = await _unitOfWork.Repository<AssetValuation>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetValuation";
                info.EntityNumber = valuation?.ValuationReportReference ?? valuation?.FixedAsset?.AssetCode;
                info.EntityName = valuation?.FixedAsset?.Name;
                info.ActionUrl = $"/finance/fixed-assets/valuations";
                return info;
            }

            if (key == Normalize("AssetTransfer"))
            {
                var transfer = await _unitOfWork.Repository<AssetTransfer>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetTransfer";
                info.EntityNumber = transfer?.ReferenceNumber ?? transfer?.FixedAsset?.AssetCode;
                info.EntityName = transfer?.ToLocation;
                info.ActionUrl = $"/finance/fixed-assets/transfers";
                return info;
            }

            if (key == Normalize("AssetDisposal"))
            {
                var disposal = await _unitOfWork.Repository<AssetDisposal>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetDisposal";
                info.EntityNumber = disposal?.ReferenceNumber ?? disposal?.FixedAsset?.AssetCode;
                info.EntityName = disposal?.Reason;
                info.ActionUrl = $"/finance/fixed-assets/disposals";
                return info;
            }

            if (key == Normalize("AssetVerificationSession"))
            {
                var session = await _unitOfWork.Repository<AssetVerificationSession>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AssetVerificationSession";
                info.EntityNumber = session?.ReferenceNumber;
                info.EntityName = session?.SessionName;
                info.ActionUrl = $"/finance/fixed-assets/verification/{entityId}";
                return info;
            }

            if (key == Normalize("CapitalProject"))
            {
                var capitalProject = await _unitOfWork.Repository<CapitalProject>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "CapitalProject";
                info.EntityNumber = capitalProject?.ProjectCode;
                info.EntityName = capitalProject?.Name;
                info.ActionUrl = $"/finance/fixed-assets/capital-projects/{entityId}";
                return info;
            }

            if (key == Normalize("LeaseContract"))
            {
                var lease = await _unitOfWork.Repository<LeaseContract>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeaseContract";
                info.EntityNumber = lease?.ContractNumber;
                info.EntityName = lease?.Description;
                info.ActionUrl = $"/finance/fixed-assets/leases/{entityId}";
                return info;
            }

            // Newer Sales workflow entities are preserved alongside Finance display links for shared approval screens.
            if (key == Normalize("SalesAgreement") || key == Normalize("SALES_AGREEMENT") || key == Normalize("Sales Agreement"))
            {
                var agreement = await _unitOfWork.Repository<SalesAgreement>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalesAgreement";
                info.EntityNumber = agreement?.DocumentNumber;
                info.EntityName = agreement == null
                    ? null
                    : string.IsNullOrWhiteSpace(agreement.CustomerName)
                        ? agreement.AgreementTitle
                        : $"{agreement.AgreementTitle} ({agreement.CustomerName})";
                info.ActionUrl = $"/sales/agreements/{entityId}";
                return info;
            }

            if (key == Normalize("SalesAllocation") || key == Normalize("SALES_ALLOCATION") || key == Normalize("Sales Allocation") || key == Normalize("PlotAllocation"))
            {
                var allocation = await _unitOfWork.Repository<SalesAllocation>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalesAllocation";
                info.EntityNumber = allocation?.SourceItemCode ?? allocation?.SourceItemId;
                info.EntityName = allocation == null
                    ? null
                    : string.IsNullOrWhiteSpace(allocation.CustomerName)
                        ? allocation.SourceItemName
                        : $"{allocation.SourceItemName} ({allocation.CustomerName})";
                info.ActionUrl = $"/sales/allocations/{entityId}";
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
