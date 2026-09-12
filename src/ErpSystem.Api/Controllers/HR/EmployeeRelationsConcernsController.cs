using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Anonymous / whistleblower intake — area 9c slice 6, decisions D-2 and D-9.
/// </summary>
/// <remarks>
/// <para><b>⚠ Anonymous means UNATTRIBUTED, not UNAUTHENTICATED.</b> Everything here needs a valid
/// internal token; what the reporting endpoints do not do is record who called. A truly public
/// surface would be only the second anonymous endpoint in the whole application — the first,
/// training certificate verification, is still awaiting TDC's answer — and needs its own security
/// review before it exists. <b>The honest limit, so nobody over-promises to staff: request logs and
/// the reverse proxy still see the caller.</b> If TDC needs untraceable reporting, that is a
/// different build and should be raised as such.</para>
///
/// <para><b>Only the reporter is anonymous.</b> HR triaging, replying, closing or converting is
/// recorded against them by name — the desk is accountable for what it does with a report.</para>
///
/// <para><b>Rate limiting is not decoration here.</b> <c>report</c> is an unattributed write, and
/// <c>track</c> takes a secret in the body — brute-forcing a retrieval code is the obvious attack
/// on this surface, and both carry <c>SensitivePolicy</c> (5 per minute per caller).</para>
/// </remarks>
[ApiController]
[Route("api/hr/employee-relations/concerns")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class EmployeeRelationsConcernsController : ControllerBase
{
    private readonly IEmployeeRelationsConcernService _service;
    private readonly ICurrentUserService _currentUser;

    public EmployeeRelationsConcernsController(
        IEmployeeRelationsConcernService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // The reporter's side — ungated, and unattributed
    // =========================================================================

    /// <summary>
    /// Reports a concern without giving a name. Returns the retrieval code <b>once</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately ungated beyond <c>InternalOnly</c>: a whistleblowing channel that only some
    /// staff could use would not be one. And deliberately <b>no</b> employee id is read from the
    /// token — the service method takes no actor parameter at all, so there is no path by which a
    /// caller's identity could reach the row even by mistake.
    /// </remarks>
    [HttpPost]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult<ConcernReceiptDto>> Report([FromBody] ReportConcernDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.ReportAsync(dto));
    }

    /// <summary>
    /// The reporter's own view of their report, unlocked by their retrieval code.
    /// </summary>
    /// <remarks>
    /// ⚠ Answers the same refusal whether the number or the code is wrong — otherwise this endpoint
    /// would confirm whether a given concern number exists, which is a slow but perfectly good way
    /// to discover that somebody reported something.
    /// </remarks>
    [HttpPost("track")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> Track([FromBody] TrackConcernDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.TrackAsync(dto));
    }

    /// <summary>The reporter adds to their own thread, still without giving a name.</summary>
    [HttpPost("track/updates")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> AddReporterUpdate(
        [FromBody] AddConcernUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AddReporterUpdateAsync(dto));
    }

    // =========================================================================
    // HR's side — gated, and attributed
    // =========================================================================

    /// <summary>The triage queue.</summary>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmployeeRelationsConcernDto>>> GetAll(
        [FromQuery] ConcernStatus? status = null)
        => Ok(await _service.GetAllAsync(status));

    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    /// <summary>Records what HR made of it, and where that leaves it.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/triage")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> Triage(
        Guid id, [FromBody] TriageConcernDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.TriageAsync(id, dto, employeeId));
    }

    /// <summary>
    /// HR replies on the thread — the channel that makes anonymous intake useful rather than a
    /// suggestion box, because the commonest outcome is that HR needs one more detail.
    /// </summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> Reply(
        Guid id, [FromBody] ReplyToConcernDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.ReplyAsync(id, dto, employeeId));
    }

    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> Close(
        Guid id, [FromBody] CloseConcernDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.CloseAsync(id, dto, employeeId));
    }

    /// <summary>
    /// Converts a concern into a named employee-relations case.
    /// </summary>
    /// <remarks>
    /// ⚠ Never into a grievance — that would be HR raising one on somebody's behalf by a longer
    /// route. And the new case carries the concern's subject and statement only: <b>not the
    /// thread</b>, which may hold things the reporter said precisely because they were anonymous and
    /// which would become readable by the case's primary party.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult<EmployeeRelationsConcernDto>> Convert(
        Guid id, [FromBody] ConvertConcernDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.ConvertAsync(id, dto, employeeId));
    }
}
