using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class DailyAttendanceListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public DateTime AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public decimal? ActualWorkHours { get; set; }
    public decimal? OvertimeHours { get; set; }
    public bool IsLate { get; set; }
    public bool IsEarlyDeparture { get; set; }
}

// Detail DTO
public class DailyAttendanceDetailDto
{
    public Guid Id { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }

    // Date & Status
    public DateTime AttendanceDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public AttendanceStatus Status { get; set; }
    public string StatusName { get; set; }
    public string StatusReason { get; set; }

    // Work Schedule
    public Guid? WorkScheduleId { get; set; }
    public string WorkScheduleName { get; set; }
    public TimeSpan? ScheduledStartTime { get; set; }
    public TimeSpan? ScheduledEndTime { get; set; }
    public decimal ScheduledWorkHours { get; set; }

    // Check In/Out
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string CheckInLocation { get; set; }
    public string CheckOutLocation { get; set; }
    public string CheckInDevice { get; set; }
    public string CheckInIpAddress { get; set; }
    public string CheckOutDevice { get; set; }
    public string CheckOutIpAddress { get; set; }

    // Time Tracking
    public decimal ActualWorkHours { get; set; }
    public decimal? OvertimeHours { get; set; }
    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public int? EarlyDepartureMinutes { get; set; }

    // Leave/Holiday
    public Guid? LeaveApplicationId { get; set; }
    public string LeaveType { get; set; }
    public Guid? PublicHolidayId { get; set; }
    public string HolidayName { get; set; }

    // Remote Work
    public bool IsRemoteWork { get; set; }
    public string RemoteWorkLocation { get; set; }

    // Station
    public Guid? StationId { get; set; }
    public string StationName { get; set; }

    // Overtime
    public bool HasOvertimeApproval { get; set; }
    public Guid? OvertimeApprovedById { get; set; }
    public string OvertimeApprovedByName { get; set; }

    // Verification
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public Guid? VerifiedById { get; set; }
    public string VerifiedByName { get; set; }
    public string VerificationNotes { get; set; }

    // Exception Handling
    public bool IsException { get; set; }
    public string ExceptionReason { get; set; }

    public string Notes { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Manual Entry DTO
public class ManualAttendanceEntryDto
{
    public Guid EmployeeId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string StatusReason { get; set; }
    public string Notes { get; set; }
}

// Regularization DTOs
public class AttendanceRegularizationListDto
{
    public Guid Id { get; set; }
    public string RegularizationNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public DateTime AttendanceDate { get; set; }
    public RegularizationType RegularizationType { get; set; }
    public string RegularizationTypeName { get; set; }
    public DateTime RequestDate { get; set; }
    public RegularizationStatus Status { get; set; }
    public string StatusName { get; set; }
}

public class AttendanceRegularizationDetailDto
{
    public Guid Id { get; set; }
    public string RegularizationNumber { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }

    // Attendance Reference
    public Guid AttendanceId { get; set; }
    public DateTime AttendanceDate { get; set; }

    // Regularization Details
    public RegularizationType RegularizationType { get; set; }
    public string RegularizationTypeName { get; set; }
    public DateTime? RequestedCheckInTime { get; set; }
    public DateTime? RequestedCheckOutTime { get; set; }
    public string Reason { get; set; }
    public string SupportingDocuments { get; set; }

    // Current Values
    public DateTime? CurrentCheckInTime { get; set; }
    public DateTime? CurrentCheckOutTime { get; set; }

    // Status
    public RegularizationStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime RequestDate { get; set; }

    // Approval
    public DateTime? ApprovalDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public string ApprovalComments { get; set; }
    public string RejectionReason { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAttendanceRegularizationDto
{
    public Guid AttendanceId { get; set; }
    public RegularizationType RegularizationType { get; set; }
    public DateTime? RequestedCheckInTime { get; set; }
    public DateTime? RequestedCheckOutTime { get; set; }
    public string Reason { get; set; }
    public string SupportingDocuments { get; set; }
}

// Overtime Request DTOs
public class OvertimeRequestListDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public DateTime OvertimeDate { get; set; }
    public OvertimeType OvertimeType { get; set; }
    public string OvertimeTypeName { get; set; }
    public decimal PlannedOvertimeHours { get; set; }
    public OvertimeRequestStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime RequestDate { get; set; }
}

public class OvertimeRequestDetailDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }

    // Overtime Details
    public DateTime OvertimeDate { get; set; }
    public OvertimeType OvertimeType { get; set; }
    public string OvertimeTypeName { get; set; }
    public TimeSpan PlannedStartTime { get; set; }
    public TimeSpan PlannedEndTime { get; set; }
    public decimal PlannedOvertimeHours { get; set; }
    public string Purpose { get; set; }
    public string TaskDetails { get; set; }

    // Status
    public OvertimeRequestStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime RequestDate { get; set; }

    // Approval
    public DateTime? ApprovalDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public string ApprovalComments { get; set; }
    public string RejectionReason { get; set; }

    // Actual Time
    public Guid? AttendanceId { get; set; }
    public decimal? ActualOvertimeHours { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateOvertimeRequestDto
{
    public DateTime OvertimeDate { get; set; }
    public OvertimeType OvertimeType { get; set; }
    public TimeSpan PlannedStartTime { get; set; }
    public TimeSpan PlannedEndTime { get; set; }
    public string Purpose { get; set; }
    public string TaskDetails { get; set; }
}

// Monthly Summary DTO
public class MonthlyAttendanceSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; }

    // Counts
    public int TotalWorkingDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public int HalfDays { get; set; }
    public int LeaveDays { get; set; }
    public int PublicHolidays { get; set; }
    public int WeekendDays { get; set; }

    // Hours
    public decimal TotalScheduledHours { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalUndertimeHours { get; set; }

    // Percentages
    public decimal AttendancePercentage { get; set; }
    public decimal PunctualityPercentage { get; set; }

    // Status
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public string FinalizedByName { get; set; }
    public string Notes { get; set; }
}

// Work Schedule DTOs
public class WorkScheduleDto
{
    public Guid Id { get; set; }
    public string ScheduleName { get; set; }
    public string Description { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public string ScheduleTypeName { get; set; }
    public TimeSpan? StandardStartTime { get; set; }
    public TimeSpan? StandardEndTime { get; set; }
    public decimal StandardHoursPerDay { get; set; }
    public decimal StandardHoursPerWeek { get; set; }
    public bool IsMondayWorking { get; set; }
    public bool IsTuesdayWorking { get; set; }
    public bool IsWednesdayWorking { get; set; }
    public bool IsThursdayWorking { get; set; }
    public bool IsFridayWorking { get; set; }
    public bool IsSaturdayWorking { get; set; }
    public bool IsSundayWorking { get; set; }
    public bool IsFlexibleTime { get; set; }
    public TimeSpan? FlexibleStartTimeFrom { get; set; }
    public TimeSpan? FlexibleStartTimeTo { get; set; }
    public decimal? MaxOvertimeHoursPerDay { get; set; }
    public decimal? MaxOvertimeHoursPerWeek { get; set; }
    public bool IsActive { get; set; }
}

// Dashboard DTO
public class AttendanceDashboardDto
{
    public DateTime Date { get; set; }
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int LateToday { get; set; }
    public int RemoteWorkToday { get; set; }
    public decimal AttendanceRateToday { get; set; }
    public decimal AttendanceRateThisMonth { get; set; }
    public int PendingRegularizations { get; set; }
    public int PendingOvertimeRequests { get; set; }
    public Dictionary<AttendanceStatus, int> TodayAttendanceByStatus { get; set; }
    public List<DailyAttendanceListDto> LateArrivals { get; set; }
    public List<DailyAttendanceListDto> EarlyDepartures { get; set; }
}