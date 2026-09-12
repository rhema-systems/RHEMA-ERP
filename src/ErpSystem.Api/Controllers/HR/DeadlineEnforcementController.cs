using ErpSystem.Api.Models;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// HR's manual override on a stalled appraisal pipeline. Both actions are audited against the
/// caller — the advancing officer is taken from the token, never from the request, because this
/// is the record of who overrode a step someone else was supposed to complete.
/// </summary>
[ApiController]
[Route("api/DeadlineEnforcement")]
[Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
public class DeadlineEnforcementController : ControllerBase
{
    private readonly IAppraisalWorkflowService _workflowService;
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeadlineEnforcementController> _logger;

    public DeadlineEnforcementController(
        IAppraisalWorkflowService workflowService,
        IPerformanceAppraisalService appraisalService,
        ICurrentUserService currentUserService,
        ILogger<DeadlineEnforcementController> logger)
    {
        _workflowService    = workflowService;
        _appraisalService   = appraisalService;
        _currentUserService = currentUserService;
        _logger             = logger;
    }

    /// <summary>
    /// HR "advance overdue appraisals" action for a cycle. Honours the cycle's AutoLockOnDeadline
    /// setting: when enabled, every active appraisal whose current step deadline has passed is
    /// advanced via the audited manual-advance path; when disabled, nothing is changed. On demand —
    /// there is no background job.
    /// </summary>
    [HttpPost("enforce/{cycleId:guid}")]
    [ProducesResponseType(typeof(DeadlineEnforcementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnforceDeadlines(Guid cycleId, CancellationToken ct = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _workflowService.AdvanceOverdueAppraisalsAsync(cycleId, employeeId, ct);

            _logger.LogInformation(
                "Advance-overdue for cycle {CycleId}: autoLock={AutoLock}, evaluated={Evaluated}, advanced={Advanced}",
                cycleId, result.AutoLockEnabled, result.Evaluated, result.Advanced);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enforcing deadlines for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while enforcing deadlines");
        }
    }

    /// <summary>
    /// The acting HR officer, from the token.
    ///
    /// ⚠ This used to read a raw <c>employee_id</c> claim and fall back to <c>Guid.Empty</c>,
    /// so an override could be recorded against nobody — on the one action whose entire point is
    /// the audit trail.
    /// </summary>
    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so an override cannot be attributed to you." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    /// <summary>
    /// Manually advance an appraisal past a single stalled pipeline sub-step.
    /// HR specifies the target sub-status (or omits it to advance the current blocking step) and
    /// provides a reason; the advancing officer comes from the token.
    /// </summary>
    [HttpPost("advance/{appraisalId:guid}")]
    [ProducesResponseType(typeof(ManualAdvanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ManuallyAdvance(
        Guid appraisalId, [FromBody] ManualAdvanceRequest req, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _workflowService.ManuallyAdvanceStepAsync(
                appraisalId,
                req.TargetSubStatus,
                req.Reason,
                employeeId,
                ct);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid advance for appraisal {AppraisalId}", appraisalId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error manually advancing appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while advancing the appraisal");
        }
    }
}


