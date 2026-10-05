using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffAttendance;

// =========================================================================
// AttendanceRecord
// Lightweight daily record for simple clock-in/out scenarios or as a
// reporting projection of DailyAttendance.
// =========================================================================

public class StaffAttendanceRecord : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }
	
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

    public DateOnly Date { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public double? WorkedHours { get; set; }

    public double? OvertimeHours { get; set; }

    public StaffAttendanceStatus Status { get; set; } = StaffAttendanceStatus.Present;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// =========================================================================
// DailyAttendance
// Full-detail daily record. Central entity for all attendance operations:
// tardiness, breaks, overtime, location, remote work, device tracking,
// leave/holiday references, verification, and exceptions.
// =========================================================================

public class StaffDailyAttendance : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	public DateOnly AttendanceDate { get; set; }

	public DayOfWeek DayOfWeek { get; set; }

	// ── Work Schedule ─────────────────────────────────────────────────────

	public Guid? WorkScheduleId { get; set; }
	public TimeSpan? ScheduledStartTime { get; set; }
	public TimeSpan? ScheduledEndTime { get; set; }
	public decimal? ScheduledWorkHours { get; set; }

	// ── Actual Times ──────────────────────────────────────────────────────

	public TimeSpan? ActualCheckInTime { get; set; }
	public TimeSpan? ActualCheckOutTime { get; set; }
	public decimal? ActualWorkHours { get; set; }

	// ── Status ────────────────────────────────────────────────────────────

	public StaffAttendanceStatus Status { get; set; }

	[MaxLength(500)]
	public string? StatusReason { get; set; }

	// ── Tardiness ─────────────────────────────────────────────────────────

	public bool IsLate { get; set; }

	/// <summary>Minutes late beyond the grace period.</summary>
	public int? LateMinutes { get; set; }

	public bool IsEarlyDeparture { get; set; }
	public int? EarlyDepartureMinutes { get; set; }

	// ── Breaks ────────────────────────────────────────────────────────────

	public TimeSpan? BreakStartTime { get; set; }
	public TimeSpan? BreakEndTime { get; set; }
	public int? TotalBreakMinutes { get; set; }

	// ── Overtime ──────────────────────────────────────────────────────────

	public bool IsOvertime { get; set; }
	public decimal? OvertimeHours { get; set; }
	public bool OvertimeApproved { get; set; }
	public Guid? OvertimeApprovedById { get; set; }

	// ── Location ──────────────────────────────────────────────────────────

	public Guid? LocationId { get; set; }

	[MaxLength(500)]
	public string? CheckInLocation { get; set; }

	[MaxLength(500)]
	public string? CheckOutLocation { get; set; }

	/// <summary>GPS latitude at check-in (populated when geofencing is enabled).</summary>
	public double? CheckInLatitude { get; set; }

	/// <summary>GPS longitude at check-in.</summary>
	public double? CheckInLongitude { get; set; }

	/// <summary>GPS latitude at check-out.</summary>
	public double? CheckOutLatitude { get; set; }

	/// <summary>GPS longitude at check-out.</summary>
	public double? CheckOutLongitude { get; set; }

	public LocationVerificationStatus CheckInLocationStatus { get; set; } = LocationVerificationStatus.Unverified;
	public LocationVerificationStatus CheckOutLocationStatus { get; set; } = LocationVerificationStatus.Unverified;

	/// <summary>FK to the geofence zone where check-in occurred.</summary>
	public Guid? CheckInGeofenceZoneId { get; set; }

	// ── Remote Work ───────────────────────────────────────────────────────

	public bool IsRemoteWork { get; set; }

	[MaxLength(500)]
	public string? RemoteWorkLocation { get; set; }

	/// <summary>FK to the approved RemoteWorkRequest that covers this day.</summary>
	public Guid? RemoteWorkRequestId { get; set; }

	// ── Device / IP ───────────────────────────────────────────────────────

	[MaxLength(200)]
	public string? CheckInDevice { get; set; }

	[MaxLength(50)]
	public string? CheckInIpAddress { get; set; }

	[MaxLength(200)]
	public string? CheckOutDevice { get; set; }

	[MaxLength(50)]
	public string? CheckOutIpAddress { get; set; }

	// ── Leave & Holiday References ────────────────────────────────────────

	public Guid? LeaveRequestId { get; set; }
	public Guid? PublicHolidayId { get; set; }

	/// <summary>
	/// The staff-travel request that posted this day as <c>OnDuty</c> (travel final closure, lane 9, D-53) — how travel's
	/// posting knows its own rows, as leave's knows its own by <see cref="LeaveRequestId"/>. Set-null if the trip is deleted.
	/// </summary>
	public Guid? StaffTravelRequestId { get; set; }

	// ── Pay Period ────────────────────────────────────────────────────────

	/// <summary>FK to the PayPeriod this attendance day belongs to.</summary>
	public Guid? PayPeriodId { get; set; }

	// ── Verification ──────────────────────────────────────────────────────

	public bool RequiresVerification { get; set; }
	public bool IsVerified { get; set; }
	public DateTime? VerifiedDate { get; set; }
	public Guid? VerifiedById { get; set; }

	[MaxLength(1000)]
	public string? VerificationNotes { get; set; }

	// ── Exceptions ────────────────────────────────────────────────────────

	public bool HasException { get; set; }

	[MaxLength(1000)]
	public string? ExceptionReason { get; set; }

	public bool ExceptionApproved { get; set; }
	public Guid? ExceptionApprovedById { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// ── Navigation ────────────────────────────────────────────────────────

	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(WorkScheduleId))]
	public virtual WorkSchedule? WorkSchedule { get; set; }

	[ForeignKey(nameof(LocationId))]
	public virtual Location? Location { get; set; }

	[ForeignKey(nameof(CheckInGeofenceZoneId))]
	public virtual GeofenceZone? CheckInGeofenceZone { get; set; }

	[ForeignKey(nameof(RemoteWorkRequestId))]
	public virtual RemoteWorkRequest? RemoteWorkRequest { get; set; }

	[ForeignKey(nameof(PayPeriodId))]
	public virtual PayPeriod? PayPeriod { get; set; }

	[ForeignKey(nameof(LeaveRequestId))]
	public virtual LeaveRequest? LeaveRequest { get; set; }

	[ForeignKey(nameof(PublicHolidayId))]
	public virtual PublicHoliday? PublicHoliday { get; set; }

	[ForeignKey(nameof(OvertimeApprovedById))]
	public virtual Employee? OvertimeApprovedBy { get; set; }

	[ForeignKey(nameof(VerifiedById))]
	public virtual Employee? VerifiedBy { get; set; }

	[ForeignKey(nameof(ExceptionApprovedById))]
	public virtual Employee? ExceptionApprovedBy { get; set; }

	public virtual ICollection<StaffAttendanceLog> AttendanceLogs { get; set; } = new List<StaffAttendanceLog>();
	public virtual ICollection<StaffAttendanceRegularization> Regularizations { get; set; } = new List<StaffAttendanceRegularization>();
}

// =========================================================================
// AttendanceLog
// Raw punch log from biometric or other devices. A background job
// processes these into DailyAttendance records.
// =========================================================================

public class StaffAttendanceLog : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	public DateTime LogDateTime { get; set; }

	public AttendanceLogType LogType { get; set; }

	public Guid? DeviceId { get; set; }

	[MaxLength(100)]
	public string? DeviceSerialNumber { get; set; }

	[MaxLength(500)]
	public string? Location { get; set; }

	/// <summary>GPS latitude captured at the device/app at punch time.</summary>
	public double? Latitude { get; set; }

	/// <summary>GPS longitude captured at the device/app at punch time.</summary>
	public double? Longitude { get; set; }

	/// <summary>True once converted to a DailyAttendance record.</summary>
	public bool IsProcessed { get; set; }

	public DateTime? ProcessedDate { get; set; }

	public Guid? AttendanceId { get; set; }

	/// <summary>Original device payload (JSON / hex) for debugging.</summary>
	[MaxLength(2000)]
	public string? RawData { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(DeviceId))]
	public virtual StaffAttendanceDevice? Device { get; set; }

	[ForeignKey(nameof(AttendanceId))]
	public virtual StaffDailyAttendance? Attendance { get; set; }

	public virtual ICollection<AttendanceLocationVerificationLog> VerificationLogs { get; set; } = new List<AttendanceLocationVerificationLog>();
}

// =========================================================================
// AttendanceRegularization
// Employee request to correct a DailyAttendance record.
// Types: missing punch, wrong time, forgot to mark, system error.
// =========================================================================

public class StaffAttendanceRegularization : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string RegularizationNumber { get; set; } = string.Empty;

	[Required]
	public Guid EmployeeId { get; set; }

	[Required]
	public Guid AttendanceId { get; set; }

	public DateTime RequestDate { get; set; }
	public DateOnly AttendanceDate { get; set; }

	public RegularizationType Type { get; set; }

	public TimeSpan? RequestedCheckInTime { get; set; }
	public TimeSpan? RequestedCheckOutTime { get; set; }

	[Required]
	[MaxLength(1000)]
	public string Reason { get; set; } = string.Empty;

	/// <summary>Comma-separated file paths / document references.</summary>
	[MaxLength(2000)]
	public string? SupportingDocuments { get; set; }

	public AttendanceRegularizationStatus Status { get; set; } = AttendanceRegularizationStatus.Pending;

	// Approval
	public Guid? ApprovedById { get; set; }
	public DateTime? ApprovalDate { get; set; }

	[MaxLength(1000)]
	public string? ApprovalComments { get; set; }

	// Rejection
	public DateTime? RejectedDate { get; set; }

	[MaxLength(1000)]
	public string? RejectionReason { get; set; }

	// Application
	/// <summary>True once the approved correction has been written back to DailyAttendance.</summary>
	public bool IsApplied { get; set; }
	public DateTime? AppliedDate { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(AttendanceId))]
	public virtual StaffDailyAttendance Attendance { get; set; } = null!;

	[ForeignKey(nameof(ApprovedById))]
	public virtual Employee? ApprovedBy { get; set; }
}

// =========================================================================
// MonthlyAttendanceSummary
// Aggregated monthly roll-up per employee. Finalized by HR at month-end
// and consumed by payroll integration.
// =========================================================================

public class StaffMonthlyAttendanceSummary : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	public int Year { get; set; }
	public int Month { get; set; }

	// ── Day Counts ────────────────────────────────────────────────────────

	public int TotalWorkingDays { get; set; }
	public int DaysPresent { get; set; }
	public int DaysAbsent { get; set; }
	public int DaysOnLeave { get; set; }
	public int DaysLate { get; set; }
	public int DaysRemoteWork { get; set; }
	public int PublicHolidays { get; set; }
	public int Weekends { get; set; }
	public int DaysHalfDay { get; set; }

	// ── Hours ─────────────────────────────────────────────────────────────

	public decimal TotalScheduledHours { get; set; }
	public decimal TotalWorkedHours { get; set; }
	public decimal TotalOvertimeHours { get; set; }
	public decimal TotalUndertimeHours { get; set; }
	public decimal TotalBreakHours { get; set; }

	// ── Tardiness ─────────────────────────────────────────────────────────

	public int TotalLateMinutes { get; set; }
	public int NumberOfLateDays { get; set; }
	public int TotalEarlyDepartureMinutes { get; set; }
	public int NumberOfEarlyDepartureDays { get; set; }

	// ── Percentages ───────────────────────────────────────────────────────

	/// <summary>DaysPresent / TotalWorkingDays × 100</summary>
	public decimal AttendancePercentage { get; set; }

	/// <summary>(DaysPresent - DaysLate) / DaysPresent × 100</summary>
	public decimal PunctualityPercentage { get; set; }

	// ── Pay Period Reference ──────────────────────────────────────────────

	public Guid? PayPeriodId { get; set; }

	// ── Finalization ──────────────────────────────────────────────────────

	public bool IsFinalized { get; set; }
	public DateTime? FinalizedDate { get; set; }
	public Guid? FinalizedById { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(FinalizedById))]
	public virtual Employee? FinalizedBy { get; set; }

	[ForeignKey(nameof(PayPeriodId))]
	public virtual PayPeriod? PayPeriod { get; set; }
}

// =========================================================================
// BulkAttendanceImport
// Tracks mass attendance import batches for audit and error reporting.
// =========================================================================

public class StaffBulkAttendanceImport : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string ImportReference { get; set; } = string.Empty;

	[Required]
	public Guid ImportedById { get; set; }

	public DateTime ImportDate { get; set; }

	[MaxLength(500)]
	public string? SourceFileName { get; set; }

	public AttendanceImportSourceType SourceType { get; set; }

	public int TotalRows { get; set; }
	public int SuccessCount { get; set; }
	public int FailureCount { get; set; }

	public AttendanceImportStatus Status { get; set; } = AttendanceImportStatus.Pending;

	/// <summary>JSON summary of validation errors keyed by row number.</summary>
	[MaxLength(8000)]
	public string? ErrorSummary { get; set; }

	public DateTime? CompletedDate { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(ImportedById))]
	public virtual Employee ImportedBy { get; set; } = null!;

	public virtual ICollection<StaffBulkAttendanceImportRow> ImportRows { get; set; } = new List<StaffBulkAttendanceImportRow>();
}

// =========================================================================
// BulkAttendanceImportRow
// Individual row result within a bulk import batch.
// =========================================================================

public class StaffBulkAttendanceImportRow : TenantEntity
{
	[Required]
	public Guid ImportId { get; set; }

	public int RowNumber { get; set; }

	/// <summary>Resolved employee (null if employee ID could not be matched).</summary>
	public Guid? EmployeeId { get; set; }

	/// <summary>Original raw row content stored as JSON or CSV string for audit.</summary>
	[Required]
	[MaxLength(4000)]
	public string RawData { get; set; } = string.Empty;

	public DateOnly? AttendanceDate { get; set; }
	public TimeOnly? CheckInTime { get; set; }
	public TimeOnly? CheckOutTime { get; set; }

	public bool IsSuccess { get; set; }

	[MaxLength(2000)]
	public string? ErrorMessage { get; set; }

	/// <summary>FK to the DailyAttendance record created from this row (set on success).</summary>
	public Guid? CreatedAttendanceId { get; set; }

	// Navigation
	[ForeignKey(nameof(ImportId))]
	public virtual StaffBulkAttendanceImport Import { get; set; } = null!;

	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee? Employee { get; set; }

	[ForeignKey(nameof(CreatedAttendanceId))]
	public virtual StaffDailyAttendance? CreatedAttendance { get; set; }
}

// =========================================================================
// WorkSchedule
// Defines a named work schedule — fixed, flexible, shift-based, or
// compressed. Holds all rules about hours, breaks, overtime caps,
// and grace periods.
// =========================================================================

public class WorkSchedule : TenantEntity
{
	[Required]
	[MaxLength(150)]
	public string ScheduleName { get; set; } = string.Empty;

	[MaxLength(1000)]
	public string? Description { get; set; }

	public WorkScheduleType Type { get; set; }

	/// <summary>True if this is the tenant-wide default schedule.</summary>
	public bool IsDefault { get; set; }

	public bool IsActive { get; set; } = true;

	// ── Standard Hours ────────────────────────────────────────────────────

	public TimeSpan StandardStartTime { get; set; }
	public TimeSpan StandardEndTime { get; set; }
	public decimal StandardHoursPerDay { get; set; }
	public decimal StandardHoursPerWeek { get; set; }

	// ── Flexible Time ─────────────────────────────────────────────────────

	public bool HasFlexibleStartTime { get; set; }
	public TimeSpan? FlexibleStartTimeEarliest { get; set; }
	public TimeSpan? FlexibleStartTimeLatest { get; set; }

	public bool HasFlexibleEndTime { get; set; }
	public TimeSpan? FlexibleEndTimeEarliest { get; set; }
	public TimeSpan? FlexibleEndTimeLatest { get; set; }

	// ── Core Hours (flexible schedules) ───────────────────────────────────

	/// <summary>
	/// Core hours define the window where all flexible employees must be present.
	/// </summary>
	public bool HasCoreHours { get; set; }
	public TimeSpan? CoreHoursStart { get; set; }
	public TimeSpan? CoreHoursEnd { get; set; }

	// ── Breaks ────────────────────────────────────────────────────────────

	public bool HasMandatoryBreak { get; set; }
	public int? BreakDurationMinutes { get; set; }
	public bool IsBreakPaid { get; set; }

	// ── Working Days ──────────────────────────────────────────────────────

	public bool WorksMonday { get; set; } = true;
	public bool WorksTuesday { get; set; } = true;
	public bool WorksWednesday { get; set; } = true;
	public bool WorksThursday { get; set; } = true;
	public bool WorksFriday { get; set; } = true;
	public bool WorksSaturday { get; set; }
	public bool WorksSunday { get; set; }

	// ── Overtime Rules ────────────────────────────────────────────────────

	public bool AllowsOvertime { get; set; }
	public bool OvertimeRequiresPreApproval { get; set; }
	public decimal? MaxOvertimeHoursPerDay { get; set; }
	public decimal? MaxOvertimeHoursPerWeek { get; set; }

	// ── Grace Periods ─────────────────────────────────────────────────────

	/// <summary>Minutes past start before an arrival is marked as late.</summary>
	public int? LateGracePeriodMinutes { get; set; }

	/// <summary>Minutes before scheduled end that early departure is still accepted.</summary>
	public int? EarlyDepartureGracePeriodMinutes { get; set; }

	// Navigation
	public virtual ICollection<EmployeeWorkSchedule> EmployeeSchedules { get; set; } = new List<EmployeeWorkSchedule>();
	public virtual ICollection<ShiftDefinition> Shifts { get; set; } = new List<ShiftDefinition>();
}

// =========================================================================
// EmployeeWorkSchedule
// Assignment of a WorkSchedule to an Employee for a date range.
// IsCurrent flags the active assignment.
// =========================================================================

public class EmployeeWorkSchedule : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	[Required]
	public Guid WorkScheduleId { get; set; }

	public DateOnly EffectiveDate { get; set; }

	/// <summary>Null means open-ended (still active).</summary>
	public DateOnly? EndDate { get; set; }

	public bool IsCurrent { get; set; }

	[MaxLength(500)]
	public string? AssignmentReason { get; set; }

	public Guid? AssignedById { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(WorkScheduleId))]
	public virtual WorkSchedule WorkSchedule { get; set; } = null!;

	[ForeignKey(nameof(AssignedById))]
	public virtual Employee? AssignedBy { get; set; }
}

 // =========================================================================
// ShiftDefinition
// A named shift within a WorkSchedule (Morning, Afternoon, Night, etc.).
// Carries night allowance and overtime eligibility flags at the shift level.
// =========================================================================

public class ShiftDefinition : TenantEntity
{
	[Required]
	public Guid WorkScheduleId { get; set; }

	[Required]
	[MaxLength(100)]
	public string ShiftName { get; set; } = string.Empty;

	[MaxLength(500)]
	public string? Description { get; set; }

	public ShiftType Type { get; set; }

	public TimeSpan StartTime { get; set; }
	public TimeSpan EndTime { get; set; }

	/// <summary>Total shift duration in hours (can span midnight).</summary>
	public decimal ShiftHours { get; set; }

	// ── Night Shift ───────────────────────────────────────────────────────

	public bool IsNightShift { get; set; }

	/// <summary>
	/// True if employees on this shift attract a night allowance payment.
	/// Position-level exemptions in PositionAllowancePolicy can override this.
	/// </summary>
	public bool AttractsNightAllowance { get; set; }

	// ── Overtime ──────────────────────────────────────────────────────────

	/// <summary>
	/// True if overtime is permitted on this shift.
	/// Position-level eligibility in PositionAllowancePolicy also applies.
	/// </summary>
	public bool AllowsOvertime { get; set; }

	// ── Shift Differential ────────────────────────────────────────────────

	public bool HasShiftDifferential { get; set; }

	/// <summary>Percentage added to base pay (e.g. 15 = 15%).</summary>
	public decimal? ShiftDifferentialPercentage { get; set; }

	public int DisplayOrder { get; set; }

	public bool IsActive { get; set; } = true;

	// Navigation
	[ForeignKey(nameof(WorkScheduleId))]
	public virtual WorkSchedule WorkSchedule { get; set; } = null!;

	public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
	public virtual ICollection<ShiftRotationStage> RotationStages { get; set; } = new List<ShiftRotationStage>();
	public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
}

// =========================================================================
// ShiftAssignment
// Assigns a ShiftDefinition to an Employee for a specific date window,
// with optional recurrence pattern.
// =========================================================================

public class ShiftAssignment : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	[Required]
	public Guid ShiftDefinitionId { get; set; }

	public DateTime AssignmentDate { get; set; }

	/// <summary>Null = open-ended assignment.</summary>
	public DateTime? EndDate { get; set; }

	public bool IsRecurring { get; set; }
	public RecurrencePattern? RecurrencePattern { get; set; }

	public Guid? AssignedById { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(ShiftDefinitionId))]
	public virtual ShiftDefinition ShiftDefinition { get; set; } = null!;

	[ForeignKey(nameof(AssignedById))]
	public virtual Employee? AssignedBy { get; set; }
}

// =========================================================================
// ShiftRotationPlan
// Defines a named rotation plan that cycles employees through a sequence
// of shifts on a regular interval (weekly, monthly, quarterly, etc.).
// =========================================================================

public class ShiftRotationPlan : TenantEntity
{
	[Required]
	[MaxLength(150)]
	public string PlanName { get; set; } = string.Empty;

	[MaxLength(1000)]
	public string? Description { get; set; }

	public ShiftRotationCycle RotationCycle { get; set; }

	/// <summary>
	/// Explicit cycle length in days — derived from RotationCycle but stored
	/// for easy querying (Weekly = 7, Fortnightly = 14, Monthly ≈ 30, etc.).
	/// </summary>
	public int CycleLengthDays { get; set; }

	public DateOnly StartDate { get; set; }

	/// <summary>Null = plan runs indefinitely.</summary>
	public DateOnly? EndDate { get; set; }

	public bool IsActive { get; set; } = true;

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	public virtual ICollection<ShiftRotationStage> Stages { get; set; } = new List<ShiftRotationStage>();
	public virtual ICollection<ShiftRotationMember> Members { get; set; } = new List<ShiftRotationMember>();
}

// =========================================================================
// ShiftRotationStage
// One stage in a ShiftRotationPlan. Defines which shift applies at
// position N in the cycle.
// =========================================================================

public class ShiftRotationStage : TenantEntity
{
	[Required]
	public Guid ShiftRotationPlanId { get; set; }

	/// <summary>Sequence position in the rotation (1, 2, 3 …).</summary>
	public int StageOrder { get; set; }

	[Required]
	public Guid ShiftDefinitionId { get; set; }

	/// <summary>
	/// Number of rotation cycles this stage lasts before advancing to the next.
	/// Normally 1. Set to 2 for "two months on nights before rotating".
	/// </summary>
	public int DurationCycles { get; set; } = 1;

	/// <summary>Optional label, e.g. "Week 1 – Morning Shift".</summary>
	[MaxLength(200)]
	public string? Label { get; set; }

	// Navigation
	[ForeignKey(nameof(ShiftRotationPlanId))]
	public virtual ShiftRotationPlan ShiftRotationPlan { get; set; } = null!;

	[ForeignKey(nameof(ShiftDefinitionId))]
	public virtual ShiftDefinition ShiftDefinition { get; set; } = null!;
}

// =========================================================================
// ShiftRotationMember
// Links an Employee (or a group via OrganizationUnitId / TeamId) to a
// ShiftRotationPlan, and tracks which stage they are currently on.
// =========================================================================

public class ShiftRotationMember : TenantEntity
{
	[Required]
	public Guid ShiftRotationPlanId { get; set; }

	/// <summary>Set when assigning an individual employee.</summary>
	public Guid? EmployeeId { get; set; }

	/// <summary>Set when assigning an entire organization unit.</summary>
	public Guid? OrganizationUnitId { get; set; }

	/// <summary>Set when assigning a team/group.</summary>
	public Guid? TeamId { get; set; }

	/// <summary>Which stage the employee / group is currently on (1-based).</summary>
	public int CurrentStageOrder { get; set; } = 1;

	public DateOnly JoinDate { get; set; }

	/// <summary>Null = still active on this plan.</summary>
	public DateOnly? ExitDate { get; set; }

	[MaxLength(500)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(ShiftRotationPlanId))]
	public virtual ShiftRotationPlan ShiftRotationPlan { get; set; } = null!;

	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee? Employee { get; set; }

	[ForeignKey(nameof(OrganizationUnitId))]
	public virtual OrganizationUnit? OrganizationUnit { get; set; }

	[ForeignKey(nameof(TeamId))]
	public virtual Team? Team { get; set; }
}

// =========================================================================
// PositionOvertimeAllowancePolicy
// Defines eligibility and exemptions for overtime, night allowance,
// shift differential, and other extras at the Position level.
// Avoids per-employee configuration overhead — set it once on the position
// and all holders inherit it. Use EmployeeOvertimeAllowanceOverride for exceptions.
// =========================================================================

public class PositionOvertimePolicy : TenantEntity
{
	/// <summary>FK to the Position entity in your HR core module.</summary>
	[Required]
	public Guid PositionId { get; set; }

	public OvertimeAllowanceType AllowanceType { get; set; }

	/// <summary>
	/// True if the position qualifies for this allowance by default.
	/// IsExempt takes precedence when both are true.
	/// </summary>
	public bool IsEligible { get; set; }

	/// <summary>
	/// True if the position is explicitly exempted from this allowance
	/// regardless of shift or schedule rules.
	/// </summary>
	public bool IsExempt { get; set; }

	/// <summary>Required when IsExempt = true.</summary>
	[MaxLength(1000)]
	public string? ExemptionReason { get; set; }

	/// <summary>Position-level daily cap (used for overtime allowance type).</summary>
	public decimal? MaxHoursPerDay { get; set; }

	/// <summary>Position-level weekly cap (used for overtime allowance type).</summary>
	public decimal? MaxHoursPerWeek { get; set; }

	/// <summary>
	/// When true, employees in this position must obtain pre-approval before
	/// working the overtime / attracting the allowance.
	/// </summary>
	public bool RequiresPreApproval { get; set; }

	public DateOnly EffectiveDate { get; set; }

	/// <summary>Null = policy applies indefinitely.</summary>
	public DateOnly? ExpiryDate { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(PositionId))]
	public virtual EmployeePosition? Position { get; set; }

	public virtual ICollection<EmployeeOvertimeOverride> EmployeeOverrides { get; set; } = new List<EmployeeOvertimeOverride>();
}

// =========================================================================
// EmployeeOvertimeOverride
// Per-employee override of a PositionAllowancePolicy.
// Use only when an individual's eligibility differs from their position's
// default. Every override must be approved and reasoned.
// =========================================================================

public class EmployeeOvertimeOverride : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	/// <summary>FK to the PositionOvertimePolicy this override is scoped to.</summary>
	public Guid? PolicyId { get; set; }

	public OvertimeAllowanceType AllowanceType { get; set; }

	/// <summary>Override value for IsEligible at the individual level.</summary>
	public bool IsEligible { get; set; }

	/// <summary>Override value for IsExempt at the individual level.</summary>
	public bool IsExempt { get; set; }

	[Required]
	[MaxLength(1000)]
	public string OverrideReason { get; set; } = string.Empty;

	[Required]
	public Guid ApprovedById { get; set; }

	public DateTime ApprovalDate { get; set; }

	public DateOnly EffectiveDate { get; set; }

	/// <summary>Null = override applies indefinitely.</summary>
	public DateOnly? ExpiryDate { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(PolicyId))]
	public virtual PositionOvertimePolicy? Policy { get; set; }

	[ForeignKey(nameof(ApprovedById))]
	public virtual Employee ApprovedBy { get; set; } = null!;
}

// =========================================================================
// OvertimeRequest
// Level 2 + 3 of the overtime workflow.
//   Level 2: Employee / supervisor submits a pre-approval request.
//   Level 3: Supervisor confirms actual hours worked after completion.
//
// The system should reject a request at submission if the employee's
// position does not have IsEligible = true in PositionOvertimePolicy
// for AllowanceType.Overtime (unless an EmployeeOvertimeOverride applies).
// =========================================================================

public class StaffOvertimeRequest : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string RequestNumber { get; set; } = string.Empty;

	[Required]
	public Guid EmployeeId { get; set; }

	public DateTime RequestDate { get; set; }
	public DateTime OvertimeDate { get; set; }

	// ── Planned Details ───────────────────────────────────────────────────

	public TimeSpan PlannedStartTime { get; set; }
	public TimeSpan PlannedEndTime { get; set; }
	public decimal PlannedOvertimeHours { get; set; }

	[Required]
	[MaxLength(1000)]
	public string Purpose { get; set; } = string.Empty;

	[MaxLength(2000)]
	public string? TaskDetails { get; set; }

	public OvertimeType Type { get; set; }
	public OvertimeRequestStatus Status { get; set; } = OvertimeRequestStatus.Pending;

	// ── Level 2: Pre-Approval ─────────────────────────────────────────────

	public Guid? ApprovedById { get; set; }
	public DateTime? ApprovalDate { get; set; }

	[MaxLength(1000)]
	public string? ApprovalComments { get; set; }

	public DateTime? RejectedDate { get; set; }

	[MaxLength(1000)]
	public string? RejectionReason { get; set; }

	// ── Level 3: Supervisor Confirmation (post-work) ──────────────────────

	/// <summary>
	/// Actual hours worked, filled in by the supervisor after the work is done.
	/// This is the value used for payroll calculation.
	/// </summary>
	public decimal? ActualOvertimeHours { get; set; }

	/// <summary>
	/// FK to the supervisor who confirms the actual work was performed.
	/// Completes level 3 of the workflow.
	/// </summary>
	public Guid? SupervisorConfirmedById { get; set; }

	public DateTime? SupervisorConfirmedDate { get; set; }

	[MaxLength(1000)]
	public string? SupervisorNotes { get; set; }

	// ── Attendance Link ───────────────────────────────────────────────────

	/// <summary>Linked to the DailyAttendance record once the overtime day is processed.</summary>
	public Guid? AttendanceId { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(ApprovedById))]
	public virtual Employee? ApprovedBy { get; set; }

	[ForeignKey(nameof(SupervisorConfirmedById))]
	public virtual Employee? SupervisorConfirmedBy { get; set; }

	[ForeignKey(nameof(AttendanceId))]
	public virtual StaffDailyAttendance? Attendance { get; set; }
}

// =========================================================================
// EmployeeBiometric
// Stores a biometric template enrolled for an employee.
// Extended with per-part capture metadata, quality scoring, template
// format standard, and enrolment / revocation audit trail.
// =========================================================================

public class EmployeeBiometric : TenantEntity
{
	[Required]
	public Guid EmployeeId { get; set; }

	public BiometricType BiometricType { get; set; }

	/// <summary>
	/// Specific body part captured, e.g. "RightIndexFinger", "LeftIris".
	/// Use FingerPosition enum for fingerprints; free text for other types.
	/// </summary>
	[MaxLength(100)]
	public string? BodyPart { get; set; }

	/// <summary>
	/// Strongly-typed finger position for fingerprint templates.
	/// Null for non-fingerprint biometric types.
	/// </summary>
	public FingerPosition? FingerPosition { get; set; }

	/// <summary>Base64-encoded biometric template or secure hash.</summary>
	[Required]
	[MaxLength(2000)]
	public string BiometricData { get; set; } = string.Empty;

	/// <summary>
	/// Template format standard, e.g. ISO-19794-2, ANSI-378, proprietary.
	/// Important for interoperability between devices and vendors.
	/// </summary>
	[MaxLength(100)]
	public string? TemplateFormat { get; set; }

	/// <summary>
	/// Capture quality score on a 0–100 scale (higher = better quality).
	/// Used to determine whether re-enrolment is needed.
	/// </summary>
	public int? QualityScore { get; set; }

	/// <summary>Device ID string used during enrolment.</summary>
	[MaxLength(100)]
	public string? DeviceId { get; set; }

	/// <summary>Model name of the device used for enrolment.</summary>
	[MaxLength(200)]
	public string? DeviceModel { get; set; }

	public DateTime EnrolledDate { get; set; } = DateTime.UtcNow;

	/// <summary>FK to the HR user who performed the enrolment.</summary>
	public Guid? EnrolledById { get; set; }

	public bool IsActive { get; set; } = true;

	/// <summary>Date the template was revoked. Populated when IsActive = false.</summary>
	public DateTime? RevokedDate { get; set; }

	[MaxLength(500)]
	public string? RevokedReason { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(EnrolledById))]
	public virtual Employee? EnrolledBy { get; set; }
}

// =========================================================================
// BiometricDevice
// Registered biometric or RFID device. Tracks network configuration,
// sync status, and the work station it is physically located at.
// =========================================================================

public class StaffAttendanceDevice : TenantEntity
{
	/// <summary>External device identifier string (from the device manufacturer).</summary>
	[Required]
	[MaxLength(100)]
	public string DeviceId { get; set; } = string.Empty;

	[Required]
	[MaxLength(200)]
	public string DeviceName { get; set; } = string.Empty;

	[MaxLength(200)]
	public string? DeviceModel { get; set; }

	[MaxLength(100)]
	public string? Manufacturer { get; set; }

	[MaxLength(100)]
	public string? FirmwareVersion { get; set; }

	public AttendanceDeviceType DeviceType { get; set; }

	/// <summary>FK to the work station / access point where this device is installed.</summary>
	public Guid? LocationId { get; set; }

	[Required]
	[MaxLength(500)]
	public string LocationDescription { get; set; } = string.Empty;

	[MaxLength(50)]
	public string? IpAddress { get; set; }

	public int? Port { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime? LastSyncDate { get; set; }

	/// <summary>
	/// Number of unprocessed punch logs still on the device awaiting sync.
	/// Updated each sync run.
	/// </summary>
	public int? PendingSyncCount { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(LocationId))]
	public virtual Location? Location { get; set; }
}

// =========================================================================
// GeofenceZone
// Defines an approved GPS zone within which employees must be located
// when clocking in or out. Supports circular and polygon shapes.
// The attendance processor validates AttendanceLog GPS coordinates against
// all active zones assigned to the employee's station.
// =========================================================================

public class GeofenceZone : TenantEntity
{
	[Required]
	[MaxLength(150)]
	public string ZoneName { get; set; } = string.Empty;

	[MaxLength(500)]
	public string? Description { get; set; }

	public GeofenceShape Shape { get; set; } = GeofenceShape.Circle;

	// ── Circular Zone ─────────────────────────────────────────────────────

	/// <summary>Centre latitude (used when Shape = Circle).</summary>
	public double? CentreLatitude { get; set; }

	/// <summary>Centre longitude (used when Shape = Circle).</summary>
	public double? CentreLongitude { get; set; }

	/// <summary>Radius in metres (used when Shape = Circle).</summary>
	public double? RadiusMetres { get; set; }

	// ── Polygon Zone ──────────────────────────────────────────────────────

	/// <summary>
	/// JSON array of {lat, lng} coordinate pairs defining the polygon boundary.
	/// Used when Shape = Polygon. Example: [{"lat":5.6,"lng":-0.18}, ...]
	/// </summary>
	[MaxLength(8000)]
	public string? PolygonCoordinatesJson { get; set; }

	// ── Settings ──────────────────────────────────────────────────────────

	/// <summary>When true, punches outside this zone are flagged but still accepted.</summary>
	public bool SoftEnforcement { get; set; } = true;

	/// <summary>When true, punches outside this zone are hard-rejected by the system.</summary>
	public bool HardEnforcement { get; set; }

	public bool IsActive { get; set; } = true;

	[MaxLength(500)]
	public string? Notes { get; set; }

	// Navigation
	public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
	public virtual ICollection<AttendanceLocationVerificationLog> VerificationLogs { get; set; } = new List<AttendanceLocationVerificationLog>();
}

// =========================================================================
// LocationVerificationLog
// Records the outcome of each GPS verification check performed when
// processing an AttendanceLog punch event.
// =========================================================================

public class AttendanceLocationVerificationLog : TenantEntity
{
	[Required]
	public Guid AttendanceLogId { get; set; }

	[Required]
	public Guid EmployeeId { get; set; }

	public DateTime VerificationDateTime { get; set; }

	public double Latitude { get; set; }
	public double Longitude { get; set; }

	/// <summary>FK to the zone that was checked (null if no zone was configured).</summary>
	public Guid? GeofenceZoneId { get; set; }

	/// <summary>Distance in metres between the punch location and the zone centre/boundary.</summary>
	public double? DistanceFromZoneMetres { get; set; }

	public LocationVerificationStatus Status { get; set; }

	[MaxLength(500)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(AttendanceLogId))]
	public virtual StaffAttendanceLog AttendanceLog { get; set; } = null!;

	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(GeofenceZoneId))]
	public virtual GeofenceZone? GeofenceZone { get; set; }
}

// =========================================================================
// RemoteWorkRequest
// Formal request for an employee to work from home or a remote location
// on specified dates. Must be approved before DailyAttendance can record
// IsRemoteWork = true for that day.
// =========================================================================

public class RemoteWorkRequest : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string RequestNumber { get; set; } = string.Empty;

	[Required]
	public Guid EmployeeId { get; set; }

	public DateTime RequestDate { get; set; }

	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }

	/// <summary>Total number of remote work days requested (excluding weekends / holidays).</summary>
	public int RequestedDays { get; set; }

	[Required]
	[MaxLength(1000)]
	public string Reason { get; set; } = string.Empty;

	[MaxLength(500)]
	public string? RemoteLocation { get; set; }

	/// <summary>True if employee has confirmed adequate home-office setup.</summary>
	public bool EquipmentConfirmed { get; set; }

	public RemoteWorkRequestStatus Status { get; set; } = RemoteWorkRequestStatus.Pending;

	// Approval
	public Guid? ApprovedById { get; set; }
	public DateTime? ApprovalDate { get; set; }

	[MaxLength(1000)]
	public string? ApprovalComments { get; set; }

	// Rejection
	public DateTime? RejectedDate { get; set; }

	[MaxLength(1000)]
	public string? RejectionReason { get; set; }

	// Navigation
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(ApprovedById))]
	public virtual Employee? ApprovedBy { get; set; }

	public virtual ICollection<StaffDailyAttendance> AttendanceDays { get; set; } = new List<StaffDailyAttendance>();
}

// =========================================================================
// HolidayCalendar
// Groups public holidays by country, region, or company policy.
// Employees are assigned a calendar (via their department or location)
// so the system knows which days are non-working for each person.
// =========================================================================

public class HolidayCalendar : TenantEntity
{
	[Required]
	[MaxLength(150)]
	public string CalendarName { get; set; } = string.Empty;

	[MaxLength(500)]
	public string? Description { get; set; }

    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

	/// <summary>Region or state within the country (if applicable).</summary>
	[MaxLength(100)]
	public string? Region { get; set; }

	public bool IsDefault { get; set; }

	public bool IsActive { get; set; } = true;

	// Navigation
	public virtual ICollection<PublicHoliday> PublicHolidays { get; set; } = new List<PublicHoliday>();
}

// =========================================================================
// PublicHoliday
// A single holiday entry within a HolidayCalendar.
// Supports mandatory, optional, and substitute observance types.
// =========================================================================

public class PublicHoliday : TenantEntity
{
	[Required]
	public Guid HolidayCalendarId { get; set; }

	[Required]
	[MaxLength(200)]
	public string HolidayName { get; set; } = string.Empty;

	[MaxLength(1000)]
	public string? Description { get; set; }

	public DateOnly DateFrom { get; set; }

	public DateOnly DateTo { get; set; }

	/// <summary>Derived from DateFrom.Year for convenient filtering without parsing the date.</summary>
	[NotMapped]
	public int Year => DateFrom.Year;

	public HolidayObservanceType ObservanceType { get; set; } = HolidayObservanceType.Mandatory;

	/// <summary>
	/// When a holiday falls on a weekend, some policies shift it to the
	/// nearest weekday. This field stores that substitute date.
	/// </summary>
	public DateOnly? SubstitutionDate { get; set; }

	/// <summary>
	/// True if employees who work on this holiday attract holiday pay / overtime.
	/// </summary>
	public bool AttractsHolidayPay { get; set; } = true;

	/// <summary>Multiplier applied to base pay for hours worked on this holiday (e.g. 1.5, 2.0).</summary>
	public decimal? HolidayPayMultiplier { get; set; }

	public bool IsRecurringAnnually { get; set; } = true;

	public bool IsActive { get; set; } = true;

	// Navigation
	[ForeignKey(nameof(HolidayCalendarId))]
	public virtual HolidayCalendar HolidayCalendar { get; set; } = null!;
}

// =========================================================================
// PayPeriod
// Defines payroll cutoff periods (weekly, bi-weekly, semi-monthly, monthly).
// Attendance summaries are scoped to a PayPeriod so that HR can lock a
// period before exporting to payroll — preventing retroactive changes.
// =========================================================================

public class PayPeriod : TenantEntity
{
	[Required]
	[MaxLength(100)]
	public string PeriodName { get; set; } = string.Empty;

	public PayPeriodType Type { get; set; }

	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }

	public PayPeriodStatus Status { get; set; } = PayPeriodStatus.Open;

	/// <summary>Date/time HR locked the period for payroll processing.</summary>
	public DateTime? ClosedDate { get; set; }

	public Guid? ClosedById { get; set; }

	/// <summary>Date/time attendance data was exported to the payroll system.</summary>
	public DateTime? ExportedDate { get; set; }

	public Guid? ExportedById { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(ClosedById))]
	public virtual Employee? ClosedBy { get; set; }

	[ForeignKey(nameof(ExportedById))]
	public virtual Employee? ExportedBy { get; set; }

	public virtual ICollection<StaffMonthlyAttendanceSummary> AttendanceSummaries { get; set; } = new List<StaffMonthlyAttendanceSummary>();
	public virtual ICollection<StaffAttendancePayrollExport> PayrollExports { get; set; } = new List<StaffAttendancePayrollExport>();
}

// =========================================================================
// StaffAttendancePayrollExport
// Represents a single export batch of attendance data sent to the payroll
// system for a given PayPeriod. Provides a full audit trail of what was
// sent, when, and to which system.
// =========================================================================

public class StaffAttendancePayrollExport : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string ExportReference { get; set; } = string.Empty;

	[Required]
	public Guid PayPeriodId { get; set; }

	public DateTime ExportDate { get; set; }

	[Required]
	public Guid ExportedById { get; set; }

	/// <summary>Target payroll system identifier (e.g. "Sage", "Payspace", "ADP").</summary>
	[MaxLength(100)]
	public string? TargetSystem { get; set; }

	public int TotalEmployees { get; set; }
	public int TotalRecords { get; set; }

	public PayrollExportStatus Status { get; set; }

	/// <summary>
	/// Serialised export payload snapshot (JSON) for audit purposes.
	/// Store only a summary or hash if payload is very large.
	/// </summary>
	[MaxLength(8000)]
	public string? ExportPayloadSnapshot { get; set; }

	[MaxLength(2000)]
	public string? ErrorDetails { get; set; }

	[MaxLength(500)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(PayPeriodId))]
	public virtual PayPeriod PayPeriod { get; set; } = null!;

	[ForeignKey(nameof(ExportedById))]
	public virtual Employee ExportedBy { get; set; } = null!;
}

// =========================================================================
// AttendanceAlertRule
// A configurable rule that defines when an alert should fire.
// Rules can be scoped to the entire tenant, a department, a position,
// or an individual employee.
// =========================================================================

public class StaffAttendanceAlertRule : TenantEntity
{
	[Required]
	[MaxLength(150)]
	public string RuleName { get; set; } = string.Empty;

	[MaxLength(500)]
	public string? Description { get; set; }

	public AttendanceAlertTriggerType TriggerType { get; set; }

	public AttendanceAlertSeverity Severity { get; set; } = AttendanceAlertSeverity.Warning;

	// ── Threshold ─────────────────────────────────────────────────────────

	/// <summary>
	/// The numeric threshold that triggers the alert. Interpretation depends
	/// on TriggerType:
	///   ConsecutiveAbsences     → number of consecutive absent days
	///   ChronicLateness         → number of late days within the EvaluationWindowDays
	///   MissingPunch            → number of missing punches within window
	///   OvertimeThresholdReached→ overtime hours in the window
	///   LowAttendancePercentage → attendance % below this value
	/// </summary>
	public decimal ThresholdValue { get; set; }

	/// <summary>
	/// Rolling window in calendar days over which the threshold is evaluated.
	/// E.g. ThresholdValue = 3 late days within EvaluationWindowDays = 30.
	/// </summary>
	public int? EvaluationWindowDays { get; set; }

	// ── Scope ─────────────────────────────────────────────────────────────

	/// <summary>Null = rule applies tenant-wide.</summary>
	public Guid? OrganizationUnitId { get; set; }

	/// <summary>Null = rule applies to all positions.</summary>
	public Guid? PositionId { get; set; }

	/// <summary>Null = rule applies to all employees in scope.</summary>
	public Guid? EmployeeId { get; set; }

	// ── Notification ──────────────────────────────────────────────────────

	/// <summary>True = send email when alert fires.</summary>
	public bool NotifyByEmail { get; set; } = true;

	/// <summary>True = send in-app notification.</summary>
	public bool NotifyInApp { get; set; } = true;

	/// <summary>
	/// JSON array of role names or employee IDs to notify.
	/// E.g. ["DirectSupervisor", "HRManager"] or specific Guid strings.
	/// </summary>
	[MaxLength(1000)]
	public string? NotifyRecipientsJson { get; set; }

	/// <summary>When true, the alert also requires acknowledgement before it closes.</summary>
	public bool RequiresAcknowledgement { get; set; }

	public bool IsActive { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }
	
	public virtual ICollection<StaffAttendanceAlert> Alerts { get; set; } = new List<StaffAttendanceAlert>();
}
 
 
// =========================================================================
// StaffAttendanceAlert
// An instance of an alert that has fired for a specific employee based on
// an AttendanceAlertRule. Tracks the full lifecycle from active through
// acknowledged to resolved.
// =========================================================================

public class StaffAttendanceAlert : TenantEntity
{
	[Required]
	public Guid AlertRuleId { get; set; }

	[Required]
	public Guid EmployeeId { get; set; }

	public AttendanceAlertTriggerType TriggerType { get; set; }

	public AttendanceAlertSeverity Severity { get; set; }

	public AttendanceAlertStatus Status { get; set; } = AttendanceAlertStatus.Active;

	public DateTime TriggeredDate { get; set; }

	/// <summary>
	/// Human-readable description of why the alert fired.
	/// E.g. "Employee has been absent for 3 consecutive days (Mon–Wed)."
	/// </summary>
	[Required]
	[MaxLength(1000)]
	public string TriggerDescription { get; set; } = string.Empty;

	/// <summary>
	/// The actual value that breached the threshold at the time of firing.
	/// E.g. 4 consecutive absences when threshold was 3.
	/// </summary>
	public decimal? TriggerValue { get; set; }

	// ── Acknowledgement ───────────────────────────────────────────────────

	public Guid? AcknowledgedById { get; set; }
	public DateTime? AcknowledgedDate { get; set; }

	[MaxLength(1000)]
	public string? AcknowledgementNotes { get; set; }

	// ── Resolution ────────────────────────────────────────────────────────

	public Guid? ResolvedById { get; set; }
	public DateTime? ResolvedDate { get; set; }

	[MaxLength(1000)]
	public string? ResolutionNotes { get; set; }

	// ── Dismissal ─────────────────────────────────────────────────────────

	public Guid? DismissedById { get; set; }
	public DateTime? DismissedDate { get; set; }

	[MaxLength(500)]
	public string? DismissalReason { get; set; }

	// Navigation
	[ForeignKey(nameof(AlertRuleId))]
	public virtual StaffAttendanceAlertRule AlertRule { get; set; } = null!;

	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	[ForeignKey(nameof(AcknowledgedById))]
	public virtual Employee? AcknowledgedBy { get; set; }

	[ForeignKey(nameof(ResolvedById))]
	public virtual Employee? ResolvedBy { get; set; }

	[ForeignKey(nameof(DismissedById))]
	public virtual Employee? DismissedBy { get; set; }
}

// =========================================================================
// ConsultantTimesheet
// Timesheet for a consultant working at a client site. Covers a billing
// period (e.g. a week or month) and groups individual daily entries.
// Lifecycle: Draft → Submitted → SentToClient → ClientConfirmed → Billed.
// =========================================================================

public class ConsultantTimesheet : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string TimesheetNumber { get; set; } = string.Empty;

	/// <summary>FK to the Employee acting as the consultant.</summary>
	[Required]
	public Guid ConsultantId { get; set; }

	/// <summary>FK to the Client entity in your CRM / contracts module.</summary>
	[Required]
	public Guid ClientId { get; set; }

	/// <summary>Optional FK to a ClientEngagement / contract for billing rate lookups.</summary>
	public Guid? EngagementId { get; set; }

	public DateOnly PeriodStartDate { get; set; }
	public DateOnly PeriodEndDate { get; set; }

	/// <summary>Computed sum of all ConsultantTimesheetEntry.TotalHours.</summary>
	public decimal TotalHours { get; set; }

	public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;

	public DateTime? SubmittedDate { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(ConsultantId))]
	public virtual Employee Consultant { get; set; } = null!;

	[ForeignKey(nameof(ClientId))]
	public virtual ConsultantClient Client { get; set; } = null!;

	[ForeignKey(nameof(EngagementId))]
	public virtual ClientEngagement? Engagement { get; set; }

	public virtual ICollection<ConsultantTimesheetEntry> Entries { get; set; } = new List<ConsultantTimesheetEntry>();
	public virtual ICollection<ClientTimesheetConfirmation> Confirmations { get; set; } = new List<ClientTimesheetConfirmation>();
	public virtual ICollection<TimesheetInvoiceLink> InvoiceLinks { get; set; } = new List<TimesheetInvoiceLink>();
}
  
// =========================================================================
// ConsultantTimesheetEntry
// A single day's time entry within a ConsultantTimesheet.
// Records start/end times, breaks, and a description of work performed.
// =========================================================================

public class ConsultantTimesheetEntry : TenantEntity
{
	[Required]
	public Guid TimesheetId { get; set; }

	public DateOnly WorkDate { get; set; }

	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }

	/// <summary>Break duration in minutes — deducted when computing TotalHours.</summary>
	public int BreakMinutes { get; set; }

	/// <summary>
	/// Computed: (EndTime − StartTime) in hours − (BreakMinutes / 60).
	/// Stored for query performance; recomputed on any change to times.
	/// </summary>
	public decimal TotalHours { get; set; }

	[Required]
	[MaxLength(2000)]
	public string ActivitySummary { get; set; } = string.Empty;

	/// <summary>Work location for this specific day (client site, remote, etc.).</summary>
	[MaxLength(500)]
	public string? Location { get; set; }

	[MaxLength(1000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(TimesheetId))]
	public virtual ConsultantTimesheet Timesheet { get; set; } = null!;
}
 
// =========================================================================
// ClientTimesheetConfirmation
// A tokenised confirmation request sent to a client contact so they can
// verify the consultant's presence and work performed.
// The system generates a unique link containing ConfirmationToken which
// the client accesses without needing an account.
// =========================================================================

public class ClientTimesheetConfirmation : TenantEntity
{
	[Required]
	public Guid TimesheetId { get; set; }

	[Required]
	[MaxLength(200)]
	public string ClientContactEmail { get; set; } = string.Empty;

	[MaxLength(200)]
	public string? ClientContactName { get; set; }

	/// <summary>
	/// Unique UUID embedded in the confirmation link URL.
	/// Treated as a one-time token — regenerated on each resend.
	/// </summary>
	public Guid ConfirmationToken { get; set; } = Guid.NewGuid();

	/// <summary>Link becomes invalid after this date/time.</summary>
	public DateTime TokenExpiryDate { get; set; }

	public DateTime SentDate { get; set; }

	[Required]
	public Guid SentById { get; set; }

	public TimesheetConfirmationStatus Status { get; set; } = TimesheetConfirmationStatus.Sent;

	/// <summary>Date/time the client first opened the confirmation link.</summary>
	public DateTime? ViewedDate { get; set; }

	public DateTime? ConfirmedDate { get; set; }
	public DateTime? RejectedDate { get; set; }

	[MaxLength(2000)]
	public string? ClientNotes { get; set; }

	/// <summary>Client's IP address at confirmation time — stored for fraud audit.</summary>
	[MaxLength(50)]
	public string? ClientIpAddress { get; set; }

	/// <summary>Tracks how many times the confirmation link has been resent.</summary>
	public int ResendCount { get; set; }

	// Navigation
	[ForeignKey(nameof(TimesheetId))]
	public virtual ConsultantTimesheet Timesheet { get; set; } = null!;

	[ForeignKey(nameof(SentById))]
	public virtual Employee SentBy { get; set; } = null!;
}
 
// =========================================================================
// TimesheetInvoice
// Invoice generated from one or more confirmed consultant timesheets.
// Links confirmed work to a billable invoice sent to the client.
// =========================================================================

public class TimesheetInvoice : TenantEntity
{
	[Required]
	[MaxLength(50)]
	public string InvoiceNumber { get; set; } = string.Empty;

	[Required]
	public Guid ClientId { get; set; }

	[Required]
	public Guid ConsultantId { get; set; }

	public DateOnly BillingPeriodStart { get; set; }
	public DateOnly BillingPeriodEnd { get; set; }

	/// <summary>Sum of hours across all linked confirmed timesheets.</summary>
	public decimal TotalHours { get; set; }

	/// <summary>Agreed hourly rate for this engagement.</summary>
	public decimal HourlyRate { get; set; }

	/// <summary>TotalHours × HourlyRate (before tax).</summary>
	public decimal SubTotal { get; set; }

	public decimal TaxPercentage { get; set; }
	public decimal TaxAmount { get; set; }

	/// <summary>SubTotal + TaxAmount.</summary>
	public decimal TotalAmount { get; set; }

	/// <summary>ISO 4217 currency code, e.g. "GHS", "USD", "GBP".</summary>
	[Required]
	[MaxLength(3)]
	public string Currency { get; set; } = "GHS";

	public TimesheetInvoiceStatus Status { get; set; } = TimesheetInvoiceStatus.Draft;

	public DateOnly? IssuedDate { get; set; }
	public DateOnly? DueDate { get; set; }
	public DateOnly? PaidDate { get; set; }
	public decimal? PaidAmount { get; set; }

	[MaxLength(2000)]
	public string? Notes { get; set; }

	// Navigation
	[ForeignKey(nameof(ClientId))]
	public virtual ConsultantClient Client { get; set; } = null!;

	[ForeignKey(nameof(ConsultantId))]
	public virtual Employee Consultant { get; set; } = null!;

	public virtual ICollection<TimesheetInvoiceLink> LinkedTimesheets { get; set; } = new List<TimesheetInvoiceLink>();
}
 
// =========================================================================
// TimesheetInvoiceLink
// Junction table linking confirmed ConsultantTimesheets to a
// TimesheetInvoice (many timesheets can roll up into one invoice).
// =========================================================================

public class TimesheetInvoiceLink : TenantEntity
{
	[Required]
	public Guid InvoiceId { get; set; }

	[Required]
	public Guid TimesheetId { get; set; }

	/// <summary>Hours from this timesheet counted in the invoice.</summary>
	public decimal Hours { get; set; }

	/// <summary>Monetary contribution from this timesheet (Hours × Rate).</summary>
	public decimal Amount { get; set; }

	// Navigation
	[ForeignKey(nameof(InvoiceId))]
	public virtual TimesheetInvoice Invoice { get; set; } = null!;

	[ForeignKey(nameof(TimesheetId))]
	public virtual ConsultantTimesheet Timesheet { get; set; } = null!;
}
