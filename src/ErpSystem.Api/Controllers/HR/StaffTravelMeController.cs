using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// An employee's own travel: raising a request, tracking it, recalling or withdrawing it, and asking
/// for a change once it is approved.
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
[Authorize(Policy = "InternalOnly")]
public class StaffTravelMeController : HrControllerBase
{
    private readonly IStaffTravelRequestService _service;
    private readonly IStaffTravelComplianceService _compliance;
    private readonly IStaffTravelItineraryService _itineraries;
    private readonly IStaffTravelBookingService _bookings;

    public StaffTravelMeController(
        IStaffTravelRequestService service,
        IStaffTravelComplianceService compliance,
        IStaffTravelItineraryService itineraries,
        IStaffTravelBookingService bookings,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _compliance = compliance;
        _itineraries = itineraries;
        _bookings = bookings;
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

    /// <summary>One of the caller's own travel requests, as the traveller may read it.</summary>
    /// <remarks>
    /// Finding A6 (lane 1, slice 1c): this returned every comment — the desk's internal notes
    /// included — and the policy-exception decisions, and only the portal page's own filter hid them.
    /// <see cref="StaffTravelMappingExtensions.ToTravellerView"/> drops both on the server.
    /// </remarks>
    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult<StaffTravelRequestDto>> GetMyRequest(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Viewing a travel request") is { } error) return error;

        var request = await GetOwnActiveRequestAsync(id, employeeId, ct);
        return request is null ? NotFound() : Ok(request.ToTravellerView());
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
    /// <remarks>
    /// Never carries a late-submission reason: a trip whose departure has passed is the travel desk's
    /// to submit, with the reason (lane 1). Answers with where the request now is and any warnings —
    /// it answered 204 with nothing before, so a warning had nowhere to go.
    /// </remarks>
    [HttpPost("requests/{id:guid}/submit")]
    public async Task<ActionResult<StaffTravelSubmitResultDto>> SubmitMyRequest(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Submitting a travel request") is { } error) return error;

        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        return Ok(await _service.SubmitAsync(
            new SubmitStaffTravelRequestDto { RequestId = id, SubmittedByEmployeeId = employeeId }, ct));
    }

    /// <summary>
    /// The approved travel policy your trip would be checked against, and its limits — for your own
    /// request form, before you save it (finding T-16). Only the policy that covers you.
    /// </summary>
    [HttpGet("policy-preview")]
    public async Task<ActionResult<StaffTravelPolicyPreviewDto>> GetMyPolicyPreview(
        [FromQuery] DateOnly departure,
        [FromQuery] Guid? originCountryId = null, [FromQuery] Guid? destinationCountryId = null,
        CancellationToken ct = default)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading the travel policy that applies to you") is { } error) return error;

        return Ok(await _service.GetPolicyPreviewAsync(employeeId, departure, originCountryId, destinationCountryId, ct));
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

    /// <summary>
    /// Take your own request back from approval to change it (lane 1). If the travel desk submitted it,
    /// the workflow lets only them recall it, and the answer says so.
    /// </summary>
    [HttpPost("requests/{id:guid}/recall")]
    public async Task<IActionResult> RecallMyRequest(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RecallStaffTravelRequestDto? dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Recalling a travel request") is { } error) return error;

        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        await _service.RecallAsync(id, dto?.Reason, ct);
        return NoContent();
    }

    /// <summary>
    /// Ask for a change to your own approved trip: it comes back to you to edit and goes for approval
    /// again (D-9, lane 1). Bookings, advances and claims stay with the trip.
    /// </summary>
    [HttpPost("requests/{id:guid}/request-change")]
    public async Task<IActionResult> RequestChangeToMyRequest(
        Guid id, [FromBody] RequestStaffTravelChangeDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Asking for a change to a travel request") is { } error) return error;

        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        await _service.RequestChangeAsync(id, dto.Reason, ct);
        return NoContent();
    }

    // =========================================================================
    // MY TRIP — what the desk arranged (lane 7, slice 7c1, E7)
    // =========================================================================
    //
    // The request's own read already carries the trip's records, as summaries; these give the traveller the
    // detail — a flight's times, the itinerary's legs — and what is in force over the trip. Each resolves the
    // request through GetOwnActiveRequestAsync first: someone else's trip is a 404.

    /// <summary>
    /// The itinerary in force on your trip — the version the travel desk finalised (D-42) — or none, saying whether
    /// the desk is still drafting one.
    /// </summary>
    [HttpGet("requests/{id:guid}/itinerary")]
    public async Task<ActionResult<StaffTravelTravellerItineraryDto>> GetMyItinerary(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your itinerary") is { } error) return error;
        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        var current = await _itineraries.GetCurrentVersionAsync(id, ct);
        var inForce = current?.Status is TravelItineraryStatus.Approved
            or TravelItineraryStatus.Active or TravelItineraryStatus.Completed;
        return Ok(new StaffTravelTravellerItineraryDto
        {
            InForce = inForce ? current : null,
            BeingPlanned = current?.Status is TravelItineraryStatus.Draft or TravelItineraryStatus.PendingReview,
        });
    }

    /// <summary>Your trip's bookings in full — flights with their times, hotels, ground legs, car rentals.</summary>
    [HttpGet("requests/{id:guid}/bookings")]
    public async Task<ActionResult<StaffTravelTravellerBookingsDto>> GetMyBookings(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your bookings") is { } error) return error;
        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        return Ok(await _bookings.GetTravellerBookingsAsync(id, ct));
    }

    /// <summary>The destination's health requirements over your trip, and which the travel desk has cleared (D-36).</summary>
    [HttpGet("requests/{id:guid}/health-requirements")]
    public async Task<ActionResult<IReadOnlyList<StaffTravelTripHealthRequirementDto>>> GetMyHealthRequirements(
        Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your trip's health requirements") is { } error) return error;
        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        return Ok(await _compliance.GetTripHealthRequirementsAsync(id, ct));
    }

    /// <summary>
    /// Every alert in force for your destination over the trip — including ones the desk raised before you booked,
    /// which were never sent to you.
    /// </summary>
    [HttpGet("requests/{id:guid}/destination-alerts")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertSummaryDto>>> GetMyDestinationAlerts(
        Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your destination's alerts") is { } error) return error;
        if (await GetOwnActiveRequestAsync(id, employeeId, ct) is null) return NotFound();

        return Ok(await _service.GetDestinationAlertsAsync(id, ct));
    }

    /// <summary>
    /// Confirm you have read the risk assessment for your trip (E1). A Critical trip's flight is not ticketed until you
    /// have (D-37).
    /// </summary>
    /// <remarks>
    /// The desk's route sits on Travel WRITE, which the Employee role never holds, and the service accepts only the
    /// traveller — so before this route no traveller could record it. The acknowledger is the token's; someone else's
    /// assessment is a 404, as a request is here.
    /// </remarks>
    [HttpPost("risk-assessments/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeMyRiskAssessment(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Acknowledging your travel risk assessment") is { } error) return error;

        try
        {
            await _compliance.AcknowledgeRiskAssessmentAsync(
                new AcknowledgeStaffTravelRiskAssessmentDto { RiskAssessmentId = id }, employeeId, ct);
        }
        catch (UnauthorizedAccessException) { return NotFound(); }
        catch (ArgumentException) { return NotFound(); }

        return NoContent();
    }

    // =========================================================================
    // MY TRAVEL DOCUMENTS (lane 7, slice 7c1, E2)
    // =========================================================================
    //
    // The traveller's own passport and other travel documents, so the visa register can answer for them (D-39) and
    // the passport's expiry is checked (O-16). The desk's rules hold: an edit takes the verification off, one primary
    // per type, a verified document is not deleted. The list shows numbers to the last four (O-7); a document's own
    // read shows its owner the full number.

    /// <summary>Your document, or null when it is not yours / does not exist — callers answer 404 for both.</summary>
    private async Task<StaffTravelDocumentDto?> GetOwnDocumentAsync(Guid id, Guid employeeId, CancellationToken ct)
    {
        try
        {
            var document = await _compliance.GetDocumentByIdAsync(id, ct);
            return document.EmployeeId == employeeId ? document : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [HttpGet("travel-documents")]
    public async Task<ActionResult<IEnumerable<StaffTravelDocumentDto>>> GetMyDocuments(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your travel documents") is { } error) return error;
        return Ok(await _compliance.GetDocumentsByEmployeeAsync(employeeId, ct));
    }

    [HttpGet("travel-documents/{id:guid}")]
    public async Task<ActionResult<StaffTravelDocumentDto>> GetMyDocument(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading a travel document") is { } error) return error;
        var document = await GetOwnDocumentAsync(id, employeeId, ct);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>Record one of your own travel documents. Whose it is comes from the token; the desk verifies it.</summary>
    [HttpPost("travel-documents")]
    public async Task<ActionResult<StaffTravelDocumentDto>> CreateMyDocument(
        [FromBody] CreateStaffTravelDocumentDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Recording a travel document") is { } error) return error;

        dto.EmployeeId = employeeId;
        var created = await _compliance.CreateDocumentAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetMyDocument), new { id = created.Id }, created);
    }

    /// <summary>Correct one of your documents — the desk's verification comes off, as for any edit.</summary>
    [HttpPut("travel-documents/{id:guid}")]
    public async Task<ActionResult<StaffTravelDocumentDto>> UpdateMyDocument(
        Guid id, [FromBody] UpdateStaffTravelDocumentDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var employeeId,
                "Correcting a travel document") is { } error) return error;
        if (await GetOwnDocumentAsync(id, employeeId, ct) is null) return NotFound();

        return Ok(await _compliance.UpdateDocumentAsync(dto, userId, ct));
    }

    /// <summary>Remove one of your documents while it is unverified — a verified one is kept (O-15).</summary>
    [HttpDelete("travel-documents/{id:guid}")]
    public async Task<IActionResult> DeleteMyDocument(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Removing a travel document") is { } error) return error;
        if (await GetOwnDocumentAsync(id, employeeId, ct) is null) return NotFound();

        await _compliance.DeleteDocumentAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // MY DESTINATION ALERTS
    // =========================================================================
    //
    // ⚠ Why these exist here rather than on the compliance controller.
    //
    // `POST compliance/alert-notifications/{id}/acknowledge` sits on the Travel WRITE policy, and
    // `AcknowledgeNotificationAsync` refuses anyone but the employee the alert was sent to. Those
    // two rules do not overlap: `HrStaffGrants` gives Travel.Write to HR staff and never to the
    // `Employee` role — the map's own remarks say so — so a traveller cannot pass the endpoint
    // gate, and an HR officer who passes it is then refused by the service. The action was
    // unreachable by anyone except a desk officer acknowledging an alert about their own trip.
    //
    // Same shape as the discipline authority gate: a rule nobody can reach is not a rule. And the
    // same answer this codebase already uses for the assets acknowledgement — a token-scoped
    // route, on a controller that expects its callers to hold no travel permission at all, taking
    // no employee id from anywhere.

    /// <summary>Destination alerts sent to the caller for their trips.</summary>
    [HttpGet("alert-notifications")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertNotificationDto>>> GetMyAlerts(
        CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your travel alerts") is { } error) return error;
        return Ok(await _compliance.GetNotificationsByEmployeeAsync(employeeId, ct));
    }

    /// <summary>The ones the caller has not yet confirmed reading.</summary>
    [HttpGet("alert-notifications/unacknowledged")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertNotificationDto>>> GetMyUnacknowledgedAlerts(
        CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your travel alerts") is { } error) return error;
        return Ok(await _compliance.GetUnacknowledgedNotificationsAsync(employeeId, ct));
    }

    /// <summary>
    /// Confirm you have read a destination alert.
    /// </summary>
    /// <remarks>
    /// The acknowledger is the token's, and the service refuses a notification addressed to
    /// someone else — an acknowledgement anyone can record on your behalf records nothing. That
    /// check is the service's and is not repeated here; this route simply stops being the reason
    /// nobody could reach it.
    /// </remarks>
    [HttpPost("alert-notifications/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeMyAlert(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Acknowledging a travel alert") is { } error) return error;

        try
        {
            await _compliance.AcknowledgeNotificationAsync(
                new AcknowledgeStaffTravelAlertNotificationDto { NotificationId = id }, employeeId, ct);
        }
        catch (UnauthorizedAccessException)
        {
            // Someone else's alert. 404 rather than 403, so the response does not confirm that a
            // notification with this id exists — the same answer this controller gives for a
            // request that is not the caller's.
            return NotFound();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        return NoContent();
    }
}
