using System.Text.Json;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employee self-service portal: workforce mobility, and the employee's own company assets.
/// ALL endpoints are automatically scoped to the authenticated user's employee record.
/// The frontend never passes an employee ID — the server derives it from the JWT claim.
/// </summary>
/// <remarks>
/// The asset routes (area 16 slice 6) delegate to the same services <c>api/Assets</c> calls, so the
/// authorization rules are enforced once and in one place. This controller adds no rule of its own;
/// it removes the need for the client to know its own employee id.
/// </remarks>
[ApiController]
[Route("api/employee-portal")]
[Authorize(Policy = "InternalOnly")]
public class EmployeePortalController : ControllerBase
{
    private readonly IStaffMovementService         _movementService;
    private readonly IStaffActingAppointmentService _actingService;
    private readonly IAssetAssignmentService       _assignmentService;
    private readonly IAssetRequisitionService      _requisitionService;
    private readonly IAssetTermsLetterService      _termsLetterService;
    private readonly IAssetSurchargeService        _surchargeService;
    private readonly ILeaveService                 _leaveService;
    private readonly IPublicHolidayService         _holidayService;
    private readonly ITrainingDashboardService     _trainingDashboardService;
    private readonly IEmployeeCertificateService   _certificateService;
    private readonly ICurrentUserService           _currentUser;
    private readonly ApplicationDbContext          _db;

    // Slice 11 — the inbox and unified notifications fan out to these.
    private readonly IWorkflowEntityDisplayService  _entityDisplayService;
    private readonly INotificationService           _notificationService;
    private readonly IAppraisalNotificationService  _appraisalNotificationService;
    private readonly IOrientationNotificationService _orientationNotificationService;
    private readonly IStaffGrievanceService         _grievanceService;
    private readonly ISheRiskAssessmentService      _riskAssessmentService;
    private readonly ITrainingServiceBondService    _bondService;
    private readonly IHrAnnouncementService         _announcementService;
    private readonly IHrPolicyService               _policyService;

    public EmployeePortalController(
        IStaffMovementService          movementService,
        IStaffActingAppointmentService actingService,
        IAssetAssignmentService        assignmentService,
        IAssetRequisitionService       requisitionService,
        IAssetTermsLetterService       termsLetterService,
        IAssetSurchargeService         surchargeService,
        ILeaveService                  leaveService,
        IPublicHolidayService          holidayService,
        ITrainingDashboardService      trainingDashboardService,
        IEmployeeCertificateService    certificateService,
        ICurrentUserService            currentUser,
        ApplicationDbContext           db,
        IWorkflowEntityDisplayService  entityDisplayService,
        INotificationService           notificationService,
        IAppraisalNotificationService  appraisalNotificationService,
        IOrientationNotificationService orientationNotificationService,
        IStaffGrievanceService         grievanceService,
        ISheRiskAssessmentService      riskAssessmentService,
        ITrainingServiceBondService    bondService,
        IHrAnnouncementService         announcementService,
        IHrPolicyService               policyService)
    {
        _movementService    = movementService;
        _actingService      = actingService;
        _assignmentService  = assignmentService;
        _requisitionService = requisitionService;
        _termsLetterService = termsLetterService;
        _surchargeService   = surchargeService;
        _leaveService       = leaveService;
        _holidayService     = holidayService;
        _trainingDashboardService = trainingDashboardService;
        _certificateService = certificateService;
        _currentUser        = currentUser;
        _db                 = db;
        _entityDisplayService = entityDisplayService;
        _notificationService  = notificationService;
        _appraisalNotificationService   = appraisalNotificationService;
        _orientationNotificationService = orientationNotificationService;
        _grievanceService     = grievanceService;
        _riskAssessmentService = riskAssessmentService;
        _bondService          = bondService;
        _announcementService  = announcementService;
        _policyService        = policyService;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    // =========================================================================
    // DASHBOARD
    // =========================================================================

    /// <summary>
    /// Returns an aggregated overview of the current employee's movement history and active assignments.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var allMovements = (await _movementService.GetByEmployeeAsync(empId, ct)).ToList();

        var pending = allMovements
            .Where(m => m.Status == StaffMovementStatus.EmployeeAcceptancePending)
            .ToList();

        var activeTemporary = allMovements
            .Where(m => m.IsTemporary && !m.ReturnProcessed && m.Status == StaffMovementStatus.Implemented)
            .ToList();

        // Most recent implemented/approved movement defines the current role start date
        var latestImplemented = allMovements
            .Where(m => m.Status is StaffMovementStatus.Implemented or StaffMovementStatus.Approved)
            .OrderByDescending(m => m.EffectiveDate ?? m.RequestDate)
            .FirstOrDefault();

        var dashboard = new EmployeePortalDashboardDto
        {
            EmployeeId   = empId,
            EmployeeName = _currentUser.UserName ?? string.Empty,

            CurrentPositionTitle        = latestImplemented?.NewPositionTitle ?? latestImplemented?.CurrentPositionTitle,
            CurrentOrganizationUnitName = latestImplemented?.NewOrganizationUnitName ?? latestImplemented?.CurrentOrganizationUnitName,

            TotalMovements            = allMovements.Count,
            TotalPromotions           = allMovements.Count(m => m.MovementType == StaffMovementType.Promotion),
            TotalTransfers            = allMovements.Count(m => m.MovementType == StaffMovementType.Transfer),
            TotalActingAppointments   = allMovements.Count(m => m.MovementType == StaffMovementType.ActingAppointment),
            TotalSecondments          = allMovements.Count(m => m.MovementType == StaffMovementType.Secondment),

            PendingResponseCount           = pending.Count,
            ActiveActingAppointmentCount   = activeTemporary.Count(m => m.MovementType == StaffMovementType.ActingAppointment),
            ActiveSecondmentCount          = activeTemporary.Count(m => m.MovementType == StaffMovementType.Secondment),

            CurrentRoleStartDate = latestImplemented?.EffectiveDate,

            RecentMovements          = allMovements.OrderByDescending(m => m.RequestDate).Take(5),
            PendingResponseMovements = pending,
            ActiveTemporaryAssignments = activeTemporary
        };

        return Ok(dashboard);
    }

    // =========================================================================
    // HOME — area 25 slice 3
    // =========================================================================

    /// <summary>
    /// The personal aggregate behind the portal landing: what needs the employee's action,
    /// their leave standing, assets, learning, and expiring documents — one read.
    /// </summary>
    /// <remarks>
    /// Every figure here MUST agree with the detail read its tile links to — each count is
    /// computed from the same service call the detail screen makes, never re-derived. The
    /// payslip and announcements fields are typed stubs (slices 10 and 12 wire them); they
    /// exist now so the frontend contract does not change shape twice.
    /// </remarks>
    [HttpGet("home")]
    public async Task<IActionResult> GetHome(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year  = DateTime.UtcNow.Year;

        var movements    = (await _movementService.GetByEmployeeAsync(empId, ct)).ToList();
        var held         = (await _assignmentService.GetActiveAssignmentsForEmployeeAsync(empId)).ToList();
        var requisitions = (await _requisitionService.GetForEmployeeAsync(empId)).ToList();
        var surcharges   = (await _surchargeService.GetByEmployeeIdAsync(empId)).ToList();
        var balances     = (await _leaveService.GetEmployeeLeaveBalancesAsync(empId, year)).ToList();
        var training     = await _trainingDashboardService.GetEmployeeSummaryAsync(empId, ct);
        var certificates = (await _certificateService.GetByEmployeeIdAsync(empId, ct)).ToList();
        // Next 12 months is enough to always find "the next holiday" without scanning years.
        var holidays     = (await _holidayService.GetInRangeAsync(today, today.AddYears(1), ct)).ToList();

        var openRequisitionStatuses = new[]
        {
            AssetRequisitionStatus.Draft,
            AssetRequisitionStatus.Submitted,
            AssetRequisitionStatus.UnderReview,
            AssetRequisitionStatus.Approved
        };

        var nextHoliday = holidays
            .Where(h => h.DateTo >= today)
            .OrderBy(h => h.DateFrom)
            .FirstOrDefault();

        var expiringDocuments = certificates
            .Where(c => c.ExpiryDate is { } exp && exp >= DateTime.UtcNow && exp <= DateTime.UtcNow.AddDays(90))
            .OrderBy(c => c.ExpiryDate)
            .Take(5)
            .Select(c => new PortalExpiringDocumentDto
            {
                Id              = c.Id,
                Name            = c.CertificateName,
                Kind            = c.CategoryName,
                ExpiryDate      = c.ExpiryDate!.Value,
                DaysUntilExpiry = (int)(c.ExpiryDate!.Value - DateTime.UtcNow).TotalDays,
            })
            .ToList();

        return Ok(new EmployeePortalHomeDto
        {
            EmployeeId   = empId,
            EmployeeName = _currentUser.UserName ?? string.Empty,

            MovementsAwaitingMyResponse  = movements.Count(m => m.Status == StaffMovementStatus.EmployeeAcceptancePending),
            SurchargesAwaitingMyResponse = surcharges.Count(x =>
                x.Status == AssetSurchargeStatus.WithEmployee
                && x.EmployeeResponse == AssetSurchargeEmployeeResponse.NotYetGiven),
            AssetsAwaitingAcknowledgement = held.Count(a => !a.EmployeeAcknowledged),

            LeaveBalances = balances.Select(b => new PortalLeaveBalanceDto
            {
                LeaveTypeId   = b.LeaveTypeId,
                LeaveTypeName = b.LeaveTypeName,
                LeaveTypeCategory = b.LeaveTypeCategory,
                AvailableDays = b.AvailableDays,
                UsedDays      = b.UsedDays,
                PendingDays   = b.PendingDays,
                EntitledDays  = b.EntitledDays,
            }).ToList(),
            NextHoliday = nextHoliday is null
                ? null
                : new PortalHolidayDto { Name = nextHoliday.HolidayName, Date = nextHoliday.DateFrom },

            AssetsHeldCount           = held.Count,
            OpenAssetRequisitionCount = requisitions.Count(r => openRequisitionStatuses.Contains(r.Status)),

            ActiveCertificatesCount    = training.ActiveCertificatesCount,
            ExpiringCertificatesCount  = training.ExpiringCertificatesCount,
            TrainingComplianceRate     = training.ComplianceRate,
            LearningPathsEnrolledCount = training.LearningPathsEnrolledCount,

            ExpiringDocuments = expiringDocuments,

            // "Latest" is the newest PAY PERIOD, not the newest generation — snapshots are
            // regenerated in place, so GeneratedAt moves without the payslip being new.
            LatestPayslip = await _db.Set<PayrollPayslipSnapshot>()
                .AsNoTracking()
                .Where(s => s.EmployeeId == empId && !s.IsDeleted)
                .OrderByDescending(s => s.PayrollRun.PayPeriodTo)
                .ThenByDescending(s => s.GeneratedAt)
                .Select(s => new PortalPayslipStubDto
                {
                    PayslipNumber = s.PayslipNumber,
                    GeneratedAt   = s.GeneratedAt,
                    NetPay        = s.NetIncome,
                })
                .FirstOrDefaultAsync(ct),

            // Slice 12c: the typed stub cut in slice 3 is now live. Membership is evaluated
            // per read, so a transfer changes what the employee sees without anyone republishing.
            Announcements = (await _announcementService.GetMineForDashboardAsync(empId, 3, ct))
                .Select(a => new PortalAnnouncementStubDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    PublishedAt = a.PublishedAt,
                })
                .ToList(),
        });
    }

    // =========================================================================
    // PAYSLIPS — the D4 read-only adapter (area 25 slice 10)
    // =========================================================================
    // Payroll is another team's module and stays untouched: these routes read the frozen
    // PayrollPayslipSnapshot table directly — never the live run (whose reads rebuild
    // payslips and carry desk gates), never a payroll service. No write, no recompute.
    // The scoping law is the self-service one: the employee comes from the token, no route
    // or query parameter carries an employee id, and somebody else's snapshot id is a
    // 404 lookup miss — never a 403.

    /// <summary>The caller's own payslips, newest pay period first.</summary>
    /// <remarks>Empty until payroll generates snapshots for a run the employee is in —
    /// the portal shows what payroll has published, nothing earlier.</remarks>
    [HttpGet("payslips")]
    public async Task<IActionResult> GetMyPayslips(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var rows = await _db.Set<PayrollPayslipSnapshot>()
            .AsNoTracking()
            .Where(s => s.EmployeeId == empId && !s.IsDeleted)
            .OrderByDescending(s => s.PayrollRun.PayPeriodTo)
            .ThenByDescending(s => s.GeneratedAt)
            .Select(s => new
            {
                s.Id,
                s.PayslipNumber,
                s.PayrollRun.RunNumber,
                s.PayrollRun.PayPeriod,
                s.PayrollRun.PayPeriodFrom,
                s.PayrollRun.PayPeriodTo,
                s.PayrollRun.CurrencyCode,
                s.PayrollRun.IsSeparateBonusRun,
                s.GrossIncome,
                s.NetIncome,
                s.TaxAmount,
                s.EmployeeContribution,
                s.GeneratedAt,
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>One of the caller's own payslips, rendered from its frozen snapshot.</summary>
    /// <remarks>
    /// The stored <c>SnapshotJson</c> is PascalCase (payroll serializes it without options),
    /// so it is deserialized into the payroll DTO here and returned typed — the response then
    /// camelCases like every other payload instead of leaking the storage casing. A snapshot
    /// whose JSON no longer parses returns its header with <c>payslip: null</c> rather than
    /// failing the whole read.
    /// </remarks>
    [HttpGet("payslips/{id:guid}")]
    public async Task<IActionResult> GetMyPayslip(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        // Ownership is part of the lookup itself: an unowned id and a missing id are the
        // same 404, so this route cannot be used to discover which snapshots exist.
        var row = await _db.Set<PayrollPayslipSnapshot>()
            .AsNoTracking()
            .Where(s => s.Id == id && s.EmployeeId == empId && !s.IsDeleted)
            .Select(s => new
            {
                s.Id,
                s.PayslipNumber,
                s.GeneratedAt,
                s.GrossIncome,
                s.NetIncome,
                s.TaxAmount,
                s.EmployeeContribution,
                s.SnapshotJson,
                s.PayrollRun.RunNumber,
                s.PayrollRun.PayPeriod,
                s.PayrollRun.PayPeriodFrom,
                s.PayrollRun.PayPeriodTo,
                s.PayrollRun.CurrencyCode,
                s.PayrollRun.IsSeparateBonusRun,
            })
            .FirstOrDefaultAsync(ct);
        if (row is null) return NotFound();

        PayrollPayslipDto? payslip;
        try
        {
            payslip = JsonSerializer.Deserialize<PayrollPayslipDto>(row.SnapshotJson);
        }
        catch (JsonException)
        {
            payslip = null;
        }

        return Ok(new
        {
            row.Id,
            row.PayslipNumber,
            row.RunNumber,
            row.PayPeriod,
            row.PayPeriodFrom,
            row.PayPeriodTo,
            row.CurrencyCode,
            row.IsSeparateBonusRun,
            row.GrossIncome,
            row.NetIncome,
            row.TaxAmount,
            row.EmployeeContribution,
            row.GeneratedAt,
            Payslip = payslip,
        });
    }

    // =========================================================================
    // MOVEMENTS
    // =========================================================================

    /// <summary>Returns all movements for the current employee, newest-first.</summary>
    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements(
        [FromQuery] StaffMovementType?   type   = null,
        [FromQuery] StaffMovementStatus? status = null,
        CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var movements = await _movementService.GetByEmployeeAsync(empId, ct);

        if (type is not null)
            movements = movements.Where(m => m.MovementType == type);

        if (status is not null)
            movements = movements.Where(m => m.Status == status);

        return Ok(movements.OrderByDescending(m => m.RequestDate));
    }

    /// <summary>
    /// Returns the full detail for a single movement.
    /// Returns 403 if the movement does not belong to the current employee.
    /// </summary>
    [HttpGet("movements/{id:guid}")]
    public async Task<IActionResult> GetMovement(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var movement = await _movementService.GetByIdAsync(id, ct);
        if (movement is null)          return NotFound();
        if (movement.EmployeeId != empId) return Forbid();

        return Ok(movement);
    }

    /// <summary>Returns movements currently awaiting the employee's acceptance/rejection.</summary>
    [HttpGet("movements/pending-response")]
    public async Task<IActionResult> GetPendingResponse(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var all = await _movementService.GetByEmployeeAsync(empId, ct);
        return Ok(all.Where(m => m.Status == StaffMovementStatus.EmployeeAcceptancePending));
    }

    /// <summary>
    /// Records the employee's acceptance or rejection of a proposed movement.
    /// The employee may only respond to movements assigned to themselves.
    /// </summary>
    [HttpPost("movements/{id:guid}/respond")]
    public async Task<IActionResult> Respond(
        Guid id,
        [FromBody] EmployeePortalRespondDto dto,
        CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        // Security: verify ownership before responding
        var movement = await _movementService.GetByIdAsync(id, ct);
        if (movement is null)             return NotFound();
        if (movement.EmployeeId != empId) return Forbid();
        if (movement.Status != StaffMovementStatus.EmployeeAcceptancePending)
            return Problem(
                detail:     "This movement is not awaiting your response.",
                statusCode: StatusCodes.Status409Conflict,
                title:      "Invalid State");

        var respondDto = new RespondToStaffMovementDto
        {
            MovementId = id,
            Accepted   = dto.Accepted,
            Comments   = dto.Comments
        };

        var success = await _movementService.RecordEmployeeResponseAsync(respondDto, empId, ct);
        return success ? Ok() : Problem(detail: "Unable to record response.", statusCode: 500);
    }

    // =========================================================================
    // CAREER PATH
    // =========================================================================

    /// <summary>Returns all movements ordered chronologically for career path visualization.</summary>
    [HttpGet("career-path")]
    public async Task<IActionResult> GetCareerPath(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var movements = await _movementService.GetByEmployeeAsync(empId, ct);
        return Ok(movements
            .Where(m => m.Status is StaffMovementStatus.Implemented or StaffMovementStatus.Approved)
            .OrderBy(m => m.EffectiveDate ?? m.RequestDate));
    }

    // =========================================================================
    // ACTING APPOINTMENTS
    // =========================================================================

    /// <summary>Acting appointments for the current employee (standalone entity, read-only).</summary>
    /// <remarks>
    /// The FULL rows, not the register summary: the subject's own allowance and who they are
    /// covering for are exactly what this read exists to show (area 25 slice 7).
    /// </remarks>
    [HttpGet("acting-appointments")]
    public async Task<IActionResult> GetActingAppointments(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var appointments = await _actingService.GetDetailedByEmployeeIdAsync(empId, ct);
        return Ok(appointments.OrderByDescending(a => a.StartDate));
    }

    // =========================================================================
    // SECONDMENTS
    // =========================================================================

    [HttpGet("secondments")]
    public async Task<IActionResult> GetSecondments(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var movements = await _movementService.GetByEmployeeAsync(empId, ct);
        return Ok(movements
            .Where(m => m.MovementType == StaffMovementType.Secondment)
            .OrderByDescending(m => m.EffectiveDate ?? m.RequestDate));
    }

    // =========================================================================
    // NOTIFICATIONS (derived from movement states)
    // =========================================================================

    /// <summary>
    /// Generates portal notifications derived from the employee's movement data.
    /// These are computed at request time — not persisted notifications.
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var notifications = await BuildMovementNotificationsAsync(empId, ct);
        return Ok(notifications.OrderByDescending(n => n.IsActionRequired).ThenByDescending(n => n.CreatedAt));
    }

    /// <summary>The movement-derived rows, shared by the movements feed above and the unified
    /// portal feed (slice 11). Computed, never persisted — ids do not survive a refresh.</summary>
    private async Task<List<EmployeePortalNotificationDto>> BuildMovementNotificationsAsync(Guid empId, CancellationToken ct)
    {
        var movements = (await _movementService.GetByEmployeeAsync(empId, ct)).ToList();
        var notifications = new List<EmployeePortalNotificationDto>();
        var now = DateTime.UtcNow;

        foreach (var m in movements)
        {
            // Response required
            if (m.Status == StaffMovementStatus.EmployeeAcceptancePending)
            {
                notifications.Add(new EmployeePortalNotificationDto
                {
                    Id               = Guid.NewGuid(),
                    Title            = "Response Required",
                    Message          = $"Your {m.MovementTypeName} ({m.MovementNumber}) requires your acceptance or rejection.",
                    Category         = "ResponseRequired",
                    IsActionRequired = true,
                    CreatedAt        = m.RequestDate,
                    MovementId       = m.Id,
                    MovementNumber   = m.MovementNumber,
                    // Area 25 slice 7: the recipient is the employee, so the link lands on the
                    // portal movement detail (the old /employee/* paths were Blazor-era and
                    // resolved to nothing).
                    ActionUrl        = $"/me/movements/{m.Id}"
                });
            }

            // Recently approved (within last 7 days)
            if (m.Status == StaffMovementStatus.Approved &&
                m.RequestDate >= now.AddDays(-7))
            {
                notifications.Add(new EmployeePortalNotificationDto
                {
                    Id             = Guid.NewGuid(),
                    Title          = "Movement Approved",
                    Message        = $"Your {m.MovementTypeName} ({m.MovementNumber}) has been approved.",
                    Category       = "Approved",
                    CreatedAt      = m.RequestDate,
                    MovementId     = m.Id,
                    MovementNumber = m.MovementNumber,
                    ActionUrl      = $"/me/movements/{m.Id}"
                });
            }

            // Recently implemented (within last 7 days)
            if (m.Status == StaffMovementStatus.Implemented &&
                m.EffectiveDate.HasValue &&
                m.EffectiveDate.Value >= now.AddDays(-7))
            {
                notifications.Add(new EmployeePortalNotificationDto
                {
                    Id             = Guid.NewGuid(),
                    Title          = "Movement Implemented",
                    Message        = $"Your {m.MovementTypeName} ({m.MovementNumber}) has been implemented as of {m.EffectiveDate:d MMM yyyy}.",
                    Category       = "Implemented",
                    CreatedAt      = m.EffectiveDate.Value,
                    MovementId     = m.Id,
                    MovementNumber = m.MovementNumber,
                    ActionUrl      = $"/me/movements/{m.Id}"
                });
            }

            // Temporary assignment ending soon (within 14 days)
            if (m.IsTemporary && !m.ReturnProcessed &&
                m.TemporaryEndDate.HasValue &&
                m.TemporaryEndDate.Value <= now.AddDays(14) &&
                m.TemporaryEndDate.Value > now)
            {
                var daysLeft = (m.TemporaryEndDate.Value - now).Days;
                notifications.Add(new EmployeePortalNotificationDto
                {
                    Id               = Guid.NewGuid(),
                    Title            = "Assignment Ending Soon",
                    Message          = $"Your {m.MovementTypeName} ({m.MovementNumber}) is due to end in {daysLeft} day(s) on {m.TemporaryEndDate:d MMM yyyy}.",
                    Category         = "EndingSoon",
                    IsActionRequired = false,
                    CreatedAt        = now,
                    MovementId       = m.Id,
                    MovementNumber   = m.MovementNumber,
                    ActionUrl        = $"/me/movements/{m.Id}"
                });
            }
        }

        return notifications;
    }

    // =========================================================================
    // APPROVALS & TASKS INBOX + UNIFIED NOTIFICATIONS — area 25 slice 11
    // =========================================================================
    //
    // READ AND NAVIGATE, deliberately. The generic engine endpoints
    // (api/Workflow/approvals/{id}/process, steps/{id}/process, mobile/actions) drive the
    // engine but never apply the module's IWorkflowStatusAdapter — the approval row is
    // consumed while the entity strands in Submitted (measured live in the slice-11 probe;
    // recorded as a cross-module defect). Only the module's own approve endpoint applies
    // the outcome, so every inbox row carries the record's URL and the approval act
    // happens there. The raw feeds (approvals/pending, tasks/pending) also die mid-stream
    // on entity-graph cycles whenever they have content — which is why this projection
    // exists instead of the portal consuming them.

    private static readonly WorkflowInstanceStatus[] LiveInstanceStatuses =
    [
        WorkflowInstanceStatus.Created,
        WorkflowInstanceStatus.InProgress,
        WorkflowInstanceStatus.Waiting,
        WorkflowInstanceStatus.Suspended,
    ];

    /// <summary>
    /// Everything waiting on the caller: workflow approvals addressed to them (directly or
    /// through a role), workflow tasks assigned to them, and the module action items the
    /// HR areas scattered (acknowledge / respond / accept), consolidated.
    /// </summary>
    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (!Guid.TryParse(_currentUser.UserId, out var userId)) return NoEmployee();

        var approvals   = await BuildPendingApprovalsAsync(userId, ct);
        var tasks       = await BuildPendingTasksAsync(userId, ct);
        var actionItems = await BuildActionItemsAsync(empId, ct);

        return Ok(new EmployeePortalInboxDto
        {
            Approvals   = approvals,
            Tasks       = tasks,
            ActionItems = actionItems,
        });
    }

    /// <summary>The light counts behind the top-nav badges and the landing chips.</summary>
    [HttpGet("inbox/counts")]
    public async Task<IActionResult> GetInboxCounts(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (!Guid.TryParse(_currentUser.UserId, out var userId)) return NoEmployee();
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        var roles = (_currentUser.Roles ?? Array.Empty<string>()).ToList();

        var pendingApprovals = await PendingApprovalsQuery(userId, tenantId, roles).CountAsync(ct);
        var pendingTasks     = await PendingTasksQuery(userId, tenantId).CountAsync(ct);
        var actionItems      = (await BuildActionItemsAsync(empId, ct)).Count;
        var unread           = await SumUnreadNotificationsAsync(userId, tenantId, empId, ct);

        return Ok(new PortalInboxCountsDto
        {
            PendingApprovals    = pendingApprovals,
            PendingTasks        = pendingTasks,
            ActionItems         = actionItems,
            UnreadNotifications = unread,
        });
    }

    /// <summary>
    /// The unified notification feed: the general store (user-keyed), the appraisal and
    /// orientation stores (employee-keyed), and the computed movement rows — one list,
    /// one shape. Mark-read stays with each store's own endpoint; the client dispatches
    /// by <c>source</c>.
    /// </summary>
    [HttpGet("my-notifications")]
    public async Task<IActionResult> GetMyNotifications(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (!Guid.TryParse(_currentUser.UserId, out var userId)) return NoEmployee();
        var tenantId = _currentUser.TenantId ?? Guid.Empty;

        var general     = await _notificationService.GetNotificationsAsync(userId, tenantId, page: 1, pageSize: 50);
        var appraisal   = await _appraisalNotificationService.GetAllNotificationsAsync(empId, page: 1, pageSize: 50, ct);
        var orientation = (await _orientationNotificationService.GetByRecipientAsync(empId, unreadOnly: false, ct)).ToList();
        var movement    = await BuildMovementNotificationsAsync(empId, ct);

        var items = new List<PortalNotificationItemDto>();

        items.AddRange(general.Items.Select(n => new PortalNotificationItemDto
        {
            Source           = "General",
            Id               = n.Id,
            Title            = n.Title,
            Message          = n.Message,
            Category         = n.Type,
            IsActionRequired = false,
            CreatedAt        = n.Timestamp,
            IsRead           = n.IsRead,
            CanMarkRead      = true,
            ActionUrl        = n.ActionUrl,
        }));

        items.AddRange(appraisal.Select(n => new PortalNotificationItemDto
        {
            Source           = "Appraisal",
            Id               = n.NotificationId,
            Title            = n.Title,
            Message          = n.Message,
            Category         = n.Type.ToString(),
            IsActionRequired = false,
            CreatedAt        = n.CreatedDate,
            IsRead           = n.IsRead,
            CanMarkRead      = true,
            ActionUrl        = n.NavigationUrl,
        }));

        items.AddRange(orientation.Select(n => new PortalNotificationItemDto
        {
            Source           = "Orientation",
            Id               = n.Id,
            Title            = n.Subject,
            Message          = n.Message ?? string.Empty,
            Category         = n.TypeName,
            IsActionRequired = false,
            CreatedAt        = n.SentAt,
            IsRead           = n.IsRead,
            CanMarkRead      = true,
            ActionUrl        = n.NavigationUrl,
        }));

        items.AddRange(movement.Select(n => new PortalNotificationItemDto
        {
            Source           = "Movement",
            Id               = n.Id,
            Title            = n.Title,
            Message          = n.Message,
            Category         = n.Category,
            IsActionRequired = n.IsActionRequired,
            CreatedAt        = n.CreatedAt,
            IsRead           = null,           // computed rows have no read state
            CanMarkRead      = false,          // and their ids do not survive a refresh
            ActionUrl        = n.ActionUrl,
        }));

        return Ok(new PortalNotificationsDto
        {
            Items = items
                .OrderByDescending(n => n.IsActionRequired)
                .ThenByDescending(n => n.CreatedAt)
                .Take(100)
                .ToList(),
            UnreadCount = await SumUnreadNotificationsAsync(userId, tenantId, empId, ct),
        });
    }

    // ── Inbox internals ──────────────────────────────────────────────────────

    /// <summary>The true unread total: each persisted store's own unread-count read, summed.
    /// The computed movement rows are excluded — they have no read state to be unread in.</summary>
    private async Task<int> SumUnreadNotificationsAsync(Guid userId, Guid tenantId, Guid empId, CancellationToken ct)
        => await _notificationService.GetUnreadCountAsync(userId, tenantId)
         + await _appraisalNotificationService.GetUnreadCountAsync(empId, ct)
         + await _orientationNotificationService.GetUnreadCountAsync(empId, ct);

    private IQueryable<Core.Entities.Workflow.WorkflowApproval> PendingApprovalsQuery(
        Guid userId, Guid tenantId, List<string> roles)
        => _db.WorkflowApprovals
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.TenantId == tenantId
                && a.Status == WorkflowApprovalStatus.Pending
                && (a.ApproverId == userId
                    || (a.ApproverRole != null && roles.Contains(a.ApproverRole)))
                && !a.StepInstance.IsDeleted
                && LiveInstanceStatuses.Contains(a.StepInstance.WorkflowInstance.Status));

    private IQueryable<Core.Entities.Workflow.WorkflowStepInstance> PendingTasksQuery(Guid userId, Guid tenantId)
        => _db.WorkflowStepInstances
            .AsNoTracking()
            .Where(si => !si.IsDeleted
                && si.TenantId == tenantId
                && si.Status == WorkflowStepInstanceStatus.Pending
                && si.AssignedToId == userId
                && LiveInstanceStatuses.Contains(si.WorkflowInstance.Status)
                // A step with pending approval rows is an approval step — it belongs in
                // the approvals register, not twice.
                && !si.Approvals.Any(ap => !ap.IsDeleted && ap.Status == WorkflowApprovalStatus.Pending));

    private async Task<List<PortalApprovalItemDto>> BuildPendingApprovalsAsync(Guid userId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        var roles = (_currentUser.Roles ?? Array.Empty<string>()).ToList();

        var rows = await PendingApprovalsQuery(userId, tenantId, roles)
            .OrderBy(a => a.DueDate ?? DateTime.MaxValue)
            .ThenBy(a => a.RequestedDate)
            .Take(100)
            .Select(a => new
            {
                a.Id,
                a.StepInstanceId,
                a.ApproverId,
                a.ApproverRole,
                a.RequestedDate,
                a.DueDate,
                a.Priority,
                StepName       = a.StepInstance.WorkflowStep.Name,
                WorkflowName   = a.StepInstance.WorkflowInstance.WorkflowDefinition.Name,
                EntityTypeName = a.StepInstance.WorkflowInstance.EntityType != null
                    ? a.StepInstance.WorkflowInstance.EntityType.Name
                    : null,
                a.StepInstance.WorkflowInstance.EntityId,
            })
            .ToListAsync(ct);

        var items = new List<PortalApprovalItemDto>(rows.Count);
        foreach (var row in rows)
        {
            var (entityGuid, number, name, url) =
                await ResolveEntityDisplayAsync(row.EntityTypeName, row.EntityId);
            items.Add(new PortalApprovalItemDto
            {
                ApprovalId     = row.Id,
                StepInstanceId = row.StepInstanceId,
                StepName       = row.StepName,
                WorkflowName   = row.WorkflowName,
                EntityType     = row.EntityTypeName ?? "Unknown",
                EntityId       = entityGuid,
                EntityNumber   = number,
                EntityName     = name,
                ActionUrl      = url,
                RequestedDate  = row.RequestedDate,
                DueDate        = row.DueDate,
                Priority       = row.Priority.ToString(),
                ApproverRole   = row.ApproverId == userId ? null : row.ApproverRole,
            });
        }
        return items;
    }

    private async Task<List<PortalTaskItemDto>> BuildPendingTasksAsync(Guid userId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;

        var rows = await PendingTasksQuery(userId, tenantId)
            .OrderBy(si => si.DueDate ?? DateTime.MaxValue)
            .ThenBy(si => si.CreatedDate)
            .Take(100)
            .Select(si => new
            {
                si.Id,
                si.CreatedDate,
                si.DueDate,
                StepName       = si.WorkflowStep.Name,
                WorkflowName   = si.WorkflowInstance.WorkflowDefinition.Name,
                EntityTypeName = si.WorkflowInstance.EntityType != null
                    ? si.WorkflowInstance.EntityType.Name
                    : null,
                si.WorkflowInstance.EntityId,
            })
            .ToListAsync(ct);

        var items = new List<PortalTaskItemDto>(rows.Count);
        foreach (var row in rows)
        {
            var (entityGuid, number, name, url) =
                await ResolveEntityDisplayAsync(row.EntityTypeName, row.EntityId);
            items.Add(new PortalTaskItemDto
            {
                StepInstanceId = row.Id,
                StepName       = row.StepName,
                WorkflowName   = row.WorkflowName,
                EntityType     = row.EntityTypeName ?? "Unknown",
                EntityId       = entityGuid,
                EntityNumber   = number,
                EntityName     = name,
                ActionUrl      = url,
                CreatedDate    = row.CreatedDate,
                DueDate        = row.DueDate,
            });
        }
        return items;
    }

    /// <summary>Entity display identity via the canonical resolver; a missing type or an
    /// empty id degrades to an unlinked row, never a failed inbox.</summary>
    private async Task<(Guid? EntityGuid, string? Number, string? Name, string? Url)>
        ResolveEntityDisplayAsync(string? entityTypeName, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityTypeName) || entityId == Guid.Empty)
            return (entityId == Guid.Empty ? null : entityId, null, null, null);
        var info = await _entityDisplayService.GetEntityDisplayInfoAsync(entityTypeName, entityId);
        return (entityId, info.EntityNumber, info.EntityName, info.ActionUrl);
    }

    /// <summary>
    /// The module acts waiting on the caller, consolidated (the slice-9 residual). Every
    /// figure comes from the same service read its target screen makes, so the register
    /// and the screens cannot disagree.
    /// </summary>
    private async Task<List<PortalActionItemDto>> BuildActionItemsAsync(Guid empId, CancellationToken ct)
    {
        var items = new List<PortalActionItemDto>();

        var movements = (await _movementService.GetByEmployeeAsync(empId, ct))
            .Where(m => m.Status == StaffMovementStatus.EmployeeAcceptancePending);
        items.AddRange(movements.Select(m => new PortalActionItemDto
        {
            Kind      = "MovementResponse",
            EntityId  = m.Id,
            Title     = $"{m.MovementTypeName} {m.MovementNumber} needs your response",
            ActionUrl = $"/me/movements/{m.Id}",
            Date      = m.RequestDate,
        }));

        var surcharges = (await _surchargeService.GetByEmployeeIdAsync(empId))
            .Where(x => x.Status == AssetSurchargeStatus.WithEmployee
                     && x.EmployeeResponse == AssetSurchargeEmployeeResponse.NotYetGiven);
        items.AddRange(surcharges.Select(s => new PortalActionItemDto
        {
            Kind      = "SurchargeResponse",
            EntityId  = s.Id,
            Title     = $"Charge {s.SurchargeNumber} on {s.AssetName} awaits your reply",
            Detail    = $"{s.CurrencyCode} {s.AssessedAmount:n2}",
            ActionUrl = "/me/assets",
        }));

        var unacknowledged = (await _assignmentService.GetActiveAssignmentsForEmployeeAsync(empId))
            .Where(a => !a.EmployeeAcknowledged);
        items.AddRange(unacknowledged.Select(a => new PortalActionItemDto
        {
            Kind      = "AssetAcknowledgement",
            EntityId  = a.Id,
            Title     = $"Sign for {a.AssetName} ({a.AssetNumber})",
            ActionUrl = "/me/assets",
        }));

        // The subject's unacknowledged discipline notices. The flat notifications route is
        // desk-gated by design (slice 9) — the portal reads them through the case, and this
        // register points there.
        var notices = await _db.Set<StaffDisciplineNotification>()
            .AsNoTracking()
            .Where(n => !n.IsDeleted
                && n.AcknowledgedDate == null
                && !n.DisciplinaryAction.IsDeleted
                && n.DisciplinaryAction.EmployeeId == empId)
            .Select(n => new
            {
                n.Id,
                n.DisciplinaryActionId,
                n.NotificationType,
                n.SentDate,
                n.DisciplinaryAction.CaseNumber,
            })
            .ToListAsync(ct);
        items.AddRange(notices.Select(n => new PortalActionItemDto
        {
            Kind      = "DisciplineNotice",
            EntityId  = n.Id,
            Title     = $"{n.NotificationType} notice on case {n.CaseNumber} to acknowledge",
            ActionUrl = $"/me/discipline/{n.DisciplinaryActionId}",
            Date      = n.SentDate,
        }));

        var grievances = await _grievanceService.GetAwaitingMyResponseAsync(empId, ct);
        items.AddRange(grievances.Select(g => new PortalActionItemDto
        {
            Kind      = "GrievanceResponse",
            EntityId  = g.Id,
            Title     = $"Grievance {g.GrievanceNumber} awaits your response",
            Detail    = g.Subject,
            ActionUrl = $"/me/grievances/{g.Id}",
            Date      = g.FiledDate,
        }));

        // Risk acknowledgements collapse to ONE summary row: the read returns every
        // approved/active assessment in the tenant flagged per-caller (slice-8 design), so a
        // fresh employee "owes" dozens at once — per-row items would drown the register, and
        // every row's act happens on the same list page anyway.
        var unsignedRisks = (await _riskAssessmentService.GetForEmployeeAcknowledgementAsync(empId, ct))
            .Count(r => !r.AcknowledgedByMe);
        if (unsignedRisks > 0)
        {
            items.Add(new PortalActionItemDto
            {
                Kind      = "RiskAssessmentAcknowledgement",
                EntityId  = Guid.Empty,
                Title     = unsignedRisks == 1
                    ? "1 risk assessment awaits your acknowledgement"
                    : $"{unsignedRisks} risk assessments await your acknowledgement",
                ActionUrl = "/me/safety/risk-assessments",
            });
        }

        // Slice 12d: policies still waiting on a signature. Listed per policy rather than
        // collapsed like the risk acknowledgements, because each one is a distinct document the
        // employee must actually open and read — and there are a handful, not dozens.
        var policies = await _policyService.GetMyOutstandingAsync(empId, ct);
        items.AddRange(policies.Select(p => new PortalActionItemDto
        {
            Kind      = "PolicyAcknowledgement",
            EntityId  = p.Id,
            Title     = $"Acknowledge \"{p.Title}\"",
            Detail    = p.AcknowledgementDueBy is { } due
                ? $"{p.CategoryName} · due {due:d MMM yyyy}"
                : p.CategoryName,
            ActionUrl = $"/me/policies/{p.Id}",
            Date      = p.PublishedAt,
        }));

        var bonds = (await _bondService.GetByEmployeeAsync(empId, ct))
            .Where(b => b.Status == TrainingBondStatus.PendingAcceptance);
        items.AddRange(bonds.Select(b => new PortalActionItemDto
        {
            Kind      = "TrainingBondAcceptance",
            EntityId  = b.Id,
            Title     = $"Training bond for {b.ProgramName} awaits your acceptance",
            Detail    = $"{b.Currency} {b.BondAmount:n2} · {b.BondDurationMonths} months",
            ActionUrl = $"/me/training/bonds/{b.Id}",
        }));

        return items;
    }

    // =========================================================================
    // STAFF / COMPANY ASSETS - area 16 slice 6 (AST-6, AST-6b, AST-8)
    // =========================================================================
    //
    // Every route below is a delegation, not a second implementation. The rules about who may read,
    // sign for, edit or withdraw an asset record live in AssetsServices' AssetActor and fire here
    // exactly as they fire on api/Assets - so the portal cannot become a wider door than the
    // register, and a rule tightened in one place cannot be left loose in the other.
    //
    // What these routes add is the ABSENCE of an employee id. api/Assets/assignments/employee/{id}
    // is correct and gated, but a screen that must know its own employee id can pass someone
    // else's, and that is how several of this module's authorization holes started. Here the token
    // supplies it and the client has nothing to get wrong.

    /// <summary>What the employee currently holds - the portal's main list.</summary>
    [HttpGet("assets")]
    public async Task<IActionResult> GetMyAssets()
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _assignmentService.GetActiveAssignmentsForEmployeeAsync(empId));
    }

    /// <summary>Everything the employee has ever held, returned assets included.</summary>
    [HttpGet("assets/history")]
    public async Task<IActionResult> GetMyAssetHistory()
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _assignmentService.GetByEmployeeIdAsync(empId));
    }

    /// <summary>The counters the portal landing needs, and the three short lists behind them.</summary>
    [HttpGet("assets/summary")]
    public async Task<IActionResult> GetMyAssetSummary()
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var held         = (await _assignmentService.GetActiveAssignmentsForEmployeeAsync(empId)).ToList();
        var requisitions = (await _requisitionService.GetForEmployeeAsync(empId)).ToList();
        // Only charges that have been PUT to the employee come back here — the service hides HR's
        // own drafts from their subject, which is why this reads the same list they can open.
        var surcharges   = (await _surchargeService.GetByEmployeeIdAsync(empId)).ToList();

        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var unsigned = held.Where(a => !a.EmployeeAcknowledged).ToList();

        var openStatuses = new[]
        {
            AssetRequisitionStatus.Draft,
            AssetRequisitionStatus.Submitted,
            AssetRequisitionStatus.UnderReview,
            AssetRequisitionStatus.Approved
        };
        var open = requisitions.Where(r => openStatuses.Contains(r.Status)).ToList();

        var awaitingResponse = surcharges
            .Where(x => x.Status == AssetSurchargeStatus.WithEmployee
                && x.EmployeeResponse == AssetSurchargeEmployeeResponse.NotYetGiven)
            .ToList();

        // Live means the charge can still cost them something: rejected, waived, cancelled and
        // fully recovered ones are over, and showing them as outstanding would be a debt that is not.
        var liveSurcharges = surcharges
            .Where(x => x.Status is AssetSurchargeStatus.WithEmployee
                or AssetSurchargeStatus.Submitted
                or AssetSurchargeStatus.Approved
                or AssetSurchargeStatus.Recovering)
            .ToList();

        return Ok(new EmployeePortalAssetSummaryDto
        {
            EmployeeId                   = empId,
            HeldCount                    = held.Count,
            AwaitingAcknowledgementCount = unsigned.Count,
            // An assignment is late when the date it was due back has passed, whatever its status
            // still says - the status only turns Overdue if something sweeps it, and nothing does
            // yet (that arrives with the slice 11 reminder sweep).
            OverdueReturnCount           = held.Count(a => a.ExpectedReturnDate is { } due && due < today),
            OpenRequisitionCount         = open.Count,
            DraftRequisitionCount        = requisitions.Count(r => r.Status == AssetRequisitionStatus.Draft),

            // AST-3 / D9. "Awaiting my response" is the only figure on this payload that means the
            // employee's own inaction has a consequence, so it is counted separately from the rest.
            SurchargesAwaitingMyResponseCount = awaitingResponse.Count,
            OpenSurchargeCount                = liveSurcharges.Count,
            OutstandingSurchargeAmount        = liveSurcharges.Sum(x => x.AmountOutstanding),

            Held                         = held,
            AwaitingAcknowledgement      = unsigned,
            OpenRequisitions             = open,
            SurchargesAwaitingMyResponse = awaitingResponse
        });
    }

    /// <summary>One of the employee's own assignments, in full.</summary>
    [HttpGet("assets/{id:guid}")]
    public async Task<IActionResult> GetMyAsset(Guid id)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();

        var assignment = await _assignmentService.GetByIdAsync(id);
        return assignment is null ? NotFound() : Ok(assignment);
    }

    /// <summary>
    /// The employee signs for what they were given - AST-8.
    /// </summary>
    /// <remarks>
    /// The service refuses this for anybody but the assignment's subject, <b>HR included</b>. That
    /// is deliberate and it is why this route exists at all: acknowledgement is the employee's own
    /// testimony, and the portal is where they give it.
    /// </remarks>
    [HttpPost("assets/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeMyAsset(Guid id)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();

        await _assignmentService.AcknowledgeAssignmentAsync(new AcknowledgeAssignmentDto { AssignmentId = id });
        return Ok(new { message = "Receipt acknowledged" });
    }

    /// <summary>The responsibility-and-terms document for one of the employee's own assignments - AST-5.</summary>
    [HttpGet("assets/{id:guid}/terms-document")]
    public async Task<IActionResult> GetMyAssetTermsDocument(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();
        return Ok(await _termsLetterService.GenerateAsync(id, ct));
    }

    // -- Requisitions ---------------------------------------------------------

    /// <summary>Every requisition the employee is a party to - raised by them, or raised for them.</summary>
    [HttpGet("asset-requisitions")]
    public async Task<IActionResult> GetMyAssetRequisitions()
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _requisitionService.GetForEmployeeAsync(empId));
    }

    /// <summary>One requisition the employee raised or is the beneficiary of.</summary>
    [HttpGet("asset-requisitions/{id:guid}")]
    public async Task<IActionResult> GetMyAssetRequisition(Guid id)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();

        var requisition = await _requisitionService.GetByIdAsync(id);
        return requisition is null ? NotFound() : Ok(requisition);
    }

    /// <summary>
    /// Request an asset - AST-6, and AST-6b where <c>beneficiaryEmployeeId</c> names somebody else.
    /// </summary>
    /// <remarks>
    /// The requester is the token's employee and the payload has no say in it. Naming a beneficiary
    /// is refused unless the caller is that employee's recorded line manager, or holds the HR role.
    /// What comes back is a <b>draft</b>; nothing reaches an approver until it is submitted.
    /// </remarks>
    [HttpPost("asset-requisitions")]
    public async Task<IActionResult> CreateMyAssetRequisition([FromBody] CreateAssetRequisitionDto dto)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();

        var created = await _requisitionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMyAssetRequisition), new { id = created.Id }, created);
    }

    /// <summary>Edit a requisition the employee raised, while it is still a draft.</summary>
    [HttpPut("asset-requisitions/{id:guid}")]
    public async Task<IActionResult> UpdateMyAssetRequisition(Guid id, [FromBody] UpdateAssetRequisitionDto dto)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();
        return Ok(await _requisitionService.UpdateAsync(id, dto));
    }

    /// <summary>Send the draft for approval - the workflow engine decides who sees it.</summary>
    [HttpPost("asset-requisitions/{id:guid}/submit")]
    public async Task<IActionResult> SubmitMyAssetRequisition(Guid id)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();
        return Ok(await _requisitionService.SubmitAsync(id));
    }

    /// <summary>Pull a submitted requisition back to draft.</summary>
    [HttpPost("asset-requisitions/{id:guid}/recall")]
    public async Task<IActionResult> RecallMyAssetRequisition(
        Guid id,
        [FromBody] EmployeePortalRecallDto? dto = null)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();
        return Ok(await _requisitionService.RecallAsync(id, dto?.Reason));
    }

    /// <summary>Withdraw a requisition entirely.</summary>
    [HttpDelete("asset-requisitions/{id:guid}")]
    public async Task<IActionResult> DeleteMyAssetRequisition(Guid id)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();

        await _requisitionService.DeleteAsync(id);
        return Ok(new { message = "Asset requisition withdrawn" });
    }

    // -- Surcharges (AST-3, decision D9) --------------------------------------
    //
    // The employee sees a charge only once it has been PUT to them; HR's drafts are invisible here
    // and answer 404 rather than 403, because a 403 would confirm that a charge against them is
    // being written. Answering is theirs alone — HR is refused, exactly as it is for acknowledging
    // receipt of an asset.

    /// <summary>Charges raised against the employee that have been served on them.</summary>
    [HttpGet("asset-surcharges")]
    public async Task<IActionResult> GetMyAssetSurcharges()
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _surchargeService.GetByEmployeeIdAsync(empId));
    }

    /// <summary>One charge against the employee, in full — what it is for, and what they answered.</summary>
    [HttpGet("asset-surcharges/{id:guid}")]
    public async Task<IActionResult> GetMyAssetSurcharge(Guid id)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();

        var surcharge = await _surchargeService.GetByIdAsync(id);
        return surcharge is null ? NotFound() : Ok(surcharge);
    }

    /// <summary>
    /// The employee accepts or disputes a charge — their right of reply, decision D9.
    /// </summary>
    /// <remarks>
    /// Both answers send the charge on for approval; they differ in what the approver reads. A
    /// dispute does not stop the employer, and it was never going to — what it does is oblige them
    /// to decide with the employee's account in front of them, and leave a record that they did.
    /// </remarks>
    [HttpPost("asset-surcharges/{id:guid}/respond")]
    public async Task<IActionResult> RespondToMyAssetSurcharge(
        Guid id,
        [FromBody] RespondToAssetSurchargeDto dto)
    {
        if (_currentUser.EmployeeId is not Guid) return NoEmployee();
        return Ok(await _surchargeService.RespondAsync(id, dto));
    }

}

// ── Input DTO ─────────────────────────────────────────────────────────────────

/// <summary>Request body for recording an employee's response to a proposed movement.</summary>
public sealed class EmployeePortalRespondDto
{
    public bool    Accepted { get; set; }
    public string? Comments { get; set; }
}

/// <summary>Why a submitted requisition is being pulled back - optional.</summary>
public sealed class EmployeePortalRecallDto
{
    public string? Reason { get; set; }
}
