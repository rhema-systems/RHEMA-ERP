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
/// Salary Notches API (tenant-aware), read-only.
///
/// Notches mirror the payroll grade notches and carry the amounts HR reads for basic pay. They are
/// maintained by the projection, not edited here. See <see cref="SalaryGradesController"/>.
/// </summary>
[ApiController]
[Route("api/hr/salary-notches")]
[Authorize(Policy = "InternalOnly")]
public class SalaryNotchesController : ControllerBase
{
    private const string ReadOnlyMessage =
        "Salary notches are defined in Payroll and mirrored into HR. Edit them in Payroll " +
        "(Administration → HR → Payroll → Grades Setup); changes appear here automatically. " +
        "To maintain the structure in HR instead, set the salary structure source to HR under HR policy settings.";

    private readonly ISalaryNotchService _salaryNotchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly ILogger<SalaryNotchesController> _logger;

    public SalaryNotchesController(
        ISalaryNotchService salaryNotchService,
        ICurrentUserService currentUserService,
        ICompanyHrPolicySettingsService policySettings,
        ILogger<SalaryNotchesController> logger)
    {
        _policySettings = policySettings;
        _salaryNotchService = salaryNotchService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a salary notch by ID.
    /// </summary>
    [HttpGet("{notchId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryNotchDto>> GetById(Guid notchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryNotchService.GetNotchByIdAsync(tenantId, notchId, cancellationToken);
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
            _logger.LogError(ex, "Error retrieving salary notch with ID {NotchId}", notchId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the salary notch");
        }
    }

    /// <summary>Adds a notch to a level. 409 while Payroll is the structure source.</summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryNotchDto>> Create([FromBody] CreateSalaryNotchDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try
        {
            var created = await _salaryNotchService.CreateNotchAsync(GetTenantIdOrThrow(), dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Updates a notch. 409 while Payroll is the structure source.</summary>
    [HttpPut("{notchId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryNotchDto>> Update(Guid notchId, [FromBody] UpdateSalaryNotchDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        dto.Id = notchId;
        try { return Ok(await _salaryNotchService.UpdateNotchAsync(GetTenantIdOrThrow(), dto, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Activates or retires a notch. 409 while Payroll is the structure source.</summary>
    [HttpPut("{notchId:guid}/active")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryNotchDto>> SetActive(Guid notchId, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try { return Ok(await _salaryNotchService.SetNotchActiveAsync(GetTenantIdOrThrow(), notchId, isActive, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Deletes a notch. 409 while Payroll is the structure source.</summary>
    [HttpDelete("{notchId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid notchId, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try { return await _salaryNotchService.DeleteNotchAsync(GetTenantIdOrThrow(), notchId, cancellationToken) ? NoContent() : NotFound(); }
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
