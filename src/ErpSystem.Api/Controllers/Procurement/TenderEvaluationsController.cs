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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.tender.evaluate")]
    public async Task<ActionResult<TenderEvaluationDto>> CreateEvaluation([FromBody] CreateEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationService.CreateEvaluationAsync(dto);
            return CreatedAtAction(nameof(GetEvaluation), new { id = evaluation.Id }, evaluation);
        }
        catch (ProcurementEvaluationCommitteeNotFoundException ex)
        {
            return NotFound(CommitteeProblem(404, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(CommitteeProblem(409, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeValidationException ex)
        {
            return UnprocessableEntity(CommitteeProblem(422, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(403, CommitteeProblem(403, "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (TenderBidInitiationValidationException ex)
        {
            return UnprocessableEntity(PaymentAdmissionProblem(ex));
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
    [Authorize(Policy = "procurement.tender.evaluate")]
    public async Task<ActionResult<TenderEvaluationDto>> UpdateEvaluation(Guid id, [FromBody] UpdateEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationService.UpdateEvaluationAsync(id, dto);
            return Ok(evaluation);
        }
        catch (ProcurementEvaluationCommitteeNotFoundException ex)
        {
            return NotFound(CommitteeProblem(404, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(CommitteeProblem(409, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeValidationException ex)
        {
            return UnprocessableEntity(CommitteeProblem(422, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(403, CommitteeProblem(403, "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (TenderBidInitiationValidationException ex)
        {
            return UnprocessableEntity(PaymentAdmissionProblem(ex));
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
    [Authorize(Policy = "procurement.tender.evaluate")]
    public async Task<ActionResult<TenderEvaluationDto>> SubmitEvaluation(Guid id, [FromBody] SubmitEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationService.SubmitEvaluationAsync(id, dto);
            return Ok(evaluation);
        }
        catch (ProcurementEvaluationCommitteeNotFoundException ex)
        {
            return NotFound(CommitteeProblem(404, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(CommitteeProblem(409, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeValidationException ex)
        {
            return UnprocessableEntity(CommitteeProblem(422, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(403, CommitteeProblem(403, "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (TenderBidInitiationValidationException ex)
        {
            return UnprocessableEntity(PaymentAdmissionProblem(ex));
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
    [Authorize(Policy = "procurement.tender.evaluate")]
    public async Task<ActionResult> DeleteEvaluation(Guid id)
    {
        try
        {
            await _evaluationService.DeleteEvaluationAsync(id);
            return NoContent();
        }
        catch (ProcurementEvaluationCommitteeNotFoundException ex)
        {
            return NotFound(CommitteeProblem(404, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(CommitteeProblem(409, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeValidationException ex)
        {
            return UnprocessableEntity(CommitteeProblem(422, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(403, CommitteeProblem(403, "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (TenderBidInitiationValidationException ex)
        {
            return UnprocessableEntity(PaymentAdmissionProblem(ex));
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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
    [Authorize(Policy = "procurement.records.read")]
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

    /// <summary>
    /// Calculate QCBS (Quality and Cost Based Selection) scores for a tender.
    /// This applies the technical/financial weighting and ranks bids accordingly.
    /// </summary>
    [HttpPost("qcbs/{tenderId}")]
    [Authorize(Policy = "procurement.tender.evaluate")]
    public async Task<ActionResult<QCBSEvaluationResultDto>> CalculateQCBSScores(Guid tenderId)
    {
        try
        {
            var result = await _evaluationService.CalculateQCBSScoresAsync(tenderId);
            return Ok(result);
        }
        catch (ProcurementEvaluationCommitteeConflictException ex)
        {
            return Conflict(CommitteeProblem(409, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeValidationException ex)
        {
            return UnprocessableEntity(CommitteeProblem(422, ex.Code, ex.Message));
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException ex)
        {
            return StatusCode(403, CommitteeProblem(403, "EVALUATION_COMMITTEE_ACCESS_FORBIDDEN", ex.Message));
        }
        catch (TenderBidInitiationValidationException ex)
        {
            return UnprocessableEntity(PaymentAdmissionProblem(ex));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation calculating QCBS scores for tender {TenderId}", tenderId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating QCBS scores for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while calculating QCBS scores");
        }
    }

    /// <summary>
    /// Get stored QCBS evaluation results for a tender without recalculating.
    /// Returns 404 if QCBS evaluation has not been run yet.
    /// </summary>
    [HttpGet("qcbs/{tenderId}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<QCBSEvaluationResultDto>> GetQCBSEvaluationResults(Guid tenderId)
    {
        try
        {
            var result = await _evaluationService.GetQCBSEvaluationResultsAsync(tenderId);
            if (result == null)
            {
                return NotFound("QCBS evaluation has not been run for this tender yet");
            }
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation getting QCBS results for tender {TenderId}", tenderId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QCBS evaluation results for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving QCBS evaluation results");
        }
    }

    private ProblemDetails CommitteeProblem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = "Evaluation committee control",
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };

    private ProblemDetails PaymentAdmissionProblem(TenderBidInitiationValidationException exception) => new()
    {
        Status = StatusCodes.Status422UnprocessableEntity,
        Title = "Bid payment is not eligible for evaluation",
        Detail = exception.Message,
        Instance = Request.Path.Value,
        Extensions =
        {
            ["code"] = exception.Code,
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
