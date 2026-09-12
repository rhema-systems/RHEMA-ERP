using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Pay-for-performance intake. Approving an appraisal recommendation of a compensation type
/// raises one of these; HR puts the figure on it and walks it to Applied, at which point payroll
/// owns the change. Nothing here touches actual pay.
/// </summary>
/// <remarks>
/// <para><b>W3 slice 14</b>, on <c>HR.Performance.*</c> — the same tiering, for the same reasons,
/// as <see cref="EmploymentActionProposalsController"/>: reads → Read; setting the figure,
/// submit and mark-applied (status-checked only in the service) → Write; approve/reject/recall
/// carry no permission because the service validates the workflow assignee or initiator per
/// instance, and the old SuperAdmin/HR class gate would have refused a non-HR assignee.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class SalaryReviewProposalsController : ControllerBase
{
    private readonly ISalaryReviewProposalService _service;
    private readonly ILogger<SalaryReviewProposalsController> _logger;

    public SalaryReviewProposalsController(ISalaryReviewProposalService service, ILogger<SalaryReviewProposalsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Salary review proposal rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>List salary review proposals (optionally filtered by status)</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<SalaryReviewProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] SalaryReviewProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetAllAsync(status, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing salary review proposals");
            return StatusCode(500, "An error occurred while retrieving proposals");
        }
    }

    /// <summary>Get one salary review proposal</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetByIdAsync(id, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving salary review proposal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the proposal");
        }
    }

    /// <summary>
    /// Set the proposed percentage or amount, and any notes. Only while the proposal is Proposed.
    ///
    /// <para>The recommendation handler raises a proposal with no figure on it — it only knows
    /// that an appraisal called for a merit increase, not how much — so this is where the number
    /// is actually decided.</para>
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSalaryReviewProposalDto dto, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try { return Ok(await _service.UpdateAsync(id, dto, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "amending the proposal"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error amending salary review proposal {Id}", id);
            return StatusCode(500, "An error occurred while amending the proposal");
        }
    }

    // ── Approval, on the generic workflow engine ────────────────────────────
    //
    // ⚠ Approval authority comes from the published SalaryReviewProposal workflow definition,
    // not from any attribute on this controller: submit/approve/reject are inoperable
    // until one is published, and `POST api/Workflow/entity-types/seed` has been re-run.
    // Approve/reject/recall deliberately carry no permission — the service refuses anyone but
    // the workflow assignee (approve/reject) or the initiator (recall).

    /// <summary>Send the proposal for approval. Refused until a figure has been set.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.SubmitForApprovalAsync(id, cancellationToken), id, "submitting the proposal");

    /// <summary>Approve the current workflow step.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.ApproveAsync(id, cancellationToken), id, "approving the proposal");

    /// <summary>Reject the proposal at the current workflow step.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Reject(Guid id, [FromBody] UpdateEmploymentActionProposalDto? dto, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.RejectAsync(id, dto?.Notes, cancellationToken), id, "rejecting the proposal");

    /// <summary>Pull a pending proposal back so its figure can be reworked.</summary>
    [HttpPost("{id:guid}/recall")]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Recall(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.RecallAsync(id, cancellationToken), id, "recalling the proposal");

    /// <summary>Record that payroll has made the change. Only from Approved.</summary>
    [HttpPost("{id:guid}/mark-applied")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> MarkApplied(Guid id, [FromBody] UpdateEmploymentActionProposalDto? dto, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.MarkAppliedAsync(id, dto?.Notes, cancellationToken), id, "marking the proposal applied");

    private async Task<IActionResult> RunWorkflowAction(Func<Task<SalaryReviewProposalDto>> action, Guid id, string description)
    {
        try { return Ok(await action()); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, description); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {Description} for salary review proposal {Id}", description, id);
            return StatusCode(500, $"An error occurred while {description}");
        }
    }
}
