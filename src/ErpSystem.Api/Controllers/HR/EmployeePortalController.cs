using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
[Authorize]
public class EmployeePortalController : ControllerBase
{
    private readonly IStaffMovementService         _movementService;
    private readonly IStaffActingAppointmentService _actingService;
    private readonly IAssetAssignmentService       _assignmentService;
    private readonly IAssetRequisitionService      _requisitionService;
    private readonly IAssetTermsLetterService      _termsLetterService;
    private readonly ICurrentUserService           _currentUser;

    public EmployeePortalController(
        IStaffMovementService          movementService,
        IStaffActingAppointmentService actingService,
        IAssetAssignmentService        assignmentService,
        IAssetRequisitionService       requisitionService,
        IAssetTermsLetterService       termsLetterService,
        ICurrentUserService            currentUser)
    {
        _movementService    = movementService;
        _actingService      = actingService;
        _assignmentService  = assignmentService;
        _requisitionService = requisitionService;
        _termsLetterService = termsLetterService;
        _currentUser        = currentUser;
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
    [HttpGet("acting-appointments")]
    public async Task<IActionResult> GetActingAppointments(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var appointments = await _actingService.GetByEmployeeIdAsync(empId, ct);
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
                    ActionUrl        = $"/employee/movements/{m.Id}/respond"
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
                    ActionUrl      = $"/employee/movements/{m.Id}"
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
                    ActionUrl      = $"/employee/movements/{m.Id}"
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
                    ActionUrl        = $"/employee/movements/{m.Id}"
                });
            }
        }

        return Ok(notifications.OrderByDescending(n => n.IsActionRequired).ThenByDescending(n => n.CreatedAt));
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
            Held                         = held,
            AwaitingAcknowledgement      = unsigned,
            OpenRequisitions             = open
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
