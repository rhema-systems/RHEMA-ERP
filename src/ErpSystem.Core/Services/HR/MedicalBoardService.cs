using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="IMedicalBoardService"/>
public class MedicalBoardService : IMedicalBoardService
{
    private readonly IGenericRepository<MedicalBoard> _boards;
    private readonly IGenericRepository<MedicalBoardMember> _members;
    private readonly IGenericRepository<MedicalBoardSitting> _sittings;
    private readonly IGenericRepository<MedicalBoardDocument> _documents;
    private readonly IGenericRepository<Employee> _employees;
    private readonly IGenericRepository<Physician> _physicians;
    private readonly IGenericRepository<HealthcareFacility> _facilities;
    private readonly IGenericRepository<EmployeeHealthProfile> _profiles;
    private readonly IGenericRepository<EmployeeMedicalExam> _exams;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<MedicalBoardService> _logger;

    public MedicalBoardService(
        IGenericRepository<MedicalBoard> boards,
        IGenericRepository<MedicalBoardMember> members,
        IGenericRepository<MedicalBoardSitting> sittings,
        IGenericRepository<MedicalBoardDocument> documents,
        IGenericRepository<Employee> employees,
        IGenericRepository<Physician> physicians,
        IGenericRepository<HealthcareFacility> facilities,
        IGenericRepository<EmployeeHealthProfile> profiles,
        IGenericRepository<EmployeeMedicalExam> exams,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider clock,
        ILogger<MedicalBoardService> logger)
    {
        _boards = boards;
        _members = members;
        _sittings = sittings;
        _documents = documents;
        _employees = employees;
        _physicians = physicians;
        _facilities = facilities;
        _profiles = profiles;
        _exams = exams;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _clock = clock;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global filter and TenantId
    // auto-stamp are inert. Following the RHEMA convention, every read and write below scopes to the
    // authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        if (_currentUserService.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>A board owned by another tenant is reported as missing, not as forbidden.</summary>
    private async Task<MedicalBoard> RequireBoardAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var board = await _boards.GetQueryable()
            .Include(b => b.Members).ThenInclude(m => m.Physician)
            .Include(b => b.Members).ThenInclude(m => m.Employee)
            .Include(b => b.Sittings)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, ct);

        if (board is null)
            throw new ArgumentException($"Medical board '{id}' not found.");

        return board;
    }

    public async Task<PagedResult<MedicalBoardDto>> GetBoardsAsync(
        MedicalBoardFilterDto filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _boards.GetQueryable()
            .Include(b => b.Employee)
            .Include(b => b.Members).ThenInclude(m => m.Physician)
            .Include(b => b.Members).ThenInclude(m => m.Employee)
            .Include(b => b.Sittings)
            .Include(b => b.Facility)
            .Include(b => b.BasedOnExam)
            .Where(b => b.TenantId == tenantId);

        if (filter.EmployeeId is Guid employeeId) query = query.Where(b => b.EmployeeId == employeeId);
        if (filter.Status is MedicalBoardStatus status) query = query.Where(b => b.Status == status);
        if (filter.Purpose is MedicalBoardPurpose purpose) query = query.Where(b => b.Purpose == purpose);
        if (filter.From is DateOnly from) query = query.Where(b => b.RequestedOn >= from);
        if (filter.To is DateOnly to) query = query.Where(b => b.RequestedOn <= to);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(b =>
                b.BoardNumber.Contains(term) ||
                b.Reason.Contains(term) ||
                b.Employee.FirstName.Contains(term) ||
                b.Employee.LastName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(b => b.RequestedOn)
            .ThenByDescending(b => b.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = rows.Select(ToDto).ToList();
        await FillActorNamesAsync(dtos, tenantId, ct);

        return new PagedResult<MedicalBoardDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<MedicalBoardDto?> GetBoardAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await _boards.GetQueryable()
            .Include(b => b.Employee)
            .Include(b => b.Members).ThenInclude(m => m.Physician)
            .Include(b => b.Members).ThenInclude(m => m.Employee)
            .Include(b => b.Sittings)
            .Include(b => b.Facility)
            .Include(b => b.BasedOnExam)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, ct);

        if (board is null) return null;

        var dto = ToDto(board);
        await FillActorNamesAsync(new[] { dto }, tenantId, ct);
        return dto;
    }

    public async Task<MedicalBoardDto> RequestBoardAsync(RequestMedicalBoardDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var employee = await _employees.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.TenantId == tenantId, ct);
        if (employee is null)
            throw new ArgumentException($"Employee '{dto.EmployeeId}' not found.");

        // ⚠ Round 5, lane K1. Nullable in the DTO so that leaving it out lands HERE, worded, rather
        // than binding to 0; and an undefined number is refused the same way.
        if (dto.Purpose is not MedicalBoardPurpose purpose || !Enum.IsDefined(purpose))
            throw new InvalidOperationException(
                "Say what the board is for: extended sick leave, an injury on duty, fitness for duty, "
                + "medical retirement, or other. Leave reads it to decide whether the board can stand "
                + "as evidence for an absence.");

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException(
                "Say why a board is being asked for. A board convened without a stated question is one "
                + "nobody can tell whether it answered.");

        var (healthProfileId, examId, facilityId) = await ResolveReferencesAsync(dto, tenantId, ct);

        var board = new MedicalBoard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            // ⚠ Timestamped, not counted — see the entity. A counted sequence repeats after a soft
            // delete, and a sequential board number would be enumerable.
            BoardNumber = $"MB-{_clock.UtcNow:yyyyMMddHHmmssfff}",
            EmployeeId = dto.EmployeeId,
            Status = MedicalBoardStatus.Requested,
            Purpose = purpose,
            Reason = dto.Reason.Trim(),
            RequestedById = _currentUserService.EmployeeId,
            RequestedOn = _clock.TodayUtc,
            HealthProfileId = healthProfileId,
            BasedOnExamId = examId,
            FacilityId = facilityId
        };

        await _boards.AddAsync(board);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Medical board {number} requested for employee {employee}",
            board.BoardNumber, board.EmployeeId);

        return (await GetBoardAsync(board.Id, ct))!;
    }

    /// <summary>
    /// Checks the facility, the health profile and the examination a board is asked with, and fills
    /// the profile in where it follows from the others (round 5, lane K3).
    /// </summary>
    /// <remarks>
    /// <para>⚠ Before this, all three were stored as given. A mistyped id failed the foreign key as a
    /// 500, and — worse, because nothing failed — another employee's examination could be named as
    /// the one a board was based on, putting somebody else's clinical record behind a finding.</para>
    ///
    /// <para>Not-found is an <see cref="ArgumentException"/> (404), as the employee and the physician
    /// already are. Somebody else's record is a refusal that says so (400).</para>
    /// </remarks>
    private async Task<(Guid? HealthProfileId, Guid? ExamId, Guid? FacilityId)> ResolveReferencesAsync(
        RequestMedicalBoardDto dto, Guid tenantId, CancellationToken ct)
    {
        if (dto.FacilityId is Guid facilityId)
        {
            var known = await _facilities.GetQueryable()
                .AnyAsync(f => f.Id == facilityId && f.TenantId == tenantId, ct);
            if (!known)
                throw new ArgumentException($"Healthcare facility '{facilityId}' not found.");
        }

        Guid? profileId;

        if (dto.BasedOnExamId is Guid examId)
        {
            var exam = await _exams.GetQueryable()
                .Where(e => e.Id == examId && e.TenantId == tenantId)
                .Select(e => new { e.HealthProfileId, e.HealthProfile.EmployeeId })
                .FirstOrDefaultAsync(ct);
            if (exam is null)
                throw new ArgumentException($"Medical examination '{examId}' not found.");

            if (exam.EmployeeId != dto.EmployeeId)
                throw new InvalidOperationException(
                    "That examination is another employee's. A board can only be based on an examination "
                    + "of the person it is about.");

            if (dto.HealthProfileId is Guid named && named != exam.HealthProfileId)
                throw new InvalidOperationException(
                    "That examination is recorded on a different health profile from the one named. Name "
                    + "the examination alone and its profile follows from it.");

            profileId = exam.HealthProfileId;
        }
        else if (dto.HealthProfileId is Guid namedProfileId)
        {
            var profile = await _profiles.GetQueryable()
                .Where(p => p.Id == namedProfileId && p.TenantId == tenantId)
                .Select(p => new { p.EmployeeId })
                .FirstOrDefaultAsync(ct);
            if (profile is null)
                throw new ArgumentException($"Health profile '{namedProfileId}' not found.");

            if (profile.EmployeeId != dto.EmployeeId)
                throw new InvalidOperationException(
                    "That health profile is another employee's. A board's clinical record must be the "
                    + "record of the person it is about.");

            profileId = namedProfileId;
        }
        else
        {
            // Named neither: the subject's own record, when there is one. The board's page opens it,
            // which is the only thing the column is for.
            profileId = await _profiles.GetQueryable()
                .Where(p => p.EmployeeId == dto.EmployeeId && p.TenantId == tenantId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        return (profileId, dto.BasedOnExamId, dto.FacilityId);
    }

    public async Task<MedicalBoardMemberDto> AddMemberAsync(
        Guid boardId, AddMedicalBoardMemberDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardAsync(boardId, ct);

        RefuseIfSettled(board, "appoint a member to");

        // ⚠ One of the three, never none. A member with no physician, no employee and no name is a
        // row saying somebody was on the board without saying who — worse than not recording them,
        // because the board then looks properly constituted.
        var hasPhysician = dto.PhysicianId is Guid;
        var hasEmployee = dto.EmployeeId is Guid;
        var hasName = !string.IsNullOrWhiteSpace(dto.MemberName);

        if (!hasPhysician && !hasEmployee && !hasName)
            throw new InvalidOperationException(
                "Identify the member: pick them from the physician register, pick an employee, or type "
                + "their name. Who sat on a board is most of what its authority rests on.");

        // ⚠ Round 5, lane K7: exactly one. A row naming a physician AND typing a name would say two
        // people sat in one seat, and the read would quietly show only the first.
        if ((hasPhysician ? 1 : 0) + (hasEmployee ? 1 : 0) + (hasName ? 1 : 0) > 1)
            throw new InvalidOperationException(
                "Identify the member one way only — from the physician register, as an employee, or by "
                + "name. A member row that names two people says two sat in one seat.");

        if (dto.PhysicianId is Guid physicianId)
        {
            var known = await _physicians.GetQueryable()
                .AnyAsync(p => p.Id == physicianId && p.TenantId == tenantId, ct);
            if (!known)
                throw new ArgumentException($"Physician '{physicianId}' not found.");
        }

        if (dto.EmployeeId is Guid memberEmployeeId)
        {
            var known = await _employees.GetQueryable()
                .AnyAsync(e => e.Id == memberEmployeeId && e.TenantId == tenantId, ct);
            if (!known)
                throw new ArgumentException($"Employee '{memberEmployeeId}' not found.");

            // ⚠ THE GUARD THAT MATTERS ON THIS ENTITY. Nobody sits in judgement on their own
            // fitness. A board listing its subject among its members would discredit its own
            // finding — and that finding is what leave and separation then rest on — so it is
            // refused at the door rather than left for somebody to notice in the minutes.
            if (memberEmployeeId == board.EmployeeId)
                throw new InvalidOperationException(
                    "This board is about this employee, so they cannot sit on it. If they are to be "
                    + "represented, appoint their representative instead.");
        }

        // The same person twice is a data-entry slip, and it makes the minutes read as though the
        // board were larger than it was.
        var alreadySeated = board.Members.Any(m => !m.IsDeleted && (
            (hasPhysician && m.PhysicianId == dto.PhysicianId) ||
            (hasEmployee && m.EmployeeId == dto.EmployeeId)));

        if (alreadySeated)
            throw new InvalidOperationException("That person is already on this board.");

        // A board has one chair. Appointing a second is almost always a mistake, and it makes the
        // minutes unreadable.
        if (dto.Role == MedicalBoardMemberRole.Chair &&
            board.Members.Any(m => m.Role == MedicalBoardMemberRole.Chair && !m.IsDeleted))
        {
            throw new InvalidOperationException(
                "This board already has a chair. Remove the current one first if the chair is changing.");
        }

        var member = new MedicalBoardMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BoardId = boardId,
            PhysicianId = dto.PhysicianId,
            EmployeeId = dto.EmployeeId,
            MemberName = dto.MemberName?.Trim(),
            Institution = dto.Institution?.Trim(),
            Role = dto.Role
        };

        await _members.AddAsync(member);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _members.GetQueryable()
            .Include(m => m.Physician)
            .Include(m => m.Employee)
            .FirstAsync(m => m.Id == member.Id, ct);

        return ToDto(saved);
    }

    public async Task<bool> RemoveMemberAsync(Guid boardId, Guid memberId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardAsync(boardId, ct);

        RefuseIfSettled(board, "remove a member from");

        var member = await _members.GetQueryable()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.BoardId == boardId && m.TenantId == tenantId, ct);

        if (member is null) return false;

        await _members.DeleteAsync(member);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MedicalBoardDto> ConveneAsync(Guid boardId, CancellationToken ct = default)
    {
        var board = await RequireBoardAsync(boardId, ct);

        if (board.Status != MedicalBoardStatus.Requested)
            throw new InvalidOperationException(
                $"This board is {board.Status.ToString().ToLowerInvariant()}, so it cannot be convened again.");

        // ⚠ A board IS its panel. Convening one with nobody appointed would produce a record that
        // looks convened and can never legitimately report.
        if (!board.Members.Any(m => !m.IsDeleted))
            throw new InvalidOperationException(
                "Appoint at least one member before convening. A board with nobody on it cannot sit.");

        board.Status = MedicalBoardStatus.Convened;
        board.ConvenedOn = _clock.TodayUtc;

        await _boards.UpdateAsync(board);
        await _unitOfWork.SaveChangesAsync(ct);

        return (await GetBoardAsync(boardId, ct))!;
    }

    public async Task<MedicalBoardSittingDto> RecordSittingAsync(
        Guid boardId, RecordMedicalBoardSittingDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardAsync(boardId, ct);

        RefuseIfSettled(board, "record a sitting on");

        if (board.Status == MedicalBoardStatus.Requested)
            throw new InvalidOperationException(
                "Convene the board before recording a sitting — a panel that has not been appointed "
                + "cannot have met.");

        var sitting = new MedicalBoardSitting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BoardId = boardId,
            SittingDate = dto.SittingDate == default ? _clock.TodayUtc : dto.SittingDate,
            Venue = dto.Venue?.Trim(),
            Notes = dto.Notes?.Trim()
        };

        await _sittings.AddAsync(sitting);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(sitting);
    }

    public async Task<MedicalBoardDto> ConcludeAsync(
        Guid boardId, ConcludeMedicalBoardDto dto, CancellationToken ct = default)
    {
        var board = await RequireBoardAsync(boardId, ct);

        if (board.Status != MedicalBoardStatus.Convened)
            throw new InvalidOperationException(
                board.Status == MedicalBoardStatus.Concluded
                    ? "This board has already reported. A finding that needs revisiting is a new board."
                    : $"A board must be convened before it can report. This one is {board.Status.ToString().ToLowerInvariant()}.");

        // ⚠ Round 5, lane K7. The outcome used to be a plain enum, so leaving it out bound to 0 — no
        // finding at all — and the board concluded on it, frozen for good.
        if (dto.Outcome is not MedicalExamResult outcome || !Enum.IsDefined(outcome))
            throw new InvalidOperationException(
                "Give the board's finding: fit, fit with restrictions, temporarily unfit, unfit, or "
                + "requires further investigation. A board that reports without one has not reported.");

        if (string.IsNullOrWhiteSpace(dto.Recommendation))
            throw new InvalidOperationException(
                "A board reports by recommending something. Say what it recommends.");

        // ⚠ Round 5, lane K7. Convening needs a member, but members can be removed while the board is
        // convened — so a board could reach its finding with nobody on it.
        if (!board.Members.Any(m => !m.IsDeleted))
            throw new InvalidOperationException(
                "Appoint at least one member before the board reports. A finding nobody is recorded as "
                + "having made rests on nothing.");

        // ⚠ At least one sitting. A board that reports without ever having met is the shape of a
        // recommendation somebody wrote on its behalf, and the record would not show the difference.
        if (!board.Sittings.Any(s => !s.IsDeleted))
            throw new InvalidOperationException(
                "Record at least one sitting before the board reports. A board that never met cannot "
                + "have reached a finding.");

        board.Status = MedicalBoardStatus.Concluded;
        board.Outcome = outcome;
        board.Findings = dto.Findings?.Trim();
        board.Recommendation = dto.Recommendation.Trim();
        board.Restrictions = dto.Restrictions?.Trim();
        board.ReviewDueDate = dto.ReviewDueDate;
        board.RecommendsMedicalRetirement = dto.RecommendsMedicalRetirement;
        board.ConcludedOn = _clock.TodayUtc;
        board.ConcludedById = _currentUserService.EmployeeId;

        await _boards.UpdateAsync(board);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Medical board {number} concluded: {outcome}{retirement}",
            board.BoardNumber, board.Outcome,
            board.RecommendsMedicalRetirement ? ", recommending medical retirement" : string.Empty);

        return (await GetBoardAsync(boardId, ct))!;
    }

    /// <remarks>
    /// ⚠ Round 5, lane K5: one status, two words. Stopped while only Requested, nobody was ever
    /// appointed as a panel — the REQUEST is cancelled. Stopped once Convened, a panel existed and is
    /// DISSOLVED; its members and sittings stay on the record, because they happened. The status is
    /// the same either way (nothing downstream treats them differently), so the word is read from
    /// <c>ConvenedOn</c> rather than stored twice.
    /// </remarks>
    public async Task<MedicalBoardDto> CancelAsync(Guid boardId, string reason, CancellationToken ct = default)
    {
        var board = await RequireBoardAsync(boardId, ct);

        if (board.Status == MedicalBoardStatus.Concluded)
            throw new InvalidOperationException(
                "This board has reported and cannot be cancelled or dissolved. Leave and separation may "
                + "already be resting on its recommendation.");

        if (board.Status == MedicalBoardStatus.Cancelled)
            throw new InvalidOperationException(board.ConvenedOn is null
                ? "This request is already cancelled."
                : "This board has already been dissolved.");

        var dissolving = board.Status == MedicalBoardStatus.Convened;

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(dissolving
                ? "Say why the board is being dissolved."
                : "Say why the request for a board is being cancelled.");

        board.Status = MedicalBoardStatus.Cancelled;
        board.CancellationReason = reason.Trim();
        board.CancelledOn = _clock.TodayUtc;
        board.CancelledById = _currentUserService.EmployeeId;

        await _boards.UpdateAsync(board);
        await _unitOfWork.SaveChangesAsync(ct);

        return (await GetBoardAsync(boardId, ct))!;
    }

    // ── Documents (round 5, lane K4) ─────────────────────────────────────────────────────────

    /// <summary>The board alone — a document write needs no members or sittings.</summary>
    private async Task<MedicalBoard> RequireBoardHeaderAsync(Guid boardId, Guid tenantId, CancellationToken ct)
        => await _boards.GetQueryable()
               .AsNoTracking()
               .FirstOrDefaultAsync(b => b.Id == boardId && b.TenantId == tenantId, ct)
           ?? throw new ArgumentException($"Medical board '{boardId}' not found.");

    public async Task<IReadOnlyList<MedicalBoardDocumentDto>> GetDocumentsAsync(Guid boardId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await RequireBoardHeaderAsync(boardId, tenantId, ct);

        var rows = await _documents.GetQueryable()
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Where(d => d.BoardId == boardId && d.TenantId == tenantId)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<MedicalBoardDocumentDto> AddDocumentAsync(
        Guid boardId, Guid uploadedById, string fileName, long? fileSize, string? description,
        Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        // ⚠ No status check, deliberately: the signed report arrives after the board has concluded,
        // and the letter standing a board down after it was dissolved. Adding a paper rewrites nothing.
        await RequireBoardHeaderAsync(boardId, tenantId, ct);

        var document = new MedicalBoardDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BoardId = boardId,
            FileName = fileName,
            FileSize = fileSize,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UploadDate = _clock.UtcNow,
            UploadedById = uploadedById,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId
        };

        await _documents.AddAsync(document);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _documents.GetQueryable()
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .FirstAsync(d => d.Id == document.Id, ct);

        return ToDto(saved);
    }

    public async Task<MedicalBoardDocument?> GetDocumentForDownloadAsync(
        Guid boardId, Guid documentId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _documents.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.BoardId == boardId && d.TenantId == tenantId, ct);
    }

    public async Task<bool> RemoveDocumentAsync(Guid boardId, Guid documentId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardHeaderAsync(boardId, tenantId, ct);

        // ⚠ The same freeze as members and sittings: once the board has reported or been stopped, its
        // papers are part of the record a finding (or the decision to stop) rests on.
        RefuseIfSettled(board, "remove a document from");

        var document = await _documents.GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.BoardId == boardId && d.TenantId == tenantId, ct);
        if (document is null) return false;

        await _documents.DeleteAsync(document);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Refuses a change to a board that has finished, whichever way it finished.
    /// </summary>
    /// <remarks>
    /// ⚠ The membership and the sittings are part of what a recommendation MEANS. Editing them after
    /// the board has reported would rewrite who decided and on what — while a leave request approved
    /// on that recommendation stays approved. Hence one guard, used by every mutating path.
    /// </remarks>
    private static void RefuseIfSettled(MedicalBoard board, string what)
    {
        if (board.Status == MedicalBoardStatus.Concluded)
            throw new InvalidOperationException(
                $"This board has reported, so you cannot {what} it. Its membership and sittings are "
                + "part of what its recommendation means.");

        if (board.Status == MedicalBoardStatus.Cancelled)
            throw new InvalidOperationException(board.ConvenedOn is null
                ? $"This request for a board was cancelled, so you cannot {what} it."
                : $"This board was dissolved, so you cannot {what} it.");
    }

    /// <summary>
    /// Resolves the three bare actor columns to names, in one query.
    /// </summary>
    /// <remarks>
    /// <c>RequestedById</c>, <c>ConcludedById</c> and <c>CancelledById</c> carry no navigation — an unpaired one to
    /// <c>Employee</c> mints a shadow <c>EmployeeId1</c> column. Same treatment leave gives
    /// <c>RescheduledById</c> and <c>RecalledById</c>.
    /// </remarks>
    private async Task FillActorNamesAsync(IEnumerable<MedicalBoardDto> dtos, Guid tenantId, CancellationToken ct)
    {
        var list = dtos as ICollection<MedicalBoardDto> ?? dtos.ToList();

        var ids = list.SelectMany(d => new[] { d.RequestedById, d.ConcludedById, d.CancelledById })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

        if (ids.Count == 0) return;

        var names = await _employees.GetQueryable()
            .Where(e => e.TenantId == tenantId && ids.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.LastName })
            .ToListAsync(ct);

        string? NameOf(Guid? id) => id is Guid a
            ? names.Where(n => n.Id == a).Select(n => $"{n.FirstName} {n.LastName}".Trim()).FirstOrDefault()
            : null;

        foreach (var dto in list)
        {
            dto.RequestedByName = NameOf(dto.RequestedById);
            dto.ConcludedByName = NameOf(dto.ConcludedById);
            dto.CancelledByName = NameOf(dto.CancelledById);
        }
    }

    private static MedicalBoardDto ToDto(MedicalBoard b) => new()
    {
        Id = b.Id,
        BoardNumber = b.BoardNumber,
        EmployeeId = b.EmployeeId,
        EmployeeName = b.Employee is null ? string.Empty : $"{b.Employee.FirstName} {b.Employee.LastName}".Trim(),
        EmployeeNumber = b.Employee?.EmployeeNumber ?? string.Empty,
        Status = b.Status,
        Purpose = b.Purpose,
        CoversAbsence = MedicalBoard.CoversAbsence(b.Purpose),
        Reason = b.Reason,
        RequestedById = b.RequestedById,
        RequestedOn = b.RequestedOn,
        ConvenedOn = b.ConvenedOn,
        HealthProfileId = b.HealthProfileId,
        BasedOnExamId = b.BasedOnExamId,
        BasedOnExamDate = b.BasedOnExam?.ExamDate,
        BasedOnExamResult = b.BasedOnExam?.Result,
        FacilityId = b.FacilityId,
        FacilityName = b.Facility?.FacilityName,
        Outcome = b.Outcome,
        Findings = b.Findings,
        Recommendation = b.Recommendation,
        Restrictions = b.Restrictions,
        ReviewDueDate = b.ReviewDueDate,
        RecommendsMedicalRetirement = b.RecommendsMedicalRetirement,
        ConcludedOn = b.ConcludedOn,
        ConcludedById = b.ConcludedById,
        CancellationReason = b.CancellationReason,
        CancelledOn = b.CancelledOn,
        CancelledById = b.CancelledById,
        WasDissolved = b.Status == MedicalBoardStatus.Cancelled && b.ConvenedOn is not null,
        Members = b.Members?.Where(m => !m.IsDeleted).Select(ToDto).ToList() ?? new(),
        Sittings = b.Sittings?.Where(s => !s.IsDeleted).OrderBy(s => s.SittingDate).Select(ToDto).ToList() ?? new()
    };

    private static MedicalBoardMemberDto ToDto(MedicalBoardMember m) => new()
    {
        Id = m.Id,
        BoardId = m.BoardId,
        PhysicianId = m.PhysicianId,
        EmployeeId = m.EmployeeId,
        // Registered identity wins over the typed name: if somebody is on file, the file is the
        // better answer, and MemberName may well be a stale spelling of it.
        DisplayName = m.Physician is not null
            ? $"{m.Physician.Title} {m.Physician.FirstName} {m.Physician.LastName}".Trim()
            : m.Employee is not null
                ? $"{m.Employee.FirstName} {m.Employee.LastName}".Trim()
                : m.MemberName ?? string.Empty,
        MemberKind = m.PhysicianId is not null ? "Physician"
                   : m.EmployeeId is not null ? "Employee"
                   : "External",
        Institution = m.Institution,
        Role = m.Role
    };

    private static MedicalBoardDocumentDto ToDto(MedicalBoardDocument d) => new()
    {
        Id = d.Id,
        BoardId = d.BoardId,
        FileName = d.FileName,
        FileSize = d.FileSize,
        Description = d.Description,
        UploadDate = d.UploadDate,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy is null ? null : $"{d.UploadedBy.FirstName} {d.UploadedBy.LastName}".Trim()
    };

    private static MedicalBoardSittingDto ToDto(MedicalBoardSitting s) => new()
    {
        Id = s.Id,
        BoardId = s.BoardId,
        SittingDate = s.SittingDate,
        Venue = s.Venue,
        Notes = s.Notes
    };
}
