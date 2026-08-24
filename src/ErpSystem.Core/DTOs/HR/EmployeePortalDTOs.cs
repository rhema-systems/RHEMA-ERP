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

// ============================================================================
// STAFF / COMPANY ASSETS — area 16 slice 6 (AST-6, AST-8)
// ============================================================================

/// <summary>
/// What the employee holds and what they have asked for, in the four numbers the portal landing
/// needs before it decides what to shout about.
/// </summary>
/// <remarks>
/// Deliberately a separate payload from <c>EmployeePortalDashboardDto</c> rather than more fields
/// on it: that one is the movements dashboard, and widening it would make every asset read a cost
/// paid by a screen that does not want it. Both are cheap; neither is coupled to the other.
/// </remarks>
public sealed class EmployeePortalAssetSummaryDto
{
    public Guid EmployeeId { get; set; }

    /// <summary>Assets currently in this employee's hands.</summary>
    public int HeldCount { get; set; }

    /// <summary>Held assets they have not yet signed for — AST-8, the portal's one nag.</summary>
    public int AwaitingAcknowledgementCount { get; set; }

    /// <summary>Held assets already past the date they were due back.</summary>
    public int OverdueReturnCount { get; set; }

    /// <summary>Requisitions they are a party to that have not yet been settled either way.</summary>
    public int OpenRequisitionCount { get; set; }

    /// <summary>Requisitions still in draft — raised and then never sent for approval.</summary>
    public int DraftRequisitionCount { get; set; }

    public IEnumerable<AssetAssignmentSummaryDto> Held { get; set; } = [];
    public IEnumerable<AssetAssignmentSummaryDto> AwaitingAcknowledgement { get; set; } = [];
    public IEnumerable<AssetRequisitionSummaryDto> OpenRequisitions { get; set; } = [];
}
