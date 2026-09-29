using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Estate;

public sealed class EstateFacilityDutyAttendance : TenantEntity
{
    public Guid DutyRosterId { get; set; }
    public EstateFacilityDutyRoster? DutyRoster { get; set; }
    public DateTime DutyDate { get; set; }

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

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime RecordedAt { get; set; }
}
