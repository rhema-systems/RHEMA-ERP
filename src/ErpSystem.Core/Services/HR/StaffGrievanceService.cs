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
    private readonly IEmployeeRelationsResponderService _responders;
    private readonly ILogger<StaffGrievanceService> _logger;

    public StaffGrievanceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IEmployeeRelationsResponderService responders,
        ILogger<StaffGrievanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _responders = responders;
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

    /// <summary>
    /// A case somebody can still act on. The single definition of "open" in this service.
    /// </summary>
    /// <remarks>
    /// ⚠ Extracted in slice 8 because three readers of "is an answer owed" had drifted apart: the
    /// summary DTO's <c>AwaitingResponse</c> and the register's <c>awaitingResponseOnly</c> filter
    /// both looked only at the STEP, while <c>GetAwaitingResponseAsync</c> also required the case to
    /// be open. On live data that was 227 versus 174 — a 53-case gap, every one of them WITHDRAWN.
    /// Nobody owes an answer on a case the employee withdrew.
    /// </remarks>
    private static bool IsOpen(GrievanceStatus status)
        => status is GrievanceStatus.Filed or GrievanceStatus.UnderReview or GrievanceStatus.Escalated;

    private StaffGrievanceStep CurrentStep(StaffGrievance grievance)
        => grievance.Steps.OrderBy(s => s.Sequence).LastOrDefault()
           ?? throw new InvalidOperationException("This grievance has no escalation step, which should not be possible.");

    /// <summary>
    /// Names a responder on a step from the matrix, if the matrix has one — area 9c slice 5.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Resolving to nobody is a supported outcome, not a failure.</b> The matrix covers
    /// 0 of 48 units on a fresh tenant, and a case in an uncovered unit must still be filed: it
    /// simply arrives unassigned for HR to route by hand, exactly as every case did before slice 5.
    /// This method therefore returns quietly rather than throwing, and the harness asserts that a
    /// case in an uncovered unit files successfully.</para>
    ///
    /// <para>The griever is never auto-assigned to answer their own case. The matrix could name
    /// them — somebody who answers the HOD rung can also raise a grievance — and
    /// <c>RespondAsync</c> would then refuse the only person the step names, leaving it stuck with
    /// no way forward but an HR override.</para>
    /// </remarks>
    private async Task TryAutoAssignAsync(
        StaffGrievance grievance, StaffGrievanceStep step, CancellationToken cancellationToken)
    {
        if (step.AssignedToId.HasValue) return;

        // The primary party's unit. There is no unit on the case itself — see GetPagedAsync.
        var unitId = grievance.Employee?.OrganizationUnitId
                     ?? (await _unitOfWork.Repository<Employee>().GetByIdAsync(grievance.EmployeeId))?.OrganizationUnitId;

        var resolution = await _responders.ResolveAsync(unitId, step.Level, cancellationToken);
        if (resolution.ResponderEmployeeId is not Guid responderId) return;
        if (responderId == grievance.EmployeeId) return;

        step.AssignedToId = responderId;
        step.UpdatedAt = DateTime.UtcNow;

        if (grievance.Status == GrievanceStatus.Filed)
            grievance.Status = GrievanceStatus.UnderReview;
    }

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
        // ⚠ Open AND unanswered, matching GetAwaitingResponseAsync exactly. Before slice 8 this
        // checked only the step, so `awaitingResponseOnly=true` returned 227 cases against that
        // endpoint's 174 — the 53 extra were all WITHDRAWN, and none of them was waiting on anybody.
        if (awaitingResponseOnly == true)
            query = query.Where(g => (g.Status == GrievanceStatus.Filed
                                      || g.Status == GrievanceStatus.UnderReview
                                      || g.Status == GrievanceStatus.Escalated)
                                     && g.Steps.OrderByDescending(s => s.Sequence).First().Outcome
                                        == GrievanceStepOutcome.AwaitingResponse);
        else if (awaitingResponseOnly == false)
            query = query.Where(g => !((g.Status == GrievanceStatus.Filed
                                        || g.Status == GrievanceStatus.UnderReview
                                        || g.Status == GrievanceStatus.Escalated)
                                       && g.Steps.OrderByDescending(s => s.Sequence).First().Outcome
                                          == GrievanceStepOutcome.AwaitingResponse));

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
                AwaitingResponse = (g.Status == GrievanceStatus.Filed
                                    || g.Status == GrievanceStatus.UnderReview
                                    || g.Status == GrievanceStatus.Escalated)
                                   && g.Steps.OrderByDescending(s => s.Sequence).First().Outcome
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
        return await ToDtoAsync(grievance, callerEmployeeId, cancellationToken);
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

        // Area 9c slice 5 — the matrix names the Supervisor rung's responder if it has one. Done
        // after the save so the step has an identity; a no-match leaves the case unassigned, which
        // is exactly how every case behaved before the matrix existed.
        var saved = await GetOwnedAsync(grievance.Id, cancellationToken);
        await TryAutoAssignAsync(saved, CurrentStep(saved), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance filed: {Number}", grievance.GrievanceNumber);

        return await ToDtoAsync(await GetOwnedAsync(grievance.Id, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

        // Area 9c slice 5 — the rung it has just arrived at gets its responder from the matrix.
        // ⚠ This is the point of the matrix: before it, every escalation landed on a rung nobody
        // was named for, and the case sat there until an HR officer happened to look.
        var escalated = await GetOwnedAsync(grievanceId, cancellationToken);
        await TryAutoAssignAsync(escalated, CurrentStep(escalated), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grievance {Number} escalated to {Level}", grievance.GrievanceNumber, nextLevel);

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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


        // ── The raise-an-ER-case-from-here affordance (area 9c slice 9) ───────
        // Validated and set BEFORE the case is saved, so a refused link means no case at all. The
        // two-call alternative — open, then link — fails halfway and leaves the desk an orphan.
        if (dto.Source.HasValue != dto.SourceRecordId.HasValue)
            throw new InvalidOperationException(
                "Supply both the source and the source record id, or neither.");

        if (dto.Source is EmployeeRelationsLinkSource openSource)
        {
            // At open time the case has exactly one person on it — the primary party — so the
            // "about somebody on the case" rule is checked against that one id.
            await EnsureSourceIsAboutSomebodyOnCaseAsync(
                openSource, dto.SourceRecordId!.Value, new HashSet<Guid> { subject.Id }, cancellationToken);
            SetLink(grievance, openSource, dto.SourceRecordId!.Value);
        }

        await _unitOfWork.Repository<StaffGrievance>().AddAsync(grievance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var openedCase = await GetOwnedAsync(grievance.Id, cancellationToken);
        await TryAutoAssignAsync(openedCase, CurrentStep(openedCase), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee-relations case opened: {Number} ({Type})",
            grievance.GrievanceNumber, grievance.CaseType);

        return await ToDtoAsync(await GetOwnedAsync(grievance.Id, cancellationToken), null, cancellationToken);
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

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
            return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), recordedByEmployeeId, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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
        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
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

    // ── Cross-links to other modules' records (area 9c slice 9) ───────────────

    /// <summary>
    /// Cross-references this case to a safety incident, a PIP or a disciplinary case.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ Deliberately NOT state-gated</b>, and it is the only write in this service that
    /// is not. Every other write changes what the case SAYS — a response, a decision, an
    /// interpretation — and freezing those on a terminal case is what makes the outcome defensible.
    /// A cross-reference says nothing about the merits; it is filing. And the moment you most want
    /// to file a cross-reference is precisely when reviewing a closed case against the disciplinary
    /// action that followed it, which an <c>EnsureOpen</c> here would have made impossible.</para>
    ///
    /// <para><b>Re-pointing is refused, not silently applied.</b> A link that quietly moves leaves
    /// no record of what it used to say, so changing one is unlink-then-link: two acts, two log
    /// lines.</para>
    /// </remarks>
    public async Task<StaffGrievanceDto> LinkSourceAsync(
        Guid grievanceId, LinkErCaseSourceDto dto, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);

        var existing = ExistingLinkId(grievance, dto.Source);
        if (existing == dto.RecordId)
            throw new InvalidOperationException($"This case is already linked to that {Label(dto.Source)}.");
        if (existing.HasValue)
            throw new InvalidOperationException(
                $"This case is already linked to a different {Label(dto.Source)}. Remove that link first — "
                + "re-pointing a cross-reference would lose the record of what it used to say.");

        await EnsureSourceIsAboutSomebodyOnCaseAsync(
            dto.Source, dto.RecordId, OnCaseEmployeeIds(grievance), cancellationToken);

        SetLink(grievance, dto.Source, dto.RecordId);
        grievance.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee-relations case {Number} linked to {Source} {RecordId}.",
            grievance.GrievanceNumber, dto.Source, dto.RecordId);

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
    }

    public async Task<StaffGrievanceDto> UnlinkSourceAsync(
        Guid grievanceId, EmployeeRelationsLinkSource source, CancellationToken cancellationToken = default)
    {
        var grievance = await GetOwnedAsync(grievanceId, cancellationToken);

        if (ExistingLinkId(grievance, source) is null)
            throw new InvalidOperationException($"This case has no {Label(source)} link to remove.");

        SetLink(grievance, source, null);
        grievance.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee-relations case {Number} unlinked from {Source}.",
            grievance.GrievanceNumber, source);

        return await ToDtoAsync(await GetOwnedAsync(grievanceId, cancellationToken), null, cancellationToken);
    }

    /// <summary>
    /// The reverse read — which employee-relations cases point at this record.
    /// </summary>
    /// <remarks>
    /// Runs off <see cref="RegisterQuery"/> and projects in the database: this is what a source
    /// module's screen calls to render "2 employee-relations cases", and dragging whole case graphs
    /// across to count them is the shape slice 1 already avoided on the register itself.
    /// </remarks>
    public async Task<IEnumerable<ErLinkedCaseDto>> GetCasesForSourceAsync(
        EmployeeRelationsLinkSource source, Guid recordId, CancellationToken cancellationToken = default)
    {
        var query = RegisterQuery(GetTenantId());

        query = source switch
        {
            EmployeeRelationsLinkSource.SafetyIncident => query.Where(g => g.SafetyIncidentId == recordId),
            EmployeeRelationsLinkSource.PerformanceImprovementPlan => query.Where(g => g.PerformanceImprovementPlanId == recordId),
            EmployeeRelationsLinkSource.DisciplinaryCase => query.Where(g => g.StaffDisciplinaryActionId == recordId),
            _ => throw new ArgumentException($"'{source}' is not a linkable record type."),
        };

        return await query
            .OrderByDescending(g => g.FiledDate)
            .Select(g => new ErLinkedCaseDto
            {
                Id = g.Id,
                GrievanceNumber = g.GrievanceNumber,
                CaseType = g.CaseType,
                Subject = g.Subject,
                Status = g.Status,
                FiledDate = g.FiledDate,
                EmployeeId = g.EmployeeId,
                EmployeeName = g.Employee.FullName,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The rule: a case may only be linked to a record about somebody already named on it.
    /// </summary>
    /// <remarks>
    /// <para>It does two jobs at once. It gives the link a checkable meaning — <i>this case and
    /// this record are about the same person</i> — rather than "somebody thought these were
    /// related", which no rule can ever police. And it makes it impossible for a cross-reference to
    /// introduce a person the case file does not already name, which is the whole of its leak
    /// surface.</para>
    ///
    /// <para><b>It is not a hardship.</b> A union consultation about a pattern of incidents links
    /// once the affected employee is on the case as a party — and a case file that names who it is
    /// about is more correct than one that does not. The refusal says exactly that.</para>
    ///
    /// <para><b>Stood-down parties do not count.</b> Linking is a present-tense act by the desk; a
    /// party who has withdrawn from the case is not a reason to start cross-referencing records
    /// about them.</para>
    /// </remarks>
    private async Task EnsureSourceIsAboutSomebodyOnCaseAsync(
        EmployeeRelationsLinkSource source, Guid recordId, ISet<Guid> onCase, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        var summary = await LoadSourceSummaryAsync(source, recordId, tenantId, cancellationToken);
        // A soft-deleted source is "not found" for the purpose of MAKING a link. An existing link
        // to one is kept and rendered dead — see ErCaseSourceLinkDto.Available — but nobody may
        // create a new reference to a record its own module has retired.
        if (summary is null || summary.Deleted)
            throw new ArgumentException($"The {Label(source)} with ID '{recordId}' was not found.");

        var about = await LoadSourceAboutEmployeesAsync(source, recordId, tenantId, cancellationToken);
        if (!about.Any(onCase.Contains))
            throw new InvalidOperationException(
                $"That {Label(source)} is not about anybody on this case. A cross-reference must be to a record "
                + "concerning the primary party or one of the case's parties — add the person to the case first, "
                + "then link.");
    }

    /// <summary>The thin pointer a link renders as. Number, date, status — never substance.</summary>
    private sealed record SourceSummary(
        string Number, DateTime Date, string Status, Guid? SubjectId, string? SubjectName, bool Deleted);

    private async Task<SourceSummary?> LoadSourceSummaryAsync(
        EmployeeRelationsLinkSource source, Guid recordId, Guid tenantId, CancellationToken cancellationToken)
    {
        // GetQueryableIncludingDeleted throughout: a link to a retired record must still RENDER,
        // carrying Available = false, rather than silently disappearing off the case file.
        switch (source)
        {
            case EmployeeRelationsLinkSource.SafetyIncident:
            {
                var row = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Safety.SafetyIncident>()
                    .GetQueryableIncludingDeleted(x => x.Id == recordId && x.TenantId == tenantId)
                    .Select(x => new { x.IncidentNumber, x.IncidentDate, x.Status, x.IsDeleted })
                    .FirstOrDefaultAsync(cancellationToken);

                // ⚠ SubjectEmployeeId is deliberately null for an incident. An incident has no ONE
                // subject — it has involved persons, witnesses and a reporter — and naming any one
                // of them here would be a guess presented as a fact.
                return row is null ? null : new SourceSummary(
                    row.IncidentNumber, row.IncidentDate, Humanise(row.Status.ToString()), null, null, row.IsDeleted);
            }

            case EmployeeRelationsLinkSource.PerformanceImprovementPlan:
            {
                var row = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.PerformanceImprovementPlan>()
                    .GetQueryableIncludingDeleted(x => x.Id == recordId && x.TenantId == tenantId)
                    .Select(x => new { x.PipNumber, x.StartDate, x.Status, x.IsDeleted, x.EmployeeId, Name = x.Employee.FullName })
                    .FirstOrDefaultAsync(cancellationToken);

                return row is null ? null : new SourceSummary(
                    row.PipNumber, row.StartDate, Humanise(row.Status.ToString()), row.EmployeeId, row.Name, row.IsDeleted);
            }

            case EmployeeRelationsLinkSource.DisciplinaryCase:
            {
                var row = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffDiscipline.StaffDisciplinaryAction>()
                    .GetQueryableIncludingDeleted(x => x.Id == recordId && x.TenantId == tenantId)
                    .Select(x => new { x.CaseNumber, x.IncidentDate, x.Status, x.IsDeleted, x.EmployeeId, Name = x.Employee.FullName })
                    .FirstOrDefaultAsync(cancellationToken);

                return row is null ? null : new SourceSummary(
                    row.CaseNumber, row.IncidentDate, Humanise(row.Status.ToString()), row.EmployeeId, row.Name, row.IsDeleted);
            }

            default:
                throw new ArgumentException($"'{source}' is not a linkable record type.");
        }
    }

    /// <summary>
    /// Whom a source record is about — the set the case must intersect for a link to be allowed.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="LoadSourceSummaryAsync"/> rather than folded into it because reading a
    /// case file does not need it: an incident's involved-person query would otherwise run on every
    /// case read that happens to carry an incident link, to answer a question only the WRITE asks.
    ///
    /// <para>For an incident the set is its involved persons who are employees, plus the reporter.
    /// The reporter is included on purpose: an employee-relations case about how somebody was
    /// treated <i>for having reported</i> an incident is about that incident, and excluding them
    /// would have made the clearest victimisation case in the module unrecordable.</para>
    /// </remarks>
    private async Task<IReadOnlyCollection<Guid>> LoadSourceAboutEmployeesAsync(
        EmployeeRelationsLinkSource source, Guid recordId, Guid tenantId, CancellationToken cancellationToken)
    {
        switch (source)
        {
            case EmployeeRelationsLinkSource.SafetyIncident:
            {
                var involved = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Safety.SafetyIncidentInvolvedPerson>()
                    .GetQueryable(p => p.IncidentId == recordId && p.TenantId == tenantId && p.EmployeeId != null)
                    .Select(p => p.EmployeeId!.Value)
                    .ToListAsync(cancellationToken);

                var reporter = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Safety.SafetyIncident>()
                    .GetQueryableIncludingDeleted(x => x.Id == recordId && x.TenantId == tenantId)
                    .Select(x => (Guid?)x.ReportedById)
                    .FirstOrDefaultAsync(cancellationToken);

                if (reporter is Guid reporterId) involved.Add(reporterId);
                return involved;
            }

            case EmployeeRelationsLinkSource.PerformanceImprovementPlan:
            {
                // The subject only. A PIP's supervisor and HR owner run it; they are not what it is
                // about, and counting them would let any case about a manager link to every plan
                // they happen to supervise.
                var id = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.PerformanceImprovementPlan>()
                    .GetQueryableIncludingDeleted(x => x.Id == recordId && x.TenantId == tenantId)
                    .Select(x => (Guid?)x.EmployeeId)
                    .FirstOrDefaultAsync(cancellationToken);
                return id is Guid pipEmployee ? new[] { pipEmployee } : Array.Empty<Guid>();
            }

            case EmployeeRelationsLinkSource.DisciplinaryCase:
            {
                // Likewise: the accused, not the reporter or the decision-maker.
                var id = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffDiscipline.StaffDisciplinaryAction>()
                    .GetQueryableIncludingDeleted(x => x.Id == recordId && x.TenantId == tenantId)
                    .Select(x => (Guid?)x.EmployeeId)
                    .FirstOrDefaultAsync(cancellationToken);
                return id is Guid accused ? new[] { accused } : Array.Empty<Guid>();
            }

            default:
                throw new ArgumentException($"'{source}' is not a linkable record type.");
        }
    }

    /// <summary>The primary party plus every ACTIVE party who is an employee.</summary>
    private static HashSet<Guid> OnCaseEmployeeIds(StaffGrievance grievance)
    {
        var ids = new HashSet<Guid> { grievance.EmployeeId };
        foreach (var party in grievance.Parties)
        {
            if (party.IsDeleted || party.RemovedDate != null) continue;
            if (party.EmployeeId is Guid employeeId) ids.Add(employeeId);
        }
        return ids;
    }

    private static Guid? ExistingLinkId(StaffGrievance g, EmployeeRelationsLinkSource source) => source switch
    {
        EmployeeRelationsLinkSource.SafetyIncident => g.SafetyIncidentId,
        EmployeeRelationsLinkSource.PerformanceImprovementPlan => g.PerformanceImprovementPlanId,
        EmployeeRelationsLinkSource.DisciplinaryCase => g.StaffDisciplinaryActionId,
        _ => throw new ArgumentException($"'{source}' is not a linkable record type."),
    };

    private static void SetLink(StaffGrievance g, EmployeeRelationsLinkSource source, Guid? recordId)
    {
        switch (source)
        {
            case EmployeeRelationsLinkSource.SafetyIncident: g.SafetyIncidentId = recordId; break;
            case EmployeeRelationsLinkSource.PerformanceImprovementPlan: g.PerformanceImprovementPlanId = recordId; break;
            case EmployeeRelationsLinkSource.DisciplinaryCase: g.StaffDisciplinaryActionId = recordId; break;
            default: throw new ArgumentException($"'{source}' is not a linkable record type.");
        }
    }

    private static string Label(EmployeeRelationsLinkSource source) => source switch
    {
        EmployeeRelationsLinkSource.SafetyIncident => "safety incident",
        EmployeeRelationsLinkSource.PerformanceImprovementPlan => "performance improvement plan",
        EmployeeRelationsLinkSource.DisciplinaryCase => "disciplinary case",
        _ => "record",
    };

    /// <summary>
    /// The case file's link block. <b>HR only</b> — see <see cref="StaffGrievanceDto.Links"/>.
    /// </summary>
    private async Task<List<ErCaseSourceLinkDto>> BuildLinksAsync(
        StaffGrievance g, CancellationToken cancellationToken)
    {
        var links = new List<ErCaseSourceLinkDto>();
        if (!IsHr) return links;

        var tenantId = GetTenantId();

        // Assembled in enum order, so the block reads the same way twice.
        var present = new List<(EmployeeRelationsLinkSource Source, Guid RecordId)>();
        if (g.SafetyIncidentId is Guid incident) present.Add((EmployeeRelationsLinkSource.SafetyIncident, incident));
        if (g.PerformanceImprovementPlanId is Guid pip) present.Add((EmployeeRelationsLinkSource.PerformanceImprovementPlan, pip));
        if (g.StaffDisciplinaryActionId is Guid discipline) present.Add((EmployeeRelationsLinkSource.DisciplinaryCase, discipline));

        foreach (var (source, recordId) in present)
        {
            var summary = await LoadSourceSummaryAsync(source, recordId, tenantId, cancellationToken);

            links.Add(new ErCaseSourceLinkDto
            {
                Source = source,
                SourceLabel = Humanise(source.ToString()),
                RecordId = recordId,
                // A row that has vanished entirely — a hard delete in the source module, which the
                // FK should prevent — still renders as a dead reference rather than throwing. A case
                // file must not become unreadable because another module broke its own rules.
                Number = summary?.Number ?? "(record unavailable)",
                Date = summary?.Date ?? default,
                Status = summary?.Status ?? string.Empty,
                SubjectEmployeeId = summary?.SubjectId,
                SubjectEmployeeName = summary?.SubjectName,
                Available = summary is { Deleted: false },
            });
        }

        return links;
    }

    /// <summary>"HeadOfDepartment" → "Head Of Department". Enum names are not labels.</summary>
    private static string Humanise(string pascal)
        => string.Concat(pascal.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));

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
        // ⚠ Open AND unanswered. The step fact alone said "awaiting response" on a withdrawn case,
        // which is not true of anybody: the employee took it back, so no rung owes an answer.
        AwaitingResponse = IsOpen(g.Status)
            && g.Steps.OrderBy(s => s.Sequence).LastOrDefault()?.Outcome
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
    /// The case file, with its cross-links loaded — area 9c slice 9.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists at all, rather than an Include in <see cref="Scoped"/>.</b> A
    /// <c>SafetyIncident</c> alone carries a 4000-character Description, three 2000-character cause
    /// fields, a 4000-character root-cause analysis and 4000 characters of findings. Joining it onto
    /// the case root — where the statement, the interpretation, the investigation report and the
    /// resolution already live — is the same 8060-byte row that made <c>FileAsync</c> save a
    /// grievance and then die reading it back in slice 3. The links are loaded as thin projections
    /// instead, and only for HR.</para>
    ///
    /// <para><b>And why EVERY write path goes through it</b>, not just link and unlink. A write
    /// response that returns the case file with an empty <c>Links</c> is the stale-navigation defect
    /// this module has hit before: a client that re-renders from the response of <c>respond</c>
    /// would watch the desk's cross-references disappear.</para>
    /// </remarks>
    private async Task<StaffGrievanceDto> ToDtoAsync(
        StaffGrievance g, Guid? callerEmployeeId, CancellationToken cancellationToken)
    {
        var dto = ToDto(g, callerEmployeeId);
        dto.Links = await BuildLinksAsync(g, cancellationToken);
        return dto;
    }

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
        AwaitingResponse = IsOpen(g.Status)
            && g.Steps.OrderBy(s => s.Sequence).LastOrDefault()?.Outcome
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
