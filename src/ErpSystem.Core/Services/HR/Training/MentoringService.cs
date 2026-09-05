using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class MentoringService : IMentoringService
{
    private readonly IMentoringProgramRepository _programRepository;
    private readonly IMentoringPairRepository _pairRepository;
    private readonly IMentoringSessionRepository _sessionRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MentoringService> _logger;

    public MentoringService(
        IMentoringProgramRepository programRepository,
        IMentoringPairRepository pairRepository,
        IMentoringSessionRepository sessionRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<MentoringService> logger)
    {
        _programRepository = programRepository;
        _pairRepository = pairRepository;
        _sessionRepository = sessionRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A row owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<MentoringProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring program with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringProgram> GetOwnedProgramWithDetailsAsync(Guid id)
    {
        var entity = await _programRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring program with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringPair> GetOwnedPairAsync(Guid id)
    {
        var entity = await _pairRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring pair with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringPair> GetOwnedPairWithDetailsAsync(Guid id)
    {
        var entity = await _pairRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring pair with ID '{id}' not found.");
        return entity;
    }

    private async Task<MentoringSession> GetOwnedSessionAsync(Guid id)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Mentoring session with ID '{id}' not found.");
        return entity;
    }

    private bool IsHr()
        => _currentUserProvider.HasRole(Constants.Roles.Hr)
        || _currentUserProvider.HasRole(Constants.Roles.SuperAdmin)
        || _currentUserProvider.HasRole(Constants.Roles.TenantAdmin);

    /// <summary>
    /// Who may look inside a mentoring pair: the two people in it, the programme's coordinator, and HR.
    ///
    /// This matters more here than elsewhere in Training. A session carries <c>MentorNotes</c> and
    /// <c>MenteeNotes</c> — candid write-ups of how someone is getting on, written on the understanding
    /// that the relationship is private. Tenant scope alone let any authenticated employee read any
    /// pair's notes by id, which is the kind of thing that stops people writing anything honest.
    /// </summary>
    private void EnsurePairVisible(MentoringPair pair, Guid actorEmployeeId)
    {
        var allowed = pair.MentorId == actorEmployeeId
                   || pair.MenteeId == actorEmployeeId
                   || pair.Program?.CoordinatedById == actorEmployeeId
                   || IsHr();

        if (!allowed)
            throw new UnauthorizedAccessException(
                "Mentoring records are visible to the mentor, the mentee, the programme coordinator and HR.");
    }

    // ── Author-only fields ────────────────────────────────────────────────────
    //
    // Seeing a pair is not the same as reading everything in it. A session mixes two kinds of content
    // under one shape: a *shared record* (topics, action items, attendance) that exists to be acted on
    // jointly, and *private reflections* that exist to help their author think. Only the second kind
    // needs protecting, and it needs protecting from everyone — the counterparty, the coordinator and
    // HR alike, since the whole return on a mentoring scheme is that people write down what they
    // actually think rather than what reads well.
    //
    // The ratings are the same mistake one table over. MentorRating is given BY the mentee and
    // MenteeRating BY the mentor, so each is a score of the person who must not see it: a 1-5 rating of
    // a named colleague that the colleague can read is a negotiation, not a rating.
    //
    // Note this is authorship, not seniority — HR and the coordinator hold no special claim here. They
    // get the metadata that answers "is this working" (frequency, attendance, duration, status,
    // closure notes) and none of the prose.

    /// <summary>Blanks the notes this viewer did not write.</summary>
    private static MentoringSessionDto RedactForViewer(MentoringSessionDto dto, MentoringPair pair, Guid actorEmployeeId)
    {
        if (pair.MentorId != actorEmployeeId) dto.MentorNotes = null;
        if (pair.MenteeId != actorEmployeeId) dto.MenteeNotes = null;
        return dto;
    }

    /// <summary>Blanks the rating this viewer did not give, and says which side they are on.</summary>
    private static MentoringPairDto RedactForViewer(MentoringPairDto dto, Guid actorEmployeeId)
    {
        dto.ViewerIsMentor = dto.MentorId == actorEmployeeId;
        dto.ViewerIsMentee = dto.MenteeId == actorEmployeeId;

        if (!dto.ViewerIsMentee) dto.MentorRating = null;  // the mentee's score of the mentor
        if (!dto.ViewerIsMentor) dto.MenteeRating = null;  // the mentor's score of the mentee
        return dto;
    }

    // ── Program queries ───────────────────────────────────────────────────────

    public async Task<MentoringProgramDto> GetProgramByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MentoringProgramSummaryDto>> GetAllProgramsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Program CRUD ──────────────────────────────────────────────────────────

    public async Task<MentoringProgramDto> CreateProgramAsync(CreateMentoringProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring program created: {ProgramName}", dto.ProgramName);

        // Re-read through the includes chain: the entity we just built has no CoordinatedBy loaded, so
        // returning it directly hands the caller a blank coordinator on the very response that is meant
        // to confirm what was saved. Untracked, so the read cannot be answered from the identity map.
        var saved = await _programRepository.GetWithFullDetailsUntrackedAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<MentoringProgramDto> UpdateProgramAsync(UpdateMentoringProgramDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _programRepository.GetWithFullDetailsUntrackedAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(id);

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring program {ProgramId} deleted", id);

        return true;
    }

    // ── Pair queries ──────────────────────────────────────────────────────────

    public async Task<MentoringPairDto> GetPairByIdAsync(Guid id, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPairWithDetailsAsync(id);
        EnsurePairVisible(entity, actorEmployeeId);
        return RedactForViewer(entity.ToDto(), actorEmployeeId);
    }

    /// <summary>
    /// The caller's own mentoring, on both sides of the relationship — the read every mentoring screen
    /// needs and the module had no endpoint for. Without it a self-service page has to pass an employee
    /// id, which is exactly the parameter that lets someone ask about a colleague instead.
    /// </summary>
    public async Task<IEnumerable<MentoringPairSummaryDto>> GetMyPairsAsync(Guid actorEmployeeId, CancellationToken cancellationToken = default)
        => await GetPairsForEmployeeAsync(actorEmployeeId, cancellationToken);

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetActivePairsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pairRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pairRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var asMentor = await _pairRepository.GetByMentorIdAsync(employeeId);
        var asMentee = await _pairRepository.GetByMenteeIdAsync(employeeId);

        return asMentor.Concat(asMentee)
            .Where(p => p.TenantId == tenantId)
            .DistinctBy(p => p.Id)
            .ToSummaryDtoList();
    }

    // ── Pair CRUD & workflow ──────────────────────────────────────────────────

    public async Task<MentoringPairDto> CreatePairAsync(CreateMentoringPairDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedProgramAsync(dto.ProgramId);

        var mentor = await _employeeRepository.GetByIdAsync(dto.MentorId);
        if (mentor == null || mentor.TenantId != current)
            throw new ArgumentException($"Employee with ID '{dto.MentorId}' not found.");

        var mentee = await _employeeRepository.GetByIdAsync(dto.MenteeId);
        if (mentee == null || mentee.TenantId != current)
            throw new ArgumentException($"Employee with ID '{dto.MenteeId}' not found.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _pairRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring pair created: mentor {MentorId} — mentee {MenteeId}", dto.MentorId, dto.MenteeId);

        var saved = await _pairRepository.GetWithFullDetailsUntrackedAsync(entity.Id);
        return RedactForViewer((saved ?? entity).ToDto(), createdByUserId);
    }

    public async Task<MentoringPairDto> UpdatePairAsync(UpdateMentoringPairDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPairWithDetailsAsync(dto.Id);
        EnsurePairVisible(entity, updatedByUserId);

        // Each rating belongs to the person who gave it, and the other party never received it in the
        // payload they are editing from — so preserve it rather than let a blank field overwrite it.
        var existingMentorRating = entity.MentorRating;
        var existingMenteeRating = entity.MenteeRating;

        entity.UpdateEntity(dto, updatedByUserId);

        if (entity.MenteeId != updatedByUserId) entity.MentorRating = existingMentorRating;
        if (entity.MentorId != updatedByUserId) entity.MenteeRating = existingMenteeRating;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pairRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _pairRepository.GetWithFullDetailsUntrackedAsync(entity.Id);
        return RedactForViewer((saved ?? entity).ToDto(), updatedByUserId);
    }

    public async Task<MentoringPairDto> ClosePairAsync(Guid pairId, string closureNotes, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        // W3 slice 8: closing was the one pair mutation that skipped the visibility gate, so any
        // internal user holding a pair id could end anyone's mentoring relationship. Ending one is
        // for the people in it, the coordinator, or HR — the same circle that may look inside it.
        var entity = await GetOwnedPairWithDetailsAsync(pairId);
        EnsurePairVisible(entity, updatedByUserId);

        if (entity.Status == MentoringStatus.Completed || entity.Status == MentoringStatus.Cancelled)
            throw new InvalidOperationException($"Mentoring pair is already closed with status '{entity.Status}'.");

        entity.Status = MentoringStatus.Completed;
        entity.ClosureNotes = closureNotes;
        entity.EndDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _pairRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring pair {PairId} closed", pairId);

        // Returns the closed pair rather than a bare true: the caller's screen has to show the new
        // status, end date and closure notes, and a bool forces it to refetch what this call just knew.
        var saved = await _pairRepository.GetWithFullDetailsUntrackedAsync(entity.Id);
        return RedactForViewer((saved ?? entity).ToDto(), updatedByUserId);
    }

    // ── Session sub-operations ────────────────────────────────────────────────

    public async Task<MentoringSessionDto> GetSessionByIdAsync(Guid id, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(id);

        var pair = await GetOwnedPairWithDetailsAsync(entity.PairId);
        EnsurePairVisible(pair, actorEmployeeId);

        return RedactForViewer(entity.ToDto(), pair, actorEmployeeId);
    }

    public async Task<MentoringSessionDto> LogSessionAsync(CreateMentoringSessionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Writing into someone else's mentoring relationship is worse than reading it, so the same rule
        // applies on the way in.
        var pair = await GetOwnedPairWithDetailsAsync(dto.PairId);
        EnsurePairVisible(pair, createdByUserId);

        var entity = dto.ToEntity(current, createdByUserId);

        // Notes are author-only on the way in too, or the read rule would just be hiding words the
        // system had already attributed to someone who never wrote them.
        if (pair.MentorId != createdByUserId) entity.MentorNotes = null;
        if (pair.MenteeId != createdByUserId) entity.MenteeNotes = null;

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mentoring session added for pair {PairId}", dto.PairId);

        // MentorName/MenteeName/ProgramName all resolve through Pair, which a freshly built entity has
        // never loaded — so without this the log's newest row is the one with three blank columns.
        var saved = await _sessionRepository.GetWithPairUntrackedAsync(entity.Id);
        return RedactForViewer((saved ?? entity).ToDto(), pair, createdByUserId);
    }

    public async Task<IEnumerable<MentoringSessionDto>> GetSessionsForPairAsync(Guid pairId, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var pair = await GetOwnedPairWithDetailsAsync(pairId);
        EnsurePairVisible(pair, actorEmployeeId);

        var entities = await _sessionRepository.GetByPairIdAsync(pairId);
        return entities
            .Where(e => e.TenantId == tenantId)
            .Select(e => RedactForViewer(e.ToDto(), pair, actorEmployeeId))
            .ToList();
    }

    public async Task<MentoringSessionDto> UpdateSessionAsync(UpdateMentoringSessionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(dto.Id);

        var pair = await GetOwnedPairWithDetailsAsync(entity.PairId);
        EnsurePairVisible(pair, updatedByUserId);

        // Captured before the mapper overwrites them. The other party's notes are not merely invisible
        // to this caller — they were blanked out of the payload this caller was given, so writing the
        // DTO back verbatim would erase prose the editor never saw and could not have meant to delete.
        var existingMentorNotes = entity.MentorNotes;
        var existingMenteeNotes = entity.MenteeNotes;

        entity.UpdateEntity(dto, updatedByUserId);

        if (pair.MentorId != updatedByUserId) entity.MentorNotes = existingMentorNotes;
        if (pair.MenteeId != updatedByUserId) entity.MenteeNotes = existingMenteeNotes;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _sessionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _sessionRepository.GetWithPairUntrackedAsync(entity.Id);
        return RedactForViewer((saved ?? entity).ToDto(), pair, updatedByUserId);
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, Guid actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(sessionId);

        var pair = await GetOwnedPairWithDetailsAsync(entity.PairId);
        EnsurePairVisible(pair, actorEmployeeId);

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
