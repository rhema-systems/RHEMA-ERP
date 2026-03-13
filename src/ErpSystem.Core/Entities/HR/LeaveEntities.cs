using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

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

    public int? MaxConsecutiveDays { get; set; }

    public int? MinDaysNotice { get; set; } // Minimum notice period in days

    public bool RequiresApproval { get; set; } = true;

    public bool RequiresDocumentation { get; set; }

    [MaxLength(50)]
    public string? CalendarColor { get; set; }

    public bool HasSubTypes { get; set; }

    public bool AppliesToAllCategories { get; set; }

    // Policy constraints
    public bool AllowCarryOver { get; set; }

    public int? MaxCarryOverDays { get; set; }

    public bool CountWeekendsAsLeave { get; set; } = true;

    public bool CountHolidaysAsLeave { get; set; } = false;

    public bool AllowCashConversion { get; set; }

    // Targeted policies - JSON or separate linking table
    [MaxLength(2000)]
    public string? ApplicableToGenders { get; set; } // JSON array or comma-separated

    [MaxLength(2000)]
    public string? ApplicableToDepartments { get; set; } // JSON array of GUIDs

    [MaxLength(2000)]
    public string? ApplicableToEmploymentTypes { get; set; } // JSON array

    [MaxLength(2000)]
    public string? ApplicableToPositions { get; set; } // JSON array of GUIDs    

    // Navigation properties
    public virtual List<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; } = new List<LeaveCategoryAllocation>();
    public virtual List<LeaveSubType> LeaveSubTypes { get; set; } = new List<LeaveSubType>();
    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
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

    public int MaxDaysAllowed { get; set; }

    public virtual LeaveType LeaveType { get; set; } = null!;
}

/// <summary>
/// Represents the allocation of leave days based on staff category.
/// </summary>
public class LeaveCategoryAllocation : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public Guid StaffLevelId { get; set; }

    public int AllocationDays { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel StaffLevel {get; set; } = null!;
}

/// <summary>
/// Employee leave balances per leave type per year
/// </summary>
public class LeaveBalance : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal EntitledDays { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal UsedDays { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CarriedOverDays { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal AdjustmentDays { get; set; } // Manual adjustments

    [MaxLength(500)]
    public string? AdjustmentReason { get; set; }

    // Computed property
    [NotMapped]
    public decimal AvailableDays => EntitledDays + CarriedOverDays + AdjustmentDays - UsedDays;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;
}

/// <summary>
/// Represents an employee's planned leave schedule within a specific year
/// </summary>
public class LeavePlan : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public Guid RelieverId { get; set; }

    public Guid PlannedBy { get; set; }

    public int Year { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department Department { get; set; } = null!;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    public virtual Employee? RelieverEmployee { get; set; }

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

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TotalDays { get; set; } // Calculated based on policy

    public DateTime RequestDate { get; set; }

    public Guid? LeavePlanId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    // Reliever/covering staff
    public Guid? RelieverEmployeeId { get; set; }

    [MaxLength(500)]
    public string? RelieverNotes { get; set; }

    // Approval workflow
    public Guid? ApprovedByEmployeeId { get; set; }

    public DateTime? ApprovalDate { get; set; }

    [MaxLength(1000)]
    public string? ApprovalNotes { get; set; }

    public DateTime? RejectionDate { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Closure
    public DateTime? ClosureDate { get; set; }

    [MaxLength(500)]
    public string? ClosureNotes { get; set; }

    public DateTime? CancellationDate { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    // Computed
    [NotMapped]
    public bool IsActive => Status == LeaveStatus.Approved && StartDate <= DateTime.Today && EndDate >= DateTime.Today;

    public virtual Employee Employee { get; set; } = null!;

    public virtual LeaveType LeaveType { get; set; } = null!;

    public virtual Employee? RelieverEmployee { get; set; }

    public virtual Employee? ApprovedByEmployee { get; set; }

    public virtual LeavePlan? LeavePlan { get; set; }
}

/// <summary>
/// National/company holidays
/// </summary>
public class PublicHoliday : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int Year { get; set; }

    public bool IsRecurring { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsOptional { get; set; } // Some companies have optional holidays
}
