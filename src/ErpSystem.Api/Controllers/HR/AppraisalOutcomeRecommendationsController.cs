using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalOutcomeRecommendationsController : ControllerBase
{
    private readonly IAppraisalOutcomeService _service;
    private readonly ILogger<AppraisalOutcomeRecommendationsController> _logger;

    public AppraisalOutcomeRecommendationsController(IAppraisalOutcomeService service, ILogger<AppraisalOutcomeRecommendationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private Guid GetEmployeeId()
        => Guid.TryParse(User.FindFirst("employee_id")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>Get recommendations for an appraisal</summary>
    [HttpGet("by-appraisal/{appraisalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalOutcomeRecommendationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAppraisal(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetByAppraisalAsync(appraisalId, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for appraisal {Id}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving recommendations");
        }
    }

    /// <summary>HR worklist of recommendations, optionally filtered by status</summary>
    [HttpGet("worklist")]
    [Authorize(Roles = "SuperAdmin,HR")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalOutcomeRecommendationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorklist([FromQuery] RecommendationStatus? status = null, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetWorklistAsync(status, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendation worklist");
            return StatusCode(500, "An error occurred while retrieving the worklist");
        }
    }

    /// <summary>Propose a recommendation for an appraisal</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Propose([FromBody] CreateAppraisalOutcomeRecommendationDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.ProposeAsync(dto, GetEmployeeId(), cancellationToken);
            return CreatedAtAction(nameof(GetByAppraisal), new { appraisalId = result.PerformanceAppraisalId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proposing recommendation");
            return StatusCode(500, "An error occurred while proposing the recommendation");
        }
    }

    /// <summary>Approve a recommendation and dispatch it to the owning module (HR)</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SuperAdmin,HR")]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.ApproveAsync(id, GetEmployeeId(), cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving recommendation {Id}", id);
            return StatusCode(500, "An error occurred while approving the recommendation");
        }
    }

    /// <summary>Reject a recommendation (HR)</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,HR")]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ResolveRecommendationDto dto, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.RejectAsync(id, GetEmployeeId(), dto?.Notes, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting recommendation {Id}", id);
            return StatusCode(500, "An error occurred while rejecting the recommendation");
        }
    }

    /// <summary>Dismiss a recommendation (HR)</summary>
    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Roles = "SuperAdmin,HR")]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dismiss(Guid id, [FromBody] ResolveRecommendationDto dto, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.DismissAsync(id, GetEmployeeId(), dto?.Notes, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing recommendation {Id}", id);
            return StatusCode(500, "An error occurred while dismissing the recommendation");
        }
    }
}
