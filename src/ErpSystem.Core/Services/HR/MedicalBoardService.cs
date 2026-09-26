using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="IMedicalBoardService"/>
public class MedicalBoardService : IMedicalBoardService
{
    private readonly IGenericRepository<MedicalBoard> _boards;
    private readonly IGenericRepository<MedicalBoardCase> _cases;
    private readonly IGenericRepository<MedicalBoardMember> _members;
    private readonly IGenericRepository<MedicalBoardSitting> _sittings;
    private readonly IGenericRepository<MedicalBoardSittingAttendance> _attendance;
    private readonly IGenericRepository<MedicalBoardDocument> _documents;
    private readonly IGenericRepository<Employee> _employees;
    private readonly IGenericRepository<Physician> _physicians;
    private readonly IGenericRepository<HealthcareFacility> _facilities;
    private readonly IGenericRepository<EmployeeHealthProfile> _profiles;
    private readonly IGenericRepository<EmployeeMedicalExam> _exams;
    private readonly ICompanyHrPolicyProvider _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<MedicalBoardService> _logger;

    public MedicalBoardService(
        IGenericRepository<MedicalBoard> boards,
        IGenericRepository<MedicalBoardCase> cases,
        IGenericRepository<MedicalBoardMember> members,
        IGenericRepository<MedicalBoardSitting> sittings,
        IGenericRepository<MedicalBoardSittingAttendance> attendance,
        IGenericRepository<MedicalBoardDocument> documents,
        IGenericRepository<Employee> employees,
        IGenericRepository<Physician> physicians,
        IGenericRepository<HealthcareFacility> facilities,
        IGenericRepository<EmployeeHealthProfile> profiles,
        IGenericRepository<EmployeeMedicalExam> exams,
        ICompanyHrPolicyProvider policy,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider clock,
        ILogger<MedicalBoardService> logger)
    {
        _boards = boards;
        _cases = cases;
        _members = members;
        _sittings = sittings;
        _attendance = attendance;
        _documents = documents;
        _employees = employees;
        _physicians = physicians;
        _facilities = facilities;
        _profiles = profiles;
        _exams = exams;
        _policy = policy;
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

    /// <summary>
    /// A board with everything a rule needs. ⚠ The soft-delete filter reaches Includes too, so
    /// <c>Members</c> here is the CURRENT panel. Who sat at a past sitting comes from
    /// <see cref="EveryMemberAsync"/>.
    /// </summary>
    private IQueryable<MedicalBoard> BoardGraph(Guid tenantId) => _boards.GetQueryable()
        .Include(b => b.Facility)
        .Include(b => b.Cases).ThenInclude(c => c.Employee)
        .Include(b => b.Cases).ThenInclude(c => c.BasedOnExam)
        .Include(b => b.Members).ThenInclude(m => m.Physician)
        .Include(b => b.Members).ThenInclude(m => m.Employee)
        .Include(b => b.Sittings).ThenInclude(s => s.Attendance)
        .AsSplitQuery()
        .Where(b => b.TenantId == tenantId);

    /// <summary>
    /// Every member ever seated on these boards, removed ones included: attendance names who SAT, not
    /// who sits now, and a member removed after deciding a case still decided it.
    /// </summary>
    /// <remarks>
    /// ⚠ Untracked on purpose. Loaded tracked, the soft-deleted members would be fixed up into the
    /// tracked board's <c>Members</c> and reappear as current (the EF soft-delete fixup trap).
    /// </remarks>
    private async Task<Dictionary<Guid, MedicalBoardMember>> EveryMemberAsync(
        IReadOnlyCollection<Guid> boardIds, Guid tenantId, CancellationToken ct)
        => await _members.GetQueryableIncludingDeleted(m => boardIds.Contains(m.BoardId) && m.TenantId == tenantId)
            .AsNoTracking()
            .Include(m => m.Physician)
            .Include(m => m.Employee)
            .ToDictionaryAsync(m => m.Id, ct);

    /// <summary>A board owned by another tenant is reported as missing, not as forbidden.</summary>
    private async Task<MedicalBoard> RequireBoardAsync(Guid id, CancellationToken ct)
        => await BoardGraph(GetTenantId()).FirstOrDefaultAsync(b => b.Id == id, ct)
           ?? throw new ArgumentException($"Medical board '{id}' not found.");

    public async Task<PagedResult<MedicalBoardDto>> GetBoardsAsync(
        MedicalBoardFilterDto filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BoardGraph(tenantId);

        if (filter.EmployeeId is Guid employeeId)
            query = query.Where(b => b.Cases.Any(c => !c.IsDeleted && c.EmployeeId == employeeId));
        if (filter.Status is MedicalBoardStatus status) query = query.Where(b => b.Status == status);
        if (filter.Purpose is MedicalBoardPurpose purpose)
            query = query.Where(b => b.Cases.Any(c => !c.IsDeleted && c.Purpose == purpose));
        if (filter.Kind is MedicalBoardKind kind) query = query.Where(b => b.Kind == kind);
        if (filter.From is DateOnly from) query = query.Where(b => b.RequestedOn >= from);
        if (filter.To is DateOnly to) query = query.Where(b => b.RequestedOn <= to);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(b =>
                b.BoardNumber.Contains(term) ||
                b.Cases.Any(c => !c.IsDeleted && (
                    c.Reason.Contains(term) ||
                    c.Employee.FirstName.Contains(term) ||
                    c.Employee.LastName.Contains(term))));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(b => b.RequestedOn)
            .ThenByDescending(b => b.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var everyMember = await EveryMemberAsync(rows.Select(b => b.Id).ToList(), tenantId, ct);
        var dtos = rows.Select(b => ToDto(b, everyMember)).ToList();
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
        var board = await BoardGraph(tenantId).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (board is null) return null;

        var dto = ToDto(board, await EveryMemberAsync(new[] { board.Id }, tenantId, ct));
        await FillActorNamesAsync(new[] { dto }, tenantId, ct);
        return dto;
    }

    // ── Asking for a board, and the cases before it ──────────────────────────────────────────

    public async Task<MedicalBoardDto> RequestBoardAsync(RequestMedicalBoardDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        var kind = dto.Kind ?? MedicalBoardKind.Employer;
        if (!Enum.IsDefined(kind))
            throw new InvalidOperationException("Say who convenes the board: the employer, or one of the statutory boards.");

        if (dto.FacilityId is Guid facilityId)
        {
            var known = await _facilities.GetQueryable()
                .AnyAsync(f => f.Id == facilityId && f.TenantId == tenantId, ct);
            if (!known)
                throw new ArgumentException($"Healthcare facility '{facilityId}' not found.");
        }

        var first = await ValidateCaseAsync(
            tenantId, board: null, dto.EmployeeId, dto.Purpose, dto.Reason, dto.HealthProfileId, dto.BasedOnExamId, ct);

        var board = new MedicalBoard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            // ⚠ Timestamped, not counted — see the entity. A counted sequence repeats after a soft
            // delete, and a sequential board number would be enumerable.
            BoardNumber = $"MB-{_clock.UtcNow:yyyyMMddHHmmssfff}",
            Kind = kind,
            Status = MedicalBoardStatus.Requested,
            RequestedById = _currentUserService.EmployeeId,
            RequestedOn = _clock.TodayUtc,
            FacilityId = dto.FacilityId
        };
        first.BoardId = board.Id;

        await _boards.AddAsync(board);
        await _cases.AddAsync(first);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Medical board {number} requested, with a case for employee {employee}",
            board.BoardNumber, first.EmployeeId);

        return (await GetBoardAsync(board.Id, ct))!;
    }

    public async Task<MedicalBoardCaseDto> AddCaseAsync(Guid boardId, AddMedicalBoardCaseDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardAsync(boardId, ct);
        RefuseIfSettled(board, "list a case before");

        var entity = await ValidateCaseAsync(
            tenantId, board, dto.EmployeeId, dto.Purpose, dto.Reason, dto.HealthProfileId, dto.BasedOnExamId, ct);
        entity.BoardId = board.Id;

        await _cases.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        var read = (await GetBoardAsync(boardId, ct))!;
        return read.Cases.First(c => c.Id == entity.Id);
    }

    /// <summary>
    /// The rules a case meets wherever it is listed — the first with its board, or a later one.
    /// </summary>
    private async Task<MedicalBoardCase> ValidateCaseAsync(
        Guid tenantId, MedicalBoard? board, Guid employeeId, MedicalBoardPurpose? purpose, string? reason,
        Guid? healthProfileId, Guid? basedOnExamId, CancellationToken ct)
    {
        var employee = await _employees.GetQueryable()
            .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId, ct);
        if (!employee)
            throw new ArgumentException($"Employee '{employeeId}' not found.");

        // ⚠ Round 5, lane K1. Nullable in the DTO so that leaving it out lands HERE, worded, rather
        // than binding to 0; and an undefined number is refused the same way.
        if (purpose is not MedicalBoardPurpose defined || !Enum.IsDefined(defined))
            throw new InvalidOperationException(
                "Say what the board is for: extended sick leave, an injury on duty, fitness for duty, "
                + "medical retirement, or other. Leave reads it to decide whether the board can stand "
                + "as evidence for an absence.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "Say why a board is being asked for. A board convened without a stated question is one "
                + "nobody can tell whether it answered.");

        if (board is not null)
        {
            // One employee, one case, per board — the unique index says the same. A withdrawn case
            // still holds the place: its withdrawal is part of this board's record.
            var existing = board.Cases.FirstOrDefault(c => !c.IsDeleted && c.EmployeeId == employeeId);
            if (existing is not null)
                throw new InvalidOperationException(existing.Status == MedicalBoardCaseStatus.Withdrawn
                    ? "This employee's case was withdrawn from this board. A new question about them is a "
                      + "case before a new board."
                    : "This employee already has a case before this board. A finding that needs revisiting "
                      + "is a case before a new board.");

            // ⚠ Nobody sits in judgement on their own fitness — in either order.
            if (board.Members.Any(m => !m.IsDeleted && m.EmployeeId == employeeId))
                throw new InvalidOperationException(
                    "This employee sits on this board, so their case cannot be heard by it. Remove them "
                    + "from the board first, or ask another board.");
        }

        var (profileId, examId) = await ResolveReferencesAsync(employeeId, healthProfileId, basedOnExamId, tenantId, ct);

        return new MedicalBoardCase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            Purpose = defined,
            Reason = reason!.Trim(),
            RequestedById = _currentUserService.EmployeeId,
            RequestedOn = _clock.TodayUtc,
            HealthProfileId = profileId,
            BasedOnExamId = examId,
            Status = MedicalBoardCaseStatus.Listed
        };
    }

    /// <summary>
    /// Checks the health profile and the examination a case is asked with, and fills the profile in
    /// where it follows from them (round 5, lane K3).
    /// </summary>
    /// <remarks>
    /// <para>⚠ Before lane K3 these were stored as given. A mistyped id failed the foreign key as a
    /// 500, and — worse, because nothing failed — another employee's examination could be named as
    /// the one a finding was based on, putting somebody else's clinical record behind it.</para>
    ///
    /// <para>Not-found is an <see cref="ArgumentException"/> (404), as the employee and the physician
    /// already are. Somebody else's record is a refusal that says so (400).</para>
    /// </remarks>
    private async Task<(Guid? HealthProfileId, Guid? ExamId)> ResolveReferencesAsync(
        Guid employeeId, Guid? healthProfileId, Guid? basedOnExamId, Guid tenantId, CancellationToken ct)
    {
        Guid? profileId;

        if (basedOnExamId is Guid examId)
        {
            var exam = await _exams.GetQueryable()
                .Where(e => e.Id == examId && e.TenantId == tenantId)
                .Select(e => new { e.HealthProfileId, e.HealthProfile.EmployeeId })
                .FirstOrDefaultAsync(ct);
            if (exam is null)
                throw new ArgumentException($"Medical examination '{examId}' not found.");

            if (exam.EmployeeId != employeeId)
                throw new InvalidOperationException(
                    "That examination is another employee's. A board can only be based on an examination "
                    + "of the person it is about.");

            if (healthProfileId is Guid named && named != exam.HealthProfileId)
                throw new InvalidOperationException(
                    "That examination is recorded on a different health profile from the one named. Name "
                    + "the examination alone and its profile follows from it.");

            profileId = exam.HealthProfileId;
        }
        else if (healthProfileId is Guid namedProfileId)
        {
            var profile = await _profiles.GetQueryable()
                .Where(p => p.Id == namedProfileId && p.TenantId == tenantId)
                .Select(p => new { p.EmployeeId })
                .FirstOrDefaultAsync(ct);
            if (profile is null)
                throw new ArgumentException($"Health profile '{namedProfileId}' not found.");

            if (profile.EmployeeId != employeeId)
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
                .Where(p => p.EmployeeId == employeeId && p.TenantId == tenantId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        return (profileId, basedOnExamId);
    }

    // ── Deciding a case, or withdrawing it ───────────────────────────────────────────────────

    /// <summary>Deciding members: chair and member. Secretary and observer attend without deciding.</summary>
    private static bool Decides(MedicalBoardMemberRole role) =>
        role is MedicalBoardMemberRole.Chair or MedicalBoardMemberRole.Member;

    public async Task<MedicalBoardDto> ConcludeCaseAsync(
        Guid boardId, Guid caseId, ConcludeMedicalBoardCaseDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardAsync(boardId, ct);
        var @case = RequireCase(board, caseId);

        if (@case.Status != MedicalBoardCaseStatus.Listed)
            throw new InvalidOperationException(@case.Status == MedicalBoardCaseStatus.Concluded
                ? "This case has already been decided. A finding that needs revisiting is a case before a new board."
                : "This case was withdrawn, so it cannot be decided.");

        if (board.Status != MedicalBoardStatus.Convened)
            throw new InvalidOperationException(
                $"A board must be convened before it decides a case. This one is {board.Status.ToString().ToLowerInvariant()}.");

        // ⚠ Round 5, lane K7. Nullable so that leaving it out is refused rather than bound to 0.
        if (dto.Outcome is not MedicalExamResult outcome || !Enum.IsDefined(outcome))
            throw new InvalidOperationException(
                "Give the board's finding: fit, fit with restrictions, temporarily unfit, unfit, or "
                + "requires further investigation. A case decided without one has not been decided.");

        if (string.IsNullOrWhiteSpace(dto.Recommendation))
            throw new InvalidOperationException(
                "A board reports by recommending something. Say what it recommends.");

        // ⚠ Lane K-II-a: decided AT a sitting. A board that never met cannot have reached a finding,
        // and naming the sitting is what makes "who decided" answerable.
        if (!board.Sittings.Any(s => !s.IsDeleted))
            throw new InvalidOperationException(
                "Record at least one sitting before the board decides a case. A board that never met cannot "
                + "have reached a finding.");

        if (dto.SittingId is not Guid sittingId)
            throw new InvalidOperationException(
                "Say at which sitting the case was decided: its attendance is the panel that decided it.");

        var sitting = board.Sittings.FirstOrDefault(s => s.Id == sittingId && !s.IsDeleted)
            ?? throw new InvalidOperationException("That sitting is not one of this board's.");

        // The quorum: deciding members present. Members removed since still count — they were there.
        var present = sitting.Attendance.Where(a => !a.IsDeleted).Select(a => a.MemberId).ToHashSet();
        if (present.Count == 0)
            throw new InvalidOperationException(
                $"Record who was present at the sitting of {sitting.SittingDate:d MMM yyyy} first. A finding "
                + "nobody is recorded as having made rests on nothing.");

        var everyMember = await EveryMemberAsync(new[] { board.Id }, tenantId, ct);
        var deciding = present.Count(id => everyMember.TryGetValue(id, out var m) && Decides(m.Role));
        var quorum = Math.Max(1, (await _policy.GetAsync(ct)).MedicalBoardQuorum);
        if (deciding < quorum)
            throw new InvalidOperationException(
                $"The board needs {quorum} deciding member(s) — a chair or a member — present where it decides "
                + $"a case; the sitting of {sitting.SittingDate:d MMM yyyy} had {deciding}.");

        @case.Status = MedicalBoardCaseStatus.Concluded;
        @case.DecidedAtSittingId = sitting.Id;
        @case.Outcome = outcome;
        @case.Findings = dto.Findings?.Trim();
        @case.Recommendation = dto.Recommendation.Trim();
        @case.Restrictions = dto.Restrictions?.Trim();
        @case.ReviewDueDate = dto.ReviewDueDate;
        @case.RecommendsMedicalRetirement = dto.RecommendsMedicalRetirement;
        @case.ConcludedOn = _clock.TodayUtc;
        @case.ConcludedById = _currentUserService.EmployeeId;

        CloseIfDone(board);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Medical board {number} decided the case of employee {employee}: {outcome}{retirement}",
            board.BoardNumber, @case.EmployeeId, @case.Outcome,
            @case.RecommendsMedicalRetirement ? ", recommending medical retirement" : string.Empty);

        return (await GetBoardAsync(boardId, ct))!;
    }

    public async Task<MedicalBoardDto> WithdrawCaseAsync(Guid boardId, Guid caseId, string reason, CancellationToken ct = default)
    {
        var board = await RequireBoardAsync(boardId, ct);
        RefuseIfSettled(board, "withdraw a case from");
        var @case = RequireCase(board, caseId);

        if (@case.Status != MedicalBoardCaseStatus.Listed)
            throw new InvalidOperationException(@case.Status == MedicalBoardCaseStatus.Concluded
                ? "This case has been decided, so it cannot be withdrawn."
                : "This case is already withdrawn.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Say why the case is being withdrawn.");

        Withdraw(@case, reason.Trim());
        CloseIfDone(board);

        await _unitOfWork.SaveChangesAsync(ct);
        return (await GetBoardAsync(boardId, ct))!;
    }

    private static MedicalBoardCase RequireCase(MedicalBoard board, Guid caseId)
        => board.Cases.FirstOrDefault(c => c.Id == caseId && !c.IsDeleted)
           ?? throw new ArgumentException($"Case '{caseId}' is not before medical board {board.BoardNumber}.");

    private void Withdraw(MedicalBoardCase @case, string reason)
    {
        @case.Status = MedicalBoardCaseStatus.Withdrawn;
        @case.WithdrawnOn = _clock.TodayUtc;
        @case.WithdrawnById = _currentUserService.EmployeeId;
        @case.WithdrawalReason = reason.Length <= 1000 ? reason : reason[..1000];
    }

    /// <summary>
    /// The board reports when no case is left open and at least one was decided.
    /// </summary>
    /// <remarks>
    /// ⚠ All withdrawn and none decided is NOT reporting: nothing was decided, so the board stays open
    /// and HR cancels or dissolves it, saying why — a Concluded board with no finding would read as one
    /// that ruled.
    /// </remarks>
    private void CloseIfDone(MedicalBoard board)
    {
        var live = board.Cases.Where(c => !c.IsDeleted).ToList();
        if (live.Any(c => c.Status == MedicalBoardCaseStatus.Listed)) return;
        if (!live.Any(c => c.Status == MedicalBoardCaseStatus.Concluded)) return;

        board.Status = MedicalBoardStatus.Concluded;
        board.ConcludedOn = _clock.TodayUtc;
    }

    // ── The panel ────────────────────────────────────────────────────────────────────────────

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

        if (!Enum.IsDefined(dto.Role))
            throw new InvalidOperationException("Say what the member does on the board: chair, member, secretary or observer.");

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
            // fitness. A board listing a subject among its members would discredit its findings —
            // and those findings are what leave and separation then rest on.
            if (board.Cases.Any(c => !c.IsDeleted && c.Status != MedicalBoardCaseStatus.Withdrawn
                                     && c.EmployeeId == memberEmployeeId))
                throw new InvalidOperationException(
                    "This employee's case is before this board, so they cannot sit on it. If they are to be "
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

        // ⚠ A soft delete: attendance at past sittings keeps naming them — it records who sat.
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

        if (!board.Cases.Any(c => !c.IsDeleted && c.Status == MedicalBoardCaseStatus.Listed))
            throw new InvalidOperationException(
                "List at least one case before convening. A board with nobody before it has nothing to sit on.");

        board.Status = MedicalBoardStatus.Convened;
        board.ConvenedOn = _clock.TodayUtc;

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

        var attendees = ValidateAttendees(board, dto.AttendeeMemberIds ?? new List<Guid>());

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
        foreach (var memberId in attendees)
            await _attendance.AddAsync(new MedicalBoardSittingAttendance
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SittingId = sitting.Id, MemberId = memberId
            });
        await _unitOfWork.SaveChangesAsync(ct);

        var read = (await GetBoardAsync(boardId, ct))!;
        return read.Sittings.First(s => s.Id == sitting.Id);
    }

    public async Task<MedicalBoardSittingDto> SetSittingAttendanceAsync(
        Guid boardId, Guid sittingId, SetMedicalBoardSittingAttendanceDto dto, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var board = await RequireBoardAsync(boardId, ct);

        RefuseIfSettled(board, "change the attendance of");

        var sitting = board.Sittings.FirstOrDefault(s => s.Id == sittingId && !s.IsDeleted)
            ?? throw new ArgumentException($"Sitting '{sittingId}' is not one of medical board {board.BoardNumber}'s.");

        // ⚠ Once a case was decided at this sitting, its attendance IS the panel that decided it.
        if (board.Cases.Any(c => !c.IsDeleted && c.DecidedAtSittingId == sittingId))
            throw new InvalidOperationException(
                "A case was decided at this sitting, so who was present is fixed: it is the panel that "
                + "decided. Record a new sitting for anything decided later.");

        var attendees = ValidateAttendees(board, dto.MemberIds ?? new List<Guid>());

        var current = await _attendance.GetQueryable()
            .Where(a => a.SittingId == sittingId && a.TenantId == tenantId)
            .ToListAsync(ct);
        foreach (var row in current.Where(r => !attendees.Contains(r.MemberId)))
            await _attendance.DeleteAsync(row);
        foreach (var memberId in attendees.Where(id => current.All(r => r.MemberId != id)))
            await _attendance.AddAsync(new MedicalBoardSittingAttendance
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SittingId = sittingId, MemberId = memberId
            });
        await _unitOfWork.SaveChangesAsync(ct);

        var read = (await GetBoardAsync(boardId, ct))!;
        return read.Sittings.First(s => s.Id == sittingId);
    }

    /// <summary>Attendees must be the board's current members, each once.</summary>
    private static HashSet<Guid> ValidateAttendees(MedicalBoard board, IEnumerable<Guid> memberIds)
    {
        var ids = memberIds.ToHashSet();
        var seated = board.Members.Where(m => !m.IsDeleted).Select(m => m.Id).ToHashSet();
        if (ids.Any(id => !seated.Contains(id)))
            throw new InvalidOperationException(
                "Only members of this board can be recorded as present. Appoint them first.");
        return ids;
    }

    /// <remarks>
    /// ⚠ Round 5, lane K5: one status, two words. Stopped while only Requested, nobody was ever
    /// appointed as a panel — the REQUEST is cancelled. Stopped once Convened, a panel existed and is
    /// DISSOLVED; its members and sittings stay on the record, because they happened. Either way its
    /// open cases are withdrawn, with the board's reason (lane K-II-a).
    /// </remarks>
    public async Task<MedicalBoardDto> CancelAsync(Guid boardId, string reason, CancellationToken ct = default)
    {
        var board = await RequireBoardAsync(boardId, ct);

        if (board.Status == MedicalBoardStatus.Concluded)
            throw new InvalidOperationException(
                "This board has reported and cannot be cancelled or dissolved. Leave and separation may "
                + "already be resting on its findings.");

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

        foreach (var open in board.Cases.Where(c => !c.IsDeleted && c.Status == MedicalBoardCaseStatus.Listed))
            Withdraw(open, $"{(dissolving ? "The board was dissolved" : "The request for a board was cancelled")}: {reason.Trim()}");

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

        var caseNames = await CaseEmployeeNamesAsync(boardId, tenantId, ct);
        return rows.Select(d => ToDto(d, caseNames)).ToList();
    }

    public async Task EnsureCaseOnBoardAsync(Guid boardId, Guid caseId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var onBoard = await _cases.GetQueryable()
            .AnyAsync(c => c.Id == caseId && c.BoardId == boardId && c.TenantId == tenantId, ct);
        if (!onBoard)
            throw new InvalidOperationException("That case is not before this board.");
    }

    public async Task<MedicalBoardDocumentDto> AddDocumentAsync(
        Guid boardId, Guid? caseId, Guid uploadedById, string fileName, long? fileSize, string? description,
        Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();

        // ⚠ No status check, deliberately: the signed report arrives after the board has concluded,
        // and the letter standing a board down after it was dissolved. Adding a paper rewrites nothing.
        await RequireBoardHeaderAsync(boardId, tenantId, ct);
        if (caseId is Guid onCase)
            await EnsureCaseOnBoardAsync(boardId, onCase, ct);

        var document = new MedicalBoardDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BoardId = boardId,
            CaseId = caseId,
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

        return ToDto(saved, await CaseEmployeeNamesAsync(boardId, tenantId, ct));
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

    private async Task<Dictionary<Guid, string>> CaseEmployeeNamesAsync(Guid boardId, Guid tenantId, CancellationToken ct)
        => (await _cases.GetQueryable()
                .Where(c => c.BoardId == boardId && c.TenantId == tenantId)
                .Select(c => new { c.Id, c.Employee.FirstName, c.Employee.LastName })
                .ToListAsync(ct))
            .ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());

    /// <summary>
    /// Refuses a change to a board that has finished, whichever way it finished.
    /// </summary>
    /// <remarks>
    /// ⚠ The membership, the sittings and the cases are part of what the findings MEAN. Editing them
    /// after the board has reported would rewrite who decided and on what — while a leave request
    /// approved on a finding stays approved. Hence one guard, used by every mutating path.
    /// </remarks>
    private static void RefuseIfSettled(MedicalBoard board, string what)
    {
        if (board.Status == MedicalBoardStatus.Concluded)
            throw new InvalidOperationException(
                $"This board has reported, so you cannot {what} it. Its membership and sittings are "
                + "part of what its findings mean.");

        if (board.Status == MedicalBoardStatus.Cancelled)
            throw new InvalidOperationException(board.ConvenedOn is null
                ? $"This request for a board was cancelled, so you cannot {what} it."
                : $"This board was dissolved, so you cannot {what} it.");
    }

    /// <summary>
    /// Resolves the bare actor columns — the board's and every case's — to names, in one query.
    /// </summary>
    /// <remarks>
    /// They carry no navigation — an unpaired one to <c>Employee</c> mints a shadow
    /// <c>EmployeeId1</c> column. Same treatment leave gives <c>RescheduledById</c> and <c>RecalledById</c>.
    /// </remarks>
    private async Task FillActorNamesAsync(IEnumerable<MedicalBoardDto> dtos, Guid tenantId, CancellationToken ct)
    {
        var list = dtos as ICollection<MedicalBoardDto> ?? dtos.ToList();

        var ids = list.SelectMany(d => new[] { d.RequestedById, d.CancelledById }
                .Concat(d.Cases.SelectMany(c => new[] { c.RequestedById, c.ConcludedById, c.WithdrawnById })))
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
            dto.CancelledByName = NameOf(dto.CancelledById);
            foreach (var c in dto.Cases)
            {
                c.RequestedByName = NameOf(c.RequestedById);
                c.ConcludedByName = NameOf(c.ConcludedById);
                c.WithdrawnByName = NameOf(c.WithdrawnById);
            }
        }
    }

    // ── Mapping ──────────────────────────────────────────────────────────────────────────────

    private static string MemberName(MedicalBoardMember m) =>
        // Registered identity wins over the typed name: if somebody is on file, the file is the
        // better answer, and MemberName may well be a stale spelling of it.
        m.Physician is not null
            ? $"{m.Physician.Title} {m.Physician.FirstName} {m.Physician.LastName}".Trim()
            : m.Employee is not null
                ? $"{m.Employee.FirstName} {m.Employee.LastName}".Trim()
                : m.MemberName ?? string.Empty;

    private static MedicalBoardDto ToDto(MedicalBoard b, IReadOnlyDictionary<Guid, MedicalBoardMember> everyMember)
    {
        var sittings = (b.Sittings ?? new List<MedicalBoardSitting>()).Where(s => !s.IsDeleted).ToList();
        var cases = (b.Cases ?? new List<MedicalBoardCase>()).Where(c => !c.IsDeleted).ToList();

        List<MedicalBoardAttendeeDto> AttendeesOf(MedicalBoardSitting s) => s.Attendance
            .Where(a => !a.IsDeleted && everyMember.ContainsKey(a.MemberId))
            .Select(a => everyMember[a.MemberId])
            .OrderBy(m => m.Role)
            .Select(m => new MedicalBoardAttendeeDto
            {
                MemberId = m.Id, DisplayName = MemberName(m), Role = m.Role, Decides = Decides(m.Role)
            })
            .ToList();

        return new MedicalBoardDto
        {
            Id = b.Id,
            BoardNumber = b.BoardNumber,
            Kind = b.Kind,
            Status = b.Status,
            RequestedById = b.RequestedById,
            RequestedOn = b.RequestedOn,
            ConvenedOn = b.ConvenedOn,
            FacilityId = b.FacilityId,
            FacilityName = b.Facility?.FacilityName,
            ConcludedOn = b.ConcludedOn,
            CancellationReason = b.CancellationReason,
            CancelledOn = b.CancelledOn,
            CancelledById = b.CancelledById,
            WasDissolved = b.Status == MedicalBoardStatus.Cancelled && b.ConvenedOn is not null,
            Cases = cases
                .OrderBy(c => c.RequestedOn).ThenBy(c => c.CreatedAt)
                .Select(c =>
                {
                    var decidedAt = c.DecidedAtSittingId is Guid sid ? sittings.FirstOrDefault(s => s.Id == sid) : null;
                    return new MedicalBoardCaseDto
                    {
                        Id = c.Id,
                        BoardId = b.Id,
                        BoardNumber = b.BoardNumber,
                        EmployeeId = c.EmployeeId,
                        EmployeeName = c.Employee is null ? string.Empty : $"{c.Employee.FirstName} {c.Employee.LastName}".Trim(),
                        EmployeeNumber = c.Employee?.EmployeeNumber ?? string.Empty,
                        Purpose = c.Purpose,
                        CoversAbsence = MedicalBoardCase.CoversAbsence(c.Purpose),
                        Reason = c.Reason,
                        RequestedById = c.RequestedById,
                        RequestedOn = c.RequestedOn,
                        HealthProfileId = c.HealthProfileId,
                        BasedOnExamId = c.BasedOnExamId,
                        BasedOnExamDate = c.BasedOnExam?.ExamDate,
                        BasedOnExamResult = c.BasedOnExam?.Result,
                        Status = c.Status,
                        DecidedAtSittingId = c.DecidedAtSittingId,
                        DecidedAtSittingDate = decidedAt?.SittingDate,
                        DecidedBy = decidedAt is null ? new List<string>() : AttendeesOf(decidedAt).Select(a => a.DisplayName).ToList(),
                        Outcome = c.Outcome,
                        Findings = c.Findings,
                        Recommendation = c.Recommendation,
                        Restrictions = c.Restrictions,
                        ReviewDueDate = c.ReviewDueDate,
                        RecommendsMedicalRetirement = c.RecommendsMedicalRetirement,
                        ConcludedOn = c.ConcludedOn,
                        ConcludedById = c.ConcludedById,
                        WithdrawnOn = c.WithdrawnOn,
                        WithdrawnById = c.WithdrawnById,
                        WithdrawalReason = c.WithdrawalReason
                    };
                })
                .ToList(),
            Members = (b.Members ?? new List<MedicalBoardMember>()).Where(m => !m.IsDeleted).Select(ToDto).ToList(),
            Sittings = sittings
                .OrderBy(s => s.SittingDate).ThenBy(s => s.CreatedAt)
                .Select(s => new MedicalBoardSittingDto
                {
                    Id = s.Id,
                    BoardId = s.BoardId,
                    SittingDate = s.SittingDate,
                    Venue = s.Venue,
                    Notes = s.Notes,
                    Attendees = AttendeesOf(s),
                    CasesDecided = cases.Count(c => c.DecidedAtSittingId == s.Id)
                })
                .ToList()
        };
    }

    private static MedicalBoardMemberDto ToDto(MedicalBoardMember m) => new()
    {
        Id = m.Id,
        BoardId = m.BoardId,
        PhysicianId = m.PhysicianId,
        EmployeeId = m.EmployeeId,
        DisplayName = MemberName(m),
        MemberKind = m.PhysicianId is not null ? "Physician"
                   : m.EmployeeId is not null ? "Employee"
                   : "External",
        Institution = m.Institution,
        Role = m.Role,
        Decides = Decides(m.Role)
    };

    private static MedicalBoardDocumentDto ToDto(MedicalBoardDocument d, IReadOnlyDictionary<Guid, string> caseNames) => new()
    {
        Id = d.Id,
        BoardId = d.BoardId,
        CaseId = d.CaseId,
        CaseEmployeeName = d.CaseId is Guid c && caseNames.TryGetValue(c, out var name) ? name : null,
        FileName = d.FileName,
        FileSize = d.FileSize,
        Description = d.Description,
        UploadDate = d.UploadDate,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy is null ? null : $"{d.UploadedBy.FirstName} {d.UploadedBy.LastName}".Trim()
    };
}
