using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The performance journal — the running notebook of evidence an employee keeps for themselves and
/// a manager keeps about their reports, feeding the year-end appraisal.
///
/// <para><b>Who may read what.</b> An entry marked private belongs to its author and to nobody else
/// — <em>including HR</em>. That is a deliberate departure from the "HR sees everything" rule the
/// rest of this module follows: the setting that switches the feature on is called
/// <c>EnablePrivateJournal</c>, and a private note readable by HR is not private. A shared entry
/// (<c>IsPrivate = false</c>) is readable by its author, the author's line manager and HR. Writes —
/// edit, delete, privacy — belong to the author alone.</para>
///
/// <para><b>Every actor comes from the token.</b> Before this, <c>GetById</c> took the requesting
/// employee from the <em>query string</em> and <c>about/{managerId}/{subjectEmployeeId}</c> took
/// both from the <em>route</em>, with no gate beyond <c>[Authorize]</c> — so any authenticated user
/// could read anyone's private journal by passing their id, and the create payload named its own
/// owner. The id-bearing routes that identified the caller are gone rather than merely guarded, so
/// the hole cannot be reintroduced by a caller passing someone else's id.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class PerformanceJournalController : ControllerBase
{
    private readonly IPerformanceJournalService _journalService;
    private readonly IEmployeeService            _employeeService;
    private readonly ApplicationDbContext        _db;
    private readonly ICurrentUserService         _currentUserService;
    private readonly ILogger<PerformanceJournalController> _logger;

    public PerformanceJournalController(
        IPerformanceJournalService journalService,
        IEmployeeService employeeService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<PerformanceJournalController> logger)
    {
        _journalService     = journalService;
        _employeeService    = employeeService;
        _db                 = db;
        _currentUserService = currentUserService;
        _logger             = logger;
    }

    /// <summary>
    /// W3: whether the caller holds the given performance policy. Stands in for the old HR-role
    /// arm on SHARED entries only — a private entry stays owner-only, permission holders included,
    /// for the same reason it excludes HR: a private note readable by the desk is not private.
    /// </summary>
    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>
    /// Business rules — "private journaling is not enabled for this cycle" — come back as 422 with
    /// the rule's own message rather than falling to the generic handler, which returns a bare
    /// string body the client cannot read a message out of.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Journal rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// The caller's own employee id. Everything here is written by, about or for an employee, so a
    /// login with no employee record has nothing to see.
    /// </summary>
    private bool TryGetEmployeeId(out Guid employeeId)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
        {
            employeeId = me;
            return true;
        }

        employeeId = Guid.Empty;
        return false;
    }

    /// <summary>True when the caller is <paramref name="ownerId"/> or that employee's line manager.</summary>
    private async Task<bool> CanSeeSharedOf(Guid ownerId, CancellationToken ct)
    {
        if (await HoldsPolicyAsync(HrPermissions.PerformanceReadPolicy)) return true;
        if (!TryGetEmployeeId(out var me)) return false;
        if (me == ownerId) return true;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<Employee>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == ownerId && e.TenantId == tenantId && e.ManagerId == me, ct);
    }

    /// <summary>
    /// Entitlement for a single entry. Null means "no such entry" so the caller can 404 rather than
    /// 403 — a 403 on a missing id would confirm that it exists in another tenant.
    /// </summary>
    private async Task<bool?> CanReadEntryAsync(Guid entryId, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var me)) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return null;

        var entry = await _db.Set<PerformanceJournalEntry>()
            .AsNoTracking()
            .Where(j => j.Id == entryId && j.TenantId == tenantId)
            .Select(j => new { j.OwnerId, j.IsPrivate, OwnerManagerId = j.Owner.ManagerId })
            .FirstOrDefaultAsync(ct);

        if (entry is null) return null;
        if (entry.OwnerId == me) return true;
        // Private is owner-only, desk and HR included — see the type comment.
        if (entry.IsPrivate) return false;
        return entry.OwnerManagerId == me || await HoldsPolicyAsync(HrPermissions.PerformanceReadPolicy);
    }

    /// <summary>Editing, deleting and re-classifying an entry belong to its author alone.</summary>
    private async Task<bool?> CanWriteEntryAsync(Guid entryId, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var me)) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return null;

        var ownerId = await _db.Set<PerformanceJournalEntry>()
            .AsNoTracking()
            .Where(j => j.Id == entryId && j.TenantId == tenantId)
            .Select(j => (Guid?)j.OwnerId)
            .FirstOrDefaultAsync(ct);

        if (ownerId is null) return null;
        return ownerId == me;
    }

    /// <summary>Get a journal entry by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceJournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var allowed = await CanReadEntryAsync(id, cancellationToken);
        if (allowed is null) return NotFound(new { message = $"Journal entry with ID '{id}' not found." });
        if (allowed is false) return Forbid();

        if (!TryGetEmployeeId(out var me)) return Forbid();

        try
        {
            var result = await _journalService.GetByIdAsync(id, me, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance journal entry {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the journal entry");
        }
    }

    /// <summary>The caller's own journal, private entries included</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var me)) return Forbid();

        try
        {
            var result = await _journalService.GetByOwnerIdAsync(me, cycleId, includePrivate: true, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving own journal entries");
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>
    /// Get journal entries by owner. Private entries are returned only when the caller is the owner;
    /// a line manager or HR sees the shared ones.
    /// </summary>
    [HttpGet("by-owner/{ownerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByOwner(Guid ownerId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await CanSeeSharedOf(ownerId, cancellationToken)) return Forbid();
        TryGetEmployeeId(out var me);

        try
        {
            var result = await _journalService.GetByOwnerIdAsync(ownerId, cycleId, includePrivate: me == ownerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving journal entries for owner {OwnerId}", ownerId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Get entries shared with the manager (IsPrivate = false) for an employee</summary>
    [HttpGet("shared/{ownerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSharedWithManager(Guid ownerId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await CanSeeSharedOf(ownerId, cancellationToken)) return Forbid();

        try
        {
            var result = await _journalService.GetSharedWithManagerAsync(ownerId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared journal entries for owner {OwnerId}", ownerId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>
    /// Get entries the caller has written about one of their direct reports. The manager is the
    /// caller — the old route took them from the URL, which let anyone read anyone's notes.
    /// </summary>
    [HttpGet("about/{subjectEmployeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAboutSubject(Guid subjectEmployeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var me)) return Forbid();

        try
        {
            var result = await _journalService.GetAboutSubjectAsync(me, subjectEmployeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving journal entries by manager {ManagerId} about subject {SubjectId}", me, subjectEmployeeId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Get paged journal entries for an owner</summary>
    [HttpGet("paged/{ownerId:guid}")]
    [ProducesResponseType(typeof(PagedResult<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(Guid ownerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await CanSeeSharedOf(ownerId, cancellationToken)) return Forbid();
        TryGetEmployeeId(out var me);

        try
        {
            var result = await _journalService.GetPagedAsync(ownerId, pageNumber, pageSize, cycleId, includePrivate: me == ownerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged journal entries for owner {OwnerId}", ownerId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Create a new journal entry. The author is the caller.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PerformanceJournalEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreatePerformanceJournalEntryDto createDto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var me)) return Forbid();

        // A note *about* someone is only yours to write if they report to you. Without this an
        // employee could file entries against a colleague they have no relationship with.
        if (createDto.SubjectEmployeeId is Guid subject && subject != me
            && !await HoldsPolicyAsync(HrPermissions.PerformanceWritePolicy))
        {
            if (_currentUserService.TenantId is not Guid tenantId) return Forbid();
            var isMyReport = await _db.Set<Employee>()
                .AsNoTracking()
                .AnyAsync(e => e.Id == subject && e.TenantId == tenantId && e.ManagerId == me, cancellationToken);
            if (!isMyReport) return Forbid();
        }

        try
        {
            var result = await _journalService.CreateAsync(createDto, me, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating a journal entry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating journal entry");
            return StatusCode(500, "An error occurred while creating the journal entry");
        }
    }

    /// <summary>Update an existing journal entry</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceJournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePerformanceJournalEntryDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so a mismatch would silently edit a different row.
        if (id != updateDto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });

        var allowed = await CanWriteEntryAsync(id, cancellationToken);
        if (allowed is null) return NotFound(new { message = $"Journal entry with ID '{id}' not found." });
        if (allowed is false) return Forbid();

        try
        {
            var result = await _journalService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating a journal entry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating journal entry {Id}", id);
            return StatusCode(500, "An error occurred while updating the journal entry");
        }
    }

    /// <summary>Delete a journal entry</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var allowed = await CanWriteEntryAsync(id, cancellationToken);
        if (allowed is null) return NotFound(new { message = "Journal entry not found" });
        if (allowed is false) return Forbid();

        try
        {
            var result = await _journalService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Journal entry not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting journal entry {Id}", id);
            return StatusCode(500, "An error occurred while deleting the journal entry");
        }
    }

    /// <summary>
    /// The caller's team journal across their direct reports: their own notes about a report, plus
    /// whatever those reports have chosen to share.
    /// </summary>
    [HttpGet("team")]
    [ProducesResponseType(typeof(IEnumerable<TeamJournalListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTeamJournal(
        [FromQuery] Guid?     employeeId        = null,
        [FromQuery] Guid?     appraisalCycleId  = null,
        [FromQuery] DateTime? fromDate          = null,
        [FromQuery] DateTime? toDate            = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var managerId)) return Forbid();

        try
        {
            var directReports = await _employeeService.GetDirectReportsAsync(managerId, cancellationToken);
            var reportIds     = directReports.Select(e => e.Id).ToList();

            // Optionally narrow to a single direct report
            if (employeeId.HasValue)
            {
                if (!reportIds.Contains(employeeId.Value))
                    return BadRequest(new { message = "The specified employee is not a direct report of this manager." });
                reportIds = new List<Guid> { employeeId.Value };
            }

            var result = await _journalService.GetTeamJournalAsync(
                managerId, reportIds, appraisalCycleId, fromDate, toDate, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team journal for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving team journal entries");
        }
    }

    /// <summary>Set the privacy flag on a journal entry</summary>
    [HttpPatch("{id:guid}/privacy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrivacy(Guid id, [FromBody] bool isPrivate, CancellationToken cancellationToken = default)
    {
        // Un-privating someone else's entry would be a read hole by another route.
        var allowed = await CanWriteEntryAsync(id, cancellationToken);
        if (allowed is null) return NotFound(new { message = "Journal entry not found" });
        if (allowed is false) return Forbid();

        try
        {
            var result = await _journalService.SetPrivacyAsync(id, isPrivate, cancellationToken);
            if (!result) return NotFound(new { message = "Journal entry not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting privacy for journal entry {Id}", id);
            return StatusCode(500, "An error occurred while updating the journal entry");
        }
    }
}
