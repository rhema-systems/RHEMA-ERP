using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Approval and posting status for a calculated allocation run.
/// </summary>
public enum AllocationRunBatchStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Posted = 4,
    Cancelled = 5,
    ReadyToPost = 6
}

/// <summary>
/// Persisted, approvable snapshot of one allocation rule execution for one fiscal period.
/// Posting must use the approved snapshot lines rather than recalculating from a rule that
/// may have changed after review.
/// </summary>
public class AllocationRunBatch : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [Required]
    public Guid AllocationRuleId { get; set; }

    [ForeignKey(nameof(AllocationRuleId))]
    public virtual AllocationRule? AllocationRule { get; set; }

    [Required]
    public Guid FiscalPeriodId { get; set; }

    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod? FiscalPeriod { get; set; }

    [Required]
    public DateTime AllocationDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public AllocationRunBatchStatus Status { get; set; } = AllocationRunBatchStatus.Draft;

    public bool ApprovalRequired { get; set; } = true;

    [Required]
    public Guid SourceAccountId { get; set; }

    [ForeignKey(nameof(SourceAccountId))]
    public virtual Account? SourceAccount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SourcePeriodBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAllocated { get; set; }

    [Required]
    [MaxLength(30)]
    public string AllocationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    [Required]
    [MaxLength(10)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";

    public Guid? WorkflowInstanceId { get; set; }

    public Guid? JournalEntryId { get; set; }

    [MaxLength(50)]
    public string? JournalEntryNumber { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? SubmittedBy { get; set; }

    [MaxLength(100)]
    public string? SubmittedByName { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    [MaxLength(100)]
    public string? ApprovedByName { get; set; }

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    [MaxLength(100)]
    public string? PostedByName { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [MaxLength(200)]
    public string? IdempotencyKey { get; set; }

    public virtual ICollection<AllocationRunBatchLine> Lines { get; set; } = new List<AllocationRunBatchLine>();
}

/// <summary>
/// Approved allocation target snapshot line belonging to an allocation run batch.
/// </summary>
public class AllocationRunBatchLine : TenantEntity
{
    [Required]
    public Guid AllocationRunBatchId { get; set; }

    [ForeignKey(nameof(AllocationRunBatchId))]
    public virtual AllocationRunBatch? AllocationRunBatch { get; set; }

    public int LineNumber { get; set; }

    [Required]
    public Guid TargetAccountId { get; set; }

    [ForeignKey(nameof(TargetAccountId))]
    public virtual Account? TargetAccount { get; set; }

    public Guid? TargetDriverUnitAccountId { get; set; }

    [ForeignKey(nameof(TargetDriverUnitAccountId))]
    public virtual UnitAccount? TargetDriverUnitAccount { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal AllocationBasis { get; set; }

    [Column(TypeName = "decimal(9,4)")]
    public decimal AllocationPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [MaxLength(50)]
    public string? CostCenterCode { get; set; }
}
