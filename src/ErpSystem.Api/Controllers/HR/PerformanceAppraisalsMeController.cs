using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// An employee's own appraisal: the written answer they are entitled to give to it.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> The appraisal cycle carries a setting,
/// <c>AllowEmployeeResponse</c>, and the service enforces it — but the only route that could write
/// a response sat on <c>HR.Performance.Write</c>, the desk's policy. So "the employee's written
/// answer to their appraisal" was, as shipped, whatever an HR officer typed. Nothing on the row
/// contradicted that either: <c>AppraisalEmployeeResponse</c> has <b>no author column at all</b> —
/// no <c>RespondedById</c>, the same position D-16 found on the medical appointment and referral.</para>
///
/// <para><b>So the route is the author.</b> With no field to check, the only thing that can make
/// the record mean what it says is a door that refuses everyone except the appraisal's own
/// employee. That is this controller. The desk route is kept for HR transcribing a paper response
/// and is documented there as deliberately unwired — the same disposition, and the same reasoning,
/// as the travel desk's alert acknowledgement in D-32.</para>
///
/// <para><b>The rules, following <c>StaffTravelMeController</c>:</b></para>
/// <list type="number">
/// <item>No route or query parameter carries an employee id. The actor is the token, always.</item>
/// <item>Someone else's appraisal is a <b>404, not a 403</b> — a 403 confirms the id exists, which
/// turns this surface into an oracle for enumerating appraisal ids.</item>
/// <item>Nothing privileged has a route here. Scoring, approving and calibrating are absent by
/// construction rather than by a guard that could be mis-edited later.</item>
/// </list>
///
/// <para>Gated on bare <c>[Authorize]</c> deliberately: holding no performance permission is the
/// normal case for the people this controller serves.</para>
/// </remarks>
[ApiController]
[Route("api/performance-appraisals/me")]
[Authorize(Policy = "InternalOnly")]
public class PerformanceAppraisalsMeController : HrControllerBase
{
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly ILogger<PerformanceAppraisalsMeController> _logger;

    public PerformanceAppraisalsMeController(
        IPerformanceAppraisalService appraisalService,
        ICurrentUserService currentUser,
        ILogger<PerformanceAppraisalsMeController> logger)
        : base(currentUser)
    {
        _appraisalService = appraisalService;
        _logger = logger;
    }

    /// <summary>The caller's own written response to their own appraisal.</summary>
    /// <remarks>
    /// Refused with a 404 when the appraisal is not the caller's, and with a 400 naming the reason
    /// when the cycle's <c>AllowEmployeeResponse</c> setting is off.
    /// </remarks>
    [HttpPost("{appraisalId:guid}/responses")]
    [ProducesResponseType(typeof(AppraisalEmployeeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMyResponse(
        Guid appraisalId, [FromBody] CreateAppraisalEmployeeResponseDto createDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var error = TryGetEmployeeWriteContext(
            out _, out var userId, out var employeeId, "Responding to your appraisal");
        if (error != null)
            return error;

        try
        {
            var response = await _appraisalService.AddOwnEmployeeResponseAsync(
                appraisalId, createDto, employeeId, userId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            // Covers both "not your appraisal" and "that template item is not part of it". Neither
            // distinguishes itself to the caller on purpose.
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // The AllowEmployeeResponse gate. It says which cycle setting refused, so the employee
            // is not left staring at a disabled form with no explanation.
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding own response to appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while adding your response");
        }
    }
}
