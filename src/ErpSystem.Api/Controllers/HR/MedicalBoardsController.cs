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
/// Medical boards — convening a panel, listing the cases before it, recording its sittings and who
/// attended, and each case's finding (residue plan G4 / R-15b; round 5, lanes K and K-II-a).
/// </summary>
/// <remarks>
/// <para>⚠ <b>Gated on <c>HR.Medical.*</c> throughout, including the reads.</b> A board's findings
/// describe somebody's health, and the SHE↔Medical boundary already settled that anything carrying
/// examination results, restrictions or clearance data takes the Medical policies rather than a role
/// gate.</para>
///
/// <para>⚠ <b>That does NOT mean the HR desk is shut out, and it would be wrong to read it that
/// way.</b> <c>HrStaffGrants</c> gives the HR role <c>ViewMedicalRecords</c> and
/// <c>MaintainMedicalRecords</c>, so HR can read and record boards — deliberately, because HR
/// administers the process even though clinicians decide it. What the Medical policies buy is that
/// entitlement is stated in one grant map rather than implied by a role check, so narrowing it later
/// is an edit to that map and nothing else. An ordinary employee holds neither grant and is refused.
/// Proved in both directions by hr-leave slice 8 [5].</para>
///
/// <para>⚠ <b>There is no endpoint here for acting on a finding.</b> Leave reads a case to satisfy its
/// evidence rule; separation reads one to justify a medical retirement. Both hold a bare
/// <c>MedicalBoardId</c> and read the case about their own employee; neither writes back. A board
/// records what a panel decided; what anybody does about it belongs to the module that acts.</para>
/// </remarks>
[ApiController]
[Route("api/hr/medical-boards")]
[Authorize(Policy = "InternalOnly")]
public class MedicalBoardsController : ControllerBase
{
    private readonly IMedicalBoardService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<MedicalBoardsController> _logger;

    public MedicalBoardsController(
        IMedicalBoardService service,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<MedicalBoardsController> logger)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>The board register, filtered and paged</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [ProducesResponseType(typeof(PagedResult<MedicalBoardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MedicalBoardDto>>> GetBoards(
        [FromQuery] Guid? employeeId,
        [FromQuery] MedicalBoardStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? search,
        [FromQuery] MedicalBoardPurpose? purpose = null,
        [FromQuery] MedicalBoardKind? kind = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var filter = new MedicalBoardFilterDto
        {
            EmployeeId = employeeId, Status = status, Purpose = purpose, Kind = kind, From = from, To = to, Search = search
        };
        return Ok(await _service.GetBoardsAsync(filter, pageNumber, pageSize, ct));
    }

    /// <summary>One board, with its cases, members and sittings</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [ProducesResponseType(typeof(MedicalBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicalBoardDto>> GetBoard(Guid id, CancellationToken ct = default)
    {
        var board = await _service.GetBoardAsync(id, ct);
        return board is null ? NotFound(new { message = $"Medical board '{id}' not found." }) : Ok(board);
    }

    /// <summary>Ask for a board, with its first case</summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [ProducesResponseType(typeof(MedicalBoardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MedicalBoardDto>> RequestBoard(
        [FromBody] RequestMedicalBoardDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () =>
        {
            var board = await _service.RequestBoardAsync(dto, ct);
            return CreatedAtAction(nameof(GetBoard), new { id = board.Id }, board);
        });

    /// <summary>Another employee before the board</summary>
    /// <remarks>⚠ Refused for somebody already before it or sitting on it, and once it has reported or been stopped.</remarks>
    [HttpPost("{id:guid}/cases")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardCaseDto>> AddCase(
        Guid id, [FromBody] AddMedicalBoardCaseDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardCaseDto>(async () => Ok(await _service.AddCaseAsync(id, dto, ct)));

    /// <summary>A case decided, at a sitting</summary>
    /// <remarks>
    /// ⚠ The only state leave and separation act on, and it cannot be undone. The sitting's attendance
    /// must hold the quorum of deciding members. The board reports when its last open case closes.
    /// </remarks>
    [HttpPut("{id:guid}/cases/{caseId:guid}/conclude")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> ConcludeCase(
        Guid id, Guid caseId, [FromBody] ConcludeMedicalBoardCaseDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.ConcludeCaseAsync(id, caseId, dto, ct)));

    /// <summary>A case taken off the board without a finding</summary>
    [HttpPut("{id:guid}/cases/{caseId:guid}/withdraw")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> WithdrawCase(
        Guid id, Guid caseId, [FromBody] string reason, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.WithdrawCaseAsync(id, caseId, reason, ct)));

    /// <summary>Appoint a member</summary>
    /// <remarks>⚠ Refused once the board has reported — its membership is part of what its findings mean.</remarks>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardMemberDto>> AddMember(
        Guid id, [FromBody] AddMedicalBoardMemberDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardMemberDto>(async () => Ok(await _service.AddMemberAsync(id, dto, ct)));

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<object>> RemoveMember(Guid id, Guid memberId, CancellationToken ct = default)
        => await Guarded<object>(async () =>
        {
            var removed = await _service.RemoveMemberAsync(id, memberId, ct);
            if (!removed) return NotFound(new { message = "Member not found on this board." });
            return Ok(new { removed = true });
        });

    /// <summary>Convene the board</summary>
    /// <remarks>⚠ Refused without members, or without an open case: a board is its panel, and it needs somebody before it.</remarks>
    [HttpPut("{id:guid}/convene")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> Convene(Guid id, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.ConveneAsync(id, ct)));

    /// <summary>Record a sitting, and who was present</summary>
    [HttpPost("{id:guid}/sittings")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardSittingDto>> RecordSitting(
        Guid id, [FromBody] RecordMedicalBoardSittingDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardSittingDto>(async () => Ok(await _service.RecordSittingAsync(id, dto, ct)));

    /// <summary>Replace who was present at a sitting</summary>
    /// <remarks>⚠ Refused once a case has been decided at the sitting: its attendance is then the panel that decided.</remarks>
    [HttpPut("{id:guid}/sittings/{sittingId:guid}/attendance")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardSittingDto>> SetSittingAttendance(
        Guid id, Guid sittingId, [FromBody] SetMedicalBoardSittingAttendanceDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardSittingDto>(async () => Ok(await _service.SetSittingAttendanceAsync(id, sittingId, dto, ct)));

    // ⚠ There is no board-level conclude (lane K-II-a). A board reports by itself when its last open
    // case closes with at least one decided; what used to be PUT {id}/conclude is now per case.

    /// <summary>Stop a board that has not reported: cancel the request, or dissolve the board</summary>
    /// <remarks>
    /// ⚠ One endpoint, two words (round 5, lane K5). Still Requested, the request is cancelled; once
    /// Convened, the board is dissolved and its members and sittings stay on the record. The answer's
    /// <c>wasDissolved</c> says which happened.
    /// </remarks>
    [HttpPut("{id:guid}/cancel")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> Cancel(
        Guid id, [FromBody] string reason, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.CancelAsync(id, reason, ct)));

    // ── Documents (round 5, lane K4) ─────────────────────────────────────────────────────────
    //
    // Multipart through the controlled-upload gate (scan-mandatory: with no scanner the gate refuses
    // with 422 before a row is written), registered in the DMS as Medical restricted, and served
    // only by the download below, after the Medical read check. Never a caller-supplied path.

    /// <summary>The board's papers</summary>
    [HttpGet("{id:guid}/documents")]
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<MedicalBoardDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MedicalBoardDocumentDto>>> GetDocuments(
        Guid id, CancellationToken ct = default)
        => await Guarded<IReadOnlyList<MedicalBoardDocumentDto>>(async () => Ok(await _service.GetDocumentsAsync(id, ct)));

    /// <summary>Attach a paper to the board, or to one of its cases</summary>
    /// <remarks>
    /// Allowed at any status — the signed report usually arrives after the board has concluded. The
    /// uploader is taken from the token and must be an employee. <c>caseId</c>, when sent, must be a
    /// case before this board (lane K-II-a).
    /// </remarks>
    [HttpPost("{id:guid}/documents")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(MedicalBoardDocumentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddDocument(
        Guid id, IFormFile? file, [FromForm] string? description, [FromForm] Guid? caseId,
        CancellationToken ct = default)
    {
        // The board must exist before anything is scanned and stored: a document uploaded against a
        // board that is not there would be rolled back, but only after the scan and the DMS write.
        var board = await _service.GetBoardAsync(id, ct);
        if (board is null)
            return NotFound(new { message = $"Medical board '{id}' not found." });

        // Likewise the case: checked before the scan, not after it.
        if (caseId is Guid onCase)
        {
            try { await _service.EnsureCaseOnBoardAsync(id, onCase, ct); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "MedicalBoard",
            sourceRecordId: id,
            sourceLabel: "Medical board document",
            documentType: "MedicalBoardDocument",
            description: description,
            persist: (uploadedById, document) => _service.AddDocumentAsync(
                id, caseId, uploadedById, document.OriginalFileName, document.FileSize, description,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId, ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrMedicalBoardDocuments,
            accessProfile: "Medical restricted");
    }

    /// <summary>Stream one of the board's papers</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var document = await _service.GetDocumentForDownloadAsync(id, documentId, ct);
        if (document is null)
            return NotFound(new { message = "Document not found on this board." });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, legacyPath: null,
            document.FileName, fallbackContentType: null,
            inline: false, cancellationToken: ct);
    }

    /// <summary>Remove a paper</summary>
    /// <remarks>⚠ Refused once the board has reported or been stopped — its papers are then part of the record.</remarks>
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<object>> RemoveDocument(Guid id, Guid documentId, CancellationToken ct = default)
        => await Guarded<object>(async () =>
        {
            var removed = await _service.RemoveDocumentAsync(id, documentId, ct);
            if (!removed) return NotFound(new { message = "Document not found on this board." });
            return Ok(new { removed = true });
        });

    /// <summary>
    /// One place for the four outcomes every write here shares, so a new endpoint cannot quietly
    /// turn a refusal into a 500.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Exactly one overload, and every call site names its <c>T</c>.</b> An earlier version had
    /// a second, non-generic <c>Guarded(Func&lt;Task&lt;IActionResult&gt;&gt;)</c> beside it. Because
    /// <c>Ok(...)</c> returns a type convertible to both, overload resolution chose the non-generic
    /// one for every lambda and then could not convert the result back — six compile errors from one
    /// convenience. The explicit type argument is what keeps it unambiguous.
    /// </remarks>
    private async Task<ActionResult<T>> Guarded<T>(Func<Task<ActionResult<T>>> run)
    {
        try { return await run(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Medical board operation failed");
            return StatusCode(500, "An error occurred on the medical board.");
        }
    }
}
