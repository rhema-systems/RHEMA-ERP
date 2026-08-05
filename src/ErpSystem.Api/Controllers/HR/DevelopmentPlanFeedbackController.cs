using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevelopmentPlanFeedbackController : ControllerBase
{
    private readonly IDevelopmentPlanFeedbackService _feedbackService;
    private readonly ILogger<DevelopmentPlanFeedbackController> _logger;

    public DevelopmentPlanFeedbackController(
        IDevelopmentPlanFeedbackService feedbackService,
        ILogger<DevelopmentPlanFeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _logger          = logger;
    }

    /// <summary>Get all feedback entries for a development plan</summary>
    [HttpGet("by-plan/{planId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPlan(Guid planId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _feedbackService.GetByPlanIdAsync(planId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feedback for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving feedback.");
        }
    }

    /// <summary>Add a new feedback entry to a development plan</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanFeedbackDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        [FromBody] CreateEmployeeDevelopmentPlanFeedbackDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _feedbackService.AddAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetByPlan), new { planId = result.DevelopmentPlanId }, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding feedback to plan {PlanId}", dto.DevelopmentPlanId);
            return StatusCode(500, "An error occurred while adding feedback.");
        }
    }

    /// <summary>Delete a feedback entry</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _feedbackService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting feedback {Id}", id);
            return StatusCode(500, "An error occurred while deleting feedback.");
        }
    }
}
