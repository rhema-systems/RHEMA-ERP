using ErpSystem.Api.Models;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/DeadlineEnforcement")]
[Authorize(Roles = "HR,Admin,SuperAdmin")]
public class DeadlineEnforcementController : ControllerBase
{
    private readonly IAppraisalWorkflowService _workflowService;
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly ILogger<DeadlineEnforcementController> _logger;

    public DeadlineEnforcementController(
        IAppraisalWorkflowService workflowService,
        IPerformanceAppraisalService appraisalService,
        ILogger<DeadlineEnforcementController> logger)
    {
        _workflowService  = workflowService;
        _appraisalService = appraisalService;
        _logger           = logger;
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
        try
        {
            var result = await _workflowService.AdvanceOverdueAppraisalsAsync(cycleId, GetEmployeeId(), ct);

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

    private Guid GetEmployeeId()
        => Guid.TryParse(User.FindFirst("employee_id")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>
    /// Manually advance an appraisal past a single stalled pipeline sub-step.
    /// HR specifies the target sub-status (or omits it to advance the current blocking step),
    /// provides a reason, and their employee ID for the audit log.
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

        try
        {
            var result = await _workflowService.ManuallyAdvanceStepAsync(
                appraisalId,
                req.TargetSubStatus,
                req.Reason,
                req.AdvancedByEmployeeId,
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


