using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Read-only performance analytics: how a cycle's ratings fell out, and how one person's scores
/// have moved over the years.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceAnalyticsController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IPerformanceAnalyticsService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PerformanceAnalyticsController> _logger;

    public PerformanceAnalyticsController(
        IPerformanceAnalyticsService service,
        ICurrentUserService currentUserService,
        ApplicationDbContext db,
        ILogger<PerformanceAnalyticsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _db = db;
        _logger = logger;
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    /// <summary>
    /// True when the caller may see this employee's score history: HR, the employee themselves,
    /// or their line manager.
    ///
    /// <para>Without this, any authenticated user could read anyone's multi-year appraisal scores
    /// by passing their id — the same "actor from the URL" hole found across the appraisal run.</para>
    /// </summary>
    private async Task<bool> CanViewTrendAsync(Guid employeeId, CancellationToken ct)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me) return false;
        if (me == employeeId) return true;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<Employee>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me, ct);
    }

    /// <summary>Rating distribution for a cycle (calibration leniency/skew)</summary>
    [HttpGet("cycle/{cycleId:guid}/rating-distribution")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationDistributionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCycleRatingDistribution(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetCycleRatingDistributionAsync(cycleId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building rating distribution for cycle {Id}", cycleId);
            return StatusCode(500, "An error occurred while building the rating distribution");
        }
    }

    /// <summary>An employee's multi-year appraisal score trend (HR, the employee, or their manager)</summary>
    [HttpGet("employee/{employeeId:guid}/trend")]
    [ProducesResponseType(typeof(EmployeePerformanceTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeTrend(Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (!await CanViewTrendAsync(employeeId, cancellationToken))
            return StatusCode(403, new { message = "You can only view your own performance trend, or that of someone who reports to you." });

        try { return Ok(await _service.GetEmployeeTrendAsync(employeeId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building trend for employee {Id}", employeeId);
            return StatusCode(500, "An error occurred while building the performance trend");
        }
    }

    /// <summary>The signed-in employee's own multi-year appraisal score trend</summary>
    [HttpGet("employee/me/trend")]
    [ProducesResponseType(typeof(EmployeePerformanceTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyTrend(CancellationToken cancellationToken = default)
    {
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty)
            return BadRequest(new { message = "Your account is not linked to an employee record, so it has no performance history." });

        try { return Ok(await _service.GetEmployeeTrendAsync(me, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building trend for the current employee {Id}", me);
            return StatusCode(500, "An error occurred while building the performance trend");
        }
    }
}
