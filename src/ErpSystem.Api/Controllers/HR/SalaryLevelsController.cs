using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Levels API (tenant-aware).
/// Manages SalaryLevel and provides hierarchy access (notches).
/// </summary>
[ApiController]
[Route("api/hr/salary-levels")]
[Authorize]
public class SalaryLevelsController : ControllerBase
{
    private readonly ISalaryLevelService _salaryLevelService;
    private readonly ISalaryNotchService _salaryNotchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SalaryLevelsController> _logger;

    public SalaryLevelsController(
        ISalaryLevelService salaryLevelService,
        ISalaryNotchService salaryNotchService,
        ICurrentUserService currentUserService,
        ILogger<SalaryLevelsController> logger)
    {
        _salaryLevelService = salaryLevelService;
        _salaryNotchService = salaryNotchService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a salary level by ID including its notches.
    /// </summary>
    [HttpGet("{levelId:guid}")]
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

    /// <summary>
    /// Creates a new salary level.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryLevelDto>> Create([FromBody] CreateSalaryLevelDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            var created = await _salaryLevelService.CreateLevelAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { levelId = created.Id }, created);
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
            _logger.LogError(ex, "Error creating salary level");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the salary level");
        }
    }

    /// <summary>
    /// Updates an existing salary level.
    /// </summary>
    [HttpPut("{levelId:guid}")]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryLevelDto>> Update(Guid levelId, [FromBody] UpdateSalaryLevelDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (levelId != dto.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            var updated = await _salaryLevelService.UpdateLevelAsync(tenantId, dto, cancellationToken);
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
            _logger.LogError(ex, "Error updating salary level with ID {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the salary level");
        }
    }

    /// <summary>
    /// Activates or deactivates a salary level.
    /// </summary>
    [HttpPut("{levelId:guid}/active")]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryLevelDto>> SetActive(Guid levelId, [FromQuery] bool isActive, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var updated = await _salaryLevelService.SetLevelActiveAsync(tenantId, levelId, isActive, cancellationToken);
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
            _logger.LogError(ex, "Error updating salary level active status for {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the salary level");
        }
    }

    /// <summary>
    /// Deletes a salary level (soft delete).
    /// Deletion is prevented if the level has any salary notches.
    /// </summary>
    [HttpDelete("{levelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid levelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            await _salaryLevelService.DeleteLevelAsync(tenantId, levelId, cancellationToken);
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
            _logger.LogError(ex, "Error deleting salary level with ID {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the salary level");
        }
    }

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
