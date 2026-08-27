using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffGrievance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// FR-HR-181 — the grievance ladder. See <see cref="IStaffGrievanceService"/> for why this is not on
/// the workflow engine and why the rungs are not resolved to people.
/// </summary>
public class StaffGrievanceService : IStaffGrievanceService
{
    /// <summary>The top of FR-HR-181's ladder. Nothing escalates past the Board.</summary>
    private const GrievanceEscalationLevel TopLevel = GrievanceEscalationLevel.Board;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<StaffGrievanceService> _logger;

    public StaffGrievanceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<StaffGrievanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private bool IsHr =>
        _currentUserProvider.HasRole(Constants.Roles.Hr)
        || _currentUserProvider.HasRole(Constants.Roles.SuperAdmin)
        || _currentUserProvider.HasRole("Admin");

    /// <summary>
    /// Every read starts here: tenant-scoped, soft-delete filtered, steps ordered, names loaded.
    /// The ordering is part of the contract — FR-HR-181's value is the trail read in sequence.
    /// </summary>
    private IQueryable<StaffGrievance> Scoped(Guid tenantId) =>
        _unitOfWork.Repository<StaffGrievance>()
            .GetQueryable()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Include(g => g.Employee)
            .Include(g => g.Steps.OrderBy(s => s.Sequence)).ThenInclude(s => s.AssignedTo)
            .Include(g => g.Steps).ThenInclude(s => s.RespondedBy)
            // Area 9c slice 1. Soft-deleted parties are filtered here rather than in the projection
            // so a removed row cannot reach ActivePartyCount by a route the DTO never sees. A party
            // who has STOOD DOWN is not soft-deleted — RemovedDate is set and the row stays visible.
            .Include(g => g.Parties.Where(p => !p.IsDeleted)).ThenInclude(p => p.Employee)
            .Include(g => g.Parties.Where(p => !p.IsDeleted)).ThenInclude(p => p.RepresentsEmployee)
            .Include(g => g.Parties.Where(p => !p.IsDeleted)).ThenInclude(p => p.Union)
            .Include(g => g.Parties.Where(p => !p.IsDeleted)).ThenInclude(p => p.AddedBy);

    /// <summary>
    /// The register's query — tenant-scoped and soft-delete filtered, with NO includes.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Scoped"/>. The paged register counts and pages before it
    /// projects, and dragging every step and party of every case across the wire to build one page
    /// of summaries is the shape that made the area-25 compliance roster a 2.27 MB response.
    /// </remarks>
    private IQueryable<StaffGrievance> RegisterQuery(Guid tenantId) =>
        _unitOfWork.Repository<StaffGrievance>()
            .GetQueryable()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted);

    private async Task<StaffGrievance> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var grievance = await Scoped(GetTenantId()).FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return grievance ?? throw new ArgumentException($"Grievance with ID '{id}' not found.");
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, string role)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != GetTenantId())
            throw new ArgumentException($"The {role} employee with ID '{employeeId}' was not found.");
        return employee;
    }

    /// <remarks>
    /// Numbers come from the highest suffix already issued, over rows INCLUDING soft-deleted ones —
    /// counting live rows re-issues a number the moment anything is deleted. The same shape was fixed
    /// across the SHE, movement and disciplinary-case generators.
    /// </remarks>
    private async Task<string> GenerateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"GRV-{DateTime.UtcNow.Year}-";

        var issued = await _unitOfWork.Repository<StaffGrievance>()
            .GetQueryableIncludingDeleted(g => g.TenantId == tenantId && g.GrievanceNumber.StartsWith(prefix))
            .Select(g => g.GrievanceNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(n => int.TryParse(n[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }

    private StaffGrievanceStep CurrentStep(StaffGrievance grievance)
        => grievance.Steps.OrderBy(s => s.Sequence).LastOrDefault()
           ?? throw new InvalidOperationException("This grievance has no escalation step, which should not be possible.");

    /// <summary>
    /// The read rule: the griever, HR, or someone named on one of the steps. A grievance is usually
    /// ABOUT somebody, so this is narrower than the rest of the module — being a manager, or being
    /// the person complained of, is not by itself a reason to read it.
    /// </summary>
    private void EnsureMayRead(StaffGrievance grievance, Guid? callerEmployeeId)
    {
        if (IsHr) return;
        if (callerEmployeeId is not Guid caller)
            throw new UnauthorizedAccessException("Your user account is not linked to an employee record.");
        if (grievance.EmployeeId == caller) return;
        if (grievance.Steps.Any(s => s.AssignedToId == caller || s.RespondedById == caller)) return;

        throw new UnauthorizedAccessException("This grievance is not yours to read.");
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<StaffGrievanceSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await Scoped(GetTenantId()).OrderByDescending(g => g.FiledDate).ToListAsync(cancellationToken))
            .Select(ToSummary).ToList();

    public async Task<IEnumerable<StaffGrievanceSummaryDto>> GetByStatusAsync(GrievanceStatus status, CancellationToken cancellationToken = default)
        => (await Scoped(GetTenantId()).Where(g => g.Status == status)
                .OrderByDescending(g => g.FiledDate).ToListAsync(cancellationToken))
            .Select(ToSummary).ToList();

    public async Task<PagedResult<StaffGrievanceSummaryDto>> GetPagedAsync(
        int page,
        int pageSize,
        EmployeeRelationsCaseType? caseType = null,
        GrievanceStatus? status = null,
        GrievanceEscalationLevel? level = null,
        Guid? organizationUnitId = null,
        bool? awaitingResponseOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        // A caller who asks for page 0 means page 1, and a caller who asks for everything does not
        // get everything — an unbounded page size is how a register becomes a 2.27 MB response.
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 25 : Math.Min(pageSize, 200);

        var query = RegisterQuery(GetTenantId());

        if (caseType.HasValue) query = query.Where(g => g.CaseType == caseType.Value);
        if (status.HasValue) query = query.Where(g => g.Status == status.Value);
        if (level.HasValue) query = query.Where(g => g.CurrentLevel == level.Value);

        // The primary party's unit. There is no unit on the case itself, and putting one there
        // would go stale the moment somebody transfers — the case belongs to whoever raised it.
        if (organizationUnitId.HasValue)
            query = query.Where(g => g.Employee.OrganizationUnitId == organizationUnitId.Value);

        // "Stuck" means the rung it currently sits at has not answered. Expressed against the
        // highest-sequence step so SQL can evaluate it, rather than the in-memory CurrentStep()
        // the unpaged read uses — the two must agree, and slice 1's harness asserts that they do.
        if (awaitingResponseOnly == true)
            query = query.Where(g => g.Steps.OrderByDescending(s => s.Sequence).First().Outcome
                                     == GrievanceStepOutcome.AwaitingResponse);
        else if (awaitingResponseOnly == false)
            query = query.Where(g => g.Steps.OrderByDescending(s => s.Sequence).First().Outcome
                                     != GrievanceStepOutcome.AwaitingResponse);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(g =>
                g.GrievanceNumber.Contains(term)
                || g.Subject.Contains(term)
                || g.Employee.FirstName.Contains(term)
                || g.Employee.LastName.Contains(term)
                || g.Employee.EmployeeNumber.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Projected lean and composed after materialisation: Employee.FullName is [NotMapped], so
        // it cannot cross into SQL, and asking for the whole Employee to read three strings would
        // undo the point of paging in the database.
        var rows = await query
            .OrderByDescending(g => g.FiledDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new
            {
                g.Id,
                g.GrievanceNumber,
                g.CaseType,
                g.EmployeeId,
                g.Employee.FirstName,
                g.Employee.MiddleName,
                g.Employee.LastName,
                g.Subject,
                g.FiledDate,
                g.Status,
                g.CurrentLevel,
                AwaitingResponse = g.Steps.OrderByDescending(s => s.Sequence).First().Outcome
                                   == GrievanceStepOutcome.AwaitingResponse,
                ActivePartyCount = g.Parties.Count(p => !p.IsDeleted && p.RemovedDate == null),
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffGrievanceSummaryDto>
        {
            Items = rows.Select(r => new StaffGrievanceSummaryDto
            {
                Id = r.Id,
                GrievanceNumber = r.GrievanceNumber,
                CaseType = r.CaseType,
                EmployeeId = r.EmployeeId,
                EmployeeName = string.IsNullOrEmpty(r.MiddleName)
                    ? $"{r.FirstName} {r.LastName}"
                    : $"{r.FirstName} {r.MiddleName} {r.LastName}",
                Subject = r.Subject,
                FiledDate = r.FiledDate,
                Status = r.Status,
                CurrentLevel = r.CurrentLevel,
                AwaitingResponse = r.AwaitingResponse,
                ActivePartyCount = r.ActivePartyCount,
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<IEnumerable<StaffGrievanceSummaryDto>> GetAwaitingResponseAsync(GrievanceEscalationLevel? level = null, CancellationToken cancellationToken = default)
    {
        var open = await Scoped(GetTenantId())
            .Where(g => g.Status == GrievanceStatus.Filed
                     || g.Status == GrievanceStatus.UnderReview
                     || g.Status == GrievanceStatus.Escalated)
            .OrderBy(g => g.FiledDate)
            .ToListAsync(cancellationToken);

        return open
            .Where(g => CurrentStep(g).Outcome == GrievanceStepOutcome.AwaitingResponse)
            .Where(g => level == null || g.CurrentLevel == level)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<IEnumerable<StaffGrievanceSummaryDto>> GetMineAsync(Guid me, CancellationToken cancellationToken = default)
    {
        return (await Scoped(GetTenantId()).Where(g => g.EmployeeId == me)
                .OrderByDescending(g => g.FiledDate).ToListAsync(cancellationToken))
            .Select(ToSummary).ToList();
    }

    public async Task<IEnumerable<StaffGrievanceSummaryDto>> GetAwaitingMyResponseAsync(Guid me, CancellationToken cancellationToken = default)
    {
        var open = await Scoped(GetTenantId())
            .Where(g => g.Status != GrievanceStatus.Resolved
                     && g.Status != GrievanceStatus.Withdrawn
                     && g.Status != GrievanceStatus.Closed)
            .ToListAsync(cancellationToken);

        return open
            .Where(g =>
            {
                var step = CurrentStep(g);
                return step.Outcome == GrievanceStepOutcome.AwaitingResponse && step.AssignedToId == me;
            })
            .Select(ToSummary)
            .ToList();
    }

    public async Task<StaffGrievanceDto> GetByIdAsync(Guid id, Guid? callerEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(id, cancellationToken);
        EnsureMayRead(grievance, callerEmployeeId);
        return ToDto(grievance);
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    public async Task<StaffGrievanceDto> FileAsync(FileGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedEmployeeAsync(grieverEmployeeId, "grieving");

        var now = DateTime.UtcNow;
        var grievance = new StaffGrievance
        {
            TenantId = tenantId,
            GrievanceNumber = await GenerateNumberAsync(tenantId, cancellationToken),
            EmployeeId = grieverEmployeeId,
            Subject = dto.Subject.Trim(),
            Statement = dto.Statement.Trim(),
            FiledDate = now,
            Status = GrievanceStatus.Filed,
            CurrentLevel = GrievanceEscalationLevel.Supervisor,
            CreatedBy = grieverEmployeeId.ToString(),
        };

        // The first rung exists from the moment the grievance is filed. A step is the record of who
        // owes an answer, so a grievance with no step would be one nobody is expected to deal with.
        grievance.Steps.Add(new StaffGrievanceStep
        {
            TenantId = tenantId,
            Level = GrievanceEscalationLevel.Supervisor,
            Sequence = 1,
            ReachedDate = now,
            Outcome = GrievanceStepOutcome.AwaitingResponse,
            CreatedBy = grieverEmployeeId.ToString(),
        });

        await _unitOfWork.Repository<StaffGrievance>().AddAsync(grievance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance filed: {Number}", grievance.GrievanceNumber);

        return ToDto(await GetOwnedAsync(grievance.Id, cancellationToken));
    }

    public async Task<StaffGrievanceDto> AssignCurrentStepAsync(Guid grievanceId, AssignGrievanceStepDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var assignee = await GetOwnedEmployeeAsync(dto.AssignedToId, "assigned");

        // Naming the griever as their own responder would let them answer their own grievance, which
        // the respond path refuses anyway — better to refuse it here, where the mistake is made.
        if (dto.AssignedToId == grievance.EmployeeId)
            throw new InvalidOperationException("The employee who raised a grievance cannot be assigned to answer it.");

        var step = CurrentStep(grievance);
        if (step.Outcome != GrievanceStepOutcome.AwaitingResponse)
            throw new InvalidOperationException("This level has already been answered.");

        step.AssignedToId = assignee.Id;
        step.UpdatedAt = DateTime.UtcNow;

        if (grievance.Status == GrievanceStatus.Filed)
            grievance.Status = GrievanceStatus.UnderReview;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> RespondAsync(Guid grievanceId, RespondToGrievanceDto dto, Guid responderEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var step = CurrentStep(grievance);
        if (step.Outcome != GrievanceStepOutcome.AwaitingResponse)
            throw new InvalidOperationException("This level has already been answered.");

        if (grievance.EmployeeId == responderEmployeeId)
            throw new UnauthorizedAccessException("You cannot answer your own grievance.");

        if (!IsHr && step.AssignedToId != responderEmployeeId)
            throw new UnauthorizedAccessException("You are not the person asked to answer this grievance at this level.");

        var now = DateTime.UtcNow;
        step.Response = dto.Response.Trim();
        step.RespondedById = responderEmployeeId;
        step.RespondedDate = now;
        // The step is Resolved either way: it means "this level has answered", not "the employee is
        // satisfied". It becomes Escalated only if the employee later says the answer did not settle
        // it — that is their judgement to make, and recording it here would pre-empt them.
        step.Outcome = GrievanceStepOutcome.Resolved;
        step.UpdatedAt = now;

        if (dto.ResolvesGrievance)
        {
            grievance.Status = GrievanceStatus.Resolved;
            grievance.ResolvedDate = now;
            grievance.ResolutionSummary = dto.Response.Trim();
        }
        else
        {
            // Answered, but the responder is not claiming it settles the matter. The grievance stays
            // at this rung with the answer on record and the employee decides what happens next.
            grievance.Status = GrievanceStatus.UnderReview;
        }

        grievance.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number} answered at {Level}", grievance.GrievanceNumber, step.Level);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> EscalateAsync(Guid grievanceId, EscalateGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        // Escalation is the employee saying the answer did not satisfy them. Nobody can say that for
        // them — not HR, not the responder — so this is refused to everyone else, as filing is.
        if (grievance.EmployeeId != grieverEmployeeId)
            throw new UnauthorizedAccessException("Only the employee who raised a grievance can escalate it.");

        var step = CurrentStep(grievance);
        if (step.Outcome == GrievanceStepOutcome.AwaitingResponse)
            throw new InvalidOperationException(
                $"{step.Level} has not answered yet. A grievance can only be escalated once the level it is with has responded.");

        if (grievance.CurrentLevel >= TopLevel)
            throw new InvalidOperationException(
                "This grievance is already with the Board, which is the final level in the escalation route.");

        var now = DateTime.UtcNow;
        step.Outcome = GrievanceStepOutcome.Escalated;
        step.UpdatedAt = now;

        var nextLevel = (GrievanceEscalationLevel)((int)grievance.CurrentLevel + 1);
        grievance.CurrentLevel = nextLevel;
        grievance.Status = GrievanceStatus.Escalated;
        grievance.UpdatedAt = now;

        // Append, never amend: FR-HR-181 requires each level's response be retained, so the rung below
        // keeps its answer and the new rung starts empty.
        //
        // ⚠ Added through the REPOSITORY, not through `grievance.Steps.Add(...)`. On an edit path the
        // grievance is already tracked, and adding to its navigation collection makes EF treat the new
        // step as Modified rather than Added — it issues an UPDATE against a row that does not exist
        // and SaveChanges throws DbUpdateConcurrencyException, "expected to affect 1 row, actually
        // affected 0". That is the trap in [[ef-tracked-graph-write-traps]], and it cost a run here.
        // FileAsync may use the collection because the whole graph is new there.
        var nextStep = new StaffGrievanceStep
        {
            TenantId = grievance.TenantId,
            GrievanceId = grievance.Id,
            Level = nextLevel,
            Sequence = grievance.Steps.Max(s => s.Sequence) + 1,
            ReachedDate = now,
            Outcome = GrievanceStepOutcome.AwaitingResponse,
            CreatedBy = grieverEmployeeId.ToString(),
        };
        await _unitOfWork.Repository<StaffGrievanceStep>().AddAsync(nextStep);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number} escalated to {Level}", grievance.GrievanceNumber, nextLevel);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> WithdrawAsync(Guid grievanceId, WithdrawGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        if (grievance.EmployeeId != grieverEmployeeId)
            throw new UnauthorizedAccessException("Only the employee who raised a grievance can withdraw it.");

        var now = DateTime.UtcNow;
        grievance.Status = GrievanceStatus.Withdrawn;
        grievance.WithdrawnDate = now;
        grievance.WithdrawalReason = dto.Reason.Trim();
        grievance.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    // ── Area 9c slice 1 — the wider employee-relations register ───────────────

    public async Task<StaffGrievanceDto> OpenCaseAsync(
        OpenEmployeeRelationsCaseDto dto, Guid openedByEmployeeId, CancellationToken cancellationToken = default)
    {
        // A grievance is the employee's own complaint. This method takes an explicit employee id,
        // so permitting Grievance here would be the raise-on-behalf-of that FileAsync's whole shape
        // exists to prevent — and it would be reachable by HR, which is exactly who must not have it.
        if (dto.CaseType == EmployeeRelationsCaseType.Grievance)
            throw new InvalidOperationException(
                "A grievance can only be raised by the employee it belongs to. Use the grievance form; "
                + "this route opens the other employee-relations case types.");

        var tenantId = GetTenantId();
        var subject = await GetOwnedEmployeeAsync(dto.EmployeeId, "primary party");

        var now = DateTime.UtcNow;
        var grievance = new StaffGrievance
        {
            TenantId = tenantId,
            GrievanceNumber = await GenerateNumberAsync(tenantId, cancellationToken),
            CaseType = dto.CaseType,
            EmployeeId = subject.Id,
            Subject = dto.Subject.Trim(),
            Statement = dto.Statement.Trim(),
            FiledDate = now,
            Status = GrievanceStatus.Filed,
            // Non-grievance cases open with HR, not with the supervisor: nobody has escalated
            // anything to reach them — the desk opened them.
            CurrentLevel = GrievanceEscalationLevel.HumanResources,
            CreatedBy = openedByEmployeeId.ToString(),
        };

        grievance.Steps.Add(new StaffGrievanceStep
        {
            TenantId = tenantId,
            Level = GrievanceEscalationLevel.HumanResources,
            Sequence = 1,
            ReachedDate = now,
            Outcome = GrievanceStepOutcome.AwaitingResponse,
            CreatedBy = openedByEmployeeId.ToString(),
        });

        await _unitOfWork.Repository<StaffGrievance>().AddAsync(grievance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee-relations case opened: {Number} ({Type})",
            grievance.GrievanceNumber, grievance.CaseType);

        return ToDto(await GetOwnedAsync(grievance.Id, cancellationToken));
    }

    public async Task<StaffGrievanceDto> AddPartyAsync(
        Guid grievanceId, AddGrievancePartyDto dto, Guid addedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var hasEmployee = dto.EmployeeId.HasValue;
        var hasExternal = !string.IsNullOrWhiteSpace(dto.ExternalName);

        if (hasEmployee == hasExternal)
            throw new InvalidOperationException(
                "A party is either a member of staff or an external person: supply exactly one of "
                + "the employee and the external name.");

        Employee? employee = null;
        if (hasEmployee)
        {
            employee = await GetOwnedEmployeeAsync(dto.EmployeeId!.Value, "party");

            // The primary party is already on the case. Adding them again would put the same person
            // in two places with two different roles, and — for Respondent — would say the employee
            // raised a case against themselves.
            if (employee.Id == grievance.EmployeeId)
                throw new InvalidOperationException(
                    "This employee is already the primary party on this case and cannot be added again.");

            var alreadyOn = grievance.Parties.Any(p =>
                !p.IsDeleted && p.RemovedDate == null && p.EmployeeId == employee.Id && p.Role == dto.Role);
            if (alreadyOn)
                throw new InvalidOperationException($"This person is already on the case as {dto.Role}.");
        }

        if (dto.RepresentsEmployeeId.HasValue)
        {
            if (dto.Role is not (GrievancePartyRole.Representative or GrievancePartyRole.UnionRepresentative))
                throw new InvalidOperationException(
                    "Only a representative or union representative acts for somebody else.");

            // They must represent someone actually on the case, or the field records nothing.
            var represented = dto.RepresentsEmployeeId.Value;
            var onCase = represented == grievance.EmployeeId
                || grievance.Parties.Any(p => !p.IsDeleted && p.RemovedDate == null && p.EmployeeId == represented);
            if (!onCase)
                throw new InvalidOperationException(
                    "A representative can only act for somebody who is already a party to this case.");
        }

        if (dto.UnionId.HasValue && dto.Role != GrievancePartyRole.UnionRepresentative)
            throw new InvalidOperationException("Only a union representative acts for a union.");

        if (dto.UnionId.HasValue)
        {
            var union = await _unitOfWork.Repository<Union>().GetByIdAsync(dto.UnionId.Value);
            if (union == null || union.IsDeleted || union.TenantId != grievance.TenantId)
                throw new ArgumentException($"The union with ID '{dto.UnionId}' was not found.");
        }

        var party = new StaffGrievanceParty
        {
            TenantId = grievance.TenantId,
            GrievanceId = grievance.Id,
            Role = dto.Role,
            EmployeeId = employee?.Id,
            ExternalName = hasExternal ? dto.ExternalName!.Trim() : null,
            ExternalOrganisation = string.IsNullOrWhiteSpace(dto.ExternalOrganisation)
                ? null : dto.ExternalOrganisation.Trim(),
            RepresentsEmployeeId = dto.RepresentsEmployeeId,
            UnionId = dto.UnionId,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            AddedDate = DateTime.UtcNow,
            AddedById = addedByEmployeeId,
            CreatedBy = addedByEmployeeId.ToString(),
        };

        // ⚠ Through the REPOSITORY, not grievance.Parties.Add(...). The grievance is already tracked
        // on this path, so adding to its navigation collection makes EF treat the new row as
        // Modified and issue an UPDATE against a row that does not exist. Same trap, same fix, as
        // the escalation step above — see [[ef-tracked-graph-write-traps]].
        await _unitOfWork.Repository<StaffGrievanceParty>().AddAsync(party);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> RemovePartyAsync(
        Guid grievanceId, Guid partyId, RemoveGrievancePartyDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var party = grievance.Parties.FirstOrDefault(p => p.Id == partyId && !p.IsDeleted)
            ?? throw new ArgumentException($"Party with ID '{partyId}' was not found on this case.");

        if (party.RemovedDate != null)
            throw new InvalidOperationException("This party has already stood down.");

        // Standing somebody down leaves anyone acting FOR them without a principal, which is a fact
        // about the case worth refusing to hide: their representative rows go too, and say why.
        //
        // ⚠ Guarded on the party being internal. RepresentsEmployeeId points at an EMPLOYEE, so for
        // an external party (a union official, a lawyer) EmployeeId is null — and `p.
        // RepresentsEmployeeId == party.EmployeeId` would then be `null == null`, matching every
        // party on the case who represents nobody and standing the whole case down.
        var represented = party.EmployeeId is Guid principalId
            ? grievance.Parties
                .Where(p => !p.IsDeleted && p.RemovedDate == null && p.RepresentsEmployeeId == principalId)
                .ToList()
            : new List<StaffGrievanceParty>();

        var now = DateTime.UtcNow;
        party.RemovedDate = now;
        party.RemovalReason = dto.Reason.Trim();
        party.UpdatedAt = now;

        foreach (var rep in represented)
        {
            rep.RemovedDate = now;
            rep.RemovalReason = $"The party they acted for stood down: {dto.Reason.Trim()}";
            rep.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    private static void EnsureOpen(StaffGrievance grievance)
    {
        if (grievance.Status is GrievanceStatus.Resolved or GrievanceStatus.Withdrawn or GrievanceStatus.Closed)
            throw new InvalidOperationException($"This grievance is {grievance.Status} and cannot be changed.");
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static StaffGrievanceSummaryDto ToSummary(StaffGrievance g) => new()
    {
        Id = g.Id,
        GrievanceNumber = g.GrievanceNumber,
        CaseType = g.CaseType,
        EmployeeId = g.EmployeeId,
        EmployeeName = g.Employee?.FullName ?? string.Empty,
        Subject = g.Subject,
        FiledDate = g.FiledDate,
        Status = g.Status,
        CurrentLevel = g.CurrentLevel,
        AwaitingResponse = g.Steps.OrderBy(s => s.Sequence).LastOrDefault()?.Outcome
            == GrievanceStepOutcome.AwaitingResponse,
        ActivePartyCount = g.Parties.Count(p => !p.IsDeleted && p.RemovedDate == null),
    };

    private static StaffGrievancePartyDto ToPartyDto(StaffGrievanceParty p) => new()
    {
        Id = p.Id,
        GrievanceId = p.GrievanceId,
        Role = p.Role,
        EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee?.FullName,
        ExternalName = p.ExternalName,
        ExternalOrganisation = p.ExternalOrganisation,
        RepresentsEmployeeId = p.RepresentsEmployeeId,
        RepresentsEmployeeName = p.RepresentsEmployee?.FullName,
        UnionId = p.UnionId,
        UnionName = p.Union?.Name,
        AddedDate = p.AddedDate,
        AddedById = p.AddedById,
        AddedByName = p.AddedBy?.FullName,
        Notes = p.Notes,
        RemovedDate = p.RemovedDate,
        RemovalReason = p.RemovalReason,
    };

    private static StaffGrievanceDto ToDto(StaffGrievance g) => new()
    {
        Id = g.Id,
        TenantId = g.TenantId,
        GrievanceNumber = g.GrievanceNumber,
        CaseType = g.CaseType,
        ActivePartyCount = g.Parties.Count(p => !p.IsDeleted && p.RemovedDate == null),
        Parties = g.Parties
            .Where(p => !p.IsDeleted)
            // Role then when they joined: the file must read the same way twice, and a case with a
            // respondent and two representatives is unreadable in insertion order.
            .OrderBy(p => p.Role).ThenBy(p => p.AddedDate)
            .Select(ToPartyDto)
            .ToList(),
        EmployeeId = g.EmployeeId,
        EmployeeName = g.Employee?.FullName ?? string.Empty,
        EmployeeNumber = g.Employee?.EmployeeNumber,
        Subject = g.Subject,
        Statement = g.Statement,
        FiledDate = g.FiledDate,
        Status = g.Status,
        CurrentLevel = g.CurrentLevel,
        AwaitingResponse = g.Steps.OrderBy(s => s.Sequence).LastOrDefault()?.Outcome
            == GrievanceStepOutcome.AwaitingResponse,
        ResolvedDate = g.ResolvedDate,
        ResolutionSummary = g.ResolutionSummary,
        WithdrawnDate = g.WithdrawnDate,
        WithdrawalReason = g.WithdrawalReason,
        Steps = g.Steps.OrderBy(s => s.Sequence).Select(s => new StaffGrievanceStepDto
        {
            Id = s.Id,
            GrievanceId = s.GrievanceId,
            Level = s.Level,
            Sequence = s.Sequence,
            ReachedDate = s.ReachedDate,
            AssignedToId = s.AssignedToId,
            AssignedToName = s.AssignedTo?.FullName,
            Response = s.Response,
            RespondedDate = s.RespondedDate,
            RespondedById = s.RespondedById,
            RespondedByName = s.RespondedBy?.FullName,
            Outcome = s.Outcome,
        }).ToList(),
    };
}
