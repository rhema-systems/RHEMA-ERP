using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevelopmentPlansController : ControllerBase
{
    private readonly IDevelopmentPlanService _developmentPlanService;
    private readonly ILogger<DevelopmentPlansController> _logger;

    public DevelopmentPlansController(IDevelopmentPlanService developmentPlanService, ILogger<DevelopmentPlansController> logger)
    {
        _developmentPlanService = developmentPlanService;
        _logger = logger;
    }

    /// <summary>Get development plans with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged development plans");
            return StatusCode(500, "An error occurred while retrieving development plans");
        }
    }

    /// <summary>Get a development plan by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving development plan {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the development plan");
        }
    }

    /// <summary>Get all development plans for direct reports of a manager</summary>
    [HttpGet("by-manager/{managerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByManager(Guid managerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetByManagerIdAsync(managerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving development plans for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving team development plans");
        }
    }

    /// <summary>Get development plans for an employee</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetByEmployeeIdAsync(employeeId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving development plans for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving development plans");
        }
    }

    /// <summary>Get the active development plan for an employee, optionally filtered by cycle</summary>
    [HttpGet("active/{employeeId:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivePlan(Guid employeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetActivePlanAsync(employeeId, cycleId, cancellationToken);
            if (result == null) return NotFound(new { message = "No active development plan found" });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active development plan for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving the active development plan");
        }
    }

    /// <summary>Create a new development plan</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDevelopmentPlanDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating development plan");
            return StatusCode(500, "An error occurred while creating the development plan");
        }
    }

    /// <summary>Update an existing development plan</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeDevelopmentPlanDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating development plan {Id}", id);
            return StatusCode(500, "An error occurred while updating the development plan");
        }
    }

    /// <summary>Delete a development plan</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Development plan not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting development plan {Id}", id);
            return StatusCode(500, "An error occurred while deleting the development plan");
        }
    }

    /// <summary>Update the status of a development plan</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] DevelopmentPlanStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.UpdateStatusAsync(id, status, cancellationToken);
            if (!result) return NotFound(new { message = "Development plan not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status of development plan {Id}", id);
            return StatusCode(500, "An error occurred while updating the development plan status");
        }
    }

    // ── Objectives ────────────────────────────────────────────────────────

    /// <summary>Add an objective to a development plan</summary>
    [HttpPost("{planId:guid}/objectives")]
    [ProducesResponseType(typeof(EmployeeDevelopmentObjectiveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddObjective(Guid planId, [FromBody] CreateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.AddObjectiveAsync(planId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding objective to development plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while adding the objective");
        }
    }

    /// <summary>Get objectives for a development plan</summary>
    [HttpGet("{planId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentObjectiveDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetObjectives(Guid planId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetObjectivesAsync(planId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving objectives for development plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving objectives");
        }
    }

    /// <summary>Update an objective in a development plan</summary>
    [HttpPut("{planId:guid}/objectives/{objectiveId:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentObjectiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateObjective(Guid planId, Guid objectiveId, [FromBody] UpdateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.UpdateObjectiveAsync(planId, dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating objective {ObjectiveId} for development plan {PlanId}", objectiveId, planId);
            return StatusCode(500, "An error occurred while updating the objective");
        }
    }

    /// <summary>Delete an objective from a development plan</summary>
    [HttpDelete("{planId:guid}/objectives/{objectiveId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteObjective(Guid planId, Guid objectiveId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.DeleteObjectiveAsync(planId, objectiveId, cancellationToken);
            if (!result) return NotFound(new { message = "Objective not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting objective {ObjectiveId} from development plan {PlanId}", objectiveId, planId);
            return StatusCode(500, "An error occurred while deleting the objective");
        }
    }

    /// <summary>Update progress on a specific objective</summary>
    [HttpPatch("{planId:guid}/objectives/{objectiveId:guid}/progress")]
    [ProducesResponseType(typeof(EmployeeDevelopmentObjectiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateObjectiveProgress(Guid planId, Guid objectiveId, [FromBody] UpdateObjectiveProgressRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.UpdateObjectiveProgressAsync(planId, objectiveId, request.ProgressPercent, request.Notes, request.Status, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for objective {ObjectiveId} in development plan {PlanId}", objectiveId, planId);
            return StatusCode(500, "An error occurred while updating the objective progress");
        }
    }
}

/// <summary>Request body for updating objective progress</summary>
public record UpdateObjectiveProgressRequest(decimal ProgressPercent, string? Notes, DevelopmentObjectiveStatus Status);


