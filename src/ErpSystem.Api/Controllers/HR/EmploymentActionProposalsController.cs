using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employment-action intake. Approving an appraisal recommendation of a promotion, demotion,
/// contract renewal, termination or recognition type raises one of these; HR then actions it in
/// the module that owns the change (staff movements, awards, contracts) and marks it Actioned.
/// </summary>
/// <remarks>
/// <para><b>W3 slice 14</b>, on <c>HR.Performance.*</c> — the register is the downstream half of
/// the appraisal outcome-recommendation flow the performance desk already runs, and like the
/// recommendation itself it is desk-only: a proposed termination is not its subject's to read.
/// Reads → Read, submit and mark-actioned (status-checked only in the service) → Write.
/// Approve/reject/recall carry <b>no</b> permission — the service validates the workflow assignee
/// (approve/reject) or the initiator (recall) per instance, and the old SuperAdmin/HR class gate
/// this replaces would have refused a non-HR assignee the published definition named (the
/// slice-9 offer lesson). A non-HR approver can act but cannot read the register — recorded as a
/// slice-14 residual with the MD-reader precedent (<c>ApprovalReaderGrants</c>) as the fix shape.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class EmploymentActionProposalsController : ControllerBase
{
    private readonly IEmploymentActionProposalService _service;
    private readonly ILogger<EmploymentActionProposalsController> _logger;

    public EmploymentActionProposalsController(IEmploymentActionProposalService service, ILogger<EmploymentActionProposalsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Employment action proposal rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>List employment action proposals (optionally filtered by status)</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmploymentActionProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] EmploymentActionProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetAllAsync(status, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing employment action proposals");
            return StatusCode(500, "An error occurred while retrieving proposals");
        }
    }

    /// <summary>Get one employment action proposal</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetByIdAsync(id, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employment action proposal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the proposal");
        }
    }

    // ── Approval, on the generic workflow engine ────────────────────────────
    //
    // ⚠ Approval authority comes from the published EmploymentActionProposal workflow
    // definition, not from any attribute on this controller: submit/approve/reject are
    // inoperable until one is published, and `POST api/Workflow/entity-types/seed` has been
    // re-run after the build. Approve/reject/recall deliberately carry no permission — the
    // service refuses anyone but the workflow assignee (approve/reject) or the initiator
    // (recall), and a permission gate here would refuse the true assignee.

    /// <summary>Send the proposal for approval.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.SubmitForApprovalAsync(id, cancellationToken), id, "submitting the proposal");

    /// <summary>Approve the current workflow step.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.ApproveAsync(id, cancellationToken), id, "approving the proposal");

    /// <summary>Reject the proposal at the current workflow step.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Reject(Guid id, [FromBody] UpdateEmploymentActionProposalDto? dto, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.RejectAsync(id, dto?.Notes, cancellationToken), id, "rejecting the proposal");

    /// <summary>Pull a pending proposal back.</summary>
    [HttpPost("{id:guid}/recall")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Recall(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.RecallAsync(id, cancellationToken), id, "recalling the proposal");

    /// <summary>Record that the owning module has created the real record. Only from Approved.</summary>
    [HttpPost("{id:guid}/mark-actioned")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> MarkActioned(Guid id, [FromBody] UpdateEmploymentActionProposalDto? dto, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.MarkActionedAsync(id, dto?.Notes, cancellationToken), id, "marking the proposal actioned");

    private async Task<IActionResult> RunWorkflowAction(Func<Task<EmploymentActionProposalDto>> action, Guid id, string description)
    {
        try { return Ok(await action()); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, description); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {Description} for employment action proposal {Id}", description, id);
            return StatusCode(500, $"An error occurred while {description}");
        }
    }
}
