using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employee self-service portal for workforce mobility data.
/// ALL endpoints are automatically scoped to the authenticated user's employee record.
/// The frontend never passes an employee ID — the server derives it from the JWT claim.
/// </summary>
[ApiController]
[Route("api/employee-portal")]
[Authorize]
public class EmployeePortalController : ControllerBase
{
    private readonly IStaffMovementService         _movementService;
    private readonly IStaffActingAppointmentService _actingService;
    private readonly ICurrentUserService           _currentUser;

    public EmployeePortalController(
        IStaffMovementService          movementService,
        IStaffActingAppointmentService actingService,
        ICurrentUserService            currentUser)
    {
        _movementService = movementService;
        _actingService   = actingService;
        _currentUser     = currentUser;
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

        var success = await _movementService.RecordEmployeeResponseAsync(respondDto, ct);
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
}

// ── Input DTO ─────────────────────────────────────────────────────────────────

/// <summary>Request body for recording an employee's response to a proposed movement.</summary>
public sealed class EmployeePortalRespondDto
{
    public bool    Accepted { get; set; }
    public string? Comments { get; set; }
}
