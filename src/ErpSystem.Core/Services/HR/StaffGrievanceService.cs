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
            .Include(g => g.Parties.Where(p => !p.IsDeleted)).ThenInclude(p => p.AddedBy)
            // Area 9c slice 2 — FR-HR-181's artefacts. All on the case file read, none on the
            // register: a register row must not drag an investigation report behind it.
            .Include(g => g.HrInterpretationBy)
            .Include(g => g.ClosedBy)
            .Include(g => g.Investigation!).ThenInclude(i => i.Investigator)
            .Include(g => g.Investigation!).ThenInclude(i => i.OpenedBy)
            .Include(g => g.Resolution!).ThenInclude(r => r.DecidedBy)
            .Include(g => g.Resolution!).ThenInclude(r => r.OutcomeRecordedBy)
            .Include(g => g.Resolution!).ThenInclude(r => r.AgreementAcceptedBy)
            // Area 9c slice 3 — the case's paperwork.
            .Include(g => g.Documents.Where(d => !d.IsDeleted)).ThenInclude(d => d.UploadedBy)
            // Area 9c slice 4 — conferences and who was asked to them.
            .Include(g => g.Conferences.Where(c => !c.IsDeleted)).ThenInclude(c => c.Chair)
            .Include(g => g.Conferences.Where(c => !c.IsDeleted)).ThenInclude(c => c.Union)
            .Include(g => g.Conferences.Where(c => !c.IsDeleted)).ThenInclude(c => c.ConvenedBy)
            .Include(g => g.Conferences.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Attendees.Where(a => !a.IsDeleted)).ThenInclude(a => a.Employee)
            // ⚠ SPLIT QUERY, and it is required rather than an optimisation. As a single query this
            // graph is one JOIN across five collections and two one-to-ones, and SQL Server has to
            // materialise the result as one row: Statement (6000) + HrInterpretation (4000) +
            // ResolutionSummary (4000) + the investigation's Findings/Evidence/Recommendation (4000
            // each) + the resolution's Decision/Remedy (4000 each) + every step's Response (4000).
            // That crossed the 8060-byte row limit the moment slice 3 added the documents, and
            // FAILED ON THE READ — `FileAsync` saved the row and then died reading it back with
            // "Cannot create a row of size 8079". Splitting also removes the cartesian explosion
            // that five collections would otherwise produce. Ordering is preserved: the OrderBy
            // inside the Steps include and the explicit sorts in ToDto both still apply.
            .AsSplitQuery();

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
        return ToDto(grievance, callerEmployeeId);
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

            // Area 9c slice 2 — FR-HR-181 obligation 8. This shorthand is what used to BE the
            // resolution: an answer copied into a summary field, with no decider distinct from the
            // responder, no outcome and no remedy. It still works, because the portal screen calls
            // it, but it now leaves a real artefact behind — marked NotRecorded, which honestly says
            // "resolved, and nobody captured what was decided" rather than inventing an outcome.
            // `ResolveAsync` records a real one, and can fill this one in afterwards.
            if (grievance.Resolution == null)
            {
                await _unitOfWork.Repository<StaffGrievanceResolution>().AddAsync(new StaffGrievanceResolution
                {
                    TenantId = grievance.TenantId,
                    GrievanceId = grievance.Id,
                    Outcome = GrievanceResolutionOutcome.NotRecorded,
                    Decision = dto.Response.Trim(),
                    DecidedById = responderEmployeeId,
                    DecidedDate = now,
                    DecidedAtLevel = step.Level,
                    CreatedBy = responderEmployeeId.ToString(),
                });
            }
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

    // ── Area 9c slice 2 — FR-HR-181's missing artefacts ───────────────────────

    public async Task<StaffGrievanceDto> RecordHrInterpretationAsync(
        Guid grievanceId, RecordHrInterpretationDto dto, Guid recordedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var now = DateTime.UtcNow;
        grievance.HrInterpretation = dto.Interpretation.Trim();
        grievance.HrInterpretationById = recordedByEmployeeId;
        grievance.HrInterpretationDate = now;
        grievance.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> OpenInvestigationAsync(
        Guid grievanceId, OpenGrievanceInvestigationDto dto, Guid openedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        if (grievance.Investigation != null)
            throw new InvalidOperationException("This case already has an investigation.");

        var hasInternal = dto.InvestigatorId.HasValue;
        var hasExternal = !string.IsNullOrWhiteSpace(dto.ExternalInvestigatorName);
        if (hasInternal == hasExternal)
            throw new InvalidOperationException(
                "An investigator is either a member of staff or an external person: supply exactly "
                + "one of the investigator and the external name.");

        if (hasInternal)
        {
            var investigator = await GetOwnedEmployeeAsync(dto.InvestigatorId!.Value, "investigating");

            // The complainant cannot investigate their own complaint, and neither can anyone the
            // case is about. Both are natural-justice failures the record would otherwise hide.
            if (investigator.Id == grievance.EmployeeId)
                throw new InvalidOperationException(
                    "The employee this case belongs to cannot investigate it.");

            var isRespondent = grievance.Parties.Any(p =>
                !p.IsDeleted && p.RemovedDate == null
                && p.EmployeeId == investigator.Id
                && p.Role == GrievancePartyRole.Respondent);
            if (isRespondent)
                throw new InvalidOperationException(
                    "A respondent on this case cannot investigate it.");
        }

        var now = DateTime.UtcNow;
        await _unitOfWork.Repository<StaffGrievanceInvestigation>().AddAsync(new StaffGrievanceInvestigation
        {
            TenantId = grievance.TenantId,
            GrievanceId = grievance.Id,
            InvestigatorId = dto.InvestigatorId,
            ExternalInvestigatorName = hasExternal ? dto.ExternalInvestigatorName!.Trim() : null,
            ExternalInvestigatorOrganisation = string.IsNullOrWhiteSpace(dto.ExternalInvestigatorOrganisation)
                ? null : dto.ExternalInvestigatorOrganisation.Trim(),
            StartedDate = now,
            TargetDate = dto.TargetDate,
            OpenedById = openedByEmployeeId,
            CreatedBy = openedByEmployeeId.ToString(),
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> UpdateInvestigationAsync(
        Guid grievanceId, UpdateGrievanceInvestigationDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var investigation = grievance.Investigation
            ?? throw new ArgumentException("This case has no investigation to update.");

        if (investigation.CompletedDate != null)
            throw new InvalidOperationException(
                "This investigation has been concluded and its report can no longer be changed.");

        // Null means "leave alone", not "clear" — a partial update from a form that only edited the
        // recommendation must not wipe the findings. (The replace-set convention elsewhere in this
        // repo is the opposite; this is a field-level patch and says so.)
        if (dto.Findings != null) investigation.Findings = dto.Findings.Trim();
        if (dto.EvidenceCollected != null) investigation.EvidenceCollected = dto.EvidenceCollected.Trim();
        if (dto.Recommendation != null) investigation.Recommendation = dto.Recommendation.Trim();
        if (dto.TargetDate.HasValue) investigation.TargetDate = dto.TargetDate;
        investigation.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> CompleteInvestigationAsync(
        Guid grievanceId, CompleteGrievanceInvestigationDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var investigation = grievance.Investigation
            ?? throw new ArgumentException("This case has no investigation to conclude.");

        if (investigation.CompletedDate != null)
            throw new InvalidOperationException("This investigation has already been concluded.");

        var now = DateTime.UtcNow;
        investigation.Findings = dto.Findings.Trim();
        if (dto.EvidenceCollected != null) investigation.EvidenceCollected = dto.EvidenceCollected.Trim();
        if (dto.Recommendation != null) investigation.Recommendation = dto.Recommendation.Trim();
        investigation.CompletedDate = now;
        investigation.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number}: investigation concluded", grievance.GrievanceNumber);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> ResolveAsync(
        Guid grievanceId, ResolveGrievanceDto dto, Guid decidedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);

        if (dto.Outcome == GrievanceResolutionOutcome.NotRecorded)
            throw new InvalidOperationException(
                "Choose what was decided. 'Not Recorded' exists only to mark resolutions taken "
                + "before an outcome could be captured; it cannot be chosen.");

        // Answering is the responder's or HR's act, and so is deciding. The griever never decides
        // their own case — the same rule, for the same reason, as not answering it.
        if (grievance.EmployeeId == decidedByEmployeeId)
            throw new UnauthorizedAccessException("You cannot decide your own case.");

        var step = CurrentStep(grievance);
        if (!IsHr && step.AssignedToId != decidedByEmployeeId)
            throw new UnauthorizedAccessException(
                "You are not the person asked to answer this case at this level.");

        var now = DateTime.UtcNow;

        // ── The fill-in path: a resolution recorded through the legacy shorthand, whose outcome
        // was never captured. Completing the record is permitted; AMENDING a decision is not, so
        // the supplied Decision is deliberately ignored here rather than silently overwriting one.
        if (grievance.Resolution is { } existing)
        {
            if (existing.Outcome != GrievanceResolutionOutcome.NotRecorded)
                throw new InvalidOperationException(
                    "This case already has a recorded decision, and a decision is never amended.");

            existing.Outcome = dto.Outcome;
            if (string.IsNullOrWhiteSpace(existing.RemedyOrUndertakings)
                && !string.IsNullOrWhiteSpace(dto.RemedyOrUndertakings))
                existing.RemedyOrUndertakings = dto.RemedyOrUndertakings.Trim();
            existing.OutcomeRecordedDate = now;
            existing.OutcomeRecordedById = decidedByEmployeeId;
            existing.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
        }

        EnsureOpen(grievance);

        // Deciding also answers the rung it is decided at, if nobody has. Leaving the step
        // AwaitingResponse beside a resolved case would make the ladder read as still owing an
        // answer to a case that is over.
        if (step.Outcome == GrievanceStepOutcome.AwaitingResponse)
        {
            step.Response = dto.Decision.Trim();
            step.RespondedById = decidedByEmployeeId;
            step.RespondedDate = now;
            step.Outcome = GrievanceStepOutcome.Resolved;
            step.UpdatedAt = now;
        }

        await _unitOfWork.Repository<StaffGrievanceResolution>().AddAsync(new StaffGrievanceResolution
        {
            TenantId = grievance.TenantId,
            GrievanceId = grievance.Id,
            Outcome = dto.Outcome,
            Decision = dto.Decision.Trim(),
            RemedyOrUndertakings = string.IsNullOrWhiteSpace(dto.RemedyOrUndertakings)
                ? null : dto.RemedyOrUndertakings.Trim(),
            DecidedById = decidedByEmployeeId,
            DecidedDate = now,
            // Stamped, not derived: the rung that DECIDED it, which stays correct whatever the case
            // does afterwards.
            DecidedAtLevel = grievance.CurrentLevel,
            CreatedBy = decidedByEmployeeId.ToString(),
        });

        grievance.Status = GrievanceStatus.Resolved;
        grievance.ResolvedDate = now;
        // Kept as a mirror of the decision, not of the response — the portal screen renders it.
        grievance.ResolutionSummary = dto.Decision.Trim();
        grievance.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number} resolved: {Outcome}", grievance.GrievanceNumber, dto.Outcome);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> CloseAsync(
        Guid grievanceId, CloseGrievanceDto dto, Guid closedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        // Closing is for a case that has nowhere left to go: the ladder is exhausted. Anything
        // still climbing has a rung that owes an answer, and closing it there would let the desk
        // end a live grievance the employee is entitled to escalate.
        if (grievance.CurrentLevel < TopLevel)
            throw new InvalidOperationException(
                $"This case is still at {grievance.CurrentLevel} and can be escalated further. "
                + "Only a case that has exhausted the escalation route can be closed unresolved.");

        var step = CurrentStep(grievance);
        if (step.Outcome == GrievanceStepOutcome.AwaitingResponse)
            throw new InvalidOperationException(
                "The Board has not answered yet. A case cannot be closed unresolved before the "
                + "final level has responded.");

        var now = DateTime.UtcNow;
        grievance.Status = GrievanceStatus.Closed;
        grievance.ClosedDate = now;
        grievance.ClosureReason = dto.Reason.Trim();
        grievance.ClosedById = closedByEmployeeId;
        grievance.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number} closed unresolved at {Level}",
            grievance.GrievanceNumber, grievance.CurrentLevel);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    // ── Area 9c slice 3 — documents and the signed agreement ──────────────────

    public async Task ValidateDocumentPlacementAsync(
        Guid grievanceId, GrievanceDocumentScope scope, Guid? stepId, Guid? conferenceId, CancellationToken cancellationToken = default)
        => EnsureDocumentPlacement(await GetOwnedAsync(grievanceId, cancellationToken), scope, stepId, conferenceId);

    /// <summary>
    /// Where a document may be filed. Called twice on purpose — once by the controller BEFORE the
    /// file is uploaded, once here as the last word.
    /// </summary>
    /// <remarks>
    /// ⚠ The pre-flight call is not belt-and-braces, it is the only thing that makes these rules
    /// visible: <c>HrAttachmentUpload.ExecuteAsync</c> catches everything its persist callback
    /// throws, rolls the stored document back, and answers a generic 500. Raised only from here,
    /// every rule below reached the caller as "An error occurred while adding the attachment".
    /// </remarks>
    private static void EnsureDocumentPlacement(
        StaffGrievance grievance, GrievanceDocumentScope scope, Guid? stepId, Guid? conferenceId)
    {
        // ⚠ NOT EnsureOpen for the agreement. FR-HR-181's signed agreement is the written form of
        // a decision, so by definition it arrives on a case that has just been RESOLVED — gating it
        // on "open" would make obligation 9 unreachable by construction. Withdrawn and Closed cases
        // still take nothing.
        if (scope == GrievanceDocumentScope.Agreement) EnsureNotAbandoned(grievance);
        else EnsureOpen(grievance);

        // Scope is enforced here because neither the database nor the DTO can express "exactly the
        // right companion id for this scope".
        switch (scope)
        {
            case GrievanceDocumentScope.Step:
                if (stepId is not Guid sid)
                    throw new InvalidOperationException("A ladder-step document must say which step it belongs to.");
                if (grievance.Steps.All(s => s.Id != sid))
                    throw new ArgumentException($"Step '{sid}' is not on this case.");
                break;

            case GrievanceDocumentScope.Investigation:
                if (grievance.Investigation == null)
                    throw new InvalidOperationException(
                        "This case has no investigation, so a document cannot be filed against one.");
                break;

            case GrievanceDocumentScope.Conference:
                if (conferenceId is not Guid cid)
                    throw new InvalidOperationException("A conference document must say which meeting it belongs to.");
                if (grievance.Conferences.All(c => c.IsDeleted || c.Id != cid))
                    throw new ArgumentException($"Meeting '{cid}' is not on this case.");
                break;

            case GrievanceDocumentScope.Agreement:
                // FR-HR-181's agreement is what SETTLES the case, so there has to be a decision for
                // it to be the written form of. Uploading one to an unresolved case would produce a
                // signed agreement about nothing.
                if (grievance.Resolution == null)
                    throw new InvalidOperationException(
                        "Record the resolution decision before uploading the agreement that sets it out.");
                if (grievance.Documents.Any(d => !d.IsDeleted && d.Scope == GrievanceDocumentScope.Agreement))
                    throw new InvalidOperationException("This case already has a signed agreement.");
                break;
        }

        if (scope != GrievanceDocumentScope.Step && stepId.HasValue)
            throw new InvalidOperationException("Only a ladder-step document belongs to a step.");

        if (scope != GrievanceDocumentScope.Conference && conferenceId.HasValue)
            throw new InvalidOperationException("Only a conference document belongs to a meeting.");
    }

    public async Task<StaffGrievanceDocumentDto> AddDocumentAsync(
        Guid grievanceId,
        GrievanceDocumentScope scope,
        Guid? stepId,
        Guid? conferenceId,
        string? description,
        DateTime? agreementSignedDate,
        Guid uploadedByEmployeeId,
        string fileName,
        string filePath,
        long fileSize,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureDocumentPlacement(grievance, scope, stepId, conferenceId);

        var now = DateTime.UtcNow;
        var document = new StaffGrievanceDocument
        {
            TenantId = grievance.TenantId,
            GrievanceId = grievance.Id,
            Scope = scope,
            StepId = scope == GrievanceDocumentScope.Step ? stepId : null,
            ConferenceId = scope == GrievanceDocumentScope.Conference ? conferenceId : null,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UploadDate = now,
            UploadedById = uploadedByEmployeeId,
            CreatedBy = uploadedByEmployeeId.ToString(),
        };

        await _unitOfWork.Repository<StaffGrievanceDocument>().AddAsync(document);

        // The signature date belongs to the agreement, and it is SUPPLIED rather than stamped: the
        // signing happens in a room and the scan arrives afterwards, so UtcNow would record when
        // somebody got round to uploading it.
        if (scope == GrievanceDocumentScope.Agreement && grievance.Resolution is { } resolution)
        {
            resolution.AgreementSignedDate = agreementSignedDate ?? now;
            resolution.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = (await GetOwnedAsync(grievanceId, cancellationToken))
            .Documents.First(d => d.Id == document.Id);
        return ToDocumentDto(saved);
    }

    public async Task<StaffGrievanceDocumentDto> GetDocumentAsync(
        Guid documentId, Guid? callerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var document = await _unitOfWork.Repository<StaffGrievanceDocument>()
            .GetQueryable()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.Id == documentId)
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Document with ID '{documentId}' not found.");

        // ⚠ The entitlement check is this service's, never the download helper's — and it is the
        // CASE's rule, not a rule of its own. A document is exactly as readable as the case it is
        // filed against.
        var grievance = await GetOwnedAsync(document.GrievanceId, cancellationToken);
        EnsureMayRead(grievance, callerEmployeeId);

        return ToDocumentDto(document);
    }

    public async Task<StaffGrievanceDto> DeleteDocumentAsync(
        Guid grievanceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);

        // A wrongly-uploaded agreement has to be removable from the resolved case it landed on —
        // see AddDocumentAsync for why "open" is the wrong gate here.
        EnsureNotAbandoned(grievance);

        var document = grievance.Documents.FirstOrDefault(d => d.Id == documentId && !d.IsDeleted)
            ?? throw new ArgumentException($"Document with ID '{documentId}' was not found on this case.");

        if (document.Scope == GrievanceDocumentScope.Agreement
            && grievance.Resolution?.AgreementAcceptedDate != null)
            throw new InvalidOperationException(
                "The employee has accepted this agreement and it can no longer be removed.");

        var now = DateTime.UtcNow;
        document.IsDeleted = true;
        document.DeletedAt = now;
        document.UpdatedAt = now;

        if (document.Scope == GrievanceDocumentScope.Agreement && grievance.Resolution is { } resolution)
            resolution.AgreementSignedDate = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> AcceptAgreementAsync(
        Guid grievanceId, AcceptGrievanceAgreementDto dto, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);

        // Theirs alone, exactly as escalating and withdrawing are. HR cannot accept an agreement on
        // an employee's behalf, and nobody else can accept one about them.
        if (grievance.EmployeeId != employeeId)
            throw new UnauthorizedAccessException(
                "Only the employee this case belongs to can accept its agreement.");

        var resolution = grievance.Resolution
            ?? throw new InvalidOperationException("This case has not been resolved, so there is no agreement to accept.");

        if (!grievance.Documents.Any(d => !d.IsDeleted && d.Scope == GrievanceDocumentScope.Agreement))
            throw new InvalidOperationException(
                "There is no signed agreement on this case yet. HR uploads it before you confirm it.");

        if (resolution.AgreementAcceptedDate != null)
            throw new InvalidOperationException("You have already accepted this agreement.");

        var now = DateTime.UtcNow;
        resolution.AgreementAcceptedDate = now;
        resolution.AgreementAcceptedById = employeeId;
        resolution.UpdatedAt = now;

        // Its own column. Appending it to RemedyOrUndertakings would edit part of the frozen
        // decision to hold a remark made after the fact.
        if (!string.IsNullOrWhiteSpace(dto.Comment))
            resolution.AgreementAcceptanceComment = dto.Comment.Trim();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number}: agreement accepted by the employee", grievance.GrievanceNumber);

        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    // ── Area 9c slice 4 — conferencing, mediation, union consultation ─────────

    public async Task<StaffGrievanceDto> ScheduleConferenceAsync(
        Guid grievanceId, ScheduleGrievanceConferenceDto dto, Guid convenedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);

        var hasChair = dto.ChairId.HasValue;
        var hasExternalChair = !string.IsNullOrWhiteSpace(dto.ExternalChairName);
        if (hasChair == hasExternalChair)
            throw new InvalidOperationException(
                "A chair is either a member of staff or an external person: supply exactly one of "
                + "the chair and the external chair name.");

        if (hasChair) await GetOwnedEmployeeAsync(dto.ChairId!.Value, "chairing");

        // FR-HR-181 obligation 6: a union consultation is a consultation WITH somebody, and a row
        // that does not say which union retains nothing the requirement asks for.
        if (dto.ConferenceType == GrievanceConferenceType.UnionConsultation)
        {
            if (dto.UnionId is not Guid unionId)
                throw new InvalidOperationException("A union consultation must say which union was consulted.");

            var union = await _unitOfWork.Repository<Union>().GetByIdAsync(unionId);
            if (union == null || union.IsDeleted || union.TenantId != grievance.TenantId)
                throw new ArgumentException($"The union with ID '{unionId}' was not found.");
        }
        else if (dto.UnionId.HasValue)
        {
            throw new InvalidOperationException("Only a union consultation names a union.");
        }

        await _unitOfWork.Repository<StaffGrievanceConference>().AddAsync(new StaffGrievanceConference
        {
            TenantId = grievance.TenantId,
            GrievanceId = grievance.Id,
            ConferenceType = dto.ConferenceType,
            Status = GrievanceConferenceStatus.Scheduled,
            ScheduledFor = dto.ScheduledFor,
            Venue = string.IsNullOrWhiteSpace(dto.Venue) ? null : dto.Venue.Trim(),
            ChairId = dto.ChairId,
            ExternalChairName = hasExternalChair ? dto.ExternalChairName!.Trim() : null,
            ExternalChairOrganisation = string.IsNullOrWhiteSpace(dto.ExternalChairOrganisation)
                ? null : dto.ExternalChairOrganisation.Trim(),
            UnionId = dto.UnionId,
            Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim(),
            ConvenedById = convenedByEmployeeId,
            CreatedBy = convenedByEmployeeId.ToString(),
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> UpdateConferenceAsync(
        Guid grievanceId, Guid conferenceId, UpdateGrievanceConferenceDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);
        var conference = FindScheduledConference(grievance, conferenceId, "amended");

        // Null means "leave alone", not "clear" — a form that moved the date must not wipe the venue.
        if (dto.ScheduledFor.HasValue) conference.ScheduledFor = dto.ScheduledFor.Value;
        if (dto.Venue != null) conference.Venue = dto.Venue.Trim();
        if (dto.Purpose != null) conference.Purpose = dto.Purpose.Trim();

        if (dto.ChairId.HasValue)
        {
            await GetOwnedEmployeeAsync(dto.ChairId.Value, "chairing");
            conference.ChairId = dto.ChairId;
            conference.ExternalChairName = null;
            conference.ExternalChairOrganisation = null;
        }
        else if (!string.IsNullOrWhiteSpace(dto.ExternalChairName))
        {
            conference.ExternalChairName = dto.ExternalChairName.Trim();
            conference.ChairId = null;
        }

        conference.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> HoldConferenceAsync(
        Guid grievanceId, Guid conferenceId, HoldGrievanceConferenceDto dto, Guid recordedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);
        var conference = FindScheduledConference(grievance, conferenceId, "recorded as held");

        // The chair may write it up without being in HR — a mediator usually is not.
        if (!IsHr && conference.ChairId != recordedByEmployeeId)
            throw new UnauthorizedAccessException(
                "Only HR or the person chairing this meeting can record what happened at it.");

        var now = DateTime.UtcNow;
        conference.Status = GrievanceConferenceStatus.Held;
        conference.HeldDate = now;
        conference.Outcome = dto.Outcome.Trim();
        if (dto.Notes != null) conference.Notes = dto.Notes.Trim();
        conference.UpdatedAt = now;

        foreach (var record in dto.Attendance)
        {
            var attendee = conference.Attendees.FirstOrDefault(a => a.Id == record.AttendeeId && !a.IsDeleted);
            if (attendee == null)
                throw new ArgumentException($"Attendee '{record.AttendeeId}' is not on this meeting.");

            attendee.DidAttend = record.DidAttend;
            attendee.ApologyReason = string.IsNullOrWhiteSpace(record.ApologyReason)
                ? null : record.ApologyReason.Trim();
            attendee.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number}: {Type} held", grievance.GrievanceNumber, conference.ConferenceType);

        // Passed so a non-HR chair is not handed back a redacted copy of the notes they just wrote.
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken), recordedByEmployeeId);
    }

    public async Task<StaffGrievanceDto> CancelConferenceAsync(
        Guid grievanceId, Guid conferenceId, CancelGrievanceConferenceDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);
        var conference = FindScheduledConference(grievance, conferenceId, "cancelled");

        var now = DateTime.UtcNow;
        conference.Status = GrievanceConferenceStatus.Cancelled;
        conference.CancelledDate = now;
        conference.CancellationReason = dto.Reason.Trim();
        conference.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> AddConferenceAttendeeAsync(
        Guid grievanceId, Guid conferenceId, AddConferenceAttendeeDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);
        var conference = FindScheduledConference(grievance, conferenceId, "added to");

        var hasEmployee = dto.EmployeeId.HasValue;
        var hasExternal = !string.IsNullOrWhiteSpace(dto.ExternalName);
        if (hasEmployee == hasExternal)
            throw new InvalidOperationException(
                "An attendee is either a member of staff or an external person: supply exactly one "
                + "of the employee and the external name.");

        if (hasEmployee)
        {
            var employee = await GetOwnedEmployeeAsync(dto.EmployeeId!.Value, "attending");
            if (conference.Attendees.Any(a => !a.IsDeleted && a.EmployeeId == employee.Id))
                throw new InvalidOperationException("This person is already on the attendee list.");
        }

        await _unitOfWork.Repository<StaffGrievanceConferenceAttendee>().AddAsync(new StaffGrievanceConferenceAttendee
        {
            TenantId = grievance.TenantId,
            ConferenceId = conference.Id,
            EmployeeId = dto.EmployeeId,
            ExternalName = hasExternal ? dto.ExternalName!.Trim() : null,
            ExternalOrganisation = string.IsNullOrWhiteSpace(dto.ExternalOrganisation)
                ? null : dto.ExternalOrganisation.Trim(),
            Capacity = string.IsNullOrWhiteSpace(dto.Capacity) ? null : dto.Capacity.Trim(),
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    public async Task<StaffGrievanceDto> RemoveConferenceAttendeeAsync(
        Guid grievanceId, Guid conferenceId, Guid attendeeId, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);
        EnsureOpen(grievance);
        var conference = FindScheduledConference(grievance, conferenceId, "removed from");

        var attendee = conference.Attendees.FirstOrDefault(a => a.Id == attendeeId && !a.IsDeleted)
            ?? throw new ArgumentException($"Attendee '{attendeeId}' is not on this meeting.");

        var now = DateTime.UtcNow;
        attendee.IsDeleted = true;
        attendee.DeletedAt = now;
        attendee.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(await GetOwnedAsync(grievanceId, cancellationToken));
    }

    /// <summary>
    /// The meeting, if it is still open to change. A held meeting is a record of something that
    /// happened and is not editable afterwards; a cancelled one is not going to happen.
    /// </summary>
    private static StaffGrievanceConference FindScheduledConference(
        StaffGrievance grievance, Guid conferenceId, string verb)
    {
        var conference = grievance.Conferences.FirstOrDefault(c => c.Id == conferenceId && !c.IsDeleted)
            ?? throw new ArgumentException($"Meeting '{conferenceId}' is not on this case.");

        if (conference.Status != GrievanceConferenceStatus.Scheduled)
            throw new InvalidOperationException(
                $"This meeting is {conference.Status} and cannot be {verb}.");

        return conference;
    }

    private static void EnsureOpen(StaffGrievance grievance)
    {
        if (grievance.Status is GrievanceStatus.Resolved or GrievanceStatus.Withdrawn or GrievanceStatus.Closed)
            throw new InvalidOperationException($"This grievance is {grievance.Status} and cannot be changed.");
    }

    /// <summary>
    /// The weaker gate: everything except a case that was abandoned rather than settled.
    /// </summary>
    /// <remarks>
    /// A RESOLVED case still has work to do — FR-HR-181's signed agreement is written FROM the
    /// decision, so it necessarily arrives afterwards. A Withdrawn or Closed case has no agreement
    /// to sign, because nothing was agreed.
    /// </remarks>
    private static void EnsureNotAbandoned(StaffGrievance grievance)
    {
        if (grievance.Status is GrievanceStatus.Withdrawn or GrievanceStatus.Closed)
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

    /// <summary>
    /// Who may read a meeting's notes: HR, and the person who chaired it.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately NARROWER than who may read the case. A mediation's notes record what the
    /// other party said in a room they were promised was private, and a union consultation's record
    /// what the union said about a member — and the case's read rule admits the complainant. Without
    /// this the complainant would receive the respondent's position verbatim. The outcome, the date,
    /// the venue and the attendee list stay visible to everyone who may read the case; only the notes
    /// are withheld, and the DTO says so rather than pretending there are none.
    /// </remarks>
    private bool MaySeeConferenceNotes(StaffGrievanceConference c, Guid? callerEmployeeId)
        => IsHr || (callerEmployeeId is Guid caller && c.ChairId == caller);

    private static StaffGrievanceConferenceDto ToConferenceDto(StaffGrievanceConference c, bool maySeeNotes) => new()
    {
        Id = c.Id,
        GrievanceId = c.GrievanceId,
        ConferenceType = c.ConferenceType,
        Status = c.Status,
        ScheduledFor = c.ScheduledFor,
        Venue = c.Venue,
        ChairId = c.ChairId,
        ChairName = c.Chair?.FullName,
        ExternalChairName = c.ExternalChairName,
        ExternalChairOrganisation = c.ExternalChairOrganisation,
        UnionId = c.UnionId,
        UnionName = c.Union?.Name,
        Purpose = c.Purpose,
        Notes = maySeeNotes ? c.Notes : null,
        // "There are notes you may not see" and "there are no notes" are different facts, and a
        // reader who cannot tell them apart cannot know to ask.
        NotesRedacted = !maySeeNotes && !string.IsNullOrWhiteSpace(c.Notes),
        Outcome = c.Outcome,
        HeldDate = c.HeldDate,
        CancelledDate = c.CancelledDate,
        CancellationReason = c.CancellationReason,
        ConvenedById = c.ConvenedById,
        ConvenedByName = c.ConvenedBy?.FullName,
        Attendees = c.Attendees
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new StaffGrievanceConferenceAttendeeDto
            {
                Id = a.Id,
                ConferenceId = a.ConferenceId,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee?.FullName,
                ExternalName = a.ExternalName,
                ExternalOrganisation = a.ExternalOrganisation,
                Capacity = a.Capacity,
                DidAttend = a.DidAttend,
                ApologyReason = a.ApologyReason,
            })
            .ToList(),
    };

    private static StaffGrievanceDocumentDto ToDocumentDto(StaffGrievanceDocument d) => new()
    {
        Id = d.Id,
        GrievanceId = d.GrievanceId,
        Scope = d.Scope,
        StepId = d.StepId,
        ConferenceId = d.ConferenceId,
        FileName = d.FileName,
        FilePath = d.FilePath,
        FileSize = d.FileSize,
        Description = d.Description,
        UploadDate = d.UploadDate,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy?.FullName,
        FileUploadRecordId = d.FileUploadRecordId,
        DocumentRecordId = d.DocumentRecordId,
        DocumentVersionId = d.DocumentVersionId,
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

    /// <summary>
    /// The case file. <paramref name="callerEmployeeId"/> decides only ONE thing: whether conference
    /// notes are readable (area 9c slice 4). Everything else on this DTO is already gated by
    /// <see cref="EnsureMayRead"/> before the caller gets here.
    /// </summary>
    /// <remarks>
    /// ⚠ An INSTANCE method rather than static because the redaction needs <see cref="IsHr"/>.
    /// Write paths that do not pass a caller fall back to the role check, which is right: every
    /// write that is not the employee's own is HR-gated, and the employee's own writes should not
    /// return them somebody else's mediation notes.
    /// </remarks>
    private StaffGrievanceDto ToDto(StaffGrievance g, Guid? callerEmployeeId = null) => new()
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
        // Area 9c slice 2 — FR-HR-181's artefacts.
        HrInterpretation = g.HrInterpretation,
        HrInterpretationById = g.HrInterpretationById,
        HrInterpretationByName = g.HrInterpretationBy?.FullName,
        HrInterpretationDate = g.HrInterpretationDate,
        ClosedDate = g.ClosedDate,
        ClosureReason = g.ClosureReason,
        ClosedById = g.ClosedById,
        ClosedByName = g.ClosedBy?.FullName,
        Investigation = g.Investigation is { IsDeleted: false } i ? new StaffGrievanceInvestigationDto
        {
            Id = i.Id,
            GrievanceId = i.GrievanceId,
            InvestigatorId = i.InvestigatorId,
            InvestigatorName = i.Investigator?.FullName,
            ExternalInvestigatorName = i.ExternalInvestigatorName,
            ExternalInvestigatorOrganisation = i.ExternalInvestigatorOrganisation,
            StartedDate = i.StartedDate,
            TargetDate = i.TargetDate,
            CompletedDate = i.CompletedDate,
            Findings = i.Findings,
            EvidenceCollected = i.EvidenceCollected,
            Recommendation = i.Recommendation,
            OpenedById = i.OpenedById,
            OpenedByName = i.OpenedBy?.FullName,
        } : null,
        Resolution = g.Resolution is { IsDeleted: false } r ? new StaffGrievanceResolutionDto
        {
            Id = r.Id,
            GrievanceId = r.GrievanceId,
            Outcome = r.Outcome,
            Decision = r.Decision,
            RemedyOrUndertakings = r.RemedyOrUndertakings,
            DecidedById = r.DecidedById,
            DecidedByName = r.DecidedBy?.FullName,
            DecidedDate = r.DecidedDate,
            DecidedAtLevel = r.DecidedAtLevel,
            OutcomeRecordedDate = r.OutcomeRecordedDate,
            OutcomeRecordedByName = r.OutcomeRecordedBy?.FullName,
            AgreementSignedDate = r.AgreementSignedDate,
            AgreementAcceptedDate = r.AgreementAcceptedDate,
            AgreementAcceptedById = r.AgreementAcceptedById,
            AgreementAcceptedByName = r.AgreementAcceptedBy?.FullName,
            AgreementAcceptanceComment = r.AgreementAcceptanceComment,
        } : null,
        Documents = g.Documents
            .Where(d => !d.IsDeleted)
            // Scope then upload date: the file must read the same way twice, and a case with
            // evidence, a report and an agreement is unreadable in insertion order.
            .OrderBy(d => d.Scope).ThenBy(d => d.UploadDate)
            .Select(ToDocumentDto)
            .ToList(),
        Conferences = g.Conferences
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.ScheduledFor)
            .Select(c => ToConferenceDto(c, MaySeeConferenceNotes(c, callerEmployeeId)))
            .ToList(),
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
