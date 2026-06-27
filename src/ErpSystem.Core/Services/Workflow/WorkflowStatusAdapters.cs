using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

public class WorkflowStatusAdapterRegistry : IWorkflowStatusAdapterRegistry
{
    private readonly Dictionary<string, IWorkflowStatusAdapter> _adapters;

    public WorkflowStatusAdapterRegistry(IEnumerable<IWorkflowStatusAdapter> adapters)
    {
        _adapters = new Dictionary<string, IWorkflowStatusAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in adapters)
        {
            foreach (var entityType in adapter.EntityTypes)
            {
                if (string.IsNullOrWhiteSpace(entityType))
                {
                    continue;
                }

                _adapters[entityType.Trim()] = adapter;
            }
        }
    }

    public bool TryGetAdapter(string entityType, out IWorkflowStatusAdapter adapter)
        => _adapters.TryGetValue(entityType, out adapter!);

    public IWorkflowStatusAdapter GetAdapter(string entityType)
        => TryGetAdapter(entityType, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"No workflow status adapter registered for entity type '{entityType}'.");
}

public sealed class JobCardWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "JobCard",
        "Job Card",
        "JOB_CARD"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var jobCard = RequireJobCard(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                jobCard.JobCardStatus = "Approved";
                jobCard.ApprovalStatus = "Approved";
                jobCard.ApprovedDate = DateTime.UtcNow;
                jobCard.ApprovedById = userId;
                break;
            case WorkflowOutcome.Rejected:
                jobCard.JobCardStatus = "Rejected";
                jobCard.ApprovalStatus = "Rejected";
                jobCard.ApprovedDate = null;
                jobCard.ApprovedById = null;
                break;
            default:
                jobCard.JobCardStatus = "Submitted";
                jobCard.ApprovalStatus = "Pending";
                jobCard.ApprovedDate = null;
                jobCard.ApprovedById = null;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var jobCard = RequireJobCard(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                jobCard.JobCardStatus = "Approved";
                jobCard.ApprovalStatus = "Approved";
                jobCard.ApprovedDate = DateTime.UtcNow;
                jobCard.ApprovedById = userId;
                break;
            case WorkflowOutcome.Rejected:
                jobCard.JobCardStatus = "Rejected";
                jobCard.ApprovalStatus = "Rejected";
                jobCard.ApprovedDate = null;
                jobCard.ApprovedById = null;
                break;
            default:
                jobCard.JobCardStatus = "UnderReview";
                jobCard.ApprovalStatus = "Pending";
                jobCard.ApprovedDate = null;
                jobCard.ApprovedById = null;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var jobCard = RequireJobCard(entity);
        jobCard.JobCardStatus = "Draft";
        jobCard.ApprovalStatus = "NotStarted";
        jobCard.ApprovedDate = null;
        jobCard.ApprovedById = null;
    }

    private static JobCard RequireJobCard(object entity)
        => entity as JobCard ?? throw new InvalidOperationException("Expected JobCard entity.");
}

public sealed class PurchaseOrderWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "PurchaseOrder",
        "Purchase Order",
        "PURCHASE_ORDER",
        "PO"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var purchaseOrder = RequirePurchaseOrder(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                purchaseOrder.Status = "Approved";
                purchaseOrder.ApprovedById = userId;
                purchaseOrder.ApprovedAt = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                purchaseOrder.Status = "Rejected";
                break;
            default:
                purchaseOrder.Status = "Pending Approval";
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var purchaseOrder = RequirePurchaseOrder(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                purchaseOrder.Status = "Approved";
                purchaseOrder.ApprovedById = userId;
                purchaseOrder.ApprovedAt = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                purchaseOrder.Status = "Rejected";
                purchaseOrder.ApprovedById = null;
                purchaseOrder.ApprovedAt = null;
                break;
            default:
                purchaseOrder.Status = "Pending Approval";
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var purchaseOrder = RequirePurchaseOrder(entity);
        purchaseOrder.Status = "Draft";
        purchaseOrder.ApprovedById = null;
        purchaseOrder.ApprovedAt = null;
    }

    private static PurchaseOrder RequirePurchaseOrder(object entity)
        => entity as PurchaseOrder ?? throw new InvalidOperationException("Expected PurchaseOrder entity.");
}

public sealed class ProcurementPlanWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ProcurementPlan",
        "Procurement Plan",
        "PROCUREMENT_PLAN"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var plan = RequireProcurementPlan(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                plan.Status = "Approved";
                plan.ApprovedById = userId;
                plan.ApprovedDate = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                plan.Status = "Rejected";
                plan.ApprovedById = null;
                plan.ApprovedDate = null;
                break;
            default:
                plan.Status = "Submitted";
                plan.ReviewedById = userId;
                plan.ReviewedDate = DateTime.UtcNow;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var plan = RequireProcurementPlan(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                plan.Status = "Approved";
                plan.ApprovedById = userId;
                plan.ApprovedDate = DateTime.UtcNow;
                plan.ApprovalComments = null;
                break;
            case WorkflowOutcome.Rejected:
                plan.Status = "Rejected";
                plan.ApprovedById = null;
                plan.ApprovedDate = null;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    plan.ReviewComments = rejectionReason;
                }
                break;
            default:
                plan.Status = "UnderReview";
                plan.ReviewedById = userId;
                plan.ReviewedDate = DateTime.UtcNow;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var plan = RequireProcurementPlan(entity);
        plan.Status = "Draft";
        plan.ReviewedById = null;
        plan.ReviewedDate = null;
        plan.ApprovedById = null;
        plan.ApprovedDate = null;
        plan.ApprovalComments = null;
    }

    private static ProcurementPlan RequireProcurementPlan(object entity)
        => entity as ProcurementPlan ?? throw new InvalidOperationException("Expected ProcurementPlan entity.");
}

public sealed class PayrollRunWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "PayrollRun",
        "Payroll Run",
        "PAYROLL_RUN"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var run = RequirePayrollRun(entity);
        Apply(run, outcome, userId);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var run = RequirePayrollRun(entity);
        Apply(run, outcome, userId);
        if (!string.IsNullOrWhiteSpace(rejectionReason))
        {
            run.Notes = string.IsNullOrWhiteSpace(run.Notes)
                ? rejectionReason.Trim()
                : $"{run.Notes}{Environment.NewLine}{rejectionReason.Trim()}";
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var run = RequirePayrollRun(entity);
        run.Status = PayrollRunStatus.Draft;
        run.ReviewedAt = null;
        run.ReviewedByUserId = null;
        run.ApprovedAt = null;
        run.ApprovedByUserId = null;
    }

    private static void Apply(PayrollRun run, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                run.Status = PayrollRunStatus.Approved;
                run.ApprovedAt = DateTime.UtcNow;
                run.ApprovedByUserId = userId;
                break;
            case WorkflowOutcome.Rejected:
                run.Status = PayrollRunStatus.RolledBack;
                run.ReviewedAt = null;
                run.ReviewedByUserId = null;
                run.ApprovedAt = null;
                run.ApprovedByUserId = null;
                break;
            default:
                run.Status = PayrollRunStatus.InReview;
                run.ReviewedAt = DateTime.UtcNow;
                run.ReviewedByUserId = userId;
                run.ApprovedAt = null;
                run.ApprovedByUserId = null;
                break;
        }
    }

    private static PayrollRun RequirePayrollRun(object entity)
        => entity as PayrollRun ?? throw new InvalidOperationException("Expected PayrollRun entity.");
}

public sealed class SalesOrderWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "SalesOrder",
        "Sales Order",
        "SALES_ORDER"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var order = RequireSalesOrder(entity);
        order.SubmittedById ??= userId;
        order.SubmittedDate ??= DateTime.UtcNow;
        Apply(order, outcome, userId, comments: null);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var order = RequireSalesOrder(entity);
        Apply(order, outcome, userId, rejectionReason);
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var order = RequireSalesOrder(entity);
        order.OrderStatus = SalesOrderStatus.Draft;
        order.ApprovalStatus = "Draft";
        order.SubmittedById = null;
        order.SubmittedDate = null;
        order.ApprovedById = null;
        order.ApprovedDate = null;
        order.ApprovalComments = reason;
    }

    private static void Apply(SalesOrder order, WorkflowOutcome outcome, Guid? userId, string? comments)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                order.OrderStatus = SalesOrderStatus.Confirmed;
                order.ApprovalStatus = "Approved";
                order.ApprovedById = userId;
                order.ApprovedDate = DateTime.UtcNow;
                order.ApprovalComments = comments;
                break;
            case WorkflowOutcome.Rejected:
                order.OrderStatus = SalesOrderStatus.Rejected;
                order.ApprovalStatus = "Rejected";
                order.ApprovedById = null;
                order.ApprovedDate = null;
                order.ApprovalComments = comments;
                break;
            default:
                order.OrderStatus = SalesOrderStatus.PendingApproval;
                order.ApprovalStatus = "PendingApproval";
                order.SubmittedById ??= userId;
                order.SubmittedDate ??= DateTime.UtcNow;
                order.ApprovedById = null;
                order.ApprovedDate = null;
                if (!string.IsNullOrWhiteSpace(comments))
                {
                    order.ApprovalComments = comments;
                }
                break;
        }
    }

    private static SalesOrder RequireSalesOrder(object entity)
        => entity as SalesOrder ?? throw new InvalidOperationException("Expected SalesOrder entity.");
}

public sealed class SalesAgreementWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "SalesAgreement",
        "Sales Agreement",
        "SALES_AGREEMENT"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var agreement = RequireSalesAgreement(entity);
        Apply(agreement, outcome, userId, comments: null);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var agreement = RequireSalesAgreement(entity);
        Apply(agreement, outcome, userId, rejectionReason);
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var agreement = RequireSalesAgreement(entity);
        agreement.AgreementStatus = SalesAgreementStatus.Draft;
        agreement.ApprovedById = null;
        agreement.ApprovedDate = null;
        agreement.ApprovalComments = reason;
    }

    private static void Apply(SalesAgreement agreement, WorkflowOutcome outcome, Guid? userId, string? comments)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                agreement.AgreementStatus = SalesAgreementStatus.Active;
                agreement.ApprovedById = userId;
                agreement.ApprovedDate = DateTime.UtcNow;
                agreement.ApprovalComments = comments;
                break;
            case WorkflowOutcome.Rejected:
                agreement.AgreementStatus = SalesAgreementStatus.Draft;
                agreement.ApprovedById = null;
                agreement.ApprovedDate = null;
                agreement.ApprovalComments = comments;
                break;
            default:
                agreement.AgreementStatus = SalesAgreementStatus.PendingApproval;
                agreement.ApprovedById = null;
                agreement.ApprovedDate = null;
                if (!string.IsNullOrWhiteSpace(comments))
                {
                    agreement.ApprovalComments = comments;
                }
                break;
        }
    }

    private static SalesAgreement RequireSalesAgreement(object entity)
        => entity as SalesAgreement ?? throw new InvalidOperationException("Expected SalesAgreement entity.");
}

public sealed class SalesAllocationWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "SalesAllocation",
        "Sales Allocation",
        "PlotAllocation",
        "Plot Allocation",
        "SALES_ALLOCATION",
        "PLOT_ALLOCATION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var allocation = RequireSalesAllocation(entity);
        Apply(allocation, outcome, comments: null);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var allocation = RequireSalesAllocation(entity);
        Apply(allocation, outcome, rejectionReason);
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var allocation = RequireSalesAllocation(entity);
        allocation.Status = "Reserved";
        allocation.ReleaseReason = null;
        allocation.ReleasedDate = null;
        allocation.Notes = string.IsNullOrWhiteSpace(reason) ? allocation.Notes : reason;
    }

    private static void Apply(SalesAllocation allocation, WorkflowOutcome outcome, string? comments)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                allocation.Status = "Allocated";
                allocation.EffectiveDate ??= DateTime.UtcNow;
                allocation.ReleaseReason = null;
                allocation.ReleasedDate = null;
                break;
            case WorkflowOutcome.Rejected:
                allocation.Status = "Released";
                allocation.ReleasedDate = DateTime.UtcNow;
                allocation.ReleaseReason = comments;
                break;
            default:
                allocation.Status = "PendingApproval";
                allocation.ReleasedDate = null;
                allocation.ReleaseReason = null;
                break;
        }

        if (!string.IsNullOrWhiteSpace(comments))
        {
            allocation.Notes = comments;
        }
    }

    private static SalesAllocation RequireSalesAllocation(object entity)
        => entity as SalesAllocation ?? throw new InvalidOperationException("Expected SalesAllocation entity.");
}

public sealed class CreditNoteWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "CreditNote",
        "Credit Note",
        "CREDIT_NOTE"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var creditNote = RequireCreditNote(entity);
        Apply(creditNote, outcome, comments: null);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var creditNote = RequireCreditNote(entity);
        Apply(creditNote, outcome, rejectionReason);
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var creditNote = RequireCreditNote(entity);
        creditNote.CreditNoteStatus = CreditNoteStatus.Draft;
        AppendReason(creditNote, reason);
    }

    private static void Apply(CreditNote creditNote, WorkflowOutcome outcome, string? comments)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                creditNote.CreditNoteStatus = CreditNoteStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                creditNote.CreditNoteStatus = CreditNoteStatus.Draft;
                break;
            default:
                creditNote.CreditNoteStatus = CreditNoteStatus.PendingApproval;
                break;
        }

        AppendReason(creditNote, comments);
    }

    private static void AppendReason(CreditNote creditNote, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        creditNote.Reason = string.IsNullOrWhiteSpace(creditNote.Reason)
            ? note.Trim()
            : $"{creditNote.Reason}{Environment.NewLine}{note.Trim()}";
    }

    private static CreditNote RequireCreditNote(object entity)
        => entity as CreditNote ?? throw new InvalidOperationException("Expected CreditNote entity.");
}

public sealed class RefundWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "Refund",
        "REFUND"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var refund = RequireRefund(entity);
        Apply(refund, outcome, comments: null);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var refund = RequireRefund(entity);
        Apply(refund, outcome, rejectionReason);
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var refund = RequireRefund(entity);
        refund.RefundStatus = RefundStatus.Draft;
        AppendReason(refund, reason);
    }

    private static void Apply(Refund refund, WorkflowOutcome outcome, string? comments)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                refund.RefundStatus = RefundStatus.Approved;
                break;
            case WorkflowOutcome.Rejected:
                refund.RefundStatus = RefundStatus.Rejected;
                break;
            default:
                refund.RefundStatus = RefundStatus.PendingApproval;
                break;
        }

        AppendReason(refund, comments);
    }

    private static void AppendReason(Refund refund, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        refund.Reason = string.IsNullOrWhiteSpace(refund.Reason)
            ? note.Trim()
            : $"{refund.Reason}{Environment.NewLine}{note.Trim()}";
    }

    private static Refund RequireRefund(object entity)
        => entity as Refund ?? throw new InvalidOperationException("Expected Refund entity.");
}

public sealed class FleetTripWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "FleetTrip",
        "Fleet Trip",
        "FLEET_TRIP"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var trip = RequireFleetTrip(entity);
        Apply(trip, outcome, userId, rejectionReason: null);
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var trip = RequireFleetTrip(entity);
        Apply(trip, outcome, userId, rejectionReason);
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var trip = RequireFleetTrip(entity);
        trip.Status = FleetTripStatuses.Draft;
        trip.ApprovedAt = null;
        trip.ApprovedByUserId = null;
        trip.RejectedAt = null;
        trip.RejectedByUserId = null;
        trip.RejectionReason = null;
    }

    private static void Apply(FleetTrip trip, WorkflowOutcome outcome, Guid? userId, string? rejectionReason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                trip.Status = FleetTripStatuses.Approved;
                trip.ApprovedAt = DateTime.UtcNow;
                trip.ApprovedByUserId = userId;
                trip.RejectedAt = null;
                trip.RejectedByUserId = null;
                trip.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                trip.Status = FleetTripStatuses.Rejected;
                trip.RejectedAt = DateTime.UtcNow;
                trip.RejectedByUserId = userId;
                trip.RejectionReason = rejectionReason;
                trip.ApprovedAt = null;
                trip.ApprovedByUserId = null;
                break;
            default:
                trip.Status = FleetTripStatuses.Submitted;
                trip.ApprovedAt = null;
                trip.ApprovedByUserId = null;
                trip.RejectedAt = null;
                trip.RejectedByUserId = null;
                trip.RejectionReason = null;
                break;
        }
    }

    private static FleetTrip RequireFleetTrip(object entity)
        => entity as FleetTrip ?? throw new InvalidOperationException("Expected FleetTrip entity.");
}

public sealed class FleetTripInspectionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "FleetTripInspection",
        "Fleet Trip Inspection",
        "FLEET_TRIP_INSPECTION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(RequireInspection(entity), outcome);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(RequireInspection(entity), outcome);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var inspection = RequireInspection(entity);
        inspection.Status = "Completed";
    }

    private static void Apply(FleetTripInspection inspection, WorkflowOutcome outcome)
    {
        inspection.Status = outcome switch
        {
            WorkflowOutcome.Approved => "Approved",
            WorkflowOutcome.Rejected => "Rejected",
            _ => "Submitted"
        };
    }

    private static FleetTripInspection RequireInspection(object entity)
        => entity as FleetTripInspection ?? throw new InvalidOperationException("Expected FleetTripInspection entity.");
}

public sealed class PurchaseRequisitionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "PurchaseRequisition",
        "Purchase Requisition",
        "PURCHASE_REQUISITION",
        "PR"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var requisition = RequirePurchaseRequisition(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                requisition.Status = "Approved";
                requisition.ApprovedById = userId;
                requisition.ApprovedAt = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                requisition.Status = "Rejected";
                break;
            default:
                requisition.Status = "Pending Approval";
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var requisition = RequirePurchaseRequisition(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                requisition.Status = "Approved";
                requisition.ApprovedById = userId;
                requisition.ApprovedAt = DateTime.UtcNow;
                requisition.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                requisition.Status = "Rejected";
                requisition.ApprovedById = null;
                requisition.ApprovedAt = null;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    requisition.RejectionReason = rejectionReason;
                }
                break;
            default:
                requisition.Status = "Pending Approval";
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var requisition = RequirePurchaseRequisition(entity);
        requisition.Status = "Draft";
        requisition.ApprovedById = null;
        requisition.ApprovedAt = null;
        requisition.RejectionReason = null;
    }

    private static PurchaseRequisition RequirePurchaseRequisition(object entity)
        => entity as PurchaseRequisition ?? throw new InvalidOperationException("Expected PurchaseRequisition entity.");
}

public sealed class InventoryTransferWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "InventoryTransfer",
        "Inventory Transfer",
        "INVENTORY_TRANSFER",
        "Transfer"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var transfer = RequireInventoryTransfer(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                transfer.Status = TransferStatus.Approved;
                transfer.ApprovedById = userId;
                transfer.ApprovalDate = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                transfer.Status = TransferStatus.Rejected;
                transfer.ApprovedById = null;
                transfer.ApprovalDate = null;
                break;
            default:
                transfer.Status = TransferStatus.Submitted;
                transfer.ApprovedById = null;
                transfer.ApprovalDate = null;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var transfer = RequireInventoryTransfer(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                transfer.Status = TransferStatus.Approved;
                transfer.ApprovedById = userId;
                transfer.ApprovalDate = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                transfer.Status = TransferStatus.Rejected;
                transfer.ApprovedById = null;
                transfer.ApprovalDate = null;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    // Keep existing notes but append a clear rejection reason for audit visibility.
                    transfer.Notes = string.IsNullOrWhiteSpace(transfer.Notes)
                        ? $"Rejected: {rejectionReason}"
                        : $"{transfer.Notes}\nRejected: {rejectionReason}";
                }
                break;
            default:
                // Remain in submitted while the workflow continues through additional steps.
                transfer.Status = TransferStatus.Submitted;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var transfer = RequireInventoryTransfer(entity);
        transfer.Status = TransferStatus.Draft;
        transfer.ApprovedById = null;
        transfer.ApprovalDate = null;
    }

    private static InventoryTransfer RequireInventoryTransfer(object entity)
        => entity as InventoryTransfer ?? throw new InvalidOperationException("Expected InventoryTransfer entity.");
}

public sealed class InventoryRequisitionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "InventoryRequisition",
        "Inventory Requisition",
        "INVENTORY_REQUISITION",
        "Requisition"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var req = RequireInventoryRequisition(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                req.Status = RequisitionStatus.Approved;
                req.ApprovalDate = DateTime.UtcNow;
                req.ApprovedById = userId;
                req.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                req.Status = RequisitionStatus.Rejected;
                req.ApprovalDate = DateTime.UtcNow;
                req.ApprovedById = userId;
                break;
            default:
                req.Status = RequisitionStatus.Submitted;
                req.ApprovalDate = null;
                req.ApprovedById = null;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var req = RequireInventoryRequisition(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                req.Status = RequisitionStatus.Approved;
                req.ApprovalDate = DateTime.UtcNow;
                req.ApprovedById = userId;
                req.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                req.Status = RequisitionStatus.Rejected;
                req.ApprovalDate = DateTime.UtcNow;
                req.ApprovedById = userId;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    req.RejectionReason = rejectionReason;
                }
                break;
            default:
                // Still pending in workflow (multi-step)
                req.Status = RequisitionStatus.Submitted;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var req = RequireInventoryRequisition(entity);
        req.Status = RequisitionStatus.Draft;
        req.ApprovalDate = null;
        req.ApprovedById = null;
        req.RejectionReason = null;
    }

    private static InventoryRequisition RequireInventoryRequisition(object entity)
        => entity as InventoryRequisition ?? throw new InvalidOperationException("Expected InventoryRequisition entity.");
}

public sealed class TenderWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "Tender",
        "TENDER",
        "ProcurementTender"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var tender = RequireTender(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                // Approved for publishing (publishing is a separate action that captures deadlines/invitations).
                tender.Status = "Approved";
                break;
            case WorkflowOutcome.Rejected:
                tender.Status = "Rejected";
                break;
            default:
                tender.Status = "Submitted";
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var tender = RequireTender(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                tender.Status = "Approved";
                break;
            case WorkflowOutcome.Rejected:
                tender.Status = "Rejected";
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    // Keep tender notes but ensure the rejection reason is visible to users/admins.
                    tender.Notes = string.IsNullOrWhiteSpace(tender.Notes)
                        ? $"Rejected: {rejectionReason}"
                        : $"{tender.Notes}\nRejected: {rejectionReason}";
                }
                break;
            default:
                tender.Status = "Submitted";
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var tender = RequireTender(entity);
        tender.Status = "Draft";
    }

    private static Tender RequireTender(object entity)
        => entity as Tender ?? throw new InvalidOperationException("Expected Tender entity.");
}

public sealed class ProjectWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "Project",
        "PROJECT"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var project = RequireProject(entity);
        project.Status = outcome switch
        {
            WorkflowOutcome.Approved => ProjectStatuses.Planned,
            WorkflowOutcome.Rejected => ProjectStatuses.Draft,
            _ => ProjectStatuses.PendingApproval
        };
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var project = RequireProject(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                project.Status = ProjectStatuses.Planned;
                project.ApprovedAt = DateTime.UtcNow;
                break;
            case WorkflowOutcome.Rejected:
                project.Status = ProjectStatuses.Draft;
                project.StatusRemarks = rejectionReason;
                break;
            default:
                project.Status = ProjectStatuses.PendingApproval;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var project = RequireProject(entity);
        project.Status = ProjectStatuses.Draft;
        project.StatusRemarks = reason;
        project.ApprovedAt = null;
    }

    private static Project RequireProject(object entity)
        => entity as Project ?? throw new InvalidOperationException("Expected Project entity.");
}

public sealed class ProjectDeliverableWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ProjectDeliverable",
        "Project Deliverable",
        "PROJECT_DELIVERABLE"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var deliverable = RequireDeliverable(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                deliverable.Status = "Approved";
                deliverable.ApprovedAt = DateTime.UtcNow;
                deliverable.ApprovedById = userId;
                break;
            case WorkflowOutcome.Rejected:
                deliverable.Status = "Rejected";
                deliverable.ApprovedAt = null;
                deliverable.ApprovedById = null;
                break;
            default:
                deliverable.Status = "PendingApproval";
                deliverable.ApprovedAt = null;
                deliverable.ApprovedById = null;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var deliverable = RequireDeliverable(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                deliverable.Status = "Approved";
                deliverable.ApprovedAt = DateTime.UtcNow;
                deliverable.ApprovedById = userId;
                break;
            case WorkflowOutcome.Rejected:
                deliverable.Status = "Rejected";
                deliverable.ApprovedAt = null;
                deliverable.ApprovedById = null;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    deliverable.AcceptanceNotes = rejectionReason;
                }
                break;
            default:
                deliverable.Status = "PendingApproval";
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var deliverable = RequireDeliverable(entity);
        deliverable.Status = "Draft";
        deliverable.ApprovedAt = null;
        deliverable.ApprovedById = null;
        deliverable.AcceptanceNotes = reason;
    }

    private static ProjectDeliverable RequireDeliverable(object entity)
        => entity as ProjectDeliverable ?? throw new InvalidOperationException("Expected ProjectDeliverable entity.");
}

public sealed class ProjectClosureWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ProjectClosure",
        "Project Closure",
        "PROJECT_CLOSURE"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var closure = RequireClosure(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                closure.Status = ProjectStatuses.Closed;
                closure.ApprovedAt = DateTime.UtcNow;
                closure.ApprovedById = userId;
                break;
            case WorkflowOutcome.Rejected:
                closure.Status = "Draft";
                closure.ApprovedAt = null;
                closure.ApprovedById = null;
                break;
            default:
                closure.Status = "PendingApproval";
                closure.ApprovedAt = null;
                closure.ApprovedById = null;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var closure = RequireClosure(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                closure.Status = ProjectStatuses.Closed;
                closure.ApprovedAt = DateTime.UtcNow;
                closure.ApprovedById = userId;
                closure.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                closure.Status = "Draft";
                closure.ApprovedAt = null;
                closure.ApprovedById = null;
                closure.RejectionReason = rejectionReason;
                break;
            default:
                closure.Status = "PendingApproval";
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var closure = RequireClosure(entity);
        closure.Status = "Draft";
        closure.ApprovedAt = null;
        closure.ApprovedById = null;
        closure.RejectionReason = null;
    }

    private static ProjectClosure RequireClosure(object entity)
        => entity as ProjectClosure ?? throw new InvalidOperationException("Expected ProjectClosure entity.");
}

public sealed class ProjectBudgetRevisionWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ProjectBudgetRevision",
        "Project Budget Revision",
        "PROJECT_BUDGET_REVISION"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var revision = RequireRevision(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                revision.Status = "Approved";
                revision.ApprovedAt = DateTime.UtcNow;
                revision.ApprovedById = userId;
                revision.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                revision.Status = "Rejected";
                revision.ApprovedAt = null;
                revision.ApprovedById = null;
                break;
            default:
                revision.Status = "PendingApproval";
                revision.ApprovedAt = null;
                revision.ApprovedById = null;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var revision = RequireRevision(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                revision.Status = "Approved";
                revision.ApprovedAt = DateTime.UtcNow;
                revision.ApprovedById = userId;
                revision.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                revision.Status = "Rejected";
                revision.ApprovedAt = null;
                revision.ApprovedById = null;
                revision.RejectionReason = rejectionReason;
                break;
            default:
                revision.Status = "PendingApproval";
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var revision = RequireRevision(entity);
        revision.Status = "Draft";
        revision.ApprovedAt = null;
        revision.ApprovedById = null;
        revision.RejectionReason = null;
    }

    private static ProjectBudgetRevision RequireRevision(object entity)
        => entity as ProjectBudgetRevision ?? throw new InvalidOperationException("Expected ProjectBudgetRevision entity.");
}

public sealed class BusinessPartnerWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "BusinessPartner",
        "Business Partner",
        "BUSINESS_PARTNER",
        "Supplier",
        "Contractor"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var partner = RequirePartner(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                partner.RegistrationStatus = "Active";
                partner.ApprovalStatus = "Approved";
                partner.ApprovedById = userId;
                partner.ApprovedDate = DateTime.UtcNow;
                partner.RejectionReason = null;
                partner.IsActive = true;
                break;
            case WorkflowOutcome.Rejected:
                partner.RegistrationStatus = "Rejected";
                partner.ApprovalStatus = "Rejected";
                partner.ApprovedById = userId;
                partner.ApprovedDate = DateTime.UtcNow;
                partner.IsActive = false;
                break;
            default:
                // In workflow (multi-step), keep partner unavailable for transactions.
                partner.RegistrationStatus = "PendingApproval";
                partner.ApprovalStatus = "Pending";
                partner.ApprovedById = null;
                partner.ApprovedDate = null;
                partner.IsActive = false;
                break;
        }
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var partner = RequirePartner(entity);
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                partner.RegistrationStatus = "Active";
                partner.ApprovalStatus = "Approved";
                partner.ApprovedById = userId;
                partner.ApprovedDate = DateTime.UtcNow;
                partner.RejectionReason = null;
                partner.IsActive = true;
                break;
            case WorkflowOutcome.Rejected:
                partner.RegistrationStatus = "Rejected";
                partner.ApprovalStatus = "Rejected";
                partner.ApprovedById = userId;
                partner.ApprovedDate = DateTime.UtcNow;
                partner.IsActive = false;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    partner.RejectionReason = rejectionReason;
                }
                break;
            default:
                // Still pending in workflow (multi-step)
                partner.RegistrationStatus = "PendingApproval";
                partner.ApprovalStatus = "Pending";
                partner.IsActive = false;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var partner = RequirePartner(entity);
        partner.RegistrationStatus = "Pending";
        partner.ApprovalStatus = "Draft";
        partner.ApprovedById = null;
        partner.ApprovedDate = null;
        partner.RejectionReason = null;
        partner.IsActive = false;
    }

    private static BusinessPartner RequirePartner(object entity)
        => entity as BusinessPartner ?? throw new InvalidOperationException("Expected BusinessPartner entity.");
}

public sealed class ServiceRequestWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "ServiceRequest",
        "Service Request",
        "SERVICE_REQUEST"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
    {
        var req = RequireServiceRequest(entity);
        req.SubmittedAtUtc ??= DateTime.UtcNow;

        req.Status = outcome switch
        {
            WorkflowOutcome.Approved => EhcServiceRequestStatus.Approved,
            WorkflowOutcome.Rejected => EhcServiceRequestStatus.Rejected,
            _ => EhcServiceRequestStatus.PendingApproval
        };
    }

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var req = RequireServiceRequest(entity);
        req.SubmittedAtUtc ??= DateTime.UtcNow;

        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                req.Status = EhcServiceRequestStatus.Approved;
                req.ApprovedAtUtc = DateTime.UtcNow;
                req.ApprovedByUserId = userId;
                req.RejectedAtUtc = null;
                req.RejectedByUserId = null;
                req.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                req.Status = EhcServiceRequestStatus.Rejected;
                req.RejectedAtUtc = DateTime.UtcNow;
                req.RejectedByUserId = userId;
                req.RejectionReason = rejectionReason;
                req.ApprovedAtUtc = null;
                req.ApprovedByUserId = null;
                break;
            default:
                req.Status = EhcServiceRequestStatus.PendingApproval;
                break;
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        var req = RequireServiceRequest(entity);
        req.Status = EhcServiceRequestStatus.Draft;
        req.SubmittedAtUtc = null;
        req.ApprovedAtUtc = null;
        req.ApprovedByUserId = null;
        req.RejectedAtUtc = null;
        req.RejectedByUserId = null;
        req.RejectionReason = null;
    }

    private static EhcServiceRequest RequireServiceRequest(object entity)
        => entity as EhcServiceRequest ?? throw new InvalidOperationException("Expected EhcServiceRequest entity.");
}
