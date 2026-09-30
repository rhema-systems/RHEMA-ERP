namespace ErpSystem.Core.DTOs.Estate;

public sealed record EstateFacilityDutyRosterDto(
    Guid Id,
    string RosterReference,
    DateTime? DutyDate,
    Guid? EmployeeProfileId,
    string? EmployeeNumber,
    string StaffName,
    string StaffType,
    string DutyType,
    string? PropertyReference,
    string? PropertyUnit,
    string ServiceAreaType,
    string ServiceAreaName,
    string Frequency,
    string? DayPattern,
    DateTime StartDate,
    DateTime? EndDate,
    string ShiftStart,
    string ShiftEnd,
    string? SupervisorName,
    Guid? SupervisorEmployeeId,
    string? ToolsIssued,
    string? SuppliesIssued,
    Guid? InventoryIssueVoucherId,
    string? InventoryIssueVoucherNumber,
    string? Checklist,
    string AttendanceStatus,
    string CompletionStatus,
    string QualityStatus,
    string? LinkedMaintenanceReference,
    string? LinkedComplaintReference,
    string? LinkedProcedureCaseReference,
    DateTime? LastAttendanceAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class UpsertEstateFacilityDutyRosterDto
{
    public Guid? EmployeeProfileId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffType { get; set; } = "Cleaner";
    public string DutyType { get; set; } = "Cleaning";
    public string? PropertyReference { get; set; }
    public string? PropertyUnit { get; set; }
    public string ServiceAreaType { get; set; } = "Common Area";
    public string ServiceAreaName { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Daily";
    public string? DayPattern { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ShiftStart { get; set; } = "08:00";
    public string ShiftEnd { get; set; } = "17:00";
    public string? SupervisorName { get; set; }
    public Guid? SupervisorEmployeeId { get; set; }
    public string? ToolsIssued { get; set; }
    public string? SuppliesIssued { get; set; }
    public Guid? InventoryIssueVoucherId { get; set; }
    public string? InventoryIssueVoucherNumber { get; set; }
    public string? Checklist { get; set; }
    public string AttendanceStatus { get; set; } = "Pending";
    public string CompletionStatus { get; set; } = "Scheduled";
    public string QualityStatus { get; set; } = "Not inspected";
    public string? LinkedMaintenanceReference { get; set; }
    public string? LinkedComplaintReference { get; set; }
    public string? LinkedProcedureCaseReference { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateEstateFacilityDutyAttendanceDto
{
    public string AttendanceStatus { get; set; } = "Present";
    public string CompletionStatus { get; set; } = "Completed";
    public string QualityStatus { get; set; } = "Pending inspection";
    public string? LinkedMaintenanceReference { get; set; }
    public string? LinkedComplaintReference { get; set; }
    public string? Notes { get; set; }
}
