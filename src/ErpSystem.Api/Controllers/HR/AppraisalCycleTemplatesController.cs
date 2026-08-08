using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalCycleTemplatesController : ControllerBase
{
    private readonly IAppraisalCycleTemplateService _cycleTemplateService;
    private readonly ILogger<AppraisalCycleTemplatesController> _logger;

    public AppraisalCycleTemplatesController(IAppraisalCycleTemplateService cycleTemplateService, ILogger<AppraisalCycleTemplatesController> logger)
    {
        _cycleTemplateService = cycleTemplateService;
        _logger = logger;
    }

    /// <summary>Get an appraisal cycle template assignment by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycle template {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the cycle template");
        }
    }

    /// <summary>Get all template assignments for a cycle</summary>
    [HttpGet("by-cycle/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCycle(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.GetByCycleIdAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cycle templates for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving cycle templates");
        }
    }

    /// <summary>Get all cycle assignments for a specific template</summary>
    [HttpGet("by-template/{templateId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTemplate(Guid templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.GetByTemplateIdAsync(templateId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cycle templates for template {TemplateId}", templateId);
            return StatusCode(500, "An error occurred while retrieving cycle templates");
        }
    }

    /// <summary>Create a new cycle-template assignment</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalCycleTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalCycleTemplateDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "assigning a template to a cycle");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal cycle template");
            return StatusCode(500, "An error occurred while creating the cycle template");
        }
    }

    /// <summary>
    /// Actionable rules — "only approved templates can be assigned to a cycle" is the one
    /// callers hit — are raised as <see cref="InvalidOperationException"/> by the service.
    /// Without this they fell to the generic handler, which returns a bare string body: the
    /// client could not read a message out of it, so the user saw an unexplained 500 for a
    /// rule they had simply broken.
    ///
    /// 422 rather than 409, matching the convention already set by
    /// <c>EmployeeGoalsController</c> so the same kind of refusal answers the same way across
    /// the Performance area. Logged at warning: the request was refused correctly.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Cycle-template rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>Update an existing cycle-template assignment</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalCycleTemplateDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // The service keys off the body's Id, so a mismatch would edit a different assignment.
            if (id != updateDto.Id)
                return BadRequest(new { message = "ID mismatch" });

            var result = await _cycleTemplateService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating a template assignment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal cycle template {Id}", id);
            return StatusCode(500, "An error occurred while updating the cycle template");
        }
    }

    /// <summary>Delete a cycle-template assignment</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Cycle template assignment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal cycle template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the cycle template");
        }
    }

    /// <summary>Bulk-assign templates to a cycle</summary>
    [HttpPost("bulk-assign/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAssign(Guid cycleId, [FromBody] IEnumerable<CreateAppraisalCycleTemplateDto> assignments, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.BulkAssignAsync(cycleId, assignments, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "bulk-assigning templates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk-assigning templates to cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while bulk-assigning templates");
        }
    }

    /// <summary>Resolve the highest-priority applicable template for an employee in a cycle</summary>
    [HttpGet("resolve/{cycleId:guid}/{employeeId:guid}")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveForEmployee(Guid cycleId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cycleTemplateService.ResolveTemplateForEmployeeAsync(cycleId, employeeId, cancellationToken);
            if (result == null) return NotFound(new { message = "No applicable template found for this employee in the given cycle" });
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving template for employee {EmployeeId} in cycle {CycleId}", employeeId, cycleId);
            return StatusCode(500, "An error occurred while resolving the template");
        }
    }
}
