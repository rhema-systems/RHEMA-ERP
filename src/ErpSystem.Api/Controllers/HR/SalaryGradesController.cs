using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Grades API (tenant-aware), read-only.
///
/// The salary structure is defined in Payroll (Administration → HR → Payroll → Grades Setup). These HR
/// tables are a mirror of it, kept current by <see cref="ISalaryStructureProjectionService"/>, and exist
/// so the many HR foreign keys have rows to point at. Writing to them here would be overwritten by the
/// next projection pass, so every mutating endpoint returns 409 and points the caller at payroll.
/// </summary>
[ApiController]
[Route("api/hr/salary-grades")]
[Authorize(Policy = "InternalOnly")]
public class SalaryGradesController : ControllerBase
{
    private const string ReadOnlyMessage =
        "Salary grades are defined in Payroll and mirrored into HR. Edit them in Payroll " +
        "(Administration → HR → Payroll → Grades Setup); changes appear here automatically.";

    private readonly ISalaryGradeService _salaryGradeService;
    private readonly ISalaryLevelService _salaryLevelService;
    private readonly ISalaryStructureProjectionService _projectionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SalaryGradesController> _logger;

    public SalaryGradesController(
        ISalaryGradeService salaryGradeService,
        ISalaryLevelService salaryLevelService,
        ISalaryStructureProjectionService projectionService,
        ICurrentUserService currentUserService,
        ILogger<SalaryGradesController> logger)
    {
        _salaryGradeService = salaryGradeService;
        _salaryLevelService = salaryLevelService;
        _projectionService = projectionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private ObjectResult MirrorIsReadOnly() => Conflict(new { message = ReadOnlyMessage });

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

    /// <summary>
    /// Not supported — level order follows the payroll grade structure.
    /// </summary>
    [HttpPut("{gradeId:guid}/levels/resequence")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult ResequenceLevels(Guid gradeId, [FromBody] ResequenceSalaryLevelsDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — create the grade in Payroll instead.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create([FromBody] CreateSalaryGradeDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — edit the grade in Payroll instead.
    /// </summary>
    [HttpPut("{gradeId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Update(Guid gradeId, [FromBody] UpdateSalaryGradeDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — a grade's active state follows its payroll definition.
    /// </summary>
    [HttpPut("{gradeId:guid}/active")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult SetActive(Guid gradeId, [FromQuery] bool isActive) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — remove the grade in Payroll; the mirror deactivates it on the next sync.
    /// </summary>
    [HttpDelete("{gradeId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(Guid gradeId) => MirrorIsReadOnly();

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
