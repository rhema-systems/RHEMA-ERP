using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// One-to-ones and interim conversations held during a cycle, and the goal updates that come
/// out of them. A goal update recorded here is applied to the goal itself, so flagging a goal
/// at risk in a check-in puts it in the at-risk reports.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CheckInsController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly ICheckInService _checkInService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CheckInsController> _logger;

    public CheckInsController(
        ICheckInService checkInService,
        ICurrentUserService currentUserService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<CheckInsController> logger)
    {
        _checkInService = checkInService;
        _currentUserService = currentUserService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
    }

    /// <summary>Check-ins the signed-in employee is the subject of.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _checkInService.GetByEmployeeIdAsync(employeeId, cycleId, cancellationToken);
            // The employee's own list: they are the subject, not the conductor, so the private
            // notes are not theirs to read.
            return Ok(Redact(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Check-ins the signed-in employee is conducting — the manager's list.</summary>
    [HttpGet("me/conducting")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMineAsConductor([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _checkInService.GetByConductedByIdAsync(employeeId, cycleId, cancellationToken);
            // The conductor's own list — their private notes are their own, so no redaction.
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins conducted by {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot have check-ins." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    /// <summary>
    /// Business rules are answered with 422 and the rule's own message — "check-ins are not
    /// enabled for this appraisal cycle" is something a user hits, not a server fault.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Check-in rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    /// <summary>HR, or the employee the check-in is about, or their line manager.</summary>
    private async Task<bool> CanAccessEmployeeAsync(Guid employeeId, CancellationToken ct)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (me == employeeId) return true;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<Core.Entities.HR.Employee>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me, ct);
    }

    /// <summary>
    /// As <see cref="CanAccessCheckInAsync"/> minus the employee: editing, deleting and closing a
    /// check-in belong to whoever is holding it. The employee's record of the meeting is the
    /// conductor's, not theirs to amend.
    /// </summary>
    private async Task<bool> CanManageCheckInAsync(Guid checkInId, CancellationToken ct)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<CheckIn>()
            .AsNoTracking()
            .AnyAsync(c => c.Id == checkInId && c.TenantId == tenantId && c.ConductedById == me, ct);
    }

    /// <summary>
    /// Blanks the conductor's private notes for anyone who is not the conductor (or HR).
    /// </summary>
    /// <remarks>
    /// <c>PrivateNotes</c> is on <c>CheckInDto</c> and was returned by every read, so an employee
    /// could see their manager's private record of the meeting simply by calling the API — the
    /// Next.js screen hides the field, which is not the same as it being private. Suppression
    /// belongs here, where the caller is known, rather than in a client that can be bypassed.
    /// </remarks>
    private CheckInDto Redact(CheckInDto dto)
    {
        if (IsHr) return dto;
        if (_currentUserService.EmployeeId is Guid me && dto.ConductedById == me) return dto;
        dto.PrivateNotes = null;
        return dto;
    }

    private IEnumerable<CheckInDto> Redact(IEnumerable<CheckInDto> dtos) => dtos.Select(Redact).ToList();

    /// <summary>Every check-in in the tenant — HR's view.</summary>
    [HttpGet("paged")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(PagedResult<CheckInDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged check-ins");
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Get a check-in by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.GetByIdAsync(id, cancellationToken);
            return Ok(Redact(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-in {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the check-in");
        }
    }

    /// <summary>Get check-ins for an employee, optionally filtered by cycle</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeAsync(employeeId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.GetByEmployeeIdAsync(employeeId, cycleId, cancellationToken);
            return Ok(Redact(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>
    /// Get check-ins conducted by a specific person. HR, or that person themselves — everyone else
    /// should use <c>me/conducting</c>, which needs no id at all.
    /// </summary>
    [HttpGet("by-conductor/{conductedById:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByConductedBy(Guid conductedById, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!IsHr && _currentUserService.EmployeeId != conductedById) return Forbid();

        try
        {
            var result = await _checkInService.GetByConductedByIdAsync(conductedById, cycleId, cancellationToken);
            return Ok(Redact(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins conducted by {ConductedById}", conductedById);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Get upcoming check-ins for an employee within the next N days</summary>
    [HttpGet("upcoming/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUpcoming(Guid employeeId, [FromQuery] int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeAsync(employeeId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.GetUpcomingAsync(employeeId, daysAhead, cancellationToken);
            return Ok(Redact(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming check-ins for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving upcoming check-ins");
        }
    }

    /// <summary>Create a new check-in</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCheckInDto createDto, CancellationToken cancellationToken = default)
    {
        // Whoever schedules a check-in is normally the one holding it, and the client has no
        // employee id of its own — the token is the only place it exists. An omitted (empty)
        // ConductedById therefore resolves to the caller rather than failing the FK. An
        // explicit id is honoured, so HR scheduling on someone else's behalf still works.
        if (createDto.ConductedById == Guid.Empty)
        {
            if (!TryGetEmployeeId(out var conductorId, out var problem)) return problem!;
            createDto.ConductedById = conductorId;
        }

        try
        {
            var result = await _checkInService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating the check-in");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating check-in");
            return StatusCode(500, "An error occurred while creating the check-in");
        }
    }

    /// <summary>Update an existing check-in</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCheckInDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so without this a mismatch silently edits another row.
        if (id != updateDto.Id)
            return BadRequest(new { message = "ID mismatch" });

        if (!await CanManageCheckInAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.UpdateAsync(updateDto, cancellationToken);
            return Ok(Redact(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating check-in {Id}", id);
            return StatusCode(500, "An error occurred while updating the check-in");
        }
    }

    /// <summary>Delete a check-in</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageCheckInAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Check-in not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting check-in {Id}", id);
            return StatusCode(500, "An error occurred while deleting the check-in");
        }
    }

    /// <summary>Mark a check-in as complete</summary>
    [HttpPost("{checkInId:guid}/complete")]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid checkInId, [FromBody] CompleteCheckInRequest request, CancellationToken cancellationToken = default)
    {
        // Closing the meeting, and writing the private notes, is the conductor's act.
        if (!await CanManageCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.CompleteAsync(checkInId, request.SharedNotes, request.PrivateNotes, request.ActionItems, cancellationToken);
            return Ok(Redact(result));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "completing the check-in");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while completing the check-in");
        }
    }

    // ── Goal updates ──────────────────────────────────────────────────────

    /// <summary>Add a goal update to a check-in</summary>
    [HttpPost("{checkInId:guid}/goal-updates")]
    [ProducesResponseType(typeof(CheckInGoalUpdateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGoalUpdate(Guid checkInId, [FromBody] CreateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        // Either side of the conversation may record what it changed about a goal.
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.AddGoalUpdateAsync(checkInId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding goal update to check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while adding the goal update");
        }
    }

    /// <summary>Get goal updates for a check-in</summary>
    [HttpGet("{checkInId:guid}/goal-updates")]
    [ProducesResponseType(typeof(IEnumerable<CheckInGoalUpdateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoalUpdates(Guid checkInId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.GetGoalUpdatesAsync(checkInId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal updates for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while retrieving goal updates");
        }
    }

    /// <summary>Update a goal update record</summary>
    [HttpPut("{checkInId:guid}/goal-updates/{updateId:guid}")]
    [ProducesResponseType(typeof(CheckInGoalUpdateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGoalUpdate(Guid checkInId, Guid updateId, [FromBody] UpdateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        if (updateId != dto.Id)
            return BadRequest(new { message = "ID mismatch" });

        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.UpdateGoalUpdateAsync(checkInId, dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating goal update {UpdateId} for check-in {CheckInId}", updateId, checkInId);
            return StatusCode(500, "An error occurred while updating the goal update");
        }
    }

    /// <summary>Delete a goal update from a check-in</summary>
    [HttpDelete("{checkInId:guid}/goal-updates/{updateId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGoalUpdate(Guid checkInId, Guid updateId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.DeleteGoalUpdateAsync(checkInId, updateId, cancellationToken);
            if (!result) return NotFound(new { message = "Goal update not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting goal update {UpdateId} from check-in {CheckInId}", updateId, checkInId);
            return StatusCode(500, "An error occurred while deleting the goal update");
        }
    }

    // ── Attachments ───────────────────────────────────────────────────────

    /// <summary>
    /// HR, the employee the check-in is about, or whoever is conducting it.
    /// </summary>
    /// <remarks>
    /// ⚠ Scoped to the attachment endpoints only. The rest of this controller has no entitlement
    /// test at all — any authenticated user can read any check-in by id, private notes included —
    /// which is a real hole but a wider one than this slice set out to close. What is not
    /// acceptable is adding a *new* file-download surface with no gate, so these four endpoints
    /// carry the rule the whole controller ought to. Widening it to the rest is its own change.
    /// </remarks>
    private async Task<bool> CanAccessCheckInAsync(Guid checkInId, CancellationToken ct)
    {
        if (User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<CheckIn>()
            .AsNoTracking()
            .Where(c => c.Id == checkInId && c.TenantId == tenantId)
            .AnyAsync(c => c.EmployeeId == me || c.ConductedById == me, ct);
    }

    /// <summary>
    /// Attach a file to a check-in, through the controlled-upload gate every other HR document
    /// family uses (scan, then central-DMS registration, rolled back if the record write fails).
    /// </summary>
    /// <remarks>
    /// Replaces a JSON endpoint that took a caller-supplied <c>filePath</c> and could not have
    /// worked in any case — see <c>CheckInService.AddAttachmentAsync</c>.
    /// </remarks>
    [HttpPost("{checkInId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid checkInId, IFormFile file, [FromForm] string? description, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUserService, _logger, file,
            sourceEntityType: "CheckIn",
            sourceRecordId: checkInId,
            sourceLabel: "Check-in attachment",
            documentType: "CheckInAttachment",
            description: description,
            persist: (uploadedById, document) => _checkInService.AddAttachmentAsync(
                checkInId, uploadedById, document.OriginalFileName, document.FileSize, description,
                cancellationToken,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken);
    }

    /// <summary>
    /// Streams a check-in attachment. The file lives outside the web root, so this endpoint is the
    /// only way to it.
    /// </summary>
    [HttpGet("{checkInId:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid checkInId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<AppraisalAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.CheckInId == checkInId && a.TenantId == tenantId && !a.IsDeleted,
                cancellationToken);

        if (attachment is null)
            return NotFound(new { message = "Attachment not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorageService, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, cancellationToken);
    }

    /// <summary>Get attachments for a check-in</summary>
    [HttpGet("{checkInId:guid}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAttachments(Guid checkInId, CancellationToken cancellationToken = default)
    {
        // Entitled the same as the file itself — a listing still leaks filenames and uploaders.
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.GetAttachmentsAsync(checkInId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>Delete an attachment from a check-in</summary>
    [HttpDelete("{checkInId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid checkInId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessCheckInAsync(checkInId, cancellationToken)) return Forbid();

        try
        {
            var result = await _checkInService.DeleteAttachmentAsync(checkInId, attachmentId, cancellationToken);
            if (!result) return NotFound(new { message = "Attachment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from check-in {CheckInId}", attachmentId, checkInId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }
}

/// <summary>Request body for completing a check-in</summary>
public record CompleteCheckInRequest(string? SharedNotes, string? PrivateNotes, string? ActionItems);
