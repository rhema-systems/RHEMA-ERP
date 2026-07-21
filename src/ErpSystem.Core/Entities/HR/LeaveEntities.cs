using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.StaffAttendance;

namespace ErpSystem.Core.Entities.HR.StaffLeave;

/// <summary>
/// Configurable leave types with policies
/// </summary>
public class LeaveType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsPaid { get; set; } = true;

    public int DefaultDaysPerYear { get; set; }

    public int MaxDaysPerYear { get; set; }

    public int? MinDaysNotice { get; set; } // Minimum notice period in days

    public bool RequiresApproval { get; set; } = true;

    [MaxLength(50)]
    public string? CalendarColor { get; set; }

    public bool HasSubTypes { get; set; }

    // Policy constraints
    public bool AllowCarryOver { get; set; }

    public int? MaxCarryOverDays { get; set; }

    public bool CountWeekendsAsLeave { get; set; } = true;

    public bool CountHolidaysAsLeave { get; set; } = false;

    public bool AllowCashConversion { get; set; }

    public bool RequiresReliever { get; set; } = false;

    // ===== ACCESS / ACCRUAL POLICY (Phase 2) =====

    /// <summary>
    /// Minimum months of service from <see cref="Employee.DateEmployed"/> before an employee can
    /// apply for this leave type at all (e.g. annual leave = 12; compassionate/sick = 0/null).
    /// Distinct from the accrual policy's <see cref="LeaveAccrualPolicy.MinServiceMonths"/> which
    /// governs how much has accrued. Null/0 means available from day one.
    /// </summary>
    public int? MinServiceMonthsToAccess { get; set; }

    // ===== CARRY-OVER / FORFEITURE POLICY (Phase 2) =====

    /// <summary>
    /// If carry-over is allowed, how many months into the new year the carried-over balance
    /// remains usable before it expires (e.g. 3 = usable only in Jan–Mar). Null = no expiry.
    /// </summary>
    public int? CarryOverExpiryMonths { get; set; }

    /// <summary>
    /// When set, unused accrued days are forfeited (zeroed) this many months after year start
    /// during the year-end/cut-off processing — the "use it or lose it" / force-leave rule.
    /// </summary>
    public int? ForfeitUnusedAfterMonths { get; set; }

    /// <summary>
    /// Marks the leave type as mandatory-to-take within the year (force leave). Surfaced to HR
    /// and used by the forfeiture routine.
    /// </summary>
    public bool MandatoryAnnualLeave { get; set; }

    // ===== ENCASHMENT RATE POLICY (Phase 4) =====

    /// <summary>
    /// How the per-day encashment rate is derived for this leave type:
    /// <see cref="EncashmentRateBasis.DerivedFromEmoluments"/> = (monthly basic + linked
    /// allowances) / <see cref="EncashmentWorkingDaysPerMonth"/>; or
    /// <see cref="EncashmentRateBasis.Manual"/> = the fixed <see cref="EncashmentRatePerDay"/>.
    /// </summary>
    public EncashmentRateBasis EncashmentRateBasis { get; set; } = EncashmentRateBasis.DerivedFromEmoluments;

    /// <summary>Manual per-day encashment rate, used when <see cref="EncashmentRateBasis"/> is Manual.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EncashmentRatePerDay { get; set; }

    /// <summary>Working-days-per-month divisor used to turn a monthly emolument into a daily rate.</summary>
    public int EncashmentWorkingDaysPerMonth { get; set; } = 22;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual List<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; } = new List<LeaveCategoryAllocation>();
    public virtual List<LeaveSubType> LeaveSubTypes { get; set; } = new List<LeaveSubType>();
    public virtual ICollection<LeaveTypeEligibility> EligibilityRules { get; set; } = new List<LeaveTypeEligibility>();
    public virtual ICollection<LeaveAccrualPolicy> AccrualPolicies { get; set; } = new List<LeaveAccrualPolicy>();
    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    /// <summary>Allowance pay components that feed this leave type's derived encashment rate (Phase 4).</summary>
    public virtual ICollection<LeaveTypeAllowance> LeaveTypeAllowances { get; set; } = new List<LeaveTypeAllowance>();
}

/// <summary>
/// Join row linking a leave type to an allowance <see cref="PayComponent"/> whose value is added
/// to basic pay when deriving the leave type's per-day encashment rate (Phase 4, comment 13).
/// </summary>
public class LeaveTypeAllowance : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public Guid PayComponentId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(PayComponentId))]
    public virtual PayComponent PayComponent { get; set; } = null!;
}

/// <summary>
/// Represents specific subtypes of leave under a general leave type.
/// </summary>
public class LeaveSubType : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SubTypeName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Caps days for this subtype. Takes precedence over LeaveCategoryAllocation
    /// when both exist — enforce this rule in the domain/service layer.
    /// </summary>
    public int? MaxDaysAllowed { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    public virtual List<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; } = new List<LeaveCategoryAllocation>();
    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public virtual ICollection<LeavePlan> LeavePlans { get; set; } = new List<LeavePlan>();
}

/// <summary>
/// Leave day allocations by staff level, optionally scoped to a subtype.
/// When LeaveSubType.MaxDaysAllowed is also set, the subtype cap takes precedence.
/// </summary>
public class LeaveCategoryAllocation : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public Guid StaffLevelId { get; set; }

    public int AllocationDays { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    
    public DateOnly? EffectiveTo { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel StaffLevel { get; set; } = null!;
}

public class LeaveTypeEligibility : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public LeaveEligibilityType EligibilityType { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public Gender? Gender { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }
}

public class LeaveAccrualPolicy : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public AccrualFrequency Frequency { get; set; } = AccrualFrequency.Monthly;

    /// <summary>
    /// How the annual entitlement is released over the year (incrementally vs full-on-eligibility).
    /// </summary>
    public AccrualMode Mode { get; set; } = AccrualMode.AccrueIncrementally;

    [Column(TypeName = "decimal(5,2)")]
    public decimal AccrualRate { get; set; }

    public int? MinServiceMonths { get; set; } // Minimum months of service before accrual starts

    public bool ProRateOnJoin { get; set; }

    public bool ProRateOnExit { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;
}

/// <summary>
/// Employee leave balances per leave type per year
/// </summary>
public class LeaveBalance : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public int Year { get; set; }

    // ===== CORE VALUES (FROM POLICY / SYSTEM) =====
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal EntitledDays { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CarriedOverDays { get; set; }

    // ===== COMPUTED / AGGREGATED VALUES (CACHED) =====
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal UsedDays { get; set; } // From approved LeaveRequests

    [Column(TypeName = "decimal(5,2)")]
    public decimal PendingDays { get; set; } // From pending LeaveRequests

    [Column(TypeName = "decimal(5,2)")]
    public decimal AdjustmentDays { get; set; } // SUM of LeaveAdjustment.Days

    [Column(TypeName = "decimal(5,2)")]
    public decimal EncashedDays { get; set; } // SUM of processed LeaveEncashment.DaysEncashed

    // ===== COMPUTED PROPERTY (DO NOT STORE IN DB) =====

    [NotMapped]
    public decimal AvailableDays => EntitledDays + CarriedOverDays + AdjustmentDays - UsedDays - PendingDays - EncashedDays;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    public virtual ICollection<LeaveAdjustment> Adjustments { get; set; } = new List<LeaveAdjustment>();
}

/// <summary>
/// Represents an adjustment to an employee's leave balance
/// </summary>
public class LeaveAdjustment : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Days { get; set; } // + or -

    /// <summary>Optional structured reason from the system-wide <see cref="ReasonCode"/> lookup.</summary>
    public Guid? ReasonCodeId { get; set; }

    /// <summary>Free-text remarks (surfaced in the UI as "Remarks" alongside the reason code).</summary>
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTime AdjustmentDate { get; set; }

    public Guid PerformedBy { get; set; }

    public Guid LeaveBalanceId { get; set; }

    [ForeignKey(nameof(LeaveBalanceId))]
    public virtual LeaveBalance LeaveBalance { get; set; } = null!;

    [ForeignKey(nameof(PerformedBy))]
    public virtual Employee PerformedByEmployee { get; set; } = null!;

    [ForeignKey(nameof(ReasonCodeId))]
    public virtual ReasonCode? ReasonCode { get; set; }
}

/// <summary>
/// Represents an employee's planned leave schedule within a specific year
/// </summary>
public class LeavePlan : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public Guid? RelieverId { get; set; }

    /// <summary>Second reliever/backup; pre-defined relievers populate both slots by priority.</summary>
    public Guid? SecondRelieverId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid PlannedBy { get; set; }

    public int Year { get; set; }

    public LeavePlanStatus Status { get; set; } = LeavePlanStatus.Draft;

    // Self-service proposal flow — manager-suggested alternative dates (status ChangesSuggested)
    public DateOnly? SuggestedStartDate { get; set; }
    public DateOnly? SuggestedEndDate { get; set; }

    [MaxLength(1000)]
    public string? ManagerSuggestionNotes { get; set; }

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(RelieverId))]
    public virtual Employee? RelieverEmployee { get; set; }

    [ForeignKey(nameof(SecondRelieverId))]
    public virtual Employee? SecondRelieverEmployee { get; set; }

    [ForeignKey(nameof(PlannedBy))]
    public virtual Employee? PlannedByEmployee { get; set; }
}

/// <summary>
/// Employee leave application
/// </summary>
public class LeaveRequest : TenantEntity
{
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TotalDays { get; set; } // Calculated based on policy

    public DateTime RequestDate { get; set; }

    public Guid? LeavePlanId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Reliever/covering staff
    public Guid? RelieverEmployeeId { get; set; }

    /// <summary>Second reliever/backup; pre-defined relievers populate both slots by priority.</summary>
    public Guid? SecondRelieverEmployeeId { get; set; }

    [MaxLength(500)]
    public string? RelieverNotes { get; set; }

    [MaxLength(2000)]
    public string? HandoverNotes { get; set; } // Work handover details

    // Closure
    public DateTime? ClosureDate { get; set; }

    [MaxLength(500)]
    public string? ClosureNotes { get; set; }

    public DateTime? CancellationDate { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(RelieverEmployeeId))]
    public virtual Employee? RelieverEmployee { get; set; }

    [ForeignKey(nameof(SecondRelieverEmployeeId))]
    public virtual Employee? SecondRelieverEmployee { get; set; }

    [ForeignKey(nameof(LeavePlanId))]
    public virtual LeavePlan? LeavePlan { get; set; }

    /// <summary>Cash conversion record if leave was encashed</summary>
    public virtual LeaveEncashment? Encashment { get; set; }

    public virtual ICollection<LeaveRequestAttachment> Attachments { get; set; } = new List<LeaveRequestAttachment>();

    /// <summary>Attendance days that were recorded as leave against this request.</summary>
    public virtual ICollection<StaffDailyAttendance> AttendanceDays { get; set; } = new List<StaffDailyAttendance>();
}

public class LeaveRequestAttachment : TenantEntity
{
    public Guid LeaveRequestId { get; set; }

    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTime UploadedDate { get; set; }

    public Guid UploadedBy { get; set; }

    [ForeignKey(nameof(LeaveRequestId))]
    public virtual LeaveRequest LeaveRequest { get; set; } = null!;

    [ForeignKey(nameof(UploadedBy))]
    public virtual Employee UploadedByEmployee { get; set; } = null!;
}

/// <summary>
/// Records a cash conversion transaction when AllowCashConversion is true on the LeaveType.
/// </summary>
public class LeaveEncashment : TenantEntity
{
    public Guid LeaveRequestId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DaysEncashed { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    public LeaveEncashmentStatus Status { get; set; } = LeaveEncashmentStatus.Draft;

    // Set after payment is made (null until then)
    public DateTime? ProcessedDate { get; set; }

    public Guid? ProcessedByEmployeeId { get; set; }

    [MaxLength(500)]
    public string? PaymentReference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(LeaveRequestId))]
    public virtual LeaveRequest LeaveRequest { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(ProcessedByEmployeeId))]
    public virtual Employee? ProcessedByEmployee { get; set; }
}

/// <summary>
/// Pre-defined reliever for an employee, configured on the employee profile. Leave plans and
/// requests auto-populate their reliever slots from these (by <see cref="Priority"/>); the user
/// can still override when there is a clash. Tenant-scoped.
/// </summary>
public class EmployeeReliever : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid RelieverEmployeeId { get; set; }

    /// <summary>1 = primary reliever, 2 = backup. Drives which leave slot it fills.</summary>
    public int Priority { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(RelieverEmployeeId))]
    public virtual Employee RelieverEmployee { get; set; } = null!;
}
