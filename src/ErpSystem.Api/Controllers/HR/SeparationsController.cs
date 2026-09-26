using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
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
/// The exit register (area 9b) — FRD §A1.10 <i>Separation, Clearance &amp; Exit</i> and §3.A.2
/// <i>Separation &amp; Final Settlement</i>.
/// </summary>
/// <remarks>
/// <para>One record per exit, whatever the route. Before this controller the only way out of the
/// organisation was a disciplinary case, so resignation, retirement, contract expiry and death had
/// enum members and no route. The disciplinary route writes here too — area 9 keeps owning the
/// decision and the hearing; the exit itself lives here, so the register, the clearance run and the
/// settlement are the same for a dismissal as for a resignation.</para>
///
/// <para><b>Every action carries its own gate.</b> There is no class-level <c>[Authorize]</c>,
/// because later slices must be able to open individual actions to the MD (FR-HR-092) and to
/// Internal Audit (FR-HR-185), and stacked <c>[Authorize]</c> attributes are ANDed — a class-level
/// role gate would keep applying however the method were marked. The cost of that choice is that a
/// new endpoint added here with no attribute is an <b>open</b> endpoint. Add the gate first.</para>
///
/// <para><b>There is deliberately no by-number lookup.</b> Separation numbers are sequential, so a
/// by-number endpoint would let anyone walk the range and harvest who is leaving — which is exactly
/// what an exit register must not leak. The number is a display and search field; lookups are by
/// id.</para>
/// </remarks>
[ApiController]
[Route("api/hr/separations")]
[Authorize(Policy = "InternalOnly")] // W3 backstop: ANDed with the per-action gates below
public class SeparationsController : ControllerBase
{
    private readonly ISeparationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ISeparationReminderService _reminders;
    private readonly ILogger<SeparationsController> _logger;

    public SeparationsController(
        ISeparationService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ISeparationReminderService reminders,
        ILogger<SeparationsController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _reminders = reminders;
        _logger = logger;
    }

    /// <summary>
    /// The acting employee, or null when the caller's login is not linked to an employee record.
    /// </summary>
    /// <remarks>
    /// Null rather than a throw: an HR officer whose user account has no employee link must still
    /// be able to raise a separation for somebody else, and recording <c>Guid.Empty</c> as the
    /// initiator would be a lie the audit trail cannot tell apart from a real one. The service
    /// distinguishes "nobody was named" from "the system raised it" via
    /// <c>IsSystemInitiated</c>.
    /// </remarks>
    private Guid? ActorEmployeeId() => _currentUser.EmployeeId;

    private ActionResult ToClientError(Exception ex) => ex switch
    {
        ArgumentException => NotFound(new { message = ex.Message }),
        InvalidOperationException => BadRequest(new { message = ex.Message }),
        _ => BadRequest(new { message = ex.Message }),
    };

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>The exit register, filtered and paged.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeSeparationListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeSeparationListDto>>> GetPaged(
        [FromQuery] EmployeeSeparationQueryDto query, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(query, cancellationToken));

    /// <summary>One separation in full.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>Every separation on record for one employee, newest first.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSeparationListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSeparationListDto>>> GetForEmployee(
        Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest(new { message = "Invalid employee id." });
        return Ok(await _service.GetForEmployeeAsync(employeeId, cancellationToken));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Raise a separation. It starts as a draft; nothing is decided by creating it.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Create(
        [FromBody] CreateEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null) return BadRequest(new { message = "A separation payload is required." });

        try
        {
            var created = await _service.CreateAsync(dto, ActorEmployeeId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Amend a draft separation.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Update(
        Guid id, [FromBody] UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Name the medical board a medical retirement rests on, or clear it.</summary>
    /// <remarks>
    /// <para>Round 5, lane K-II-a: the control for a column nothing could set. Draft only; the board's
    /// case about this employee must be decided and recommend medical retirement. Submission then
    /// accepts it in place of the medical report.</para>
    ///
    /// <para>⚠ An empty body means unlink — the frontend's <c>apiService.put</c> drops a null body,
    /// so <c>EmptyBodyBehavior.Allow</c> is what lets the Unlink button work (leave's link endpoint
    /// carries the full story).</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("{id:guid}/medical-board")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> LinkMedicalBoard(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] Guid? medicalBoardId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.LinkMedicalBoardAsync(id, medicalBoardId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Submit a draft into the approval queue, deriving the dates that follow from the notice
    /// already recorded. After this the notice facts are fixed — the settlement is computed from
    /// them.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Submit(
        Guid id, [FromBody] SubmitEmployeeSeparationDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.SubmitAsync(
                id, dto ?? new SubmitEmployeeSeparationDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── FR-HR-092: the decision ───────────────────────────────────────────────

    /// <summary>
    /// Sign off a separation awaiting approval, settling any unserved notice (FR-HR-092).
    /// </summary>
    /// <remarks>
    /// ⚠ <b>A plain <c>[Authorize]</c>, and that is deliberate.</b> Who may decide depends on the
    /// record: the Managing Director may sign any separation, HR only a procedural one — a
    /// termination for absence beyond the tenant's threshold. No permission can express "may
    /// approve this one but not that one", and stacking a role attribute onto a policy attribute
    /// would AND them and admit nobody. The service reads entitlement off the record and answers
    /// 403 with the reason.
    /// </remarks>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Approve(
        Guid id, [FromBody] ApproveEmployeeSeparationDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.ApproveAsync(
                id, dto ?? new ApproveEmployeeSeparationDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Record what happens to any unserved notice — waived, paid in lieu, or neither.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Plain <c>[Authorize]</c>, like approve and reject, and for the same reason:</b> the
    /// entitlement depends on the RECORD. Whoever may sign this separation may settle its notice,
    /// and that is the MD for most exits and HR for a procedural one. No attribute can express
    /// "may decide this one but not that one", so the service reads it off the record.
    ///
    /// <para>Separate from approval because approval now runs on the generic workflow engine, whose
    /// approve action carries a comment and nothing else. A decision that changes what the leaver
    /// is paid does not belong in a comment.</para>
    /// </remarks>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("{id:guid}/notice-decision")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> RecordNoticeDecision(
        Guid id, [FromBody] RecordSeparationNoticeDecisionDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.RecordNoticeDecisionAsync(
                id, dto ?? new RecordSeparationNoticeDecisionDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Refuse a separation awaiting approval. Same entitlement rule as approving.</summary>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Reject(
        Guid id, [FromBody] RejectEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A reason is required to refuse a separation." });

        try
        {
            return Ok(await _service.RejectAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Complete the separation and apply it to the employee's master record.
    /// </summary>
    /// <remarks>
    /// ⚠ The step whose absence is the reason this area exists. Measured 2026-08-20: <b>29
    /// disciplinary terminations whose employees were all still <c>StaffStatus = Active</c></b> —
    /// the outcome was recorded somewhere nobody read, so dismissed people stayed in headcount, in
    /// the establishment counts area 17/18 made load-bearing, and on every roster. Recording an exit
    /// and applying it are two different acts, and only one of them had ever been built.
    ///
    /// <para>Available only from <c>SettlementApproved</c>: the employee record follows the payment,
    /// and payment follows Internal Audit's review.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> CompleteSeparation(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.CompleteSeparationAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Find disciplinary terminations that never produced an exit, and raise the missing
    /// separations.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>The repair for the defect this area was opened on.</b> Measured 2026-08-20: 29
    /// disciplinary terminations whose employees were all still <c>StaffStatus = Active</c>, because
    /// area 9 recorded the decision and nothing carried it into an exit.</para>
    ///
    /// <para><b>It raises separations; it does not terminate anybody.</b> Each of those exits still
    /// goes through clearance, the MD's signature and the settlement review like any other — a
    /// repair that skipped the controls would be a worse defect than the gap it closed.</para>
    ///
    /// <para>Run it with <c>dryRun=true</c> first: it reports who would be affected and writes
    /// nothing. Administration, because it writes across other people's records in bulk.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPost("repair/disciplinary-orphans")]
    [ProducesResponseType(typeof(DisciplinaryOrphanRepairDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DisciplinaryOrphanRepairDto>> RepairDisciplinaryOrphans(
        [FromQuery] bool dryRun = true, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.RepairDisciplinaryOrphansAsync(dryRun, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── The reminder sweep (FR-HR-111) ────────────────────────────────────────

    /// <summary>
    /// What the reminder sweep would raise, without writing anything.
    /// </summary>
    /// <remarks>
    /// Five kinds: a retirement or contract expiry approaching with no separation raised, a
    /// clearance with mandatory lines unanswered, a settlement with Internal Audit, and a settlement
    /// approved but never completed — the last being the defect this area was opened on, turned
    /// into a reminder rather than a discovery years later.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("reminders/preview")]
    [ProducesResponseType(typeof(IEnumerable<SeparationReminderItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeparationReminderItemDto>>> PreviewReminders(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reminders.PreviewAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Run the sweep now. Anything already raised at its current escalation tier is suppressed, so
    /// running it twice in a morning is harmless.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("reminders/run")]
    [ProducesResponseType(typeof(SeparationReminderRunResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeparationReminderRunResultDto>> RunReminderSweep(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reminders.RunSweepAsync("Manual", cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Retirement (FR-HR-093) ────────────────────────────────────────────────

    /// <summary>
    /// Who is reaching the compulsory retirement age, and who is already past it and still on
    /// strength (FR-HR-093).
    /// </summary>
    /// <remarks>
    /// ⚠ On the live tenant this answers with **nothing**, and that is correct: employee ages run
    /// 34 to 48, so nobody is within twelve years of retiring. An empty list here is a fact about
    /// the data, not a broken query — do not "fix" it.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("retirements/upcoming")]
    [ProducesResponseType(typeof(IEnumerable<UpcomingRetirementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UpcomingRetirementDto>>> GetUpcomingRetirements(
        [FromQuery] int? withinDays, [FromQuery] bool includeOverdue = true, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetUpcomingRetirementsAsync(withinDays, includeOverdue, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Raise a compulsory-retirement separation for everyone due within the horizon who does not
    /// already have one.
    /// </summary>
    /// <remarks>
    /// System-initiated: the separations it raises carry no initiator, because a birthday arriving
    /// is nobody's act. One employee's missing date of birth is reported and skipped rather than
    /// stopping the sweep for everybody else.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("retirements/sweep")]
    [ProducesResponseType(typeof(RetirementSweepResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RetirementSweepResultDto>> RunRetirementSweep(
        [FromQuery] int? withinDays, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.RunRetirementSweepAsync(withinDays, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── The exit interview ────────────────────────────────────────────────────

    /// <summary>The exit interview for this separation, or 204 where none has been recorded.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/exit-interview")]
    [ProducesResponseType(typeof(SeparationExitInterviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationExitInterviewDto>> GetExitInterview(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            var interview = await _service.GetExitInterviewAsync(id, cancellationToken);
            // 204, not 404: the separation exists and simply has no interview yet. A 404 here would
            // say the separation was missing, which is a different and more alarming thing.
            return interview is null ? NoContent() : Ok(interview);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Record or amend the exit interview.
    /// </summary>
    /// <remarks>
    /// One endpoint for both: an interview is taken once and corrected, not created twice.
    /// ⚠ Marking it declined clears every answer, so a part-filled form cannot leave ratings behind
    /// to be averaged later as though a real interview had produced them.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("{id:guid}/exit-interview")]
    [ProducesResponseType(typeof(SeparationExitInterviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationExitInterviewDto>> RecordExitInterview(
        Guid id, [FromBody] RecordExitInterviewDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "An exit interview payload is required." });

        try
        {
            return Ok(await _service.RecordExitInterviewAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Exit analytics ────────────────────────────────────────────────────────

    /// <summary>Exits, why they happened, and where the ones in flight are stuck.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("analytics")]
    [ProducesResponseType(typeof(SeparationAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeparationAnalyticsDto>> GetAnalytics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetAnalyticsAsync(from, to, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// What the exit interviews say — coverage, decline rate, the reasons people gave, and the
    /// average ratings.
    /// </summary>
    /// <remarks>
    /// ⚠ Read the coverage and decline rate before the themes. Averages over a handful of interviews
    /// out of many exits describe those few people, not the organisation — and a programme most
    /// leavers decline is telling you something before a single answer is read.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("analytics/exit-interviews")]
    [ProducesResponseType(typeof(ExitInterviewThemesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExitInterviewThemesDto>> GetExitInterviewThemes(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetExitInterviewThemesAsync(from, to, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Contract expiry ───────────────────────────────────────────────────────

    /// <summary>
    /// Whose contract is running out, and whose has already run out while they are still on
    /// strength.
    /// </summary>
    /// <remarks>
    /// ⚠ Empty on live data, and correctly so: 0 of 202 active contracts carry an end date. The
    /// same shape as the retirement queue — the rule is real, the subjects are not there yet.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("contract-expiries/upcoming")]
    [ProducesResponseType(typeof(IEnumerable<UpcomingContractExpiryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UpcomingContractExpiryDto>>> GetUpcomingContractExpiries(
        [FromQuery] int? withinDays, [FromQuery] bool includeOverdue = true, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetUpcomingContractExpiriesAsync(withinDays, includeOverdue, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Raise a contract-expiry separation for everyone due who does not already have one.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("contract-expiries/sweep")]
    [ProducesResponseType(typeof(ContractExpirySweepResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractExpirySweepResultDto>> RunContractExpirySweep(
        [FromQuery] int? withinDays, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.RunContractExpirySweepAsync(withinDays, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Final settlement (FR-HR-184) ──────────────────────────────────────────

    /// <summary>
    /// Build the settlement statement. Refused before clearance is complete — FR-HR-091 puts the
    /// clearance form ahead of computing entitlements.
    /// </summary>
    /// <remarks>
    /// Lines the system cannot value carry <b>no amount</b> rather than zero, and hold the
    /// statement open until somebody supplies the figure and names its source.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/settlement/prepare")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> PrepareSettlement(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.PrepareSettlementAsync(id, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>The settlement statement, its totals, and whether it can be closed for review.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/settlement")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> GetSettlement(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.GetSettlementAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Add a line by hand — the FR-HR-184 items the system cannot work out for itself.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/settlement/lines")]
    [ProducesResponseType(typeof(SeparationSettlementLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementLineDto>> AddSettlementLine(
        Guid id, [FromBody] AddSettlementLineDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A settlement line is required." });

        try
        {
            return Ok(await _service.AddSettlementLineAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Amend a line while the statement is a draft — including supplying an amount the system could
    /// not compute, which is what releases the block on finalising.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("settlement-lines/{lineId:guid}")]
    [ProducesResponseType(typeof(SeparationSettlementLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementLineDto>> UpdateSettlementLine(
        Guid lineId, [FromBody] UpdateSettlementLineDto dto, CancellationToken cancellationToken)
    {
        if (lineId == Guid.Empty) return BadRequest(new { message = "Invalid settlement line id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateSettlementLineAsync(lineId, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Remove a line while the statement is a draft.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpDelete("settlement-lines/{lineId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSettlementLine(Guid lineId, CancellationToken cancellationToken)
    {
        if (lineId == Guid.Empty) return BadRequest(new { message = "Invalid settlement line id." });

        try
        {
            await _service.DeleteSettlementLineAsync(lineId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Close the statement for Internal Audit's review (FR-HR-185). Refused while any line could not
    /// be valued — a settlement is not finalised with an unknown amount showing as zero.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/settlement/finalise")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> FinaliseSettlement(
        Guid id, [FromBody] FinaliseSettlementDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.FinaliseSettlementAsync(
                id, dto ?? new FinaliseSettlementDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── FR-HR-185: Internal Audit's review ────────────────────────────────────

    /// <summary>
    /// Internal Audit passes the settlement. Payment may be released after this, and not before.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>A role gate, not a permission, and not the record.</b> Unlike the MD's signature —
    /// where who may sign depends on whether the separation is procedural — Internal Audit reviews
    /// <i>every</i> settlement. The entitled party is a job title, so it is stated as a role.
    /// SuperAdmin and TenantAdmin are excluded on the same reasoning as the MD: a technical
    /// superuser passing a financial control is exactly what the control exists to prevent.</para>
    ///
    /// <para>⚠ <b>Nobody holds this role on the live tenant</b> (measured 2026-08-20). Until it is
    /// granted, every settlement will sit unreviewed and unpaid — the control holding rather than
    /// failing open, which is correct but will look like a stuck queue. Raised with TDC as an
    /// operational prerequisite.</para>
    /// </remarks>
    [Authorize(Roles = Constants.Roles.InternalAudit)]
    [HttpPost("{id:guid}/settlement/review/approve")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> ApproveSettlementReview(
        Guid id, [FromBody] ReviewSettlementDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.ApproveSettlementReviewAsync(
                id, dto ?? new ReviewSettlementDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Internal Audit returns the settlement with findings. It becomes editable again and must be
    /// corrected and re-finalised.
    /// </summary>
    [Authorize(Roles = Constants.Roles.InternalAudit)]
    [HttpPost("{id:guid}/settlement/review/return")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> ReturnSettlement(
        Guid id, [FromBody] ReviewSettlementDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "Findings are required when returning a settlement." });

        try
        {
            return Ok(await _service.ReturnSettlementAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Clearance: the tenant's form ──────────────────────────────────────────

    /// <summary>The clearance form — the catalogue every separation's checklist is built from.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("clearance-templates")]
    [ProducesResponseType(typeof(IEnumerable<SeparationClearanceTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeparationClearanceTemplateDto>>> GetClearanceTemplates(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken)
        => Ok(await _service.GetClearanceTemplatesAsync(includeInactive, cancellationToken));

    /// <summary>Add a line to the clearance form. Configuration, so administration.</summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPost("clearance-templates")]
    [ProducesResponseType(typeof(SeparationClearanceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeparationClearanceTemplateDto>> CreateClearanceTemplate(
        [FromBody] CreateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken)
    {
        if (dto == null) return BadRequest(new { message = "A clearance line is required." });

        try
        {
            return Ok(await _service.CreateClearanceTemplateAsync(dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Amend a line of the clearance form.</summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPut("clearance-templates/{id:guid}")]
    [ProducesResponseType(typeof(SeparationClearanceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceTemplateDto>> UpdateClearanceTemplate(
        Guid id, [FromBody] UpdateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid clearance line id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateClearanceTemplateAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Remove a line from the clearance form. Forms already issued keep their copy — items snapshot
    /// their template and hold no key to it — so this affects future clearances only. Retiring the
    /// line (<c>isActive: false</c>) is usually the better move.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpDelete("clearance-templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClearanceTemplate(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid clearance line id." });

        try
        {
            await _service.DeleteClearanceTemplateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Create FR-HR-183's seven default lines — outstanding loans, salary advances, company
    /// property, office equipment, duty-post keys, documents and records, payroll recoveries.
    /// Skips any that already exist by name, so it is safe to run twice.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPost("clearance-templates/seed-defaults")]
    [ProducesResponseType(typeof(IEnumerable<SeparationClearanceTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeparationClearanceTemplateDto>>> SeedDefaultClearanceTemplates(
        CancellationToken cancellationToken)
        => Ok(await _service.SeedDefaultClearanceTemplatesAsync(cancellationToken));

    // ── Clearance: one separation's form ──────────────────────────────────────

    /// <summary>Build this separation's clearance form from the active catalogue.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/clearance/start")]
    [ProducesResponseType(typeof(SeparationClearanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceDto>> StartClearance(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.StartClearanceAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Re-read the HR Assets register onto an in-progress form — FR-HR-183, area 16 slice 10.
    /// </summary>
    /// <remarks>
    /// Adds a line for anything issued to the leaver since the form was drawn and reprices the
    /// lines nobody has answered yet. Answered lines are never touched.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/clearance/refresh-assets")]
    [ProducesResponseType(typeof(SeparationClearanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceDto>> RefreshClearanceAssets(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.RefreshClearanceAssetsAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>This separation's clearance form, its totals, and whether the gate can open.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/clearance")]
    [ProducesResponseType(typeof(SeparationClearanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceDto>> GetClearance(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.GetClearanceAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Record one line's answer, and who in the owning unit gave it.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("clearance-items/{itemId:guid}")]
    [ProducesResponseType(typeof(SeparationClearanceItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceItemDto>> RecordClearanceItem(
        Guid itemId, [FromBody] RecordClearanceItemDto dto, CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty) return BadRequest(new { message = "Invalid clearance item id." });
        if (dto == null) return BadRequest(new { message = "An answer is required." });

        try
        {
            return Ok(await _service.RecordClearanceItemAsync(itemId, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Close the clearance — FR-HR-091's gate. Refused while any mandatory line is still pending or
    /// blocked, because entitlements are computed only after the form is complete.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/clearance/complete")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> CompleteClearance(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.CompleteClearanceAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    /// <summary>Every file attached to a separation, newest first.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSeparationDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeSeparationDocumentDto>>> GetDocuments(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.GetDocumentsAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Attach a file — the resignation letter, a medical report, later the signed clearance form
    /// and the settlement statement.
    /// </summary>
    /// <remarks>
    /// Goes through the controlled-upload gate, which scans it and registers it in the central
    /// repository. Entitlement is checked before storage, so an id that is not this tenant's never
    /// reaches the scanner.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(EmployeeSeparationDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachDocument(
        Guid id,
        IFormFile? file,
        [FromForm] SeparationDocumentCategory category = SeparationDocumentCategory.Other,
        [FromForm] string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        // Entitlement first, storage second: an id that is not ours must 404 before a file is
        // written and scanned.
        var separation = await _service.GetByIdAsync(id, cancellationToken);
        if (separation is null) return NotFound(new { message = $"Separation '{id}' was not found." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(EmployeeSeparation),
            sourceRecordId: id,
            sourceLabel: "Separation",
            documentType: category.ToString(),
            description: description,
            persist: (uploadedById, document) => _service.AttachDocumentAsync(
                id,
                category,
                document.OriginalFileName,
                document.FilePath,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId,
                description,
                uploadedById,
                cancellationToken),
            cancellationToken: cancellationToken,
            category: ControlledFileUploadCategories.HrSeparationDocuments);
    }

    /// <summary>Streams an attached file back, byte-for-byte.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty) return BadRequest(new { message = "Invalid document id." });
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest(new { message = "Tenant context could not be resolved." });

        var document = await _service.GetDocumentEntityAsync(documentId, cancellationToken);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId,
            legacyPath: document.FilePath,
            document.FileName,
            fallbackContentType: null,
            inline: false, cancellationToken);
    }

    /// <summary>
    /// Remove an attached file. Allowed only while the separation is a draft — after submission the
    /// attachments are part of what was approved.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpDelete("documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty) return BadRequest(new { message = "Invalid document id." });

        try
        {
            await _service.DeleteDocumentAsync(documentId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Withdraw a separation — a resignation retracted, a retirement deferred.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Cancel(
        Guid id, [FromBody] CancelEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A cancellation reason is required." });

        try
        {
            return Ok(await _service.CancelAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Delete a separation. Administration, not maintenance: a completed one is refused outright,
    /// because it is the record of somebody's exit and of what they were paid.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }
}
