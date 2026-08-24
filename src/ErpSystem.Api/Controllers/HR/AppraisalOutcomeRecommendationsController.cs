using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Appraisal outcome recommendations — what an appraisal says should happen next (a merit
/// increase, a promotion, training, a PIP, a termination).
///
/// <para>Approving one dispatches it to the module that owns the outcome, which creates a real
/// intake record: a <c>SalaryReviewProposal</c>, an <c>EmploymentActionProposal</c>, a training
/// request, a PIP. That is why approve/reject/dismiss are HR-only, and why proposing is limited
/// to the appraisee's manager or HR.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalOutcomeRecommendationsController : ControllerBase
{
    private readonly IAppraisalOutcomeService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AppraisalOutcomeRecommendationsController> _logger;

    public AppraisalOutcomeRecommendationsController(
        IAppraisalOutcomeService service,
        ICurrentUserService currentUserService,
        ILogger<AppraisalOutcomeRecommendationsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    /// <summary>
    /// The acting employee, from the token.
    ///
    /// ⚠ This used to read a raw <c>employee_id</c> claim and silently fall back to
    /// <c>Guid.Empty</c>, so every recommendation raised by a user whose claim was absent or
    /// differently named was recorded as proposed by nobody.
    /// </summary>
    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot act on appraisal recommendations." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    private bool IsPrivilegedActor =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Recommendation rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

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
    [Authorize(Roles = HrRoles)]
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

    /// <summary>Propose a recommendation for an appraisal (the appraisee's manager, or HR)</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Propose([FromBody] CreateAppraisalOutcomeRecommendationDto dto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _service.ProposeAsync(dto, employeeId, IsPrivilegedActor, cancellationToken);
            return CreatedAtAction(nameof(GetByAppraisal), new { appraisalId = result.PerformanceAppraisalId }, result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "proposing the recommendation"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proposing recommendation");
            return StatusCode(500, "An error occurred while proposing the recommendation");
        }
    }

    /// <summary>
    /// Approve a recommendation and dispatch it to the owning module (HR).
    ///
    /// <para>A recommendation that comes back <c>Approved</c> rather than <c>Actioned</c> means
    /// the dispatch failed; retry it, or action the outcome in its own module.</para>
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try { return Ok(await _service.ApproveAsync(id, employeeId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "approving the recommendation"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving recommendation {Id}", id);
            return StatusCode(500, "An error occurred while approving the recommendation");
        }
    }

    /// <summary>Re-run the dispatch for an approved recommendation whose handler failed (HR)</summary>
    [HttpPost("{id:guid}/retry-dispatch")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RetryDispatch(Guid id, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.RetryDispatchAsync(id, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "re-dispatching the recommendation"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-dispatching recommendation {Id}", id);
            return StatusCode(500, "An error occurred while re-dispatching the recommendation");
        }
    }

    /// <summary>Reject a recommendation (HR)</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ResolveRecommendationDto? dto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try { return Ok(await _service.RejectAsync(id, employeeId, dto?.Notes, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "rejecting the recommendation"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting recommendation {Id}", id);
            return StatusCode(500, "An error occurred while rejecting the recommendation");
        }
    }

    /// <summary>Dismiss a recommendation (HR)</summary>
    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(AppraisalOutcomeRecommendationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dismiss(Guid id, [FromBody] ResolveRecommendationDto? dto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try { return Ok(await _service.DismissAsync(id, employeeId, dto?.Notes, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "dismissing the recommendation"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing recommendation {Id}", id);
            return StatusCode(500, "An error occurred while dismissing the recommendation");
        }
    }
}
