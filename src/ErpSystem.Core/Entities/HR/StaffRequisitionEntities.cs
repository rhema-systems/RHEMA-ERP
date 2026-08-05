using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Requisition;

/// <summary>
/// Request to fill a position/create vacancy
/// </summary>
public class StaffRequisition : TenantEntity
{
    [MaxLength(50)]
    public string RequisitionNumber { get; set; } = string.Empty;
	
	[MaxLength(200)]
	public string? RequisitionTitle { get; set; }

	[MaxLength(500)]
	public string? Description { get; set; }

    public Guid? LocationLevelId { get; set; }

    [ForeignKey(nameof(LocationLevelId))]
    public virtual LocationLevel? LocationLevel { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;
	
	public Guid? JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription? JobDescription { get; set; }

    public StaffRequisitionType Type { get; set; }
    public StaffRequisitionPriority Priority { get; set; }
	public StaffRequisitionStatus Status { get; set; } = StaffRequisitionStatus.Draft;

    public int NumberOfPositions { get; set; } = 1;
	public int PositionsFilled { get; set; }
	[NotMapped]
	public int PositionsRemaining => NumberOfPositions - PositionsFilled;

    // If Replacement
    public Guid? ReplacementForEmployeeId { get; set; }

    [ForeignKey(nameof(ReplacementForEmployeeId))]
    public virtual Employee? ReplacementForEmployee { get; set; }

    public StaffReplacementReason? ReplacementReason { get; set; }
    public DateTime? EmployeeDepartureDate { get; set; }
	
	// Timeline
	public DateTime RequestDate { get; set; } = DateTime.UtcNow;
	public DateTime DesiredStartDate { get; set; }
	public DateTime? LatestAcceptableStartDate { get; set; }
	public DateTime? TargetFillDate { get; set; }
	public DateTime? ExpectedOfferDate { get; set; }
	public int? DaysToFill { get; set; } // Calculated field
	[MaxLength(1000)]
	public string? TargetStartDateReason { get; set; }

    // Justification
    [MaxLength(2000)]
    public string BusinessJustification { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ImpactIfNotFilled { get; set; }

	// Budget
	public bool IsBudgeted { get; set; }

	[MaxLength(50)]
    public string? BudgetCode { get; set; }

    // Recruitment Strategy
    public bool AllowInternalCandidates { get; set; }
    public bool AllowExternalCandidates { get; set; }

    // Requestor
    public Guid RequestedById { get; set; }

    [ForeignKey(nameof(RequestedById))]
    public virtual Employee RequestedBy { get; set; } = null!;

    // Fulfillment
    public bool IsFulfilled { get; set; }
    public DateTime? FulfilledDate { get; set; }
	
    // Cancellation
    public Guid? CancelledById { get; set; }

    [ForeignKey(nameof(CancelledById))]
    public virtual Employee? CancelledBy { get; set; }

    public DateTime? CancelledDate { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }
	
	// Workflow Integration
    public Guid? WorkflowInstanceId { get; set; }

    // Linked Job Vacancy
    public Guid? JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy? JobVacancy { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Concurrency token. Submit / approve / reject / hold / cancel / fulfil are load-check-mutate-save,
    /// so concurrent approvers could otherwise overwrite one another's decision.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public virtual ICollection<StaffRequisitionCost> Costs { get; set; } = new List<StaffRequisitionCost>();
    public virtual ICollection<StaffRequisitionAttachment> Attachments { get; set; } = new List<StaffRequisitionAttachment>();
    public virtual ICollection<StaffRequisitionHistory> History { get; set; } = new List<StaffRequisitionHistory>();
    public virtual ICollection<StaffRequisitionComment> Comments { get; set; } = new List<StaffRequisitionComment>();
}

public class StaffRequisitionCost : TenantEntity
{
    public Guid RequisitionId { get; set; }

    [ForeignKey(nameof(RequisitionId))]
    public virtual StaffRequisition Requisition { get; set; } = null!;
	
    public StaffRequisitionCostCategory Category { get; set; }

    [MaxLength(100)]
    public string Purpose { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g. GHS, USD, GBP).
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>
    /// Exchange rate to the tenant's base currency at time of recording.
    /// Defaults to 1 (same currency / no conversion needed).
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? PaymentVoucherNumber { get; set; }
	
    public Guid RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee RecordedBy { get; set; } = null!;

    public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
}

public class StaffRequisitionAttachment : TenantEntity
{
    public Guid RequisitionId { get; set; }

    [ForeignKey(nameof(RequisitionId))]
    public virtual StaffRequisition Requisition { get; set; } = null!;

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime UploadDate { get; set; }
	
	public Guid UploadedById { get; set; }
    
	[ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

public class StaffRequisitionComment : TenantEntity
{
    public Guid RequisitionId { get; set; }
    
    [ForeignKey(nameof(RequisitionId))]
    public virtual StaffRequisition Requisition { get; set; } = null!;
	
    /// <summary>
    /// Null for top-level comments; set to parent comment Id for replies.
    /// </summary>
    public Guid? ParentCommentId { get; set; }

    [ForeignKey(nameof(ParentCommentId))]
    public virtual StaffRequisitionComment? ParentComment { get; set; }
	
    [MaxLength(3000)]
    public string Body { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public virtual Employee Author { get; set; } = null!;

    public DateTime PostedDate { get; set; } = DateTime.UtcNow;
	
	public virtual ICollection<StaffRequisitionComment> Replies { get; set; } = new List<StaffRequisitionComment>();
}

public class StaffRequisitionHistory : TenantEntity
{
    public Guid RequisitionId { get; set; }

    [ForeignKey(nameof(RequisitionId))]
    public virtual StaffRequisition Requisition { get; set; } = null!;

    public StaffRequisitionStatus FromStatus { get; set; }
    public StaffRequisitionStatus ToStatus { get; set; }

    public Guid ChangedById { get; set; }

    [ForeignKey(nameof(ChangedById))]
    public virtual Employee ChangedBy { get; set; } = null!;

	[MaxLength(1000)]
    public string? Comments { get; set; }
	
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
}
