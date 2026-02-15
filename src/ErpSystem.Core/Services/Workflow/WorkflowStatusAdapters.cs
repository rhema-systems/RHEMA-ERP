using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
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

    private static PurchaseOrder RequirePurchaseOrder(object entity)
        => entity as PurchaseOrder ?? throw new InvalidOperationException("Expected PurchaseOrder entity.");
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

    private static Tender RequireTender(object entity)
        => entity as Tender ?? throw new InvalidOperationException("Expected Tender entity.");
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

    private static BusinessPartner RequirePartner(object entity)
        => entity as BusinessPartner ?? throw new InvalidOperationException("Expected BusinessPartner entity.");
}
