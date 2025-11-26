using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffAttendance;

/// <summary>
/// Daily employee attendance record
/// </summary>
public class DailyAttendance : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime AttendanceDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    // Work Schedule
    public Guid? WorkScheduleId { get; set; }
    public WorkSchedule? WorkSchedule { get; set; }

    public TimeSpan? ScheduledStartTime { get; set; }
    public TimeSpan? ScheduledEndTime { get; set; }
    public decimal? ScheduledWorkHours { get; set; }

    // Actual Times
    public TimeSpan? ActualCheckInTime { get; set; }
    public TimeSpan? ActualCheckOutTime { get; set; }
    public decimal? ActualWorkHours { get; set; }

    // Status
    public AttendanceStatus Status { get; set; } // Present, Absent, Late, Half-Day, Leave, Holiday
    public string? StatusReason { get; set; }

    // Tardiness
    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public int? EarlyDepartureMinutes { get; set; }

    // Break Times
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int? TotalBreakMinutes { get; set; }

    // Overtime
    public bool IsOvertime { get; set; }
    public decimal? OvertimeHours { get; set; }
    public bool OvertimeApproved { get; set; }
    public Guid? OvertimeApprovedById { get; set; }
    public Employee? OvertimeApprovedBy { get; set; }

    // Location
    public Guid? StationId { get; set; }
    public WorkStation? Station { get; set; }
    public string? CheckInLocation { get; set; }
    public string? CheckOutLocation { get; set; }

    // Remote Work
    public bool IsRemoteWork { get; set; }
    public string? RemoteWorkLocation { get; set; }

    // Device Information
    public string? CheckInDevice { get; set; }
    public string? CheckInIpAddress { get; set; }
    public string? CheckOutDevice { get; set; }
    public string? CheckOutIpAddress { get; set; }

    // Leave Reference
    public Guid? LeaveRequestId { get; set; }
    public LeaveRequest? LeaveRequest { get; set; }

    // Public Holiday
    public Guid? PublicHolidayId { get; set; }
    public PublicHoliday? PublicHoliday { get; set; }

    // Verification
    public bool RequiresVerification { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public Guid? VerifiedById { get; set; }
    public Employee? VerifiedBy { get; set; }
    public string? VerificationNotes { get; set; }

    // Exceptions
    public bool HasException { get; set; }
    public string? ExceptionReason { get; set; }
    public bool ExceptionApproved { get; set; }
    public Guid? ExceptionApprovedById { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Work schedule definition
/// </summary>
public class WorkSchedule : TenantEntity
{
    public string ScheduleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ScheduleType Type { get; set; } // Fixed, Flexible, Shift, Compressed
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }

    // Standard Work Hours
    public TimeSpan StandardStartTime { get; set; }
    public TimeSpan StandardEndTime { get; set; }
    public decimal StandardHoursPerDay { get; set; }
    public decimal StandardHoursPerWeek { get; set; }

    // Flexibility
    public bool HasFlexibleStartTime { get; set; }
    public TimeSpan? FlexibleStartTimeEarliest { get; set; }
    public TimeSpan? FlexibleStartTimeLatest { get; set; }

    public bool HasFlexibleEndTime { get; set; }
    public TimeSpan? FlexibleEndTimeEarliest { get; set; }
    public TimeSpan? FlexibleEndTimeLatest { get; set; }

    // Core Hours (for flexible schedules)
    public bool HasCoreHours { get; set; }
    public TimeSpan? CoreHoursStart { get; set; }
    public TimeSpan? CoreHoursEnd { get; set; }

    // Break Times
    public bool HasMandatoryBreak { get; set; }
    public int? BreakDurationMinutes { get; set; }
    public bool IsBreakPaid { get; set; }

    // Working Days
    public bool WorksMonday { get; set; }
    public bool WorksTuesday { get; set; }
    public bool WorksWednesday { get; set; }
    public bool WorksThursday { get; set; }
    public bool WorksFriday { get; set; }
    public bool WorksSaturday { get; set; }
    public bool WorksSunday { get; set; }

    // Overtime Rules
    public bool AllowsOvertime { get; set; }
    public bool OvertimeRequiresPreApproval { get; set; }
    public decimal? MaxOvertimeHoursPerDay { get; set; }
    public decimal? MaxOvertimeHoursPerWeek { get; set; }

    // Grace Period
    public int? LateGracePeriodMinutes { get; set; }
    public int? EarlyDepartureGracePeriodMinutes { get; set; }

    public ICollection<EmployeeWorkSchedule> EmployeeSchedules { get; set; } = new List<EmployeeWorkSchedule>();
    public ICollection<ShiftDefinition> Shifts { get; set; } = new List<ShiftDefinition>();
}

public class EmployeeWorkSchedule : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid WorkScheduleId { get; set; }
    public WorkSchedule WorkSchedule { get; set; } = null!;

    public DateTime EffectiveDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public string? AssignmentReason { get; set; }
}

/// <summary>
/// Shift definition for shift workers
/// </summary>
public class ShiftDefinition : TenantEntity
{
    public Guid WorkScheduleId { get; set; }
    public WorkSchedule WorkSchedule { get; set; } = null!;

    public string ShiftName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ShiftType Type { get; set; } // Morning, Afternoon, Night, Rotating
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal ShiftHours { get; set; }

    public bool IsNightShift { get; set; }
    public bool HasShiftDifferential { get; set; }
    public decimal? ShiftDifferentialPercentage { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// Shift assignment to employee
/// </summary>
public class ShiftAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid ShiftDefinitionId { get; set; }
    public ShiftDefinition ShiftDefinition { get; set; } = null!;

    public DateTime AssignmentDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }

    public Guid? AssignedById { get; set; }
    public Employee? AssignedBy { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Attendance regularization request
/// </summary>
public class AttendanceRegularization : TenantEntity
{
    public string RegularizationNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid AttendanceId { get; set; }
    public DailyAttendance Attendance { get; set; } = null!;

    public DateTime RequestDate { get; set; }
    public DateTime AttendanceDate { get; set; }

    // Requested Changes
    public RegularizationType Type { get; set; } // Missing Punch, Wrong Time, Forgot to Mark

    public TimeSpan? RequestedCheckInTime { get; set; }
    public TimeSpan? RequestedCheckOutTime { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? SupportingDocuments { get; set; }

    // Approval
    public RegularizationStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }

    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Implementation
    public bool IsApplied { get; set; }
    public DateTime? AppliedDate { get; set; }
}

/// <summary>
/// Overtime request
/// </summary>
public class OvertimeRequest : TenantEntity
{
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime RequestDate { get; set; }
    public DateTime OvertimeDate { get; set; }

    public TimeSpan PlannedStartTime { get; set; }
    public TimeSpan PlannedEndTime { get; set; }
    public decimal PlannedOvertimeHours { get; set; }

    public string Purpose { get; set; } = string.Empty;
    public string? TaskDetails { get; set; }

    public OvertimeType Type { get; set; } // Weekday, Weekend, Holiday
    public OvertimeRequestStatus Status { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }

    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Actual Overtime
    public decimal? ActualOvertimeHours { get; set; }
    public Guid? AttendanceId { get; set; }
    public DailyAttendance? Attendance { get; set; }
}

/// <summary>
/// Monthly attendance summary
/// </summary>
public class MonthlyAttendanceSummary : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }

    // Totals
    public int TotalWorkingDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public int DaysOnLeave { get; set; }
    public int DaysLate { get; set; }
    public int PublicHolidays { get; set; }
    public int Weekends { get; set; }

    // Hours
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalUndertimeHours { get; set; }

    // Tardiness
    public int TotalLateMinutes { get; set; }
    public int NumberOfLateDays { get; set; }
    public int TotalEarlyDepartureMinutes { get; set; }

    // Percentages
    public decimal AttendancePercentage { get; set; }
    public decimal PunctualityPercentage { get; set; }

    // Status
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public Guid? FinalizedById { get; set; }
    public Employee? FinalizedBy { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Biometric device integration
/// </summary>
public class BiometricDevice : TenantEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }

    public DeviceType Type { get; set; } // Fingerprint, Face Recognition, RFID Card

    public Guid? StationId { get; set; }
    public WorkStation? Station { get; set; }
    public string Location { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public int? Port { get; set; }

    public bool IsActive { get; set; }
    public DateTime? LastSyncDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Raw attendance log from devices
/// </summary>
public class AttendanceLog : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime LogDateTime { get; set; }
    public LogType LogType { get; set; } // Check-In, Check-Out, Break-Start, Break-End

    public Guid? DeviceId { get; set; }
    public BiometricDevice? Device { get; set; }

    public string? DeviceSerialNumber { get; set; }
    public string? Location { get; set; }

    public bool IsProcessed { get; set; }
    public DateTime? ProcessedDate { get; set; }

    public Guid? AttendanceId { get; set; }
    public DailyAttendance? Attendance { get; set; }

    public string? RawData { get; set; }
}