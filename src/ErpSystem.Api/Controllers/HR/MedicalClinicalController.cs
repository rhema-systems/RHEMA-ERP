using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical-clinical")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class MedicalClinicalController : MedicalControllerBase
{
    private readonly IMedicalClinicalService _service;

    public MedicalClinicalController(IMedicalClinicalService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // PRE-AUTHORIZATIONS
    // =========================================================================

    [HttpGet("pre-authorizations")]
    public async Task<ActionResult<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>>> GetAllPreAuthorizations(CancellationToken ct)
        => Ok(await _service.GetAllPreAuthorizationsAsync(ct));

    [HttpGet("pre-authorizations/{id:guid}")]
    public async Task<ActionResult<MedicalClaimPreAuthorizationDto>> GetPreAuthorization(Guid id, CancellationToken ct)
        => Ok(await _service.GetPreAuthorizationByIdAsync(id, ct));

    [HttpGet("pre-authorizations/number/{authorizationNumber}")]
    public async Task<ActionResult<MedicalClaimPreAuthorizationDto?>> GetPreAuthorizationByNumber(
        string authorizationNumber,
        CancellationToken ct)
        => Ok(await _service.GetPreAuthorizationByNumberAsync(authorizationNumber, ct));

    [HttpGet("employees/{employeeId:guid}/pre-authorizations")]
    public async Task<ActionResult<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>>> GetPreAuthorizationsByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetPreAuthorizationsByEmployeeAsync(employeeId, ct));

    [HttpGet("pre-authorizations/pending")]
    public async Task<ActionResult<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>>> GetPendingPreAuthorizations(
        CancellationToken ct)
        => Ok(await _service.GetPendingPreAuthorizationsAsync(ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("pre-authorizations")]
    public async Task<ActionResult<MedicalClaimPreAuthorizationDto>> CreatePreAuthorization(
        [FromBody] CreateMedicalClaimPreAuthorizationDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreatePreAuthorizationAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetPreAuthorization), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("pre-authorizations/{id:guid}")]
    public async Task<ActionResult<MedicalClaimPreAuthorizationDto>> UpdatePreAuthorization(
        Guid id,
        [FromBody] UpdateMedicalClaimPreAuthorizationDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdatePreAuthorizationAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("pre-authorizations/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePreAuthorization(
        Guid id,
        [FromBody] ApproveMedicalClaimPreAuthorizationDto dto,
        CancellationToken ct)
    {
        dto.PreAuthorizationId = id;
        if (TryGetEmployeeWriteContext(out _, out var approverUserId, out var employeeId) is { } error) return error;
        dto.ApprovedBy = employeeId; // internal approver derived from the authenticated employee, not the client
        await _service.ApprovePreAuthorizationAsync(dto, approverUserId, ct);
        return Ok(new { message = "Pre-authorization approved." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("pre-authorizations/{id:guid}/reject")]
    public async Task<IActionResult> RejectPreAuthorization(
        Guid id,
        [FromBody] RejectMedicalClaimPreAuthorizationDto dto,
        CancellationToken ct)
    {
        dto.PreAuthorizationId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.RejectPreAuthorizationAsync(dto, userId, ct);
        return Ok(new { message = "Pre-authorization rejected." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("pre-authorizations/{id:guid}")]
    public async Task<IActionResult> DeletePreAuthorization(Guid id, CancellationToken ct)
    {
        await _service.DeletePreAuthorizationAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // REFERRALS
    // =========================================================================

    [HttpGet("referrals")]
    public async Task<ActionResult<IEnumerable<MedicalReferralSummaryDto>>> GetAllReferrals(CancellationToken ct)
        => Ok(await _service.GetAllReferralsAsync(ct));

    [HttpGet("referrals/{id:guid}")]
    public async Task<ActionResult<MedicalReferralDto>> GetReferral(Guid id, CancellationToken ct)
        => Ok(await _service.GetReferralByIdAsync(id, ct));

    [HttpGet("referrals/number/{referralNumber}")]
    public async Task<ActionResult<MedicalReferralDto?>> GetReferralByNumber(string referralNumber, CancellationToken ct)
        => Ok(await _service.GetReferralByNumberAsync(referralNumber, ct));

    [HttpGet("employees/{employeeId:guid}/referrals")]
    public async Task<ActionResult<IEnumerable<MedicalReferralSummaryDto>>> GetReferralsByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetReferralsByEmployeeAsync(employeeId, ct));

    [HttpGet("referrals/pending")]
    public async Task<ActionResult<IEnumerable<MedicalReferralSummaryDto>>> GetPendingReferrals(CancellationToken ct)
        => Ok(await _service.GetPendingReferralsAsync(ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("referrals")]
    public async Task<ActionResult<MedicalReferralDto>> CreateReferral(
        [FromBody] CreateMedicalReferralDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateReferralAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetReferral), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("referrals/{id:guid}")]
    public async Task<ActionResult<MedicalReferralDto>> UpdateReferral(
        Guid id,
        [FromBody] UpdateMedicalReferralDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateReferralAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("referrals/{id:guid}/status")]
    public async Task<IActionResult> UpdateReferralStatus(
        Guid id,
        [FromBody] UpdateMedicalReferralStatusDto dto,
        CancellationToken ct)
    {
        dto.ReferralId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.UpdateReferralStatusAsync(dto, userId, ct);
        return Ok(new { message = "Referral status updated." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("referrals/{id:guid}/complete")]
    public async Task<IActionResult> CompleteReferral(
        Guid id,
        [FromBody] CompleteMedicalReferralDto dto,
        CancellationToken ct)
    {
        dto.ReferralId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.CompleteReferralAsync(dto, userId, ct);
        return Ok(new { message = "Referral completed." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("referrals/{id:guid}")]
    public async Task<IActionResult> DeleteReferral(Guid id, CancellationToken ct)
    {
        await _service.DeleteReferralAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // APPOINTMENTS
    // =========================================================================

    [HttpGet("appointments")]
    public async Task<ActionResult<IEnumerable<MedicalAppointmentSummaryDto>>> GetAllAppointments(CancellationToken ct)
        => Ok(await _service.GetAllAppointmentsAsync(ct));

    [HttpGet("appointments/{id:guid}")]
    public async Task<ActionResult<MedicalAppointmentDto>> GetAppointment(Guid id, CancellationToken ct)
        => Ok(await _service.GetAppointmentByIdAsync(id, ct));

    [HttpGet("appointments/number/{appointmentNumber}")]
    public async Task<ActionResult<MedicalAppointmentDto?>> GetAppointmentByNumber(
        string appointmentNumber,
        CancellationToken ct)
        => Ok(await _service.GetAppointmentByNumberAsync(appointmentNumber, ct));

    [HttpGet("employees/{employeeId:guid}/appointments")]
    public async Task<ActionResult<IEnumerable<MedicalAppointmentSummaryDto>>> GetAppointmentsByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetAppointmentsByEmployeeAsync(employeeId, ct));

    [HttpGet("appointments/upcoming")]
    public async Task<ActionResult<IEnumerable<MedicalAppointmentSummaryDto>>> GetUpcomingAppointments(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetUpcomingAppointmentsAsync(daysAhead, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("appointments")]
    public async Task<ActionResult<MedicalAppointmentDto>> CreateAppointment(
        [FromBody] CreateMedicalAppointmentDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateAppointmentAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetAppointment), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("appointments/{id:guid}")]
    public async Task<ActionResult<MedicalAppointmentDto>> UpdateAppointment(
        Guid id,
        [FromBody] UpdateMedicalAppointmentDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateAppointmentAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("appointments/{id:guid}/status")]
    public async Task<IActionResult> UpdateAppointmentStatus(
        Guid id,
        [FromBody] UpdateMedicalAppointmentStatusDto dto,
        CancellationToken ct)
    {
        dto.AppointmentId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.UpdateAppointmentStatusAsync(dto, userId, ct);
        return Ok(new { message = "Appointment status updated." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("appointments/{id:guid}/cancel")]
    public async Task<IActionResult> CancelAppointment(
        Guid id,
        [FromBody] CancelMedicalAppointmentDto dto,
        CancellationToken ct)
    {
        dto.AppointmentId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.CancelAppointmentAsync(dto, userId, ct);
        return Ok(new { message = "Appointment cancelled." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("appointments/{id:guid}/check-in")]
    public async Task<IActionResult> CheckInAppointment(
        Guid id,
        [FromBody] CheckInMedicalAppointmentDto dto,
        CancellationToken ct)
    {
        dto.AppointmentId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.CheckInAppointmentAsync(dto, userId, ct);
        return Ok(new { message = "Appointment checked in." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("appointments/{id:guid}/check-out")]
    public async Task<IActionResult> CheckOutAppointment(
        Guid id,
        [FromBody] CheckOutMedicalAppointmentDto dto,
        CancellationToken ct)
    {
        dto.AppointmentId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.CheckOutAppointmentAsync(dto, userId, ct);
        return Ok(new { message = "Appointment checked out." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("appointments/{id:guid}")]
    public async Task<IActionResult> DeleteAppointment(Guid id, CancellationToken ct)
    {
        await _service.DeleteAppointmentAsync(id, ct);
        return NoContent();
    }
}
