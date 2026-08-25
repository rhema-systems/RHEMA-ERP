using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The two lightweight linkages that tie the performance area together: the competencies a goal
/// requires, and the yearly objectives a check-in was about.
///
/// <para><b>Entitlement follows the parent record.</b> A goal's required skills are readable and
/// editable by HR, the goal's owner and their line manager; a check-in's objectives by HR, the
/// employee and whoever is conducting it; the development suggestions by HR, the employee and
/// their manager. Before this the whole controller was <c>[Authorize]</c> and nothing else, so any
/// authenticated user could read — and <em>replace</em> — the linkages on anyone's goals.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class PerformanceLinksController : ControllerBase
{
    private readonly IPerformanceLinkService _service;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PerformanceLinksController> _logger;

    public PerformanceLinksController(
        IPerformanceLinkService service,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<PerformanceLinksController> logger)
    {
        _service = service;
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>W3: whether the caller holds the given performance policy (seed and role fallback both count).</summary>
    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>The goal's owner, that employee's line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessGoalAsync(Guid goalId, string policy, CancellationToken ct)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<EmployeeGoal>()
            .AsNoTracking()
            .Where(g => g.Id == goalId && g.TenantId == tenantId)
            .AnyAsync(g => g.EmployeeId == me || g.Employee.ManagerId == me, ct);
    }

    /// <summary>The employee the check-in is about, whoever is conducting it, or a policy holder.</summary>
    private async Task<bool> CanAccessCheckInAsync(Guid checkInId, string policy, CancellationToken ct)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<CheckIn>()
            .AsNoTracking()
            .Where(c => c.Id == checkInId && c.TenantId == tenantId)
            .AnyAsync(c => c.EmployeeId == me || c.ConductedById == me, ct);
    }

    /// <summary>The employee, their line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessEmployeeAsync(Guid employeeId, string policy, CancellationToken ct)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (me == employeeId) return true;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<Core.Entities.HR.Employee>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me, ct);
    }

    /// <summary>Get the required skills (competencies) for an employee goal</summary>
    [HttpGet("goals/{goalId:guid}/required-skills")]
    [ProducesResponseType(typeof(IEnumerable<GoalRequiredSkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGoalRequiredSkills(Guid goalId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessGoalAsync(goalId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            return Ok(await _service.GetGoalRequiredSkillsAsync(goalId, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting required skills for goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while retrieving required skills");
        }
    }

    /// <summary>
    /// Replace the required skills for an employee goal.
    /// </summary>
    /// <remarks>
    /// ⚠ Replace-set: the body is the whole list. Posting an empty array clears every linked
    /// competency — send the full set you want to end up with, not just the additions.
    /// </remarks>
    [HttpPut("goals/{goalId:guid}/required-skills")]
    [ProducesResponseType(typeof(IEnumerable<GoalRequiredSkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGoalRequiredSkills(Guid goalId, [FromBody] List<SetGoalRequiredSkillDto> skills, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessGoalAsync(goalId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            return Ok(await _service.SetGoalRequiredSkillsAsync(goalId, skills ?? new(), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting required skills for goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while saving required skills");
        }
    }

    /// <summary>Get development-plan skill suggestions for an employee in a cycle (from their goals' required skills)</summary>
    [HttpGet("employees/{employeeId:guid}/cycles/{cycleId:guid}/skill-suggestions")]
    [ProducesResponseType(typeof(IEnumerable<DevelopmentSkillSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDevelopmentSkillSuggestions(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeAsync(employeeId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            return Ok(await _service.GetDevelopmentSkillSuggestionsAsync(employeeId, cycleId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skill suggestions for employee {EmployeeId} cycle {CycleId}", employeeId, cycleId);
            return StatusCode(500, "An error occurred while retrieving skill suggestions");
        }
    }

    /// <summary>The signed-in employee's own development suggestions for a cycle.</summary>
    /// <remarks>The client has no employee id of its own; this is how the development-plan screen
    /// asks for its own suggestions without one.</remarks>
    [HttpGet("employees/me/cycles/{cycleId:guid}/skill-suggestions")]
    [ProducesResponseType(typeof(IEnumerable<DevelopmentSkillSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyDevelopmentSkillSuggestions(Guid cycleId, CancellationToken cancellationToken = default)
    {
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return Forbid();

        try
        {
            return Ok(await _service.GetDevelopmentSkillSuggestionsAsync(me, cycleId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting own skill suggestions for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving skill suggestions");
        }
    }

    /// <summary>Get the yearly objectives linked to a check-in</summary>
    [HttpGet("check-ins/{checkInId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<CheckInObjectiveLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCheckInObjectives(Guid checkInId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            return Ok(await _service.GetCheckInObjectivesAsync(checkInId, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting objectives for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while retrieving objectives");
        }
    }

    /// <summary>
    /// Replace the yearly objectives linked to a check-in.
    /// </summary>
    /// <remarks>⚠ Replace-set, as above: an empty array unlinks every objective.</remarks>
    [HttpPut("check-ins/{checkInId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<CheckInObjectiveLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCheckInObjectives(Guid checkInId, [FromBody] List<Guid> companyGoalIds, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            return Ok(await _service.SetCheckInObjectivesAsync(checkInId, companyGoalIds ?? new(), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting objectives for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while saving objectives");
        }
    }
}
