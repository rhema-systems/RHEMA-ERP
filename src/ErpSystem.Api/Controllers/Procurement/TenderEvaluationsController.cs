using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class TenderEvaluationsController : ControllerBase
{
    private readonly ITenderEvaluationService _evaluationService;
    private readonly ILogger<TenderEvaluationsController> _logger;

    public TenderEvaluationsController(
        ITenderEvaluationService evaluationService,
        ILogger<TenderEvaluationsController> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    /// <summary>
    /// Get evaluation by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderEvaluationDto>> GetEvaluation(Guid id)
    {
        try
        {
            var evaluation = await _evaluationService.GetEvaluationByIdAsync(id);
            if (evaluation == null)
            {
                return NotFound($"Evaluation with ID {id} not found");
            }

            return Ok(evaluation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation {EvaluationId}", id);
            return StatusCode(500, "An error occurred while retrieving the evaluation");
        }
    }

    /// <summary>
    /// Get evaluations by bid ID
    /// </summary>
    [HttpGet("by-bid/{bidId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<IEnumerable<TenderEvaluationDto>>> GetEvaluationsByBid(Guid bidId)
    {
        try
        {
            // Service interface has GetBidEvaluationsAsync, not GetEvaluationsByBidIdAsync
            var evaluations = await _evaluationService.GetBidEvaluationsAsync(bidId);
            return Ok(evaluations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluations for bid {BidId}", bidId);
            return StatusCode(500, "An error occurred while retrieving evaluations");
        }
    }

    /// <summary>
    /// Get evaluations by tender ID
    /// </summary>
    [HttpGet("by-tender/{tenderId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<IEnumerable<TenderEvaluationDto>>> GetEvaluationsByTender(Guid tenderId)
    {
        try
        {
            // TODO: Service interface doesn't have GetEvaluationsByTenderIdAsync method
            // Using GetConsolidatedEvaluationsAsync instead
            var evaluations = await _evaluationService.GetConsolidatedEvaluationsAsync(tenderId);
            return Ok(evaluations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluations for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving evaluations");
        }
    }

    /// <summary>
    /// Get my evaluations (all evaluations assigned to current user)
    /// </summary>
    [HttpGet("my-evaluations")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<IEnumerable<TenderEvaluationDto>>> GetMyEvaluations()
    {
        try
        {
            // Get current user's ID from claims or context
            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return BadRequest("Unable to determine current user");
            }

            // Get evaluations for current user
            // The service will handle getting TenderEvaluator records by UserId and then fetching evaluations
            var myEvaluations = await _evaluationService.GetMyEvaluationsByUserIdAsync(userId);
            return Ok(myEvaluations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my evaluations");
            return StatusCode(500, "An error occurred while retrieving your evaluations");
        }
    }

    /// <summary>
    /// Create evaluation
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderEvaluationDto>> CreateEvaluation([FromBody] CreateEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationService.CreateEvaluationAsync(dto);
            return CreatedAtAction(nameof(GetEvaluation), new { id = evaluation.Id }, evaluation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating evaluation");
            return StatusCode(500, "An error occurred while creating the evaluation");
        }
    }

    /// <summary>
    /// Update evaluation
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderEvaluationDto>> UpdateEvaluation(Guid id, [FromBody] UpdateEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationService.UpdateEvaluationAsync(id, dto);
            return Ok(evaluation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation {EvaluationId}", id);
            return StatusCode(500, "An error occurred while updating the evaluation");
        }
    }

    /// <summary>
    /// Submit evaluation
    /// </summary>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult<TenderEvaluationDto>> SubmitEvaluation(Guid id, [FromBody] SubmitEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationService.SubmitEvaluationAsync(id, dto);
            return Ok(evaluation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting evaluation {EvaluationId}", id);
            return StatusCode(500, "An error occurred while submitting the evaluation");
        }
    }

    /// <summary>
    /// Delete evaluation
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager,Employee")]
    public async Task<ActionResult> DeleteEvaluation(Guid id)
    {
        try
        {
            await _evaluationService.DeleteEvaluationAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting evaluation {EvaluationId}", id);
            return StatusCode(500, "An error occurred while deleting the evaluation");
        }
    }

    /// <summary>
    /// Get evaluation scorecard for a bid
    /// </summary>
    [HttpGet("scorecard/bid/{bidId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<EvaluationScorecardDto>> GetEvaluationScorecard(Guid bidId)
    {
        try
        {
            // Service interface has GetBidScorecardAsync, not GetEvaluationScorecardAsync
            var scorecard = await _evaluationService.GetBidScorecardAsync(bidId);
            return Ok(scorecard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation scorecard for bid {BidId}", bidId);
            return StatusCode(500, "An error occurred while retrieving the evaluation scorecard");
        }
    }

    /// <summary>
    /// Get consolidated evaluation for a tender
    /// </summary>
    [HttpGet("consolidated/{tenderId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<ConsolidatedEvaluationDto>> GetConsolidatedEvaluation(Guid tenderId)
    {
        try
        {
            // Service interface has GetConsolidatedEvaluationsAsync (plural), not GetConsolidatedEvaluationAsync
            // Returns IEnumerable, so we'll return the collection
            var consolidatedEvaluations = await _evaluationService.GetConsolidatedEvaluationsAsync(tenderId);
            return Ok(consolidatedEvaluations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consolidated evaluation for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving the consolidated evaluation");
        }
    }

    /// <summary>
    /// Generate evaluation report for a tender
    /// </summary>
    [HttpGet("report/{tenderId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<EvaluationReportDto>> GenerateEvaluationReport(Guid tenderId)
    {
        try
        {
            // Service interface has GetTenderEvaluationReportAsync, not GenerateEvaluationReportAsync
            var report = await _evaluationService.GetTenderEvaluationReportAsync(tenderId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating evaluation report for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while generating the evaluation report");
        }
    }
}
