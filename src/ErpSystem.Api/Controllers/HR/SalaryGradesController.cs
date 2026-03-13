using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Grades API (tenant-aware).
/// Manages SalaryGrade and provides hierarchy read access (levels + notches).
/// </summary>
[ApiController]
[Route("api/hr/salary-grades")]
[Authorize]
public class SalaryGradesController : ControllerBase
{
    private readonly ISalaryGradeService _salaryGradeService;
    private readonly ISalaryLevelService _salaryLevelService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SalaryGradesController> _logger;

    public SalaryGradesController(
        ISalaryGradeService salaryGradeService,
        ISalaryLevelService salaryLevelService,
        ICurrentUserService currentUserService,
        ILogger<SalaryGradesController> logger)
    {
        _salaryGradeService = salaryGradeService;
        _salaryLevelService = salaryLevelService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all salary grades for the current tenant.
    /// </summary>
    [HttpGet]
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
    /// Re-sequences levels within a salary grade using the provided ordered level identifiers.
    /// </summary>
    [HttpPut("{gradeId:guid}/levels/resequence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResequenceLevels(Guid gradeId, [FromBody] ResequenceSalaryLevelsDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (gradeId != dto.SalaryGradeId)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            await _salaryLevelService.ResequenceLevelsAsync(tenantId, gradeId, dto.OrderedLevelIds, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resequencing salary levels for GradeId {GradeId}", gradeId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while resequencing salary levels");
        }
    }

    /// <summary>
    /// Creates a new salary grade.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SalaryGradeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryGradeDto>> Create([FromBody] CreateSalaryGradeDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            var created = await _salaryGradeService.CreateGradeAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { gradeId = created.Id }, created);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating salary grade");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the salary grade");
        }
    }

    /// <summary>
    /// Updates an existing salary grade.
    /// </summary>
    [HttpPut("{gradeId:guid}")]
    [ProducesResponseType(typeof(SalaryGradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryGradeDto>> Update(Guid gradeId, [FromBody] UpdateSalaryGradeDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (gradeId != dto.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            var updated = await _salaryGradeService.UpdateGradeAsync(tenantId, dto, cancellationToken);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating salary grade with ID {GradeId}", gradeId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the salary grade");
        }
    }

    /// <summary>
    /// Activates or deactivates a salary grade.
    /// </summary>
    [HttpPut("{gradeId:guid}/active")]
    [ProducesResponseType(typeof(SalaryGradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryGradeDto>> SetActive(Guid gradeId, [FromQuery] bool isActive, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var updated = await _salaryGradeService.SetGradeActiveAsync(tenantId, gradeId, isActive, cancellationToken);
            return Ok(updated);
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
            _logger.LogError(ex, "Error updating salary grade active status for {GradeId}", gradeId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the salary grade");
        }
    }

    /// <summary>
    /// Deletes a salary grade (soft delete).
    /// Deletion is prevented if the grade has any salary levels.
    /// </summary>
    [HttpDelete("{gradeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid gradeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            await _salaryGradeService.DeleteGradeAsync(tenantId, gradeId, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting salary grade with ID {GradeId}", gradeId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the salary grade");
        }
    }

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
