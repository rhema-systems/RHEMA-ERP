using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The peer leg of an appraisal run: what a nominated peer has been asked to score, the form
/// they score it on, and the draft/submit pair that saves it.
///
/// Every route resolves the evaluator from the token rather than taking an id — a peer
/// evaluation is only ever filled in by the peer it was assigned to, and
/// <see cref="IPeerEvaluationService"/> matches on <c>EvaluatorId</c> so a mismatched id reads
/// as "not found" rather than as a forbidden edit of someone else's form.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PeerEvaluationsController : ControllerBase
{
    private readonly IPeerEvaluationService _peerEvaluationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PeerEvaluationsController> _logger;

    public PeerEvaluationsController(
        IPeerEvaluationService peerEvaluationService,
        ICurrentUserService currentUserService,
        ILogger<PeerEvaluationsController> logger)
    {
        _peerEvaluationService = peerEvaluationService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// The peer evaluations assigned to the signed-in employee, across every cycle.
    /// A record only appears once the nomination has been approved.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IEnumerable<PeerEvaluationAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyAssignments(CancellationToken cancellationToken = default)
    {
        if (!TryGetEvaluatorId(out var evaluatorId, out var problem)) return problem!;

        try
        {
            var result = await _peerEvaluationService.GetPeerEvaluationAssignmentsAsync(evaluatorId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer evaluation assignments for evaluator {EvaluatorId}", evaluatorId);
            return StatusCode(500, "An error occurred while retrieving peer evaluation assignments");
        }
    }

    /// <summary>
    /// The scoring form for one assignment: the appraisal's frozen criterion snapshot, grade
    /// bands, and whatever the evaluator has already saved.
    /// </summary>
    [HttpGet("{evaluationId:guid}")]
    [ProducesResponseType(typeof(PeerEvaluationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid evaluationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetEvaluatorId(out var evaluatorId, out var problem)) return problem!;

        try
        {
            var result = await _peerEvaluationService.GetPeerEvaluationDetailAsync(evaluationId, evaluatorId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // The service raises this both for "no such evaluation" and for "not yours". Neither
            // confirms the id exists, which is the point.
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer evaluation {EvaluationId}", evaluationId);
            return StatusCode(500, "An error occurred while retrieving the peer evaluation");
        }
    }

    /// <summary>Saves scores without submitting. Refused once the evaluation is submitted.</summary>
    [HttpPost("{evaluationId:guid}/draft")]
    [ProducesResponseType(typeof(PeerEvaluationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveDraft(Guid evaluationId, [FromBody] SavePeerEvaluationDto saveDto, CancellationToken cancellationToken = default)
    {
        if (evaluationId != saveDto.EvaluationId)
            return BadRequest(new { message = "Evaluation ID mismatch" });

        if (!TryGetEvaluatorId(out var evaluatorId, out var problem)) return problem!;

        try
        {
            var result = await _peerEvaluationService.SavePeerEvaluationDraftAsync(saveDto, evaluatorId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "saving the peer evaluation draft");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving peer evaluation draft {EvaluationId}", evaluationId);
            return StatusCode(500, "An error occurred while saving the peer evaluation");
        }
    }

    /// <summary>
    /// Submits the evaluation. Every required criterion must be scored first — competencies
    /// always, KPI items only when the cycle's settings let peers score them.
    /// </summary>
    [HttpPost("{evaluationId:guid}/submit")]
    [ProducesResponseType(typeof(PeerEvaluationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid evaluationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetEvaluatorId(out var evaluatorId, out var problem)) return problem!;

        try
        {
            var result = await _peerEvaluationService.SubmitPeerEvaluationAsync(evaluationId, evaluatorId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "submitting the peer evaluation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting peer evaluation {EvaluationId}", evaluationId);
            return StatusCode(500, "An error occurred while submitting the peer evaluation");
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────

    private bool TryGetEvaluatorId(out Guid evaluatorId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            evaluatorId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot hold peer evaluations." });
            return false;
        }

        evaluatorId = id.Value;
        problem = null;
        return true;
    }

    /// <summary>
    /// Business rules raised by the service are answered with 422 and the rule's own message,
    /// matching <c>EmployeeGoalsController</c> and <c>AppraisalCycleTemplatesController</c>, so a
    /// client can read <c>.message</c> instead of getting a bare 500 body it cannot parse.
    /// These are expected outcomes, so they log at warning rather than error.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Peer evaluation rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }
}
