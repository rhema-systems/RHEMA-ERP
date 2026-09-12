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

    /// <summary>
    /// Charges raised against them that they have not yet answered — AST-3, decision D9.
    /// </summary>
    /// <remarks>
    /// On the portal summary because it is the one thing on this surface where the employee's own
    /// silence has a consequence. A charge they never answer goes to an approver anyway, with a
    /// note saying why — so "you have not replied to this" belongs where they will see it.
    /// </remarks>
    public int SurchargesAwaitingMyResponseCount { get; set; }

    /// <summary>Charges against them that are still live — not rejected, waived or fully recovered.</summary>
    public int OpenSurchargeCount { get; set; }

    /// <summary>What they still owe across every approved charge, in the tenant's currency.</summary>
    public decimal OutstandingSurchargeAmount { get; set; }

    public IEnumerable<AssetAssignmentSummaryDto> Held { get; set; } = [];
    public IEnumerable<AssetAssignmentSummaryDto> AwaitingAcknowledgement { get; set; } = [];
    public IEnumerable<AssetRequisitionSummaryDto> OpenRequisitions { get; set; } = [];
    public IEnumerable<AssetSurchargeSummaryDto> SurchargesAwaitingMyResponse { get; set; } = [];
}

// ============================================================================
// THE PORTAL HOME — area 25 slice 3
// ============================================================================

/// <summary>
/// The personal aggregate behind the portal landing: what needs the employee's action, and
/// the handful of numbers they came to check. One read, every figure deep-linkable.
/// </summary>
/// <remarks>
/// A sibling of <c>EmployeePortalDashboardDto</c> for the same reason that one gives above:
/// that payload is the movements dashboard and this one is the landing; each screen pays
/// only for what it shows. Every figure here must agree with the detail read it links to —
/// the harness asserts that equality (the area-7 wrong-numbers lesson).
/// </remarks>
public sealed class EmployeePortalHomeDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    // ── Waiting on me ───────────────────────────────────────────────────────
    public int MovementsAwaitingMyResponse { get; set; }
    public int SurchargesAwaitingMyResponse { get; set; }
    public int AssetsAwaitingAcknowledgement { get; set; }

    // ── Leave ───────────────────────────────────────────────────────────────
    public IEnumerable<PortalLeaveBalanceDto> LeaveBalances { get; set; } = [];
    public PortalHolidayDto? NextHoliday { get; set; }

    // ── Assets ──────────────────────────────────────────────────────────────
    public int AssetsHeldCount { get; set; }
    public int OpenAssetRequisitionCount { get; set; }

    // ── Learning ────────────────────────────────────────────────────────────
    public int ActiveCertificatesCount { get; set; }
    public int ExpiringCertificatesCount { get; set; }
    public decimal TrainingComplianceRate { get; set; }
    public int LearningPathsEnrolledCount { get; set; }

    // ── Expiring personal documents (externally-held certificates/IDs) ─────
    public IEnumerable<PortalExpiringDocumentDto> ExpiringDocuments { get; set; } = [];

    /// <summary>The newest published payslip, by PAY PERIOD (wired in slice 10); null until
    /// payroll generates snapshots for a run this employee is in.</summary>
    public PortalPayslipStubDto? LatestPayslip { get; set; }

    /// <summary>Slice-12 stub: stays empty until announcements land; the shape is the contract.</summary>
    public IEnumerable<PortalAnnouncementStubDto> Announcements { get; set; } = [];
}

/// <summary>One leave type's standing for the current year — the lean, employee-safe cut.</summary>
public sealed class PortalLeaveBalanceDto
{
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public decimal AvailableDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal PendingDays { get; set; }
    public decimal EntitledDays { get; set; }
}

public sealed class PortalHolidayDto
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

public sealed class PortalExpiringDocumentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
}

/// <summary>The home tile's payslip cut — number, when it was generated, and the net.
/// (Named "stub" since slice 3; the shape was the contract and slice 10 kept it.)</summary>
public sealed class PortalPayslipStubDto
{
    public string PayslipNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public decimal NetPay { get; set; }
}

/// <summary>Slice-12 placeholder shape.</summary>
public sealed class PortalAnnouncementStubDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}

// ============================================================================
// APPROVALS & TASKS INBOX — area 25 slice 11
// ============================================================================

/// <summary>
/// The portal inbox: everything waiting on the caller, in three registers — workflow
/// approvals addressed to them (directly or through a role), workflow tasks assigned to
/// them, and module action items (acknowledge / respond / accept acts scattered across
/// the HR areas, consolidated here as the slice-9 residual asked).
/// </summary>
/// <remarks>
/// READ AND NAVIGATE, deliberately. The generic engine endpoints
/// (<c>api/Workflow/approvals/{id}/process</c>, <c>steps/{id}/process</c>,
/// <c>workflow/platform/mobile/actions</c>) drive the engine but never apply the module's
/// <c>IWorkflowStatusAdapter</c> — the approval row is consumed while the entity strands in
/// Submitted (measured live, slice-11 probe; recorded as a cross-module defect). Only the
/// module's own approve endpoint applies the outcome, so every inbox row carries the
/// record's URL and the act happens there, on the surface that does the whole job.
/// </remarks>
public sealed class EmployeePortalInboxDto
{
    public IEnumerable<PortalApprovalItemDto> Approvals { get; set; } = [];
    public IEnumerable<PortalTaskItemDto> Tasks { get; set; } = [];
    public IEnumerable<PortalActionItemDto> ActionItems { get; set; } = [];
}

/// <summary>
/// A pending workflow approval addressed to the caller, flattened and enriched with the
/// entity's display identity (via <c>IWorkflowEntityDisplayService</c>) — the raw feeds
/// carry none of it, which is why the desk inbox shows bare GUIDs.
/// </summary>
public sealed class PortalApprovalItemDto
{
    public Guid ApprovalId { get; set; }
    public Guid StepInstanceId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? WorkflowName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? EntityNumber { get; set; }
    public string? EntityName { get; set; }
    /// <summary>The record page where the approval act lives. Desk URLs by design —
    /// an approver has desk access to the record they are asked to sign.</summary>
    public string? ActionUrl { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "Normal";
    /// <summary>Null when the approval is addressed to the caller directly; the role
    /// name when it reached them through a role arm.</summary>
    public string? ApproverRole { get; set; }
}

/// <summary>A pending workflow step assigned to the caller by user id.</summary>
public sealed class PortalTaskItemDto
{
    public Guid StepInstanceId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? WorkflowName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? EntityNumber { get; set; }
    public string? EntityName { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>
/// A module act waiting on the caller — the portal's own "to do" register.
/// Kind: MovementResponse | SurchargeResponse | AssetAcknowledgement | DisciplineNotice |
/// GrievanceResponse | RiskAssessmentAcknowledgement | TrainingBondAcceptance.
/// </summary>
public sealed class PortalActionItemDto
{
    public string Kind { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    /// <summary>Portal URLs — these acts are the employee's own and live in the portal.</summary>
    public string ActionUrl { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
}

/// <summary>The light counts read behind the top-nav badges and the landing chips.</summary>
public sealed class PortalInboxCountsDto
{
    public int PendingApprovals { get; set; }
    public int PendingTasks { get; set; }
    public int ActionItems { get; set; }
    public int UnreadNotifications { get; set; }
}

// ============================================================================
// UNIFIED NOTIFICATIONS — area 25 slice 11
// ============================================================================

/// <summary>
/// One notification row in the unified portal feed, whatever store it came from.
/// Source: General (api/Notifications, user-keyed) | Appraisal | Orientation
/// (employee-keyed HR stores) | Movement (computed, never persisted).
/// Mark-read dispatches BY SOURCE to each store's own endpoint — read state stays where
/// it always lived; this feed invents no fourth store.
/// </summary>
public sealed class PortalNotificationItemDto
{
    public string Source { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool IsActionRequired { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Null for computed rows (movements), which have no read state at all.</summary>
    public bool? IsRead { get; set; }
    /// <summary>False for computed rows — their ids do not survive a refresh.</summary>
    public bool CanMarkRead { get; set; }
    public string? ActionUrl { get; set; }
}

/// <summary>The unified feed plus its true unread count (summed from the stores' own
/// unread-count reads, not derived from the capped page).</summary>
public sealed class PortalNotificationsDto
{
    public IEnumerable<PortalNotificationItemDto> Items { get; set; } = [];
    public int UnreadCount { get; set; }
}
