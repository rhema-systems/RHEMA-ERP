using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementPurchaseOrderAmendments")]
public sealed class ProcurementPurchaseOrderAmendment : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }

    [Required, StringLength(100)]
    public string AmendmentNumber { get; set; } = string.Empty;

    public int AmendmentSequence { get; set; }
    public int BaseRevisionNumber { get; set; }
    public int ProposedRevisionNumber { get; set; }
    public ProcurementPurchaseOrderAmendmentStatus Status { get; set; }

    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ChangeScope { get; set; } = string.Empty;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string BeforeSnapshotJson { get; set; } = "{}";

    [Required, StringLength(64)]
    public string BeforeIntegrityHash { get; set; } = string.Empty;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string ProposedSnapshotJson { get; set; } = "{}";

    [Required, StringLength(64)]
    public string ProposedIntegrityHash { get; set; } = string.Empty;

    [Required, Column(TypeName = "nvarchar(max)")]
    public string DiffJson { get; set; } = "[]";

    [Required, StringLength(64)]
    public string DiffIntegrityHash { get; set; } = string.Empty;

    public ProcurementPurchaseOrderSourceType ProposedSourceType { get; set; }
    public Guid ProposedSourceId { get; set; }

    [Required, StringLength(100)]
    public string ProposedSourceReference { get; set; } = string.Empty;

    public Guid ProposedBusinessPartnerId { get; set; }
    public Guid ProposedSourceRequisitionId { get; set; }
    public Guid ProposedSourcingReleaseId { get; set; }
    public Guid ProposedSourcingCaseId { get; set; }
    public Guid ProposedAwardReadinessDecisionId { get; set; }

    [Required, StringLength(64)]
    public string ProposedSourceIntegrityHash { get; set; } = string.Empty;

    public decimal BeforeTotalAmount { get; set; }
    public decimal ProposedTotalAmount { get; set; }
    public decimal CommitmentDelta { get; set; }

    [Required, StringLength(10)]
    public string Currency { get; set; } = "USD";

    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedById { get; set; }

    [StringLength(300)]
    public string? SubmittedByName { get; set; }

    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }

    [StringLength(300)]
    public string? DecidedByName { get; set; }

    [StringLength(1000)]
    public string? DecisionComment { get; set; }

    public DateTime? AppliedAtUtc { get; set; }
    public Guid? AppliedById { get; set; }

    [StringLength(300)]
    public string? AppliedByName { get; set; }

    [Required, StringLength(30)]
    public string PurchaseOrderStatusBefore { get; set; } = "Approved";

    [StringLength(500)]
    public string? RequestEvidenceReference { get; set; }

    public Guid? RequestEvidenceWorkflowDocumentId { get; set; }
    public Guid? RequestEvidenceFileUploadRecordId { get; set; }

    [StringLength(500)]
    public string? ApprovalEvidenceReference { get; set; }

    public Guid? ApprovalEvidenceWorkflowDocumentId { get; set; }
    public Guid? ApprovalEvidenceFileUploadRecordId { get; set; }

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public BusinessPartner ProposedBusinessPartner { get; set; } = null!;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementPurchaseOrderCommitmentAdjustment> CommitmentAdjustments { get; set; } =
        new List<ProcurementPurchaseOrderCommitmentAdjustment>();
    public ICollection<ProcurementPurchaseOrderAmendmentDispatch> Dispatches { get; set; } =
        new List<ProcurementPurchaseOrderAmendmentDispatch>();
}

[Table("ProcurementPurchaseOrderCommitmentAdjustments")]
public sealed class ProcurementPurchaseOrderCommitmentAdjustment : TenantEntity
{
    public Guid AmendmentId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseRequisitionId { get; set; }
    public Guid ProcurementBudgetId { get; set; }
    public Guid BudgetCommitmentId { get; set; }
    public int Sequence { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseOrderAmountBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseOrderAmountAfter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RequisitionExposureBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RequisitionExposureAfter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommitmentAmountBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommitmentAmountAfter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DeltaAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetCommittedBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetCommittedAfter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAvailableBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAvailableAfter { get; set; }

    [Required, StringLength(10)]
    public string Currency { get; set; } = "USD";

    public DateTime AppliedAtUtc { get; set; }
    public Guid AppliedById { get; set; }

    [Required, StringLength(300)]
    public string AppliedByName { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public ProcurementPurchaseOrderAmendment Amendment { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    public ProcurementBudget ProcurementBudget { get; set; } = null!;
    public ProcurementBudgetCommitment BudgetCommitment { get; set; } = null!;
}

[Table("ProcurementPurchaseOrderAmendmentDispatches")]
public sealed class ProcurementPurchaseOrderAmendmentDispatch : TenantEntity
{
    public Guid AmendmentId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public int RevisionNumber { get; set; }
    public int Sequence { get; set; }
    public ProcurementPurchaseOrderDispatchChannel Channel { get; set; }

    [Required, StringLength(500)]
    public string Destination { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string DispatchReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string DocumentReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string OrganizationSignatureEvidenceReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string DispatchEvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }
    public DateTime DispatchedAtUtc { get; set; }
    public Guid DispatchedById { get; set; }

    [Required, StringLength(300)]
    public string DispatchedByName { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public ProcurementPurchaseOrderAmendment Amendment { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public ICollection<ProcurementPurchaseOrderAmendmentAcknowledgement> Acknowledgements { get; set; } =
        new List<ProcurementPurchaseOrderAmendmentAcknowledgement>();
}

[Table("ProcurementPurchaseOrderAmendmentAcknowledgements")]
public sealed class ProcurementPurchaseOrderAmendmentAcknowledgement : TenantEntity
{
    public Guid DispatchId { get; set; }
    public int Sequence { get; set; }
    public ProcurementPurchaseOrderAcknowledgementOutcome Outcome { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
    public Guid AcknowledgedByUserId { get; set; }
    public Guid? AcknowledgedByBusinessPartnerId { get; set; }

    [Required, StringLength(100)]
    public string AcknowledgementChannel { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string AcknowledgementReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    public Guid? EvidenceWorkflowDocumentId { get; set; }
    public Guid? EvidenceFileUploadRecordId { get; set; }

    [StringLength(1000)]
    public string? Comments { get; set; }

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public ProcurementPurchaseOrderAmendmentDispatch Dispatch { get; set; } = null!;
}
