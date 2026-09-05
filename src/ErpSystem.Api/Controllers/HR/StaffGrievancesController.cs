using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employee-relations cases: FR-HR-181's grievance and its escalation ladder, and — since area 9c —
/// the other case types an employee-relations function handles.
///
/// Gated per action, and the split is the opposite way round from the disciplinary case. A case is
/// raised ABOUT an employee, so it is HR's with a narrow window for the subject; a grievance is
/// raised BY one, so filing, escalating and withdrawing are the employee's and HR cannot do them at
/// all. What HR owns is answering, assigning, the parties, and seeing the whole register.
/// </summary>
/// <remarks>
/// <b>One route (area 9c decision D-6, completed in slice 12).</b> <c>api/hr/employee-relations</c>
/// is the correct name for a register that holds union consultations and mediations as well as
/// grievances. <c>api/grievances</c> was kept as an alias while the portal's four screens still
/// called it; slice 11 rewrote the last of them, a repo-wide grep found no consumer left anywhere,
/// and the alias was dropped here. A second route with no consumer is a second surface to secure
/// and a second thing to remember. <c>run-slice1.mjs</c> asserts it now 404s, because an alias that
/// merely stopped being mentioned in the tests is indistinguishable from one still serving traffic.
/// </remarks>
[ApiController]
[Route("api/hr/employee-relations")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class StaffGrievancesController : ControllerBase
{
    private readonly IStaffGrievanceService _service;
    private readonly IEmployeeRelationsAnalyticsService _analytics;
    private readonly ICurrentUserService _currentUser;

    // Area 9c slice 3 — the controlled upload gate and its counterpart for reading a file back.
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<StaffGrievancesController> _logger;

    public StaffGrievancesController(
        IStaffGrievanceService service,
        IEmployeeRelationsAnalyticsService analytics,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ILogger<StaffGrievancesController> logger)
    {
        _service = service;
        _analytics = analytics;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _logger = logger;
    }

    // =========================================================================
    // HR's register
    // =========================================================================

    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetByStatus(GrievanceStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    /// <summary>
    /// The employee-relations register — paged, filtered, and the read the desk screens use.
    /// </summary>
    /// <remarks>
    /// Prefer this to the unpaged <c>GET</c> above, which materialises every case in the tenant
    /// along with all of its steps and parties. <paramref name="pageSize"/> is capped at 200 by the
    /// service, so a caller cannot ask for the whole register by asking for a big enough page.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("register")]
    public async Task<ActionResult<PagedResult<StaffGrievanceSummaryDto>>> GetRegister(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] EmployeeRelationsCaseType? caseType = null,
        [FromQuery] GrievanceStatus? status = null,
        [FromQuery] GrievanceEscalationLevel? level = null,
        [FromQuery] Guid? organizationUnitId = null,
        [FromQuery] bool? awaitingResponseOnly = null,
        [FromQuery] string? search = null)
        => Ok(await _service.GetPagedAsync(
            page, pageSize, caseType, status, level, organizationUnitId, awaitingResponseOnly, search));

    /// <summary>
    /// Employee-relations analytics — area 9c slice 8.
    /// </summary>
    /// <remarks>
    /// <para>One endpoint for the whole page, on purpose. Every figure is computed from a single
    /// materialised set, so the page cannot contradict itself — and a caller cannot build a screen
    /// out of figures taken over different windows, which is how area 7's dashboard and analytics
    /// page came to disagree about the same number.</para>
    ///
    /// <para>⚠ Every breakdown carries its own denominator and every rate carries both its numbers,
    /// with a <b>null</b> percentage where the denominator is zero. "Nobody complied" and "nobody
    /// was asked" must not both render as 0%.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("analytics")]
    public async Task<ActionResult<EmployeeRelationsAnalyticsDto>> GetAnalytics(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
        => Ok(await _analytics.GetAsync(from, to, cancellationToken));

    /// <summary>Where the ladder is stuck — grievances whose current rung has not answered.</summary>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("awaiting-response")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAwaitingResponse(
        [FromQuery] GrievanceEscalationLevel? level = null)
        => Ok(await _service.GetAwaitingResponseAsync(level));

    // =========================================================================
    // The employee's own surface
    // =========================================================================

    /// <summary>The caller's own grievances. Token-derived, with no id-bearing equivalent.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetMine()
    {
        if (_currentUser.EmployeeId is not Guid employeeId) return Forbid();
        return Ok(await _service.GetMineAsync(employeeId));
    }

    /// <summary>
    /// Grievances this caller has been asked to answer. Open by design: the person answering at the
    /// supervisor or HOD rung is not in HR, and the register above refuses them — so without this
    /// they would have no way to see what they owe.
    /// </summary>
    [HttpGet("awaiting-my-response")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAwaitingMyResponse()
    {
        if (_currentUser.EmployeeId is not Guid employeeId) return Forbid();
        return Ok(await _service.GetAwaitingMyResponseAsync(employeeId));
    }

    /// <summary>
    /// One grievance. The service refuses anyone but the griever, HR, or someone named on a step —
    /// a grievance is usually about somebody, so the read surface is narrow on purpose.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffGrievanceDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id, _currentUser.EmployeeId));

    /// <summary>
    /// Raises a grievance. Deliberately ungated: this is the employee's own act, and HR cannot do it
    /// for them — there is no employee id on the payload to allow it.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StaffGrievanceDto>> File([FromBody] FileGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.FileAsync(dto, employeeId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Escalates to the next rung. The griever's act — the service refuses everyone else.</summary>
    [HttpPost("{id:guid}/escalate")]
    public async Task<ActionResult<StaffGrievanceDto>> Escalate(Guid id, [FromBody] EscalateGrievanceDto? dto = null)
    {
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.EscalateAsync(id, dto ?? new EscalateGrievanceDto(), employeeId));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<StaffGrievanceDto>> Withdraw(Guid id, [FromBody] WithdrawGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.WithdrawAsync(id, dto, employeeId));
    }

    // =========================================================================
    // Answering
    // =========================================================================

    /// <summary>Names who should answer at the current rung — how a supervisor or HOD is brought in.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<StaffGrievanceDto>> Assign(Guid id, [FromBody] AssignGrievanceStepDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AssignCurrentStepAsync(id, dto));
    }

    /// <summary>
    /// Answers at the current rung. Ungated here because the responder is often not in HR — the
    /// service allows HR or whoever the step names, and refuses everyone else including the griever.
    /// </summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<ActionResult<StaffGrievanceDto>> Respond(Guid id, [FromBody] RespondToGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.RespondAsync(id, dto, employeeId));
    }

    // =========================================================================
    // The wider register — area 9c slice 1
    // =========================================================================

    /// <summary>
    /// Opens an employee-relations case that is not a grievance — a mediation, a welfare matter,
    /// a union consultation.
    /// </summary>
    /// <remarks>
    /// HR-gated, and the mirror image of <see cref="File"/> above: this one takes an explicit
    /// employee id because the desk opens the case ABOUT somebody. The service refuses
    /// <c>CaseType.Grievance</c> here, so this cannot become a raise-on-behalf-of.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("cases")]
    public async Task<ActionResult<StaffGrievanceDto>> OpenCase([FromBody] OpenEmployeeRelationsCaseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.OpenCaseAsync(dto, employeeId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Adds somebody to a case — respondent, representative, union official, witness, mediator.
    /// </summary>
    /// <remarks>
    /// ⚠ Adding a party does NOT give them the right to read the case. The read rule stays what
    /// area 9 slice 7 set: the primary party, HR, or somebody named on a STEP. A respondent must
    /// not receive the complainant's statement by being recorded as a respondent.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/parties")]
    public async Task<ActionResult<StaffGrievanceDto>> AddParty(Guid id, [FromBody] AddGrievancePartyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.AddPartyAsync(id, dto, employeeId));
    }

    /// <summary>Stands a party down. Not a delete — the case file must still read correctly.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/parties/{partyId:guid}/remove")]
    public async Task<ActionResult<StaffGrievanceDto>> RemoveParty(
        Guid id, Guid partyId, [FromBody] RemoveGrievancePartyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.RemovePartyAsync(id, partyId, dto));
    }

    // =========================================================================
    // FR-HR-181's artefacts — area 9c slice 2
    // =========================================================================

    /// <summary>
    /// Obligation 5 — HR's formal reading of the case, which the rungs above read on the way up.
    /// </summary>
    /// <remarks>
    /// Distinct from the HR rung's step response: that is HR answering the employee, this is HR's
    /// position on the merits. Amendable while the case is open; frozen once it is not.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/hr-interpretation")]
    public async Task<ActionResult<StaffGrievanceDto>> RecordHrInterpretation(
        Guid id, [FromBody] RecordHrInterpretationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.RecordHrInterpretationAsync(id, dto, employeeId));
    }

    /// <summary>Obligation 7 — opens the investigation. One per case.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/investigation")]
    public async Task<ActionResult<StaffGrievanceDto>> OpenInvestigation(
        Guid id, [FromBody] OpenGrievanceInvestigationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.OpenInvestigationAsync(id, dto, employeeId));
    }

    /// <summary>Updates an investigation that is still open. A null field means "leave alone".</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPut("{id:guid}/investigation")]
    public async Task<ActionResult<StaffGrievanceDto>> UpdateInvestigation(
        Guid id, [FromBody] UpdateGrievanceInvestigationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateInvestigationAsync(id, dto));
    }

    /// <summary>Concludes the investigation. Refused without findings.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/investigation/complete")]
    public async Task<ActionResult<StaffGrievanceDto>> CompleteInvestigation(
        Guid id, [FromBody] CompleteGrievanceInvestigationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.CompleteInvestigationAsync(id, dto));
    }

    /// <summary>
    /// Obligation 8 — resolves the case by recording what was decided.
    /// </summary>
    /// <remarks>
    /// Ungated here for the same reason <see cref="Respond"/> is: whoever decides at the supervisor
    /// or HOD rung is not in HR. The service allows HR or whoever the step names, and refuses
    /// everyone else including the griever — you cannot decide your own case.
    ///
    /// <para>This is the act <c>respond(resolvesGrievance: true)</c> used to stand in for. That path
    /// still works and still resolves, but produces an outcome of <c>NotRecorded</c>; calling this
    /// afterwards fills that in, and is the only change a recorded decision ever accepts.</para>
    /// </remarks>
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<StaffGrievanceDto>> Resolve(Guid id, [FromBody] ResolveGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.ResolveAsync(id, dto, employeeId));
    }

    /// <summary>
    /// Closes a case that reached the Board and was answered without being resolved.
    /// </summary>
    /// <remarks>
    /// ⚠ The first writer <c>GrievanceStatus.Closed</c> has ever had. Before slice 2 every one of
    /// its three references in the solution was a read filter, and a case in this position had no
    /// terminal state at all — it sat <c>UnderReview</c> for ever and the reminder sweep chased it
    /// for ever.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<StaffGrievanceDto>> Close(Guid id, [FromBody] CloseGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.CloseAsync(id, dto, employeeId));
    }

    // =========================================================================
    // Documents and the signed agreement — area 9c slice 3
    // =========================================================================

    /// <summary>
    /// Uploads a document to the case, through the controlled upload gate.
    /// </summary>
    /// <remarks>
    /// ⚠ Multipart, and there is deliberately no JSON alternative. A create endpoint taking
    /// <c>fileName</c> and <c>filePath</c> as JSON stores no file at all — the "attachment" is a
    /// string somebody typed and the list renders it beautifully — and that is the shape area 16
    /// had to replace wholesale. Nothing on this controller accepts a caller-supplied path.
    ///
    /// <para>Scope <c>Agreement</c> is FR-HR-181 obligation 9. It needs a recorded decision to be
    /// the written form of, and there may be only one per case.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        IFormFile file,
        [FromForm] GrievanceDocumentScope scope = GrievanceDocumentScope.Case,
        [FromForm] Guid? stepId = null,
        [FromForm] Guid? conferenceId = null,
        [FromForm] string? description = null,
        [FromForm] DateTime? agreementSignedDate = null,
        CancellationToken cancellationToken = default)
    {
        // ⚠ Checked BEFORE the file goes anywhere near the gate, and this is not premature
        // optimisation. HrAttachmentUpload.ExecuteAsync catches everything its persist callback
        // throws — it has to, so a scanned and DMS-registered document is never left pointing at a
        // row that was never written — and answers a generic 500. Every placement rule raised
        // inside the callback therefore reached the caller as "An error occurred while adding the
        // attachment", with no status and no message: 12 assertions' worth, all of them rules that
        // were working correctly and could not say so. Validating first also means a refused
        // placement never stores a file, never scans one, and never needs rolling back.
        await _service.ValidateDocumentPlacementAsync(id, scope, stepId, conferenceId, cancellationToken);

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "StaffGrievance",
            sourceRecordId: id,
            sourceLabel: "Employee-relations case document",
            documentType: $"Grievance{scope}Document",
            description: description,
            persist: (uploadedById, document) => _service.AddDocumentAsync(
                id, scope, stepId, conferenceId, description, agreementSignedDate, uploadedById,
                document.OriginalFileName,
                document.FilePath,
                document.FileSize,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId,
                cancellationToken),
            cancellationToken,
            category: ControlledFileUploadCategories.HrGrievanceDocuments);
    }

    /// <summary>
    /// Streams a case document.
    /// </summary>
    /// <remarks>
    /// ⚠ Ungated at the attribute level ON PURPOSE, and gated hard in the service instead: the
    /// employee whose case it is must be able to read their own agreement, and they are not in HR.
    /// The service applies the case's own read rule — griever, HR, or somebody named on a step —
    /// before this ever streams a byte. The entitlement check is never the download helper's.
    ///
    /// <para>This endpoint is the only route to the file: it lives outside the web root and reading
    /// it needs the bearer token, so <c>filePath</c> on the DTO can never be used as an href.</para>
    /// </remarks>
    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var document = await _service.GetDocumentAsync(documentId, _currentUser.EmployeeId, cancellationToken);

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId, document.FileUploadRecordId,
            document.FilePath, document.FileName, fallbackContentType: null,
            inline: true, cancellationToken);
    }

    /// <summary>Removes a document from the case. Refused once the employee has accepted an agreement.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<ActionResult<StaffGrievanceDto>> DeleteDocument(Guid id, Guid documentId)
        => Ok(await _service.DeleteDocumentAsync(id, documentId));

    // =========================================================================
    // Conferencing, mediation and union consultation — area 9c slice 4
    // =========================================================================

    /// <summary>
    /// Convenes a meeting on the case — a case conference, a mediation, or FR-HR-181 obligation 6's
    /// union consultation.
    /// </summary>
    /// <remarks>
    /// A union consultation must name the union it consulted; nothing else may name one. That is
    /// the requirement's sixth obligation, and a row that does not say which union retains nothing
    /// it asks for.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/conferences")]
    public async Task<ActionResult<StaffGrievanceDto>> ScheduleConference(
        Guid id, [FromBody] ScheduleGrievanceConferenceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.ScheduleConferenceAsync(id, dto, employeeId));
    }

    /// <summary>Amends a meeting that has not happened yet. A null field means "leave alone".</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPut("{id:guid}/conferences/{conferenceId:guid}")]
    public async Task<ActionResult<StaffGrievanceDto>> UpdateConference(
        Guid id, Guid conferenceId, [FromBody] UpdateGrievanceConferenceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateConferenceAsync(id, conferenceId, dto));
    }

    /// <summary>
    /// Records that the meeting happened — its outcome, its notes, and who came.
    /// </summary>
    /// <remarks>
    /// Ungated here because a mediator or an external chair writes this up and is not in HR; the
    /// service permits HR or whoever chairs it, and refuses everyone else.
    ///
    /// <para>⚠ The notes are <b>redacted on read</b> to HR and the chair. They record what the other
    /// party said in a room they were promised was private, and the case's read rule admits the
    /// complainant — so without that, filing a grievance would be a way to obtain the respondent's
    /// position verbatim.</para>
    /// </remarks>
    [HttpPost("{id:guid}/conferences/{conferenceId:guid}/hold")]
    public async Task<ActionResult<StaffGrievanceDto>> HoldConference(
        Guid id, Guid conferenceId, [FromBody] HoldGrievanceConferenceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.HoldConferenceAsync(id, conferenceId, dto, employeeId));
    }

    /// <summary>Cancels a meeting that has not happened.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/conferences/{conferenceId:guid}/cancel")]
    public async Task<ActionResult<StaffGrievanceDto>> CancelConference(
        Guid id, Guid conferenceId, [FromBody] CancelGrievanceConferenceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.CancelConferenceAsync(id, conferenceId, dto));
    }

    /// <summary>Adds somebody to a meeting. Internal or external — exactly one.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/conferences/{conferenceId:guid}/attendees")]
    public async Task<ActionResult<StaffGrievanceDto>> AddConferenceAttendee(
        Guid id, Guid conferenceId, [FromBody] AddConferenceAttendeeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AddConferenceAttendeeAsync(id, conferenceId, dto));
    }

    /// <summary>Removes somebody from a meeting that has not happened yet.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpDelete("{id:guid}/conferences/{conferenceId:guid}/attendees/{attendeeId:guid}")]
    public async Task<ActionResult<StaffGrievanceDto>> RemoveConferenceAttendee(
        Guid id, Guid conferenceId, Guid attendeeId)
        => Ok(await _service.RemoveConferenceAttendeeAsync(id, conferenceId, attendeeId));

    /// <summary>
    /// The employee confirms FR-HR-181's final signed agreement.
    /// </summary>
    /// <remarks>
    /// Ungated here and refused to everyone but the case's own employee — their act, like filing,
    /// escalating and withdrawing.
    ///
    /// <para><b>Decision D-10, judged rather than assumed.</b> This is deliberately NOT a workflow
    /// approval. The engine models a proposal that somebody with authority confirms or refuses, and
    /// routes onward when they refuse. Here, if the employee declines, nothing routes anywhere — the
    /// case simply is not settled and their remedy is the ladder they already have. Modelling it as
    /// an approval would also drop a decision about the employee's own case into a queue that
    /// somebody else can action, which this module refuses everywhere else.</para>
    /// </remarks>
    [HttpPost("{id:guid}/agreement/accept")]
    public async Task<ActionResult<StaffGrievanceDto>> AcceptAgreement(
        Guid id, [FromBody] AcceptGrievanceAgreementDto? dto = null)
    {
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.AcceptAgreementAsync(id, dto ?? new AcceptGrievanceAgreementDto(), employeeId));
    }

    // =========================================================================
    // Cross-links to other modules' records — area 9c slice 9
    // =========================================================================

    /// <summary>
    /// Cross-references this case to a safety incident, a PIP or a disciplinary case.
    /// </summary>
    /// <remarks>
    /// <b>HR's act, and HR's alone.</b> Neither the griever nor the responder may link: a
    /// cross-reference is the desk's reading of how two records relate, and letting the subject of a
    /// case attach other people's records to it is the leak this whole slice is shaped around.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/links")]
    public async Task<ActionResult<StaffGrievanceDto>> LinkSource(Guid id, [FromBody] LinkErCaseSourceDto dto)
        => Ok(await _service.LinkSourceAsync(id, dto));

    /// <summary>Removes one cross-reference. Nothing in the source record changes.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpDelete("{id:guid}/links/{source}")]
    public async Task<ActionResult<StaffGrievanceDto>> UnlinkSource(Guid id, EmployeeRelationsLinkSource source)
        => Ok(await _service.UnlinkSourceAsync(id, source));

    /// <summary>
    /// The reverse read: which employee-relations cases point at this record.
    /// </summary>
    /// <remarks>
    /// Gated on the employee-relations READ permission, not on the source module's — because the
    /// answer is about employee-relations cases, not about the incident. A caller who holds it can
    /// already see every one of these rows on the register; this endpoint only saves them filtering
    /// it by hand from the source module's screen.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("by-source/{source}/{recordId:guid}")]
    public async Task<ActionResult<IEnumerable<ErLinkedCaseDto>>> GetCasesForSource(
        EmployeeRelationsLinkSource source, Guid recordId)
        => Ok(await _service.GetCasesForSourceAsync(source, recordId));
}
