using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
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
/// Reads → Read, or the Managing Director's proposals read (F3, D-94); submit and mark-actioned →
/// Write. Approve/reject/recall carry <b>no</b> permission — the service decides from the record:
/// the Managing Director (D-104), never the submitter or the employee concerned, and only the
/// submitter recalls. A permission gate here would refuse the MD, who holds none of
/// the desk's (the slice-9 offer lesson). The slice-14 residual — a non-HR approver who could act
/// but not read — is closed by the MD's proposals read.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class EmploymentActionProposalsController : ControllerBase
{
    private readonly IEmploymentActionProposalService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<EmploymentActionProposalsController> _logger;

    public EmploymentActionProposalsController(
        IEmploymentActionProposalService service,
        ICurrentUserService currentUser,
        ILogger<EmploymentActionProposalsController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>The acting employee, from the token — the submitter recorded, and the decider compared (F3).</summary>
    private Guid? ActorEmployeeId => _currentUser.EmployeeId is Guid id && id != Guid.Empty ? id : null;

    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Employment action proposal rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>List employment action proposals (optionally filtered by status)</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.PerformanceProposalsReadPolicy)]
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
    [Authorize(Policy = HrPermissions.PerformanceProposalsReadPolicy)]
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
    // Through the published EmploymentActionProposal definition when there is one, and the
    // fallback when there is none (HrWorkflowFallbackAuthority). Who decides is the record's rule,
    // run by the service on both paths (F3, F9); approve/reject/recall therefore carry no
    // permission here.

    /// <summary>Send the proposal for approval.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.SubmitForApprovalAsync(id, ActorEmployeeId, cancellationToken), id, "submitting the proposal");

    /// <summary>Approve the current workflow step.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.ApproveAsync(id, ActorEmployeeId, cancellationToken), id, "approving the proposal");

    /// <summary>Reject the proposal at the current workflow step.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Reject(Guid id, [FromBody] UpdateEmploymentActionProposalDto? dto, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.RejectAsync(id, dto?.Notes, ActorEmployeeId, cancellationToken), id, "rejecting the proposal");

    /// <summary>Pull a pending proposal back. Its submitter only.</summary>
    [HttpPost("{id:guid}/recall")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Recall(Guid id, CancellationToken cancellationToken = default)
        => RunWorkflowAction(() => _service.RecallAsync(id, ActorEmployeeId, cancellationToken), id, "recalling the proposal");

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
