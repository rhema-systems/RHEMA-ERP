using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/staff-levels")]
// No global fallback policy exists, so require authentication explicitly (matches sibling HR
// controllers) — otherwise these endpoints, including writes, are reachable anonymously.
[Authorize(Policy = "InternalOnly")]
public class StaffLevelsController : ControllerBase
{
    private readonly IStaffLevelService _staffLevelService;
    private readonly ICurrentUserService _currentUserService;

    public StaffLevelsController(IStaffLevelService staffLevelService, ICurrentUserService currentUserService)
    {
        _staffLevelService = staffLevelService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets the current tenant ID from the authenticated user context.
    /// </summary>
    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }

    /// <summary>
    /// Get all staff levels (ordered by rank).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StaffLevelListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StaffLevelListDto>>> GetAll(CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        var results = await _staffLevelService.GetAllAsync(tenantId, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Get active staff levels only (ordered by rank).
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffLevelListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StaffLevelListDto>>> GetActive(CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        var results = await _staffLevelService.GetActiveAsync(tenantId, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Get staff level by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StaffLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffLevelDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _staffLevelService.GetByIdAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
    }

    /// <summary>
    /// Get staff level by code.
    /// </summary>
    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(StaffLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffLevelDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Code is required." });

        try
        {
            var result = await _staffLevelService.GetByCodeAsync(tenantId, code, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
    }

    /// <summary>
    /// Get detailed staff level information including usage counts.
    /// </summary>
    [HttpGet("{id:guid}/details")]
    [ProducesResponseType(typeof(StaffLevelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffLevelDetailDto>> GetDetails(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _staffLevelService.GetDetailAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
    }

    /// <summary>
    /// Create a new staff level.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(StaffLevelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffLevelDto>> Create([FromBody] CreateStaffLevelDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _staffLevelService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing staff level.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StaffLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffLevelDto>> Update(Guid id, [FromBody] UpdateStaffLevelDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (id != dto.Id)
            return BadRequest(new { message = "ID mismatch." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _staffLevelService.UpdateAsync(tenantId, dto, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Activate a staff level.
    /// </summary>
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(StaffLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffLevelDto>> Activate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _staffLevelService.ActivateAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
    }

    /// <summary>
    /// Deactivate a staff level.
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(StaffLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffLevelDto>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _staffLevelService.DeactivateAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
    }

    /// <summary>
    /// Delete a staff level.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var deleted = await _staffLevelService.DeleteAsync(tenantId, id, cancellationToken);
            return deleted ? NoContent() : NotFound(new { message = "Staff level not found." });
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Staff level not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
