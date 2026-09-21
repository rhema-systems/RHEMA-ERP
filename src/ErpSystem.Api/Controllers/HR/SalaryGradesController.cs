using ErpSystem.Core.DTOs.Common;
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
/// Salary Grades API (tenant-aware).
///
/// Who maintains the salary structure is a policy setting (lane G, <c>SalaryStructureSource</c>).
/// With <b>Payroll</b> as the source — the default — the structure is defined in Administration → HR →
/// Payroll → Grades Setup, these HR tables are a mirror kept current by
/// <see cref="ISalaryStructureProjectionService"/>, and every mutating endpoint returns 409 pointing at
/// payroll, because a write here would be overwritten by the next projection pass. With <b>HR</b> as the
/// source the projection is off and the same endpoints write through the service.
/// </summary>
[ApiController]
[Route("api/hr/salary-grades")]
[Authorize(Policy = "InternalOnly")]
public class SalaryGradesController : ControllerBase
{
    private const string ReadOnlyMessage =
        "Salary grades are defined in Payroll and mirrored into HR. Edit them in Payroll " +
        "(Administration → HR → Payroll → Grades Setup); changes appear here automatically. " +
        "To maintain the structure in HR instead, set the salary structure source to HR under HR policy settings.";

    private readonly ISalaryGradeService _salaryGradeService;
    private readonly ISalaryLevelService _salaryLevelService;
    private readonly ISalaryStructureProjectionService _projectionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly ILogger<SalaryGradesController> _logger;

    public SalaryGradesController(
        ISalaryGradeService salaryGradeService,
        ISalaryLevelService salaryLevelService,
        ISalaryStructureProjectionService projectionService,
        ICurrentUserService currentUserService,
        ICompanyHrPolicySettingsService policySettings,
        ILogger<SalaryGradesController> logger)
    {
        _policySettings = policySettings;
        _salaryGradeService = salaryGradeService;
        _salaryLevelService = salaryLevelService;
        _projectionService = projectionService;
        _currentUserService = currentUserService;
        _logger = logger;
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

    /// <summary>
    /// Retrieves all salary grades for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryGradeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SalaryGradeDto>>> GetAll([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryGradeService.GetAllGradesAsync(tenantId, includeInactive, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving salary grades");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving salary grades");
        }
    }

    /// <summary>
    /// Retrieves salary grades with pagination for the current tenant.
    /// </summary>
    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(PagedResult<SalaryGradeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<SalaryGradeDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryGradeService.GetGradesPagedAsync(tenantId, pageNumber, pageSize, searchTerm, isActive, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged salary grades");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving salary grades");
        }
    }

    /// <summary>
    /// Retrieves a salary grade by ID including its levels and notches.
    /// </summary>
    [HttpGet("{gradeId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(SalaryGradeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryGradeDetailDto>> GetById(Guid gradeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryGradeService.GetGradeHierarchyAsync(tenantId, gradeId, cancellationToken);
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
            _logger.LogError(ex, "Error retrieving salary grade with ID {GradeId}", gradeId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the salary grade");
        }
    }

    /// <summary>
    /// Retrieves salary levels for a salary grade.
    /// </summary>
    [HttpGet("{gradeId:guid}/levels")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryLevelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SalaryLevelDto>>> GetLevelsByGrade(Guid gradeId, [FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryLevelService.GetLevelsByGradeAsync(tenantId, gradeId, includeInactive, cancellationToken);
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
            _logger.LogError(ex, "Error retrieving salary levels for GradeId {GradeId}", gradeId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving salary levels");
        }
    }

    /// <summary>
    /// Re-runs the projection from the payroll-defined salary structure.
    ///
    /// Reads already reconcile automatically; this exists for an explicit "refresh from payroll" action
    /// and for backfilling a tenant whose mirror has never been built.
    /// </summary>
    [HttpPost("sync")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryStructureProjectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryStructureProjectionResult>> SyncFromPayroll(CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _projectionService.ReconcileAsync(tenantId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // HR is the structure source: there is nothing to sync, and the projection says so.
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing the salary structure from payroll");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while syncing the salary structure from payroll");
        }
    }

    /// <summary>Re-orders a grade's levels. 409 while Payroll is the structure source.</summary>
    [HttpPut("{gradeId:guid}/levels/resequence")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResequenceLevels(Guid gradeId, [FromBody] ResequenceSalaryLevelsDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try
        {
            await _salaryLevelService.ResequenceLevelsAsync(GetTenantIdOrThrow(), gradeId, dto.OrderedLevelIds, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Creates a grade. 409 while Payroll is the structure source.</summary>
    /// <remarks>Two-tier: the grade's one implicit level is created with it — see the service.</remarks>
    [HttpPost]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryGradeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryGradeDto>> Create([FromBody] CreateSalaryGradeDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try
        {
            var created = await _salaryGradeService.CreateGradeAsync(GetTenantIdOrThrow(), dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Updates a grade. 409 while Payroll is the structure source.</summary>
    [HttpPut("{gradeId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryGradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryGradeDto>> Update(Guid gradeId, [FromBody] UpdateSalaryGradeDto dto, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        dto.Id = gradeId;
        try { return Ok(await _salaryGradeService.UpdateGradeAsync(GetTenantIdOrThrow(), dto, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Activates or retires a grade. 409 while Payroll is the structure source.</summary>
    [HttpPut("{gradeId:guid}/active")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(SalaryGradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryGradeDto>> SetActive(Guid gradeId, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try { return Ok(await _salaryGradeService.SetGradeActiveAsync(GetTenantIdOrThrow(), gradeId, isActive, cancellationToken)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    /// <summary>Deletes a grade that has no levels. 409 while Payroll is the structure source.</summary>
    [HttpDelete("{gradeId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid gradeId, CancellationToken cancellationToken)
    {
        if (!await IsHrMasteredAsync(cancellationToken)) return MirrorIsReadOnly();
        try { return await _salaryGradeService.DeleteGradeAsync(GetTenantIdOrThrow(), gradeId, cancellationToken) ? NoContent() : NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return ClientError(ex); }
    }

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
