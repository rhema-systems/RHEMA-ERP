using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Notches API (tenant-aware).
/// Manages SalaryNotch.
/// </summary>
[ApiController]
[Route("api/hr/salary-notches")]
[Authorize]
public class SalaryNotchesController : ControllerBase
{
    private readonly ISalaryNotchService _salaryNotchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SalaryNotchesController> _logger;

    public SalaryNotchesController(
        ISalaryNotchService salaryNotchService,
        ICurrentUserService currentUserService,
        ILogger<SalaryNotchesController> logger)
    {
        _salaryNotchService = salaryNotchService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a salary notch by ID.
    /// </summary>
    [HttpGet("{notchId:guid}")]
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

    /// <summary>
    /// Creates a new salary notch.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryNotchDto>> Create([FromBody] CreateSalaryNotchDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            var created = await _salaryNotchService.CreateNotchAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { notchId = created.Id }, created);
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
            _logger.LogError(ex, "Error creating salary notch");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the salary notch");
        }
    }

    /// <summary>
    /// Updates an existing salary notch.
    /// </summary>
    [HttpPut("{notchId:guid}")]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryNotchDto>> Update(Guid notchId, [FromBody] UpdateSalaryNotchDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (notchId != dto.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = GetTenantIdOrThrow();
            dto.TenantId = tenantId;

            var updated = await _salaryNotchService.UpdateNotchAsync(tenantId, dto, cancellationToken);
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
            _logger.LogError(ex, "Error updating salary notch with ID {NotchId}", notchId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the salary notch");
        }
    }

    /// <summary>
    /// Activates or deactivates a salary notch.
    /// </summary>
    [HttpPut("{notchId:guid}/active")]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryNotchDto>> SetActive(Guid notchId, [FromQuery] bool isActive, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var updated = await _salaryNotchService.SetNotchActiveAsync(tenantId, notchId, isActive, cancellationToken);
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
            _logger.LogError(ex, "Error updating salary notch active status for {NotchId}", notchId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the salary notch");
        }
    }

    /// <summary>
    /// Deletes a salary notch (hard delete).
    /// </summary>
    [HttpDelete("{notchId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid notchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            await _salaryNotchService.DeleteNotchAsync(tenantId, notchId, cancellationToken);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting salary notch with ID {NotchId}", notchId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the salary notch");
        }
    }

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
