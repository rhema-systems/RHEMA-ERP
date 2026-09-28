using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Estate;

public sealed class EstateFacilityDutyRoster : TenantEntity
{
    [Required, MaxLength(40)]
    public string RosterReference { get; set; } = string.Empty;

    public Guid? EmployeeProfileId { get; set; }

    [MaxLength(50)]
    public string? EmployeeNumber { get; set; }

    [Required, MaxLength(200)]
    public string StaffName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string StaffType { get; set; } = "Cleaner";

    [Required, MaxLength(60)]
    public string DutyType { get; set; } = "Cleaning";

    [MaxLength(120)]
    public string? PropertyReference { get; set; }

    [MaxLength(120)]
    public string? PropertyUnit { get; set; }

    [Required, MaxLength(60)]
    public string ServiceAreaType { get; set; } = "Common Area";

    [Required, MaxLength(200)]
    public string ServiceAreaName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Frequency { get; set; } = "Daily";

    [MaxLength(120)]
    public string? DayPattern { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [Required, MaxLength(20)]
    public string ShiftStart { get; set; } = "08:00";

    [Required, MaxLength(20)]
    public string ShiftEnd { get; set; } = "17:00";

    [MaxLength(200)]
    public string? SupervisorName { get; set; }

    [MaxLength(500)]
    public string? ToolsIssued { get; set; }

    [MaxLength(500)]
    public string? SuppliesIssued { get; set; }

    public Guid? InventoryIssueVoucherId { get; set; }

    [MaxLength(50)]
    public string? InventoryIssueVoucherNumber { get; set; }

    [MaxLength(1000)]
    public string? Checklist { get; set; }

    [Required, MaxLength(40)]
    public string AttendanceStatus { get; set; } = "Pending";

    [Required, MaxLength(40)]
    public string CompletionStatus { get; set; } = "Scheduled";

    [Required, MaxLength(40)]
    public string QualityStatus { get; set; } = "Not inspected";

    [MaxLength(120)]
    public string? LinkedMaintenanceReference { get; set; }

    [MaxLength(120)]
    public string? LinkedComplaintReference { get; set; }

    [MaxLength(120)]
    public string? LinkedProcedureCaseReference { get; set; }

    public DateTime? LastAttendanceAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
