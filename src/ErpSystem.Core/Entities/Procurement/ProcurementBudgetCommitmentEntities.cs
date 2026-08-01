using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementBudgetCommitments")]
public sealed class ProcurementBudgetCommitment : TenantEntity
{
    public Guid ProcurementBudgetId { get; set; }
    public Guid PurchaseRequisitionId { get; set; }

    [Required, StringLength(100)]
    public string ReservationReference { get; set; } = string.Empty;

    public int ReservationSequence { get; set; } = 1;
    public ProcurementBudgetCommitmentStatus Status { get; set; } = ProcurementBudgetCommitmentStatus.Reserved;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedAmount { get; set; }

    [Required, StringLength(10)]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAllocatedSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetUtilizedSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetCommittedBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAvailableBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetCommittedAfter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAvailableAfter { get; set; }

    public bool IsOverride { get; set; }
    public Guid? OverrideRuleId { get; set; }

    [StringLength(50)]
    public string? OverrideRuleCode { get; set; }

    public Guid? OverrideWorkflowInstanceId { get; set; }

    [StringLength(200)]
    public string? OverrideApprovalReference { get; set; }

    [StringLength(500)]
    public string? OverrideEvidenceReference { get; set; }

    public DateTime? OverrideApprovedAtUtc { get; set; }
    public DateTime ReservedAtUtc { get; set; }
    public Guid ReservedById { get; set; }

    [StringLength(300)]
    public string ReservedByName { get; set; } = string.Empty;

    public DateTime? ReleasedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ReleasedById { get; set; }

    [StringLength(300)]
    public string? ReleasedByName { get; set; }

    [StringLength(500)]
    public string? ReleaseReason { get; set; }

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementBudget ProcurementBudget { get; set; } = null!;
    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    public ProcurementPolicyExceptionRule? OverrideRule { get; set; }
    public WorkflowInstance? OverrideWorkflowInstance { get; set; }
}
