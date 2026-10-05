using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF ATTENDANCE RECORD DTOs
// Lightweight daily record — simple clock-in / clock-out scenarios.
// ============================================================================

#region Staff Attendance Record

public class StaffAttendanceRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public double? WorkedHours { get; set; }
    public double? OvertimeHours { get; set; }
    public StaffAttendanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
}

public class StaffAttendanceRecordSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public double? WorkedHours { get; set; }
    public StaffAttendanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateStaffAttendanceRecordDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public double? WorkedHours { get; set; }
    public double? OvertimeHours { get; set; }

    [Required]
    public StaffAttendanceStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateStaffAttendanceRecordDto : UpdateDtoBase
{
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public double? WorkedHours { get; set; }
    public double? OvertimeHours { get; set; }

    [Required]
    public StaffAttendanceStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF DAILY ATTENDANCE DTOs
// Full-detail daily record — tardiness, breaks, overtime, location,
// remote work, device tracking, leave/holiday references, verification,
// and exceptions.
// ============================================================================

#region Staff Daily Attendance

public class StaffDailyAttendanceDto : BaseDto
{
    public Guid TenantId { get; set; }

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public DateOnly AttendanceDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    // Schedule
    public Guid? WorkScheduleId { get; set; }
    public string? WorkScheduleName { get; set; }
    public TimeSpan? ScheduledStartTime { get; set; }
    public TimeSpan? ScheduledEndTime { get; set; }
    public decimal? ScheduledWorkHours { get; set; }

    // Actual
    public TimeSpan? ActualCheckInTime { get; set; }
    public TimeSpan? ActualCheckOutTime { get; set; }
    public decimal? ActualWorkHours { get; set; }

    // Status
    public StaffAttendanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? StatusReason { get; set; }

    // Tardiness
    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public int? EarlyDepartureMinutes { get; set; }

    // Breaks
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int? TotalBreakMinutes { get; set; }

    // Overtime
    public bool IsOvertime { get; set; }
    public decimal? OvertimeHours { get; set; }
    public bool OvertimeApproved { get; set; }
    public Guid? OvertimeApprovedById { get; set; }
    public string? OvertimeApprovedByName { get; set; }

    // Location
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? CheckInLocation { get; set; }
    public string? CheckOutLocation { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public LocationVerificationStatus CheckInLocationStatus { get; set; }
    public string CheckInLocationStatusName => CheckInLocationStatus.ToString();
    public LocationVerificationStatus CheckOutLocationStatus { get; set; }
    public string CheckOutLocationStatusName => CheckOutLocationStatus.ToString();
    public Guid? CheckInGeofenceZoneId { get; set; }
    public string? CheckInGeofenceZoneName { get; set; }

    // Remote Work
    public bool IsRemoteWork { get; set; }
    public string? RemoteWorkLocation { get; set; }
    public Guid? RemoteWorkRequestId { get; set; }

    // Device / IP
    public string? CheckInDevice { get; set; }
    public string? CheckInIpAddress { get; set; }
    public string? CheckOutDevice { get; set; }
    public string? CheckOutIpAddress { get; set; }

    // References
    public Guid? LeaveRequestId { get; set; }
    public Guid? PublicHolidayId { get; set; }
    public string? PublicHolidayName { get; set; }
    public Guid? PayPeriodId { get; set; }
    public string? PayPeriodName { get; set; }

    // Verification
    public bool RequiresVerification { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public string? VerificationNotes { get; set; }

    // Exceptions
    public bool HasException { get; set; }
    public string? ExceptionReason { get; set; }
    public bool ExceptionApproved { get; set; }
    public Guid? ExceptionApprovedById { get; set; }
    public string? ExceptionApprovedByName { get; set; }

    public string? Notes { get; set; }

    // Child collections
    public List<StaffAttendanceLogSummaryDto> AttendanceLogs { get; set; } = new();
    public List<StaffAttendanceRegularizationSummaryDto> Regularizations { get; set; } = new();
}

public class StaffDailyAttendanceSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateOnly AttendanceDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan? ActualCheckInTime { get; set; }
    public TimeSpan? ActualCheckOutTime { get; set; }
    public decimal? ActualWorkHours { get; set; }
    public StaffAttendanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }
    public bool IsOvertime { get; set; }
    public decimal? OvertimeHours { get; set; }
    public bool IsRemoteWork { get; set; }
    public bool HasException { get; set; }
    public bool IsVerified { get; set; }
}

public class CreateStaffDailyAttendanceDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateOnly AttendanceDate { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public Guid? WorkScheduleId { get; set; }
    public TimeSpan? ScheduledStartTime { get; set; }
    public TimeSpan? ScheduledEndTime { get; set; }
    public decimal? ScheduledWorkHours { get; set; }
    public TimeSpan? ActualCheckInTime { get; set; }
    public TimeSpan? ActualCheckOutTime { get; set; }
    public decimal? ActualWorkHours { get; set; }

    [Required]
    public StaffAttendanceStatus Status { get; set; }

    [MaxLength(500)]
    public string? StatusReason { get; set; }

    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public int? EarlyDepartureMinutes { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int? TotalBreakMinutes { get; set; }
    public bool IsOvertime { get; set; }
    public decimal? OvertimeHours { get; set; }
    public Guid? LocationId { get; set; }

    [MaxLength(500)]
    public string? CheckInLocation { get; set; }

    [MaxLength(500)]
    public string? CheckOutLocation { get; set; }

    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public Guid? CheckInGeofenceZoneId { get; set; }
    public bool IsRemoteWork { get; set; }

    [MaxLength(500)]
    public string? RemoteWorkLocation { get; set; }

    public Guid? RemoteWorkRequestId { get; set; }

    [MaxLength(200)]
    public string? CheckInDevice { get; set; }

    [MaxLength(50)]
    public string? CheckInIpAddress { get; set; }

    [MaxLength(200)]
    public string? CheckOutDevice { get; set; }

    [MaxLength(50)]
    public string? CheckOutIpAddress { get; set; }

    public Guid? LeaveRequestId { get; set; }
    public Guid? PublicHolidayId { get; set; }
    public Guid? PayPeriodId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateStaffDailyAttendanceDto : UpdateDtoBase
{
    public TimeSpan? ActualCheckInTime { get; set; }
    public TimeSpan? ActualCheckOutTime { get; set; }
    public decimal? ActualWorkHours { get; set; }

    [Required]
    public StaffAttendanceStatus Status { get; set; }

    [MaxLength(500)]
    public string? StatusReason { get; set; }

    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public int? EarlyDepartureMinutes { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int? TotalBreakMinutes { get; set; }
    public bool IsOvertime { get; set; }
    public decimal? OvertimeHours { get; set; }
    public Guid? LocationId { get; set; }
    public bool IsRemoteWork { get; set; }

    [MaxLength(500)]
    public string? RemoteWorkLocation { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Filter for the tenant-wide daily attendance search.
///
/// Every property is optional and they compose with AND, so the caller can express
/// "late and unverified in August for this org unit" in one request. The narrow reads
/// (by-date, by-status, late, overtime, remote, pending-verification) are kept for the
/// callers that already use them, but this is the one a filterable grid should target.
/// </summary>
public class StaffDailyAttendanceSearchDto
{
    /// <summary>Matches employee name or employee number, case-insensitively.</summary>
    public string? SearchTerm { get; set; }

    public Guid? EmployeeId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? WorkScheduleId { get; set; }
    public Guid? PayPeriodId { get; set; }

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Statuses to include. Empty or null means all.</summary>
    public List<StaffAttendanceStatus> Statuses { get; set; } = new();

    // Tri-state flags: null leaves the dimension unfiltered.
    public bool? IsLate { get; set; }
    public bool? IsEarlyDeparture { get; set; }
    public bool? IsOvertime { get; set; }
    public bool? IsRemoteWork { get; set; }
    public bool? HasException { get; set; }
    public bool? IsVerified { get; set; }
    public bool? RequiresVerification { get; set; }

    /// <summary>Only days where the employee was at least this many minutes late.</summary>
    public int? MinLateMinutes { get; set; }

    /// <summary>Only days with at least this much overtime.</summary>
    public decimal? MinOvertimeHours { get; set; }

    /// <summary>
    /// One of: date, employee, status, workhours, overtime, lateminutes. Anything else falls
    /// back to date. Prefixing is not supported — use <see cref="SortDescending"/>.
    /// </summary>
    public string? SortBy { get; set; }

    public bool SortDescending { get; set; } = true;
}

public class VerifyAttendanceDto
{
    [Required]
    public Guid AttendanceId { get; set; }

    [Required]
    public Guid VerifiedById { get; set; }

    public DateTime VerifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? VerificationNotes { get; set; }
}

public class ApproveAttendanceOvertimeDto
{
    [Required]
    public Guid AttendanceId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public decimal? ApprovedOvertimeHours { get; set; }

    [MaxLength(500)]
    public string? Comments { get; set; }
}

public class ApproveAttendanceExceptionDto
{
    [Required]
    public Guid AttendanceId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE LOG DTOs
// Raw punch log from biometric or other devices.
// ============================================================================

#region Staff Attendance Log

public class StaffAttendanceLogDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateTime LogDateTime { get; set; }
    public AttendanceLogType LogType { get; set; }
    public string LogTypeName => LogType.ToString();
    public Guid? DeviceId { get; set; }
    public string? DeviceSerialNumber { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public Guid? AttendanceId { get; set; }
    public string? RawData { get; set; }
    public List<AttendanceLocationVerificationLogDto> VerificationLogs { get; set; } = new();
}

public class StaffAttendanceLogSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime LogDateTime { get; set; }
    public AttendanceLogType LogType { get; set; }
    public string LogTypeName => LogType.ToString();
    public string? DeviceSerialNumber { get; set; }
    public bool IsProcessed { get; set; }
    public Guid? AttendanceId { get; set; }
}

public class CreateStaffAttendanceLogDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateTime LogDateTime { get; set; }

    [Required]
    public AttendanceLogType LogType { get; set; }

    public Guid? DeviceId { get; set; }

    [MaxLength(100)]
    public string? DeviceSerialNumber { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [MaxLength(2000)]
    public string? RawData { get; set; }
}

public class GeofenceVerificationSummaryDto
{
    public LocationVerificationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? GeofenceZoneId { get; set; }
    public string? GeofenceZoneName { get; set; }
    public double? DistanceFromZoneMetres { get; set; }
    public bool HasConfiguredZone { get; set; }
    public bool HasGpsCoordinates { get; set; }
    public string? Message { get; set; }
}

public class StaffAttendancePunchDto
{
    [Required]
    public AttendanceLogType LogType { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    /// <summary>When true (default), the punch is processed into daily attendance immediately.</summary>
    public bool ProcessImmediately { get; set; } = true;
}

public class StaffAttendancePunchResultDto
{
    public StaffAttendanceLogDto Log { get; set; } = null!;
    public GeofenceVerificationSummaryDto? Verification { get; set; }
    public Guid? DailyAttendanceId { get; set; }
    public bool ProcessedImmediately { get; set; }
}

#endregion

// ============================================================================
// ATTENDANCE LOCATION VERIFICATION LOG DTOs
// GPS verification outcome for each punch event.
// ============================================================================

#region Attendance Location Verification Log

public class AttendanceLocationVerificationLogDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AttendanceLogId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime VerificationDateTime { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Guid? GeofenceZoneId { get; set; }
    public string? GeofenceZoneName { get; set; }
    public double? DistanceFromZoneMetres { get; set; }
    public LocationVerificationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE REGULARIZATION DTOs
// Employee request to correct a DailyAttendance record.
// ============================================================================

#region Staff Attendance Regularization

public class StaffAttendanceRegularizationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RegularizationNumber { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    // Attendance reference
    public Guid AttendanceId { get; set; }
    public DateOnly AttendanceDate { get; set; }

    public DateTime RequestDate { get; set; }
    public RegularizationType Type { get; set; }
    public string TypeName => Type.ToString();
    public TimeSpan? RequestedCheckInTime { get; set; }
    public TimeSpan? RequestedCheckOutTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SupportingDocuments { get; set; }

    public AttendanceRegularizationStatus Status { get; set; }
    public string StatusName => Status.ToString();

    // Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Application
    public bool IsApplied { get; set; }
    public DateTime? AppliedDate { get; set; }
}

public class StaffAttendanceRegularizationSummaryDto
{
    public Guid Id { get; set; }
    public string RegularizationNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly AttendanceDate { get; set; }
    public RegularizationType Type { get; set; }
    public string TypeName => Type.ToString();
    public AttendanceRegularizationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime RequestDate { get; set; }
    public bool IsApplied { get; set; }
}

public class CreateStaffAttendanceRegularizationDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid AttendanceId { get; set; }

    [Required]
    public RegularizationType Type { get; set; }

    public TimeSpan? RequestedCheckInTime { get; set; }
    public TimeSpan? RequestedCheckOutTime { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? SupportingDocuments { get; set; }
}

public class ApproveRegularizationDto
{
    [Required]
    public Guid RegularizationId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovalDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }
}

public class RejectRegularizationDto
{
    [Required]
    public Guid RegularizationId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// STAFF MONTHLY ATTENDANCE SUMMARY DTOs
// Aggregated monthly roll-up per employee.
// ============================================================================

#region Staff Monthly Attendance Summary

public class StaffMonthlyAttendanceSummaryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");

    // Day Counts
    public int TotalWorkingDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public int DaysOnLeave { get; set; }
    public int DaysLate { get; set; }
    public int DaysRemoteWork { get; set; }
    public int PublicHolidays { get; set; }
    public int Weekends { get; set; }
    public int DaysHalfDay { get; set; }

    // Hours
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalUndertimeHours { get; set; }
    public decimal TotalBreakHours { get; set; }

    // Tardiness
    public int TotalLateMinutes { get; set; }
    public int NumberOfLateDays { get; set; }
    public int TotalEarlyDepartureMinutes { get; set; }
    public int NumberOfEarlyDepartureDays { get; set; }

    // Percentages
    public decimal AttendancePercentage { get; set; }
    public decimal PunctualityPercentage { get; set; }

    // Pay Period
    public Guid? PayPeriodId { get; set; }
    public string? PayPeriodName { get; set; }

    // Finalization
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public Guid? FinalizedById { get; set; }
    public string? FinalizedByName { get; set; }
    public string? Notes { get; set; }
}

public class StaffMonthlyAttendanceSummarySummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal AttendancePercentage { get; set; }
    public decimal PunctualityPercentage { get; set; }
    public bool IsFinalized { get; set; }
}

public class FinalizeMonthlyAttendanceSummaryDto
{
    [Required]
    public Guid SummaryId { get; set; }

    [Required]
    public Guid FinalizedById { get; set; }

    public DateTime FinalizedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT DTOs
// Mass attendance import batches for audit and error reporting.
// ============================================================================

#region Staff Bulk Attendance Import

public class StaffBulkAttendanceImportDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ImportReference { get; set; } = string.Empty;
    public Guid ImportedById { get; set; }
    public string ImportedByName { get; set; } = string.Empty;
    public DateTime ImportDate { get; set; }
    public string? SourceFileName { get; set; }
    public AttendanceImportSourceType SourceType { get; set; }
    public string SourceTypeName => SourceType.ToString();
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public AttendanceImportStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? ErrorSummary { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Notes { get; set; }
    public List<StaffBulkAttendanceImportRowDto> ImportRows { get; set; } = new();
}

public class StaffBulkAttendanceImportSummaryDto
{
    public Guid Id { get; set; }
    public string ImportReference { get; set; } = string.Empty;
    public string ImportedByName { get; set; } = string.Empty;
    public DateTime ImportDate { get; set; }
    public string? SourceFileName { get; set; }
    public AttendanceImportSourceType SourceType { get; set; }
    public string SourceTypeName => SourceType.ToString();
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public AttendanceImportStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? CompletedDate { get; set; }
}

public class CreateStaffBulkAttendanceImportDto : CreateDtoBase
{
    [Required]
    public Guid ImportedById { get; set; }

    [MaxLength(500)]
    public string? SourceFileName { get; set; }

    [Required]
    public AttendanceImportSourceType SourceType { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public List<CreateStaffBulkAttendanceImportRowDto> Rows { get; set; } = new();
}

public class StaffBulkAttendanceImportRowDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ImportId { get; set; }
    public int RowNumber { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string RawData { get; set; } = string.Empty;
    public DateOnly? AttendanceDate { get; set; }
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CreatedAttendanceId { get; set; }
}

public class CreateStaffBulkAttendanceImportRowDto
{
    public int RowNumber { get; set; }

    [Required]
    [MaxLength(4000)]
    public string RawData { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }
    public DateOnly? AttendanceDate { get; set; }
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
}

#endregion

// ============================================================================
// WORK SCHEDULE DTOs
// Named work schedule — fixed, flexible, shift-based, or compressed.
// ============================================================================

#region Work Schedule

public class WorkScheduleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkScheduleType Type { get; set; }
    public string TypeName => Type.ToString();
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }

    // Standard Hours
    public TimeSpan StandardStartTime { get; set; }
    public TimeSpan StandardEndTime { get; set; }
    public decimal StandardHoursPerDay { get; set; }
    public decimal StandardHoursPerWeek { get; set; }

    // Flexible Time
    public bool HasFlexibleStartTime { get; set; }
    public TimeSpan? FlexibleStartTimeEarliest { get; set; }
    public TimeSpan? FlexibleStartTimeLatest { get; set; }
    public bool HasFlexibleEndTime { get; set; }
    public TimeSpan? FlexibleEndTimeEarliest { get; set; }
    public TimeSpan? FlexibleEndTimeLatest { get; set; }

    // Core Hours
    public bool HasCoreHours { get; set; }
    public TimeSpan? CoreHoursStart { get; set; }
    public TimeSpan? CoreHoursEnd { get; set; }

    // Breaks
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

    // Overtime
    public bool AllowsOvertime { get; set; }
    public bool OvertimeRequiresPreApproval { get; set; }
    public decimal? MaxOvertimeHoursPerDay { get; set; }
    public decimal? MaxOvertimeHoursPerWeek { get; set; }

    // Grace Periods
    public int? LateGracePeriodMinutes { get; set; }
    public int? EarlyDepartureGracePeriodMinutes { get; set; }

    public List<ShiftDefinitionSummaryDto> Shifts { get; set; } = new();
}

public class WorkScheduleSummaryDto
{
    public Guid Id { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public WorkScheduleType Type { get; set; }
    public string TypeName => Type.ToString();
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public TimeSpan StandardStartTime { get; set; }
    public TimeSpan StandardEndTime { get; set; }
    public decimal StandardHoursPerDay { get; set; }
    public decimal StandardHoursPerWeek { get; set; }
    public int ShiftCount { get; set; }
}

public class CreateWorkScheduleDto : CreateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string ScheduleName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public WorkScheduleType Type { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    [Required]
    public TimeSpan StandardStartTime { get; set; }

    [Required]
    public TimeSpan StandardEndTime { get; set; }

    [Required]
    [Range(0, 24)]
    public decimal StandardHoursPerDay { get; set; }

    [Required]
    [Range(0, 168)]
    public decimal StandardHoursPerWeek { get; set; }

    public bool HasFlexibleStartTime { get; set; }
    public TimeSpan? FlexibleStartTimeEarliest { get; set; }
    public TimeSpan? FlexibleStartTimeLatest { get; set; }
    public bool HasFlexibleEndTime { get; set; }
    public TimeSpan? FlexibleEndTimeEarliest { get; set; }
    public TimeSpan? FlexibleEndTimeLatest { get; set; }
    public bool HasCoreHours { get; set; }
    public TimeSpan? CoreHoursStart { get; set; }
    public TimeSpan? CoreHoursEnd { get; set; }
    public bool HasMandatoryBreak { get; set; }
    public int? BreakDurationMinutes { get; set; }
    public bool IsBreakPaid { get; set; }
    public bool WorksMonday { get; set; } = true;
    public bool WorksTuesday { get; set; } = true;
    public bool WorksWednesday { get; set; } = true;
    public bool WorksThursday { get; set; } = true;
    public bool WorksFriday { get; set; } = true;
    public bool WorksSaturday { get; set; }
    public bool WorksSunday { get; set; }
    public bool AllowsOvertime { get; set; }
    public bool OvertimeRequiresPreApproval { get; set; }
    public decimal? MaxOvertimeHoursPerDay { get; set; }
    public decimal? MaxOvertimeHoursPerWeek { get; set; }
    public int? LateGracePeriodMinutes { get; set; }
    public int? EarlyDepartureGracePeriodMinutes { get; set; }
}

public class UpdateWorkScheduleDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string ScheduleName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public WorkScheduleType Type { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }

    [Required]
    public TimeSpan StandardStartTime { get; set; }

    [Required]
    public TimeSpan StandardEndTime { get; set; }

    [Required]
    [Range(0, 24)]
    public decimal StandardHoursPerDay { get; set; }

    [Required]
    [Range(0, 168)]
    public decimal StandardHoursPerWeek { get; set; }

    public bool HasFlexibleStartTime { get; set; }
    public TimeSpan? FlexibleStartTimeEarliest { get; set; }
    public TimeSpan? FlexibleStartTimeLatest { get; set; }
    public bool HasFlexibleEndTime { get; set; }
    public TimeSpan? FlexibleEndTimeEarliest { get; set; }
    public TimeSpan? FlexibleEndTimeLatest { get; set; }
    public bool HasCoreHours { get; set; }
    public TimeSpan? CoreHoursStart { get; set; }
    public TimeSpan? CoreHoursEnd { get; set; }
    public bool HasMandatoryBreak { get; set; }
    public int? BreakDurationMinutes { get; set; }
    public bool IsBreakPaid { get; set; }
    public bool WorksMonday { get; set; }
    public bool WorksTuesday { get; set; }
    public bool WorksWednesday { get; set; }
    public bool WorksThursday { get; set; }
    public bool WorksFriday { get; set; }
    public bool WorksSaturday { get; set; }
    public bool WorksSunday { get; set; }
    public bool AllowsOvertime { get; set; }
    public bool OvertimeRequiresPreApproval { get; set; }
    public decimal? MaxOvertimeHoursPerDay { get; set; }
    public decimal? MaxOvertimeHoursPerWeek { get; set; }
    public int? LateGracePeriodMinutes { get; set; }
    public int? EarlyDepartureGracePeriodMinutes { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE WORK SCHEDULE DTOs
// Assignment of a WorkSchedule to an Employee.
// ============================================================================

#region Employee Work Schedule

public class EmployeeWorkScheduleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid WorkScheduleId { get; set; }
    public string WorkScheduleName { get; set; } = string.Empty;
    public WorkScheduleType WorkScheduleType { get; set; }
    public string WorkScheduleTypeName => WorkScheduleType.ToString();
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? AssignmentReason { get; set; }
    public Guid? AssignedById { get; set; }
    public string? AssignedByName { get; set; }
}

public class AssignEmployeeWorkScheduleDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid WorkScheduleId { get; set; }

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(500)]
    public string? AssignmentReason { get; set; }

    public Guid? AssignedById { get; set; }
}

public class UpdateEmployeeWorkScheduleDto : UpdateDtoBase
{
    [Required]
    public Guid WorkScheduleId { get; set; }

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(500)]
    public string? AssignmentReason { get; set; }
}

#endregion

// ============================================================================
// SHIFT DEFINITION DTOs
// A named shift within a WorkSchedule.
// ============================================================================

#region Shift Definition

public class ShiftDefinitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid WorkScheduleId { get; set; }
    public string WorkScheduleName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ShiftType Type { get; set; }
    public string TypeName => Type.ToString();
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal ShiftHours { get; set; }
    public bool IsNightShift { get; set; }
    public bool AttractsNightAllowance { get; set; }
    public bool AllowsOvertime { get; set; }
    public bool HasShiftDifferential { get; set; }
    public decimal? ShiftDifferentialPercentage { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class ShiftDefinitionSummaryDto
{
    public Guid Id { get; set; }
    public Guid WorkScheduleId { get; set; }
    public string WorkScheduleName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public ShiftType Type { get; set; }
    public string TypeName => Type.ToString();
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal ShiftHours { get; set; }
    public bool IsNightShift { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateShiftDefinitionDto : CreateDtoBase
{
    [Required]
    public Guid WorkScheduleId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ShiftName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public ShiftType Type { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [Range(0.5, 24)]
    public decimal ShiftHours { get; set; }

    public bool IsNightShift { get; set; }
    public bool AttractsNightAllowance { get; set; }
    public bool AllowsOvertime { get; set; }
    public bool HasShiftDifferential { get; set; }

    [Range(0, 100)]
    public decimal? ShiftDifferentialPercentage { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateShiftDefinitionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string ShiftName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public ShiftType Type { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [Range(0.5, 24)]
    public decimal ShiftHours { get; set; }

    public bool IsNightShift { get; set; }
    public bool AttractsNightAllowance { get; set; }
    public bool AllowsOvertime { get; set; }
    public bool HasShiftDifferential { get; set; }

    [Range(0, 100)]
    public decimal? ShiftDifferentialPercentage { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// SHIFT ASSIGNMENT DTOs
// Assigns a ShiftDefinition to an Employee for a date window.
// ============================================================================

#region Shift Assignment

public class ShiftAssignmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid ShiftDefinitionId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan ShiftStartTime { get; set; }
    public TimeSpan ShiftEndTime { get; set; }
    public DateTime AssignmentDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }
    public string? RecurrencePatternName => RecurrencePattern?.ToString();
    public Guid? AssignedById { get; set; }
    public string? AssignedByName { get; set; }
    public string? Notes { get; set; }
}

public class CreateShiftAssignmentDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ShiftDefinitionId { get; set; }

    [Required]
    public DateTime AssignmentDate { get; set; }

    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }
    public Guid? AssignedById { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateShiftAssignmentDto : UpdateDtoBase
{
    [Required]
    public Guid ShiftDefinitionId { get; set; }

    [Required]
    public DateTime AssignmentDate { get; set; }

    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// SHIFT ROTATION DTOs
// Rotation plan that cycles employees through a sequence of shifts.
// ============================================================================

#region Shift Rotation

public class ShiftRotationPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ShiftRotationCycle RotationCycle { get; set; }
    public string RotationCycleName => RotationCycle.ToString();
    public int CycleLengthDays { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public List<ShiftRotationStageDto> Stages { get; set; } = new();
    public List<ShiftRotationMemberDto> Members { get; set; } = new();
}

public class ShiftRotationPlanSummaryDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public ShiftRotationCycle RotationCycle { get; set; }
    public string RotationCycleName => RotationCycle.ToString();
    public int CycleLengthDays { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }
    public int StageCount { get; set; }
    public int MemberCount { get; set; }
}

public class CreateShiftRotationPlanDto : CreateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public ShiftRotationCycle RotationCycle { get; set; }

    [Required]
    [Range(1, 365)]
    public int CycleLengthDays { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateShiftRotationPlanDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ShiftRotationStageDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ShiftRotationPlanId { get; set; }
    public int StageOrder { get; set; }
    public Guid ShiftDefinitionId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan ShiftStartTime { get; set; }
    public TimeSpan ShiftEndTime { get; set; }
    public int DurationCycles { get; set; }
    public string? Label { get; set; }
}

public class CreateShiftRotationStageDto : CreateDtoBase
{
    [Required]
    public Guid ShiftRotationPlanId { get; set; }

    [Required]
    [Range(1, 100)]
    public int StageOrder { get; set; }

    [Required]
    public Guid ShiftDefinitionId { get; set; }

    [Required]
    [Range(1, 12)]
    public int DurationCycles { get; set; } = 1;

    [MaxLength(200)]
    public string? Label { get; set; }
}

public class ShiftRotationMemberDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ShiftRotationPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public int CurrentStageOrder { get; set; }
    public DateOnly JoinDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public string? Notes { get; set; }
}

public class AddShiftRotationMemberDto : CreateDtoBase
{
    [Required]
    public Guid ShiftRotationPlanId { get; set; }

    public Guid? EmployeeId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? TeamId { get; set; }

    [Required]
    public DateOnly JoinDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// POSITION OVERTIME POLICY DTOs
// Overtime / night / shift allowance eligibility at the Position level.
// ============================================================================

#region Position Overtime Policy

public class PositionOvertimePolicyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public OvertimeAllowanceType AllowanceType { get; set; }
    public string AllowanceTypeName => AllowanceType.ToString();
    public bool IsEligible { get; set; }
    public bool IsExempt { get; set; }
    public string? ExemptionReason { get; set; }
    public decimal? MaxHoursPerDay { get; set; }
    public decimal? MaxHoursPerWeek { get; set; }
    public bool RequiresPreApproval { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class CreatePositionOvertimePolicyDto : CreateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public OvertimeAllowanceType AllowanceType { get; set; }

    public bool IsEligible { get; set; }
    public bool IsExempt { get; set; }

    [MaxLength(1000)]
    public string? ExemptionReason { get; set; }

    [Range(0, 24)]
    public decimal? MaxHoursPerDay { get; set; }

    [Range(0, 168)]
    public decimal? MaxHoursPerWeek { get; set; }

    public bool RequiresPreApproval { get; set; }

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdatePositionOvertimePolicyDto : UpdateDtoBase
{
    public bool IsEligible { get; set; }
    public bool IsExempt { get; set; }

    [MaxLength(1000)]
    public string? ExemptionReason { get; set; }

    [Range(0, 24)]
    public decimal? MaxHoursPerDay { get; set; }

    [Range(0, 168)]
    public decimal? MaxHoursPerWeek { get; set; }

    public bool RequiresPreApproval { get; set; }

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE OVERTIME OVERRIDE DTOs
// Per-employee override of a PositionOvertimePolicy.
// ============================================================================

#region Employee Overtime Override

public class EmployeeOvertimeOverrideDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid? PolicyId { get; set; }
    public string? PolicyDescription { get; set; }
    public OvertimeAllowanceType AllowanceType { get; set; }
    public string AllowanceTypeName => AllowanceType.ToString();
    public bool IsEligible { get; set; }
    public bool IsExempt { get; set; }
    public string OverrideReason { get; set; } = string.Empty;
    public Guid ApprovedById { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime ApprovalDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public class CreateEmployeeOvertimeOverrideDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? PolicyId { get; set; }

    [Required]
    public OvertimeAllowanceType AllowanceType { get; set; }

    public bool IsEligible { get; set; }
    public bool IsExempt { get; set; }

    [Required]
    [MaxLength(1000)]
    public string OverrideReason { get; set; } = string.Empty;

    [Required]
    public Guid ApprovedById { get; set; }

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}

public class UpdateEmployeeOvertimeOverrideDto : UpdateDtoBase
{
    public bool IsEligible { get; set; }
    public bool IsExempt { get; set; }

    [Required]
    [MaxLength(1000)]
    public string OverrideReason { get; set; } = string.Empty;

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}

#endregion

// ============================================================================
// STAFF OVERTIME REQUEST DTOs
// Pre-approval and supervisor confirmation workflow for overtime.
// ============================================================================

#region Staff Overtime Request

public class StaffOvertimeRequestDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; }
    public DateTime OvertimeDate { get; set; }

    // Planned
    public TimeSpan PlannedStartTime { get; set; }
    public TimeSpan PlannedEndTime { get; set; }
    public decimal PlannedOvertimeHours { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? TaskDetails { get; set; }
    public OvertimeType Type { get; set; }
    public string TypeName => Type.ToString();

    public OvertimeRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();

    // Level 2: Pre-Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Level 3: Supervisor Confirmation
    public decimal? ActualOvertimeHours { get; set; }
    public Guid? SupervisorConfirmedById { get; set; }
    public string? SupervisorConfirmedByName { get; set; }
    public DateTime? SupervisorConfirmedDate { get; set; }
    public string? SupervisorNotes { get; set; }

    public Guid? AttendanceId { get; set; }
}

public class StaffOvertimeRequestSummaryDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime OvertimeDate { get; set; }
    public decimal PlannedOvertimeHours { get; set; }
    public decimal? ActualOvertimeHours { get; set; }
    public OvertimeType Type { get; set; }
    public string TypeName => Type.ToString();
    public OvertimeRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime RequestDate { get; set; }
}

public class CreateStaffOvertimeRequestDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateTime OvertimeDate { get; set; }

    [Required]
    public TimeSpan PlannedStartTime { get; set; }

    [Required]
    public TimeSpan PlannedEndTime { get; set; }

    [Required]
    [Range(0.5, 24)]
    public decimal PlannedOvertimeHours { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Purpose { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? TaskDetails { get; set; }

    [Required]
    public OvertimeType Type { get; set; }
}

public class ApproveOvertimeRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovalDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }
}

public class RejectOvertimeRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

public class ConfirmOvertimeRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid SupervisorConfirmedById { get; set; }

    [Required]
    [Range(0.5, 24)]
    public decimal ActualOvertimeHours { get; set; }

    public DateTime ConfirmedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? SupervisorNotes { get; set; }

    public Guid? AttendanceId { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE BIOMETRIC DTOs
// Biometric templates enrolled for employees.
// ============================================================================

#region Employee Biometric

public class EmployeeBiometricDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public BiometricType BiometricType { get; set; }
    public string BiometricTypeName => BiometricType.ToString();
    public string? BodyPart { get; set; }
    public FingerPosition? FingerPosition { get; set; }
    public string? FingerPositionName => FingerPosition?.ToString();
    public string? TemplateFormat { get; set; }
    public int? QualityScore { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceModel { get; set; }
    public DateTime EnrolledDate { get; set; }
    public Guid? EnrolledById { get; set; }
    public string? EnrolledByName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? RevokedReason { get; set; }
}

public class EmployeeBiometricSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public BiometricType BiometricType { get; set; }
    public string BiometricTypeName => BiometricType.ToString();
    public string? BodyPart { get; set; }
    public FingerPosition? FingerPosition { get; set; }
    public int? QualityScore { get; set; }
    public DateTime EnrolledDate { get; set; }
    public bool IsActive { get; set; }
}

public class EnrollBiometricDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public BiometricType BiometricType { get; set; }

    [MaxLength(100)]
    public string? BodyPart { get; set; }

    public FingerPosition? FingerPosition { get; set; }

    [Required]
    [MaxLength(2000)]
    public string BiometricData { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TemplateFormat { get; set; }

    [Range(0, 100)]
    public int? QualityScore { get; set; }

    [MaxLength(100)]
    public string? DeviceId { get; set; }

    [MaxLength(200)]
    public string? DeviceModel { get; set; }

    public Guid? EnrolledById { get; set; }
}

public class RevokeBiometricDto
{
    [Required]
    public Guid BiometricId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RevokedReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// STAFF ATTENDANCE DEVICE DTOs
// Registered biometric or RFID devices.
// ============================================================================

#region Staff Attendance Device

public class StaffAttendanceDeviceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? Manufacturer { get; set; }
    public string? FirmwareVersion { get; set; }
    public AttendanceDeviceType DeviceType { get; set; }
    public string DeviceTypeName => DeviceType.ToString();
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string LocationDescription { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public int? PendingSyncCount { get; set; }
    public string? Notes { get; set; }
}

public class StaffAttendanceDeviceSummaryDto
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public AttendanceDeviceType DeviceType { get; set; }
    public string DeviceTypeName => DeviceType.ToString();
    public string? LocationName { get; set; }
    public string LocationDescription { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public int? PendingSyncCount { get; set; }
}

public class CreateStaffAttendanceDeviceDto : CreateDtoBase
{
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

    [Required]
    public AttendanceDeviceType DeviceType { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    [MaxLength(500)]
    public string LocationDescription { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public int? Port { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateStaffAttendanceDeviceDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string DeviceName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DeviceModel { get; set; }

    [MaxLength(100)]
    public string? FirmwareVersion { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    [MaxLength(500)]
    public string LocationDescription { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public int? Port { get; set; }
    public bool IsActive { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// GEOFENCE ZONE DTOs
// GPS zone within which employees must be located when clocking in/out.
// ============================================================================

#region Geofence Zone

public class GeofenceZoneDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GeofenceShape Shape { get; set; }
    public string ShapeName => Shape.ToString();
    public double? CentreLatitude { get; set; }
    public double? CentreLongitude { get; set; }
    public double? RadiusMetres { get; set; }
    public string? PolygonCoordinatesJson { get; set; }
    public bool SoftEnforcement { get; set; }
    public bool HardEnforcement { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class GeofenceZoneSummaryDto
{
    public Guid Id { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public GeofenceShape Shape { get; set; }
    public string ShapeName => Shape.ToString();
    public double? CentreLatitude { get; set; }
    public double? CentreLongitude { get; set; }
    public double? RadiusMetres { get; set; }
    /// <summary>Carried on the summary so the register's edit dialog can round-trip a polygon zone without wiping it.</summary>
    public string? PolygonCoordinatesJson { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool SoftEnforcement { get; set; }
    public bool HardEnforcement { get; set; }
    public bool IsActive { get; set; }
}

public class CreateGeofenceZoneDto : CreateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string ZoneName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public GeofenceShape Shape { get; set; }

    public double? CentreLatitude { get; set; }
    public double? CentreLongitude { get; set; }

    [Range(1, 100000)]
    public double? RadiusMetres { get; set; }

    [MaxLength(8000)]
    public string? PolygonCoordinatesJson { get; set; }

    public bool SoftEnforcement { get; set; } = true;
    public bool HardEnforcement { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateGeofenceZoneDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string ZoneName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public GeofenceShape Shape { get; set; }

    public double? CentreLatitude { get; set; }
    public double? CentreLongitude { get; set; }

    [Range(1, 100000)]
    public double? RadiusMetres { get; set; }

    [MaxLength(8000)]
    public string? PolygonCoordinatesJson { get; set; }

    public bool SoftEnforcement { get; set; }
    public bool HardEnforcement { get; set; }
    public bool IsActive { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// REMOTE WORK REQUEST DTOs
// Formal request for an employee to work from home or a remote location.
// ============================================================================

#region Remote Work Request

public class RemoteWorkRequestDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RequestedDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? RemoteLocation { get; set; }
    public bool EquipmentConfirmed { get; set; }

    public RemoteWorkRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();

    // Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }
}

public class RemoteWorkRequestSummaryDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RequestedDays { get; set; }
    public RemoteWorkRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime RequestDate { get; set; }
}

public class CreateRemoteWorkRequestDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? RemoteLocation { get; set; }

    public bool EquipmentConfirmed { get; set; }
}

public class ApproveRemoteWorkRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovalDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }
}

public class RejectRemoteWorkRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// HOLIDAY CALENDAR & PUBLIC HOLIDAY DTOs
// ============================================================================

#region Holiday Calendar

public class HolidayCalendarDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CalendarName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? Region { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public List<PublicHolidaySummaryDto> PublicHolidays { get; set; } = new();

    /// <summary>Set when the save changed which calendar is in use and so recounted granted leave (company-schedule lane 1c).</summary>
    public LeaveRechargeResultDto? LeaveRecharge { get; set; }
}

public class HolidayCalendarSummaryDto
{
    public Guid Id { get; set; }
    public string CalendarName { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public string? Region { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int HolidayCount { get; set; }
}

public class CreateHolidayCalendarDto : CreateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string CalendarName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateHolidayCalendarDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string CalendarName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Public Holiday

public class PublicHolidayDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid HolidayCalendarId { get; set; }
    public string CalendarName { get; set; } = string.Empty;
    public string HolidayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public int Year { get; set; }
    public HolidayObservanceType ObservanceType { get; set; }
    public string ObservanceTypeName => ObservanceType.ToString();
    public DateOnly? SubstitutionDate { get; set; }
    public bool AttractsHolidayPay { get; set; }
    public decimal? HolidayPayMultiplier { get; set; }
    public bool IsRecurringAnnually { get; set; }
    public bool IsActive { get; set; }

    /// <summary>The granted leave this save recounted (company-schedule lane 1c, D-15b). Set on add and update only.</summary>
    public LeaveRechargeResultDto? LeaveRecharge { get; set; }
}

public class PublicHolidaySummaryDto
{
    public Guid Id { get; set; }
    public string HolidayName { get; set; } = string.Empty;
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public int Year { get; set; }
    public HolidayObservanceType ObservanceType { get; set; }
    public string ObservanceTypeName => ObservanceType.ToString();
    public bool AttractsHolidayPay { get; set; }
    public bool IsActive { get; set; }

    // ⚠ Round 5, lane N4: every field the update writes. The calendar screen's edit dialog is filled
    // from this row, and without these four a rename blanked the description, the observed date (a
    // weekend holiday's Monday) and the pay multiplier, and reset the recurring flag — the echo shape
    // of finding L-13.
    public string? Description { get; set; }
    public DateOnly? SubstitutionDate { get; set; }
    public decimal? HolidayPayMultiplier { get; set; }
    public bool IsRecurringAnnually { get; set; }
}

public class CreatePublicHolidayDto : CreateDtoBase
{
    [Required]
    public Guid HolidayCalendarId { get; set; }

    [Required]
    [MaxLength(200)]
    public string HolidayName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateOnly DateFrom { get; set; }

    [Required]
    public DateOnly DateTo { get; set; }

    [Required]
    public HolidayObservanceType ObservanceType { get; set; }

    public DateOnly? SubstitutionDate { get; set; }
    public bool AttractsHolidayPay { get; set; } = true;

    [Range(0.1, 10)]
    public decimal? HolidayPayMultiplier { get; set; }

    public bool IsRecurringAnnually { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class UpdatePublicHolidayDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string HolidayName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateOnly DateFrom { get; set; }

    [Required]
    public DateOnly DateTo { get; set; }

    [Required]
    public HolidayObservanceType ObservanceType { get; set; }

    public DateOnly? SubstitutionDate { get; set; }
    public bool AttractsHolidayPay { get; set; }

    [Range(0.1, 10)]
    public decimal? HolidayPayMultiplier { get; set; }

    public bool IsRecurringAnnually { get; set; }
    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// PAY PERIOD DTOs
// Payroll cutoff periods scoped to attendance summaries.
// ============================================================================

#region Pay Period

public class PayPeriodDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public PayPeriodType Type { get; set; }
    public string TypeName => Type.ToString();
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PayPeriodStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ExportedDate { get; set; }
    public Guid? ExportedById { get; set; }
    public string? ExportedByName { get; set; }
    public string? Notes { get; set; }
    public int SummaryCount { get; set; }
    public int ExportCount { get; set; }
}

public class PayPeriodSummaryDto
{
    public Guid Id { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public PayPeriodType Type { get; set; }
    public string TypeName => Type.ToString();
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PayPeriodStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ClosedDate { get; set; }
    public DateTime? ExportedDate { get; set; }
}

public class CreatePayPeriodDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string PeriodName { get; set; } = string.Empty;

    [Required]
    public PayPeriodType Type { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ClosePayPeriodDto
{
    [Required]
    public Guid PayPeriodId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE PAYROLL EXPORT DTOs
// Audit trail of attendance data exports to payroll systems.
// ============================================================================

#region Staff Attendance Payroll Export

public class StaffAttendancePayrollExportDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ExportReference { get; set; } = string.Empty;
    public Guid PayPeriodId { get; set; }
    public string PayPeriodName { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public Guid ExportedById { get; set; }
    public string ExportedByName { get; set; } = string.Empty;
    public string? TargetSystem { get; set; }
    public int TotalEmployees { get; set; }
    public int TotalRecords { get; set; }
    public PayrollExportStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? ErrorDetails { get; set; }
    public string? Notes { get; set; }
}

public class StaffAttendancePayrollExportSummaryDto
{
    public Guid Id { get; set; }
    public string ExportReference { get; set; } = string.Empty;
    public string PayPeriodName { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public string ExportedByName { get; set; } = string.Empty;
    public string? TargetSystem { get; set; }
    public int TotalEmployees { get; set; }
    public int TotalRecords { get; set; }
    public PayrollExportStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateStaffAttendancePayrollExportDto : CreateDtoBase
{
    [Required]
    public Guid PayPeriodId { get; set; }

    [Required]
    public Guid ExportedById { get; set; }

    [MaxLength(100)]
    public string? TargetSystem { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE ALERT RULE DTOs
// Configurable rules that define when attendance alerts fire.
// ============================================================================

#region Staff Attendance Alert Rule

public class StaffAttendanceAlertRuleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AttendanceAlertTriggerType TriggerType { get; set; }
    public string TriggerTypeName => TriggerType.ToString();
    public AttendanceAlertSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public decimal ThresholdValue { get; set; }
    public int? EvaluationWindowDays { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public bool NotifyByEmail { get; set; }
    public bool NotifyInApp { get; set; }
    public string? NotifyRecipientsJson { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public bool IsActive { get; set; }
    public int ActiveAlertCount { get; set; }
}

public class StaffAttendanceAlertRuleSummaryDto
{
    public Guid Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public AttendanceAlertTriggerType TriggerType { get; set; }
    public string TriggerTypeName => TriggerType.ToString();
    public AttendanceAlertSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public decimal ThresholdValue { get; set; }
    public bool IsActive { get; set; }
    public int ActiveAlertCount { get; set; }
}

public class CreateStaffAttendanceAlertRuleDto : CreateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string RuleName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public AttendanceAlertTriggerType TriggerType { get; set; }

    [Required]
    public AttendanceAlertSeverity Severity { get; set; }

    [Required]
    [Range(0, 10000)]
    public decimal ThresholdValue { get; set; }

    [Range(1, 365)]
    public int? EvaluationWindowDays { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? EmployeeId { get; set; }
    public bool NotifyByEmail { get; set; } = true;
    public bool NotifyInApp { get; set; } = true;

    [MaxLength(1000)]
    public string? NotifyRecipientsJson { get; set; }

    public bool RequiresAcknowledgement { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateStaffAttendanceAlertRuleDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string RuleName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public AttendanceAlertSeverity Severity { get; set; }

    [Required]
    [Range(0, 10000)]
    public decimal ThresholdValue { get; set; }

    [Range(1, 365)]
    public int? EvaluationWindowDays { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? EmployeeId { get; set; }
    public bool NotifyByEmail { get; set; }
    public bool NotifyInApp { get; set; }

    [MaxLength(1000)]
    public string? NotifyRecipientsJson { get; set; }

    public bool RequiresAcknowledgement { get; set; }
    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE ALERT DTOs
// Fired alert instances with full acknowledgement / resolution lifecycle.
// ============================================================================

#region Staff Attendance Alert

public class StaffAttendanceAlertDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AlertRuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public AttendanceAlertTriggerType TriggerType { get; set; }
    public string TriggerTypeName => TriggerType.ToString();
    public AttendanceAlertSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public AttendanceAlertStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime TriggeredDate { get; set; }
    public string TriggerDescription { get; set; } = string.Empty;
    public decimal? TriggerValue { get; set; }

    // Acknowledgement
    public Guid? AcknowledgedById { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public string? AcknowledgementNotes { get; set; }

    // Resolution
    public Guid? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }

    // Dismissal
    public Guid? DismissedById { get; set; }
    public string? DismissedByName { get; set; }
    public DateTime? DismissedDate { get; set; }
    public string? DismissalReason { get; set; }
}

public class StaffAttendanceAlertSummaryDto
{
    public Guid Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public AttendanceAlertTriggerType TriggerType { get; set; }
    public string TriggerTypeName => TriggerType.ToString();
    public AttendanceAlertSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public AttendanceAlertStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime TriggeredDate { get; set; }
    public string TriggerDescription { get; set; } = string.Empty;
}

public class AcknowledgeAlertDto
{
    [Required]
    public Guid AlertId { get; set; }

    [Required]
    public Guid AcknowledgedById { get; set; }

    public DateTime AcknowledgedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? AcknowledgementNotes { get; set; }
}

public class ResolveAlertDto
{
    [Required]
    public Guid AlertId { get; set; }

    [Required]
    public Guid ResolvedById { get; set; }

    public DateTime ResolvedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ResolutionNotes { get; set; }
}

public class DismissAlertDto
{
    [Required]
    public Guid AlertId { get; set; }

    [Required]
    public Guid DismissedById { get; set; }

    public DateTime DismissedDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(500)]
    public string DismissalReason { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// CONSULTANT CLIENT DTOs
// External client organisations that consultants are placed with.
// ============================================================================

#region Consultant Client

public class ConsultantClientDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Description { get; set; }

    // Primary Contact
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? PrimaryContactPhone { get; set; }

    // Address
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    /// <summary>The Finance customer this client is billed as; null = invoices stay HR-side (slice 6).</summary>
    public Guid? FinanceCustomerId { get; set; }
    public string? CountryName { get; set; }

    // Billing Contact
    public string? BillingContactName { get; set; }
    public string? BillingContactEmail { get; set; }
    public string? BillingContactPhone { get; set; }
    public string? TaxIdentificationNumber { get; set; }
    public string Currency { get; set; } = "GHS";
    public int? DefaultPaymentTermsDays { get; set; }

    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    // Derived counts
    public int ActiveEngagementCount { get; set; }
    public int TimesheetCount { get; set; }
    public int OutstandingInvoiceCount { get; set; }

    public List<ClientEngagementSummaryDto> Engagements { get; set; } = new();
}

public class ConsultantClientSummaryDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? City { get; set; }
    public string? CountryName { get; set; }
    public string Currency { get; set; } = "GHS";
    public bool IsActive { get; set; }
    public int ActiveEngagementCount { get; set; }
}

public class CreateConsultantClientDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ClientName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ClientCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Industry { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(500)]
    public string? AddressLine1 { get; set; }

    [MaxLength(500)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }
    public Guid? FinanceCustomerId { get; set; }

    [MaxLength(200)]
    public string? BillingContactName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? BillingContactEmail { get; set; }

    [MaxLength(50)]
    public string? BillingContactPhone { get; set; }

    [MaxLength(50)]
    public string? TaxIdentificationNumber { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Range(0, 365)]
    public int? DefaultPaymentTermsDays { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateConsultantClientDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ClientName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Industry { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(500)]
    public string? AddressLine1 { get; set; }

    [MaxLength(500)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }
    public Guid? FinanceCustomerId { get; set; }

    [MaxLength(200)]
    public string? BillingContactName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? BillingContactEmail { get; set; }

    [MaxLength(50)]
    public string? BillingContactPhone { get; set; }

    [MaxLength(50)]
    public string? TaxIdentificationNumber { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Range(0, 365)]
    public int? DefaultPaymentTermsDays { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// CLIENT ENGAGEMENT DTOs
// A specific placement contract between a client and a consultant employee.
// ============================================================================

#region Client Engagement

public class ClientEngagementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string EngagementCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Client
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;

    // Consultant
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public string ConsultantNumber { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // Billing Terms
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "GHS";
    public BillingCycle BillingCycle { get; set; }
    public string BillingCycleName => BillingCycle.ToString();
    public decimal? MaxHoursPerWeek { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PurchaseOrderNumber { get; set; }

    public ClientEngagementStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }

    public int TimesheetCount { get; set; }
    public List<ConsultantTimesheetSummaryDto> Timesheets { get; set; } = new();
}

public class ClientEngagementSummaryDto
{
    public Guid Id { get; set; }
    public string EngagementCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "GHS";
    public BillingCycle BillingCycle { get; set; }
    public string BillingCycleName => BillingCycle.ToString();
    public ClientEngagementStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateClientEngagementDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string EngagementCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public Guid ConsultantId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Required]
    public BillingCycle BillingCycle { get; set; }

    [Range(0, 168)]
    public decimal? MaxHoursPerWeek { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ContractValue { get; set; }

    [MaxLength(100)]
    public string? PurchaseOrderNumber { get; set; }

    public ClientEngagementStatus Status { get; set; } = ClientEngagementStatus.Active;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateClientEngagementDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateOnly? EndDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Required]
    public BillingCycle BillingCycle { get; set; }

    [Range(0, 168)]
    public decimal? MaxHoursPerWeek { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ContractValue { get; set; }

    [MaxLength(100)]
    public string? PurchaseOrderNumber { get; set; }

    [Required]
    public ClientEngagementStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET DTOs
// Timesheet covering a billing period for a consultant at a client site.
// ============================================================================

#region Consultant Timesheet

public class ConsultantTimesheetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string TimesheetNumber { get; set; } = string.Empty;

    // Consultant
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public string ConsultantNumber { get; set; } = string.Empty;

    // Client
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;

    // Engagement
    public Guid? EngagementId { get; set; }
    public string? EngagementCode { get; set; }
    public string? EngagementTitle { get; set; }

    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public decimal TotalHours { get; set; }

    public TimesheetStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? SubmittedDate { get; set; }
    public string? Notes { get; set; }

    public List<ConsultantTimesheetEntryDto> Entries { get; set; } = new();
    public List<ClientTimesheetConfirmationDto> Confirmations { get; set; } = new();
}

public class ConsultantTimesheetSummaryDto
{
    public Guid Id { get; set; }
    public string TimesheetNumber { get; set; } = string.Empty;
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public decimal TotalHours { get; set; }
    public TimesheetStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? SubmittedDate { get; set; }
}

public class CreateConsultantTimesheetDto : CreateDtoBase
{
    [Required]
    public Guid ConsultantId { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    public Guid? EngagementId { get; set; }

    [Required]
    public DateOnly PeriodStartDate { get; set; }

    [Required]
    public DateOnly PeriodEndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateConsultantTimesheetDto : UpdateDtoBase
{
    public Guid? EngagementId { get; set; }
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class SubmitTimesheetDto
{
    [Required]
    public Guid TimesheetId { get; set; }

    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET ENTRY DTOs
// A single day's time entry within a ConsultantTimesheet.
// ============================================================================

#region Consultant Timesheet Entry

public class ConsultantTimesheetEntryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid TimesheetId { get; set; }
    public string TimesheetNumber { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public decimal TotalHours { get; set; }
    public string ActivitySummary { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

public class CreateConsultantTimesheetEntryDto : CreateDtoBase
{
    [Required]
    public Guid TimesheetId { get; set; }

    [Required]
    public DateOnly WorkDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [Range(0, 480)]
    public int BreakMinutes { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ActivitySummary { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateConsultantTimesheetEntryDto : UpdateDtoBase
{
    [Required]
    public DateOnly WorkDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [Range(0, 480)]
    public int BreakMinutes { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ActivitySummary { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// CLIENT TIMESHEET CONFIRMATION DTOs
// Tokenised confirmation request sent to a client contact.
// ============================================================================

#region Client Timesheet Confirmation

public class ClientTimesheetConfirmationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid TimesheetId { get; set; }
    public string TimesheetNumber { get; set; } = string.Empty;
    public string ClientContactEmail { get; set; } = string.Empty;
    public string? ClientContactName { get; set; }
    public DateTime TokenExpiryDate { get; set; }
    public DateTime SentDate { get; set; }
    public Guid SentById { get; set; }
    public string SentByName { get; set; } = string.Empty;
    public TimesheetConfirmationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ViewedDate { get; set; }
    public DateTime? ConfirmedDate { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? ClientNotes { get; set; }
    public int ResendCount { get; set; }
}

public class SendTimesheetConfirmationDto : CreateDtoBase
{
    [Required]
    public Guid TimesheetId { get; set; }

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string ClientContactEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ClientContactName { get; set; }

    [Required]
    public Guid SentById { get; set; }

    public DateTime TokenExpiryDate { get; set; } = DateTime.UtcNow.AddDays(7);
}

public class ClientConfirmTimesheetDto
{
    [Required]
    public Guid ConfirmationToken { get; set; }

    [MaxLength(2000)]
    public string? ClientNotes { get; set; }
}

public class ClientRejectTimesheetDto
{
    [Required]
    public Guid ConfirmationToken { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ClientNotes { get; set; } = string.Empty;
}

public class SendTimesheetConfirmationRequestDto
{
    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string ClientContactEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ClientContactName { get; set; }

    public DateTime? TokenExpiryDate { get; set; }
}

public class SendTimesheetConfirmationResultDto
{
    public ClientTimesheetConfirmationDto Confirmation { get; set; } = new();
    public Guid ConfirmationToken { get; set; }
    public bool EmailSent { get; set; }
}

public class ClientTimesheetConfirmationPublicDto
{
    public string TimesheetNumber { get; set; } = string.Empty;
    public string ConsultantName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public decimal TotalHours { get; set; }
    public TimesheetConfirmationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsExpired { get; set; }
    public bool CanRespond { get; set; }
    public DateTime TokenExpiryDate { get; set; }
    public List<ConsultantTimesheetEntryDto> Entries { get; set; } = new();
}

#endregion

// ============================================================================
// CONSULTANT CLIENT PORTAL DTOs
// ============================================================================
// The bespoke auth DTO family (register/login/verify/forgot/reset/change-password/
// complete-setup + auth result) was retired 2026-08-31 with the PortalBearer portal.
// Contacts are invited by HR onto main-scheme Identity accounts; setup completion
// lives on api/auth (CompleteClientSetupRequest in DTOs/Auth).

#region Consultant Client Portal

public class ConsultantClientPortalInviteDto
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(100)]
    public string? ContactRole { get; set; }
}

public class ConsultantClientPortalResendInviteDto
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// A client contact as the HR screen sees it: the contact row joined with its Identity
/// account's status. Keeps the retired portal-account summary's JSON shape so the wired
/// invite panel needed no rework (Id is now the CONTACT row id; IsEmailVerified maps to
/// Identity EmailConfirmed; IsSetupPending = the invite has not been completed).
/// </summary>
public class ConsultantClientContactSummaryDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactRole { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsActive { get; set; }
    public bool IsSetupPending { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>One client organisation's section of the contact's portal dashboard.</summary>
public class ConsultantClientPortalClientSectionDto
{
    public Guid ConsultantClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;
    public string? ContactRole { get; set; }
    public int PendingConfirmationCount { get; set; }
    public List<ConsultantTimesheetSummaryDto> PendingTimesheets { get; set; } = new();
}

/// <summary>
/// The contact's dashboard. A contact invited by several client organisations holds one
/// contact row per client and sees one section per client — the retired single-client
/// shape could not represent that.
/// </summary>
public class ConsultantClientPortalDashboardDto
{
    public string Email { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public int PendingConfirmationCount { get; set; }
    public List<ConsultantClientPortalClientSectionDto> Clients { get; set; } = new();
}

public class ConsultantClientPortalConfirmTimesheetDto
{
    [MaxLength(2000)]
    public string? ClientNotes { get; set; }
}

public class ConsultantClientPortalRejectTimesheetDto
{
    [Required, MaxLength(2000)]
    public string ClientNotes { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// TIMESHEET INVOICE DTOs
// Invoice generated from one or more confirmed consultant timesheets.
// ============================================================================

#region Timesheet Invoice

public class TimesheetInvoiceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    // Client
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;

    // Consultant
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;

    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }

    // Financials
    public decimal TotalHours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GHS";

    public TimesheetInvoiceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? PaidDate { get; set; }
    public string? Notes { get; set; }

    public List<TimesheetInvoiceLinkDto> LinkedTimesheets { get; set; } = new();
}

public class TimesheetInvoiceSummaryDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public TimesheetInvoiceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly? DueDate { get; set; }
    public DateOnly? PaidDate { get; set; }
}

public class CreateTimesheetInvoiceDto : CreateDtoBase
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public Guid ConsultantId { get; set; }

    [Required]
    public DateOnly BillingPeriodStart { get; set; }

    [Required]
    public DateOnly BillingPeriodEnd { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal TaxPercentage { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public DateOnly? DueDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<Guid> TimesheetIds { get; set; } = new();
}

public class IssueTimesheetInvoiceDto
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public DateOnly IssuedDate { get; set; }

    public DateOnly? DueDate { get; set; }
}

public class RecordTimesheetInvoicePaymentDto
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public DateOnly PaidDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// TIMESHEET INVOICE LINK DTOs
// Junction linking confirmed ConsultantTimesheets to a TimesheetInvoice.
// ============================================================================

#region Timesheet Invoice Link

public class TimesheetInvoiceLinkDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid TimesheetId { get; set; }
    public string TimesheetNumber { get; set; } = string.Empty;
    public DateOnly TimesheetPeriodStart { get; set; }
    public DateOnly TimesheetPeriodEnd { get; set; }
    public decimal Hours { get; set; }
    public decimal Amount { get; set; }
}

public class AddTimesheetToInvoiceDto : CreateDtoBase
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public Guid TimesheetId { get; set; }
}

#endregion

// ============================================================================
// ATTENDANCE DASHBOARD DTOs
// Aggregated executive dashboard payload for attendance health and KPIs.
// ============================================================================

#region Attendance Dashboard

/// <summary>
/// Top-level dashboard payload for attendance module.
/// All metrics are computed server-side in a single request.
/// </summary>
public class AttendanceDashboardDto
{
    // ── Today's snapshot ───────────────────────────────────────────────────
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int RemoteToday { get; set; }
    public decimal AttendanceRateToday { get; set; }

    // ── Current pay period ─────────────────────────────────────────────────
    public string? CurrentPayPeriodName { get; set; }
    public DateOnly? CurrentPayPeriodStart { get; set; }
    public DateOnly? CurrentPayPeriodEnd { get; set; }
    public PayPeriodStatus? CurrentPayPeriodStatus { get; set; }
    public string? CurrentPayPeriodStatusName => CurrentPayPeriodStatus?.ToString();

    // ── Overtime ───────────────────────────────────────────────────────────
    public int PendingOvertimeRequests { get; set; }
    public int ApprovedOvertimeRequests { get; set; }
    public decimal TotalOvertimeHoursThisPeriod { get; set; }

    // ── Regularization backlog ─────────────────────────────────────────────
    public int PendingRegularizations { get; set; }

    // ── Remote work ────────────────────────────────────────────────────────
    public int PendingRemoteWorkRequests { get; set; }

    // ── Alerts ─────────────────────────────────────────────────────────────
    public int ActiveCriticalAlerts { get; set; }
    public int ActiveWarningAlerts { get; set; }
    public List<StaffAttendanceAlertSummaryDto> TopAlerts { get; set; } = new();

    // ── Attendance trend (last 7 days) ─────────────────────────────────────
    public List<DailyAttendanceTrendDto> DailyTrend { get; set; } = new();

    // ── Chronic absentees ──────────────────────────────────────────────────
    public List<AttendanceRiskEmployeeDto> ChronicAbsentees { get; set; } = new();

    // ── Unprocessed logs ───────────────────────────────────────────────────
    public int UnprocessedAttendanceLogs { get; set; }

    // ── Devices ────────────────────────────────────────────────────────────
    public int ActiveDevices { get; set; }
    public int DevicesWithPendingSync { get; set; }

    // ── Meta ───────────────────────────────────────────────────────────────
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

public class DailyAttendanceTrendDto
{
    public DateOnly Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int OnLeave { get; set; }
    public int Remote { get; set; }
    public decimal AttendanceRate { get; set; }
}

public class AttendanceRiskEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public int AbsentDaysLast30 { get; set; }
    public int LateDaysLast30 { get; set; }
    public decimal AttendancePercentageLast30 { get; set; }
}

/// <summary>Consultant billing summary for dashboard widgets.</summary>
public class ConsultantBillingSummaryDto
{
    public Guid ConsultantId { get; set; }
    public string ConsultantName { get; set; } = string.Empty;
    public int ActiveEngagements { get; set; }
    public int DraftTimesheets { get; set; }
    public int PendingInvoices { get; set; }
    public decimal UnbilledHours { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = "GHS";
}

#endregion

#region Update DTOs — Regularization

public class UpdateStaffAttendanceRegularizationDto : UpdateDtoBase
{
    public TimeSpan? RequestedCheckInTime { get; set; }
    public TimeSpan? RequestedCheckOutTime { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? SupportingDocuments { get; set; }
}

#endregion

#region Update DTOs — Monthly Summary

public class UpdateStaffMonthlyAttendanceSummaryDto : UpdateDtoBase
{
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Allow HR to manually correct counts
    public int? DaysPresent { get; set; }
    public int? DaysAbsent { get; set; }
    public int? DaysOnLeave { get; set; }
    public int? TotalLateMinutes { get; set; }
    public decimal? TotalOvertimeHours { get; set; }
}

#endregion

#region Update DTOs — Remote Work Request

public class UpdateRemoteWorkRequestDto : UpdateDtoBase
{
    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [MaxLength(500)]
    public string? WorkLocation { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region Update DTOs — Pay Period

public class UpdatePayPeriodDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string PeriodName { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Update DTOs — Timesheet Invoice

public class UpdateTimesheetInvoiceDto : UpdateDtoBase
{
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Update DTOs — Staff Overtime Request

public class UpdateStaffOvertimeRequestDto : UpdateDtoBase
{
    public decimal RequestedHours { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Update DTOs — Employee Biometric

public class UpdateEmployeeBiometricDto : UpdateDtoBase
{
    [MaxLength(2000)]
    public string? BiometricData { get; set; }

    [MaxLength(100)]
    public string? TemplateFormat { get; set; }

    public int? QualityScore { get; set; }
}

#endregion

#region Update DTOs — Shift Rotation Stage

public class UpdateShiftRotationStageDto : UpdateDtoBase
{
    public int StageOrder { get; set; }

    [Required]
    public Guid ShiftDefinitionId { get; set; }

    public int DurationDays { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Update DTOs — Shift Rotation Member

public class UpdateShiftRotationMemberDto : UpdateDtoBase
{
    public int CurrentStageOrder { get; set; }
    public DateOnly? NextRotationDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion
