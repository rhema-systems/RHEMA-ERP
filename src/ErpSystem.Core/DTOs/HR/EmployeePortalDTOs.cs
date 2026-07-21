namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// EMPLOYEE PORTAL DTOs
// Employee-scoped, employee-safe data transfer objects for the self-service portal
// ============================================================================

/// <summary>
/// Aggregated dashboard data for the employee self-service movement portal.
/// Built from multiple service calls, scoped to the authenticated employee.
/// </summary>
public sealed class EmployeePortalDashboardDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? CurrentPositionTitle { get; set; }
    public string? CurrentOrganizationUnitName { get; set; }
    public string? CurrentSalaryGradeName { get; set; }

    // ── Counts ─────────────────────────────────────────────────────────────
    public int TotalMovements { get; set; }
    public int TotalPromotions { get; set; }
    public int TotalTransfers { get; set; }
    public int TotalActingAppointments { get; set; }
    public int TotalSecondments { get; set; }
    public int PendingResponseCount { get; set; }
    public int ActiveActingAppointmentCount { get; set; }
    public int ActiveSecondmentCount { get; set; }

    // ── Timeline ────────────────────────────────────────────────────────────
    public DateTime? CurrentRoleStartDate { get; set; }

    // ── Lists ───────────────────────────────────────────────────────────────
    public IEnumerable<StaffMovementSummaryDto> RecentMovements { get; set; } = [];
    public IEnumerable<StaffMovementSummaryDto> PendingResponseMovements { get; set; } = [];
    public IEnumerable<StaffMovementSummaryDto> ActiveTemporaryAssignments { get; set; } = [];
}

/// <summary>
/// Notification item derived from the employee's movement data.
/// Not persisted — generated at query time from movement states.
/// </summary>
public sealed class EmployeePortalNotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>ResponseRequired | Approved | Rejected | EndingSoon | Implemented | General</summary>
    public string Category { get; set; } = "General";

    public bool IsActionRequired { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? MovementId { get; set; }
    public string? MovementNumber { get; set; }
    public string? ActionUrl { get; set; }
}
