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

            if (key == Normalize("LeaveRequest") || key == Normalize("LEAVE_REQUEST") || key == Normalize("Leave Request"))
            {
                var leaveRequest = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeaveRequest";
                info.EntityNumber = leaveRequest?.RequestNumber;
                info.EntityName = leaveRequest == null
                    ? null
                    : $"{leaveRequest.StartDate:dd MMM yyyy} - {leaveRequest.EndDate:dd MMM yyyy} ({leaveRequest.TotalDays:0.##} days)";
                info.ActionUrl = $"/hr/leave/requests/{entityId}";
                return info;
            }

            if (key == Normalize("LeavePlan") || key == Normalize("LEAVE_PLAN") || key == Normalize("Leave Plan"))
            {
                var leavePlan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeavePlan>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeavePlan";
                info.EntityName = leavePlan == null
                    ? null
                    : $"{leavePlan.Year} plan ({leavePlan.StartDate:dd MMM} - {leavePlan.EndDate:dd MMM})";
                info.ActionUrl = $"/hr/leave/plans?planId={entityId}";
                return info;
            }

            if (key == Normalize("LeaveEncashment") || key == Normalize("LEAVE_ENCASHMENT") || key == Normalize("Leave Encashment"))
            {
                var encashment = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveEncashment>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeaveEncashment";
                info.EntityName = encashment == null
                    ? null
                    : $"{encashment.DaysEncashed:0.##} days ({encashment.Year})";
                info.ActionUrl = $"/hr/leave/encashments?encashmentId={entityId}";
                return info;
            }

            if (key == Normalize("StaffAttendanceRegularization") || key == Normalize("STAFF_ATTENDANCE_REGULARIZATION") || key == Normalize("Attendance Regularization"))
            {
                var regularization = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.StaffAttendanceRegularization>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffAttendanceRegularization";
                info.EntityNumber = regularization?.RegularizationNumber;
                info.EntityName = regularization == null
                    ? null
                    : $"{regularization.Type} on {regularization.AttendanceDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/attendance/regularizations/{entityId}";
                return info;
            }

            if (key == Normalize("StaffOvertimeRequest") || key == Normalize("STAFF_OVERTIME_REQUEST") || key == Normalize("Overtime Request"))
            {
                var overtime = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.StaffOvertimeRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffOvertimeRequest";
                info.EntityNumber = overtime?.RequestNumber;
                info.EntityName = overtime == null
                    ? null
                    : $"{overtime.PlannedOvertimeHours:0.##} hrs on {overtime.OvertimeDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/attendance/overtime/{entityId}";
                return info;
            }

            if (key == Normalize("RemoteWorkRequest") || key == Normalize("REMOTE_WORK_REQUEST") || key == Normalize("Remote Work Request"))
            {
                var remoteWork = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.RemoteWorkRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "RemoteWorkRequest";
                info.EntityNumber = remoteWork?.RequestNumber;
                info.EntityName = remoteWork == null
                    ? null
                    : $"{remoteWork.StartDate:dd MMM} - {remoteWork.EndDate:dd MMM yyyy} ({remoteWork.RequestedDays} days)";
                info.ActionUrl = $"/hr/attendance/remote-work/{entityId}";
                return info;
            }

            if (key == Normalize("ConsultantTimesheet") || key == Normalize("CONSULTANT_TIMESHEET") || key == Normalize("Consultant Timesheet"))
            {
                var timesheet = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.ConsultantTimesheet>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "ConsultantTimesheet";
                info.EntityNumber = timesheet?.TimesheetNumber;
                info.EntityName = timesheet == null
                    ? null
                    : $"{timesheet.PeriodStartDate:dd MMM} - {timesheet.PeriodEndDate:dd MMM yyyy} ({timesheet.TotalHours:0.##} hrs)";
                info.ActionUrl = $"/hr/consulting/timesheets/{entityId}";
                return info;
            }

            if (key == Normalize("AppraisalTemplate") || key == Normalize("APPRAISAL_TEMPLATE") || key == Normalize("Appraisal Template"))
            {
                var template = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.AppraisalTemplate>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AppraisalTemplate";
                info.EntityName = template?.TemplateName;
                info.ActionUrl = $"/administration/hr/performance/templates/{entityId}";
                return info;
            }

            if (key == Normalize("SalaryReviewProposal") || key == Normalize("SALARY_REVIEW_PROPOSAL") || key == Normalize("Salary Review Proposal"))
            {
                var proposal = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.SalaryReviewProposal>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee);
                info.EntityType = "SalaryReviewProposal";
                info.EntityName = proposal == null
                    ? null
                    : $"{proposal.ProposalType} — {proposal.Employee?.FullName}";
                info.ActionUrl = $"/hr/performance/proposals/salary-review/{entityId}";
                return info;
            }

            if (key == Normalize("EmploymentActionProposal") || key == Normalize("EMPLOYMENT_ACTION_PROPOSAL") || key == Normalize("Employment Action Proposal"))
            {
                var proposal = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.EmploymentActionProposal>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee);
                info.EntityType = "EmploymentActionProposal";
                info.EntityName = proposal == null
                    ? null
                    : $"{proposal.ActionType} — {proposal.Employee?.FullName}";
                info.ActionUrl = $"/hr/performance/proposals/employment-action/{entityId}";
                return info;
            }

            if (key == Normalize("PerformanceImprovementPlan") || key == Normalize("PERFORMANCE_IMPROVEMENT_PLAN") || key == Normalize("Performance Improvement Plan"))
            {
                var plan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.PerformanceImprovementPlan>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee);
                info.EntityType = "PerformanceImprovementPlan";
                info.EntityName = plan == null
                    ? null
                    : $"{plan.PipNumber} — {plan.Employee?.FullName}";
                info.ActionUrl = $"/hr/performance/pip/{entityId}";
                return info;
            }

            if (key == Normalize("StaffRequisition") || key == Normalize("STAFF_REQUISITION") || key == Normalize("Staff Requisition"))
            {
                var requisition = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Requisition.StaffRequisition>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Position);
                info.EntityType = "StaffRequisition";
                info.EntityName = requisition == null
                    ? null
                    : $"{requisition.RequisitionNumber} — {requisition.RequisitionTitle}";
                info.ActionUrl = $"/hr/recruitment/requisitions/{entityId}";
                return info;
            }

            if (key == Normalize("TrainingNomination") || key == Normalize("TRAINING_NOMINATION") || key == Normalize("Training Nomination"))
            {
                info.EntityType = "TrainingNomination";
                info.ActionUrl = $"/hr/training/nominations?nominationId={entityId}";
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

            if (key == Normalize("SupplierReturn"))
            {
                var supplierReturn = await _unitOfWork.Repository<SupplierReturn>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Vendor);
                info.EntityType = "SupplierReturn";
                info.EntityNumber = supplierReturn?.ReturnNumber;
                info.EntityName = supplierReturn?.VendorName ?? supplierReturn?.Vendor?.PartnerName;
                info.ActionUrl = $"/finance/ap/returns/{entityId}";
                return info;
            }

            if (key == Normalize("JournalBatch"))
            {
                var batch = await _unitOfWork.Repository<JournalBatch>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "JournalBatch";
                info.EntityNumber = batch?.BatchNumber;
                info.EntityName = batch?.Description;
                info.ActionUrl = $"/finance/journal-batches/{entityId}";
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
                // Return workflows use the canonical BusinessPartner identity so workflow cards agree with AR.
                var returnOrder = await _unitOfWork.Repository<ReturnOrder>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "ReturnOrder";
                info.EntityNumber = returnOrder?.DocumentNumber;
                info.EntityName = returnOrder?.BusinessPartner?.PartnerName;
                info.ActionUrl = $"/sales/return-orders";
                return info;
            }

            if (key == Normalize("CreditNote"))
            {
                var creditNote = await _unitOfWork.Repository<CreditNote>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "CreditNote";
                info.EntityNumber = creditNote?.DocumentNumber;
                info.EntityName = creditNote?.BusinessPartner?.PartnerName;
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
                var refund = await _unitOfWork.Repository<Refund>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "Refund";
                info.EntityNumber = refund?.DocumentNumber;
                info.EntityName = refund?.BusinessPartner?.PartnerName;
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

            if (key == Normalize("BankDepositBatch") || key == Normalize("Bank Deposit"))
            {
                var deposit = await _unitOfWork.Repository<BankDepositBatch>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.BankAccount);
                info.EntityType = "BankDepositBatch";
                info.EntityNumber = deposit?.DepositNumber;
                info.EntityName = deposit == null
                    ? null
                    : $"{deposit.BankAccount?.AccountName} / {deposit.Currency} {deposit.NetAmount:N2}";
                info.ActionUrl = $"/finance/cash/deposits/{entityId}";
                return info;
            }

            if (key == Normalize("ReturnedChequeCase") || key == Normalize("Returned Cheque"))
            {
                var returnedCheque = await _unitOfWork.Repository<ReturnedChequeCase>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.BankAccount);
                info.EntityType = "ReturnedChequeCase";
                info.EntityNumber = returnedCheque?.CaseNumber;
                info.EntityName = returnedCheque == null
                    ? null
                    : $"Cheque {returnedCheque.ChequeNumber} / {returnedCheque.BankAccount?.AccountName}";
                info.ActionUrl = $"/finance/cash/returned-cheques?caseId={entityId}";
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
