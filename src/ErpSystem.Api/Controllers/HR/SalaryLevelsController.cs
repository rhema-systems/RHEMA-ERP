using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Levels API (tenant-aware), read-only.
///
/// Levels are synthesized by the projection from the payroll-defined salary structure (payroll is
/// 2-tier, HR is 3-tier), so they cannot be edited here. See <see cref="SalaryGradesController"/>.
/// </summary>
[ApiController]
[Route("api/hr/salary-levels")]
[Authorize(Policy = "InternalOnly")]
public class SalaryLevelsController : ControllerBase
{
    private const string ReadOnlyMessage =
        "Salary levels are derived from the payroll-defined salary structure and cannot be edited in HR. " +
        "Edit the grade in Payroll (Administration → HR → Payroll → Grades Setup). To maintain the structure " +
        "in HR instead, set the salary structure source to HR under HR policy settings.";

    private readonly ISalaryLevelService _salaryLevelService;
    private readonly ISalaryNotchService _salaryNotchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly ILogger<SalaryLevelsController> _logger;

    public SalaryLevelsController(
        ISalaryLevelService salaryLevelService,
        ISalaryNotchService salaryNotchService,
        ICurrentUserService currentUserService,
        ICompanyHrPolicySettingsService policySettings,
        ILogger<SalaryLevelsController> logger)
    {
        _policySettings = policySettings;
        _salaryLevelService = salaryLevelService;
        _salaryNotchService = salaryNotchService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a salary level by ID including its notches.
    /// </summary>
    [HttpGet("{levelId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(SalaryLevelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryLevelDetailDto>> GetById(Guid levelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryLevelService.GetLevelDetailAsync(tenantId, levelId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving salary level with ID {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the salary level");
        }
    }

    /// <summary>
    /// Retrieves salary notches for a salary level.
    /// </summary>
    [HttpGet("{levelId:guid}/notches")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryNotchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SalaryNotchDto>>> GetNotchesByLevel(Guid levelId, [FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryNotchService.GetNotchesByLevelAsync(tenantId, levelId, includeInactive, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving salary notches for LevelId {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving salary notches");
        }
    }

    /// <summary>Adds a level to a grade. 409 while Payroll is the structure source; 400 in two-tier when the grade already has its one level.</summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryLevelDto>> Create([FromBody] CreateSalaryLevelDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try
        {
            var created = await _salaryLevelService.CreateLevelAsync(GetTenantIdOrThrow(), dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Updates a level. 409 while Payroll is the structure source.</summary>
    [HttpPut("{levelId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryLevelDto>> Update(Guid levelId, [FromBody] UpdateSalaryLevelDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        dto.Id = levelId;
        try { return Ok(await _salaryLevelService.UpdateLevelAsync(GetTenantIdOrThrow(), dto, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Activates or retires a level. 409 while Payroll is the structure source.</summary>
    [HttpPut("{levelId:guid}/active")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryLevelDto>> SetActive(Guid levelId, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try { return Ok(await _salaryLevelService.SetLevelActiveAsync(GetTenantIdOrThrow(), levelId, isActive, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Deletes a level that has no notches. 409 while Payroll is the structure source.</summary>
    [HttpDelete("{levelId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid levelId, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try { return await _salaryLevelService.DeleteLevelAsync(GetTenantIdOrThrow(), levelId, cancellationToken) ? NoContent() : NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    // ── Lane G: the 409 is conditional on who maintains the structure ─────────
    //
    // ⚠ While Payroll is the source (the default) every write answers 409 exactly as before —
    // the row would be overwritten by the next projection. While HR is the source the projection is
    // off and the same actions call the service that always existed behind them.
    private async Task<bool> IsHrMasteredAsync(CancellationToken cancellationToken)
        => (await _policySettings.GetAsync(cancellationToken)).SalaryStructureSource == SalaryStructureSource.Hr;

    private ObjectResult MirrorIsReadOnly() => Conflict(new { message = ReadOnlyMessage });

    private ActionResult ClientError(Exception ex) => ex switch
    {
        ArgumentException => NotFound(new { message = ex.Message }),
        InvalidOperationException => BadRequest(new { message = ex.Message }),
        UnauthorizedAccessException => Unauthorized(new { message = ex.Message }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
    };


    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
