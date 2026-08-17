using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// An employee's own travel: raising a request, tracking it, and withdrawing it.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Slice 0 put every staff-travel controller behind
/// <c>HR.Travel.*</c>. That is right for the travel desk's surface — the advance register, the
/// claim ledger, colleagues' passports — but travel is not an HR record *about* an employee the
/// way an appraisal is. TDC's own description is "employees or managers create travel
/// itineraries for approval", so gating without a self-service route would have locked every
/// employee out of the area's primary use case.</para>
///
/// <para><b>The rules, taken from the area-11 self-service surface:</b></para>
/// <list type="number">
/// <item>No route or query parameter carries an employee id. The actor is the token, always.</item>
/// <item>Every id-addressed operation resolves through <see cref="GetOwnActiveRequestAsync"/>.</item>
/// <item>Someone else's request is a <b>404, not a 403</b> — a 403 confirms the id exists, which
/// turns this surface into an oracle for enumerating travel-request ids.</item>
/// <item>Privileged operations have <b>no route here at all</b>. Approving, rejecting, budgeting,
/// booking, advancing and paying are absent by construction rather than by a guard that could be
/// mis-edited later.</item>
/// </list>
///
/// <para>Gated on bare <c>[Authorize]</c> deliberately: holding no travel permission is the
/// normal case for the people this controller serves.</para>
/// </remarks>
[ApiController]
[Route("api/staff-travel/me")]
[StaffTravelBusinessRules]
[Authorize]
public class StaffTravelMeController : HrControllerBase
{
    private readonly IStaffTravelRequestService _service;

    public StaffTravelMeController(IStaffTravelRequestService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    /// <summary>
    /// The caller's own request, or null when it is not theirs / does not exist. Callers turn
    /// null into 404 — never 403, and never a message that distinguishes the two cases.
    /// </summary>
    private async Task<StaffTravelRequestDto?> GetOwnActiveRequestAsync(Guid id, Guid employeeId, CancellationToken ct)
    {
        try
        {
            var request = await _service.GetByIdAsync(id, ct);
            return request.EmployeeId == employeeId ? request : null;
        }
        catch (ArgumentException)
        {
            // The service raises "not found" for an id outside the caller's tenant. Same answer.
            return null;
        }
    }

    // =========================================================================
    // MY REQUESTS
    // =========================================================================

    /// <summary>Every travel request raised for the caller.</summary>
    [HttpGet("requests")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetMyRequests(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Viewing your travel requests") is { } error) return error;

        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    /// <summary>One of the caller's own travel requests, in full.</summary>
    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult<StaffTravelRequestDto>> GetMyRequest(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Viewing a travel request") is { } error) return error;

        var request = await GetOwnActiveRequestAsync(id, employeeId, ct);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>
    /// Raise a travel request for yourself.
    /// </summary>
    /// <remarks>
    /// <c>EmployeeId</c> and <c>InitiatedById</c> are overwritten from the token rather than read
    /// from the payload. On the HR surface they are inputs — the travel desk raises travel for
    /// other people — but here accepting them would let any employee file travel in a colleague's
    /// name, which is the whole reason this controller takes no employee id anywhere.
    /// </remarks>
    [HttpPost("requests")]
    public async Task<ActionResult<StaffTravelRequestDto>> CreateMyRequest(
        [FromBody] CreateStaffTravelRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Raising a travel request") is { } error) return error;

        dto.EmployeeId = employeeId;
        dto.InitiatedById = employeeId;
        dto.InitiatedByRole = TravelInitiatorRole.Employee;

        var created = await _service.CreateAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetMyRequest), new { id = created.Id }, created);
    }

    /// <summary>Amend your own request while it is still editable.</summary>
    [HttpPut("requests/{id:guid}")]
    public async Task<ActionResult<StaffTravelRequestDto>> UpdateMyRequest(
        Guid id, [FromBody] UpdateStaffTravelRequestDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var employeeId,
                "Amending a travel request") is { } error) return error;

        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        return Ok(await _service.UpdateAsync(dto, userId, ct));
    }

    /// <summary>
    /// Submit your own request for approval. The submitter is stamped from the token, not taken
    /// from the payload.
    /// </summary>
    [HttpPost("requests/{id:guid}/submit")]
    public async Task<IActionResult> SubmitMyRequest(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Submitting a travel request") is { } error) return error;

        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        await _service.SubmitAsync(
            new SubmitStaffTravelRequestDto { RequestId = id, SubmittedById = employeeId }, ct);
        return NoContent();
    }

    /// <summary>
    /// Withdraw your own request. The canceller is stamped from the token; a reason is required
    /// because a withdrawn request that does not say why is unusable for the travel desk.
    /// </summary>
    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelMyRequest(
        Guid id, [FromBody] CancelMyStaffTravelRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var employeeId,
                "Withdrawing a travel request") is { } error) return error;

        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        await _service.CancelAsync(new CancelStaffTravelRequestDto
        {
            RequestId = id,
            CancelledById = employeeId,
            CancellationReason = dto.CancellationReason
        }, userId, ct);
        return NoContent();
    }
}
