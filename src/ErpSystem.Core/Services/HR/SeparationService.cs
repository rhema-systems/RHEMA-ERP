using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Assets;
using ErpSystem.Core.Services.HR.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The exit register (area 9b, FRD §A1.10 / §3.A.2).
/// </summary>
/// <remarks>
/// <para><b>One record per exit, whatever the route.</b> Before this service the only way out of
/// the organisation was a disciplinary case, so resignation, retirement, contract expiry and death
/// had enum members and no route at all. The disciplinary route now writes here too, through
/// <c>DisciplinaryActionId</c> — two exit stores that can disagree is how 29 disciplinary
/// terminations came to sit against employees who were all still <c>StaffStatus = Active</c>.</para>
///
/// <para><b>Status is never taken from the client.</b> A separation starts as a draft and every
/// move from there — approval, clearance, settlement, completion — is its own endpoint with its own
/// rule and its own gate. There is deliberately no "set status" path, because FR-HR-091 makes
/// clearance a gate on the settlement and a settable status would walk straight past it.</para>
/// </remarks>
public class SeparationService : ISeparationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ICurrencyService _currencies;
    private readonly IEmployeeService _employeeService;

    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;

    /// <summary>
    /// HR Assets' read-only answer to "what has this leaver not given back?" — FR-HR-183.
    /// </summary>
    /// <remarks>
    /// Area 16 owns what counts as an unreturned custody and what counts as money owed on one;
    /// this service owns the form and the gate. See <see cref="AssetCustodyClearanceBridge"/> for
    /// why the query lives over there rather than being restated here.
    /// </remarks>
    private readonly AssetCustodyClearanceBridge _assetCustody;
    private readonly IHrFinancePostingAdapter _financePosting;

    /// <summary>
    /// Leave's own answer to "how much annual leave is this person owed at a date" — the working the
    /// leave owed report uses (round 5, lane L2), so the leaver's figure and Finance's agree.
    /// </summary>
    private readonly ILeaveOwedCalculator _leaveOwed;

    private readonly ILogger<SeparationService> _logger;

    /// <summary>
    /// The workflow entity type this service drives. Who may sign at a step comes from the
    /// published definition; what the outcome MEANS on the record comes from
    /// <c>EmployeeSeparationWorkflowStatusAdapter</c>.
    /// </summary>
    private const string EntityType = "EmployeeSeparation";

    public SeparationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ICompanyHrPolicyProvider policyProvider,
        ICurrencyService currencies,
        IEmployeeService employeeService,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        AssetCustodyClearanceBridge assetCustody,
        ILogger<SeparationService> logger,
        IHrFinancePostingAdapter financePosting,
        ILeaveOwedCalculator leaveOwed)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _policyProvider = policyProvider;
        _currencies = currencies;
        _employeeService = employeeService;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
        _assetCustody = assetCustody;
        _logger = logger;
        _financePosting = financePosting;
        _leaveOwed = leaveOwed;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// Tenant-scoped separations with every navigation the DTOs read.
    /// </summary>
    /// <remarks>
    /// The includes are here rather than per-query on purpose: a list read that omits one renders
    /// its name column as "—" while still returning 200, which is the single most common defect
    /// shape in the ported HR code.
    /// </remarks>
    private IQueryable<EmployeeSeparation> Scoped(Guid tenantId)
        => _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .Include(s => s.Employee).ThenInclude(e => e.Position)
            .Include(s => s.Employee).ThenInclude(e => e.OrganizationUnit)
            .Include(s => s.InitiatedBy)
            .Include(s => s.ApprovedBy)
            .Include(s => s.CancelledBy)
            .Include(s => s.SubmittedBy)
            .Include(s => s.RejectedBy)
            .Include(s => s.NoticeDecidedBy)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted);

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PagedResult<EmployeeSeparationListDto>> GetPagedAsync(
        EmployeeSeparationQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = GetTenantId();
        var page = query.PageNumber < 1 ? 1 : query.PageNumber;
        var size = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;

        var q = Scoped(tenantId);

        if (query.EmployeeId is { } employeeId && employeeId != Guid.Empty)
            q = q.Where(s => s.EmployeeId == employeeId);

        if (query.SeparationType is { } type)
            q = q.Where(s => s.SeparationType == type);

        if (query.Status is { } status)
            q = q.Where(s => s.Status == status);

        if (query.EffectiveFrom is { } from)
            q = q.Where(s => s.EffectiveDate != null && s.EffectiveDate >= from);

        if (query.EffectiveTo is { } to)
            q = q.Where(s => s.EffectiveDate != null && s.EffectiveDate <= to);

        if (query.OnlyUnappliedToEmployee == true)
            q = q.Where(s => s.Status == SeparationStatus.Completed && s.EmployeeRecordUpdatedOn == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(s =>
                s.SeparationNumber.Contains(term)
                || s.Employee.FirstName.Contains(term)
                || s.Employee.LastName.Contains(term)
                || (s.Employee.EmployeeNumber != null && s.Employee.EmployeeNumber.Contains(term)));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        // Ordered on two keys, the second of them unique. A single non-unique key leaves the page
        // boundary to the server's whim, which is how area 17's audit found a row that appeared on
        // two pages and on neither.
        var items = await q
            .OrderByDescending(s => s.InitiatedOn)
            .ThenByDescending(s => s.SeparationNumber)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeSeparationListDto>
        {
            Items = items.Select(ToListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = size,
        };
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Scoped(tenantId).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return entity == null ? null : ToDetailDto(entity);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EmployeeSeparationListDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var items = await Scoped(tenantId)
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.InitiatedOn)
            .ThenByDescending(s => s.SeparationNumber)
            .ToListAsync(cancellationToken);

        return items.Select(ToListDto).ToList();
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CreateAsync(
        CreateEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        // Checked explicitly: [Required] is a no-op on a non-nullable Guid, so an omitted employee
        // arrives as Guid.Empty and would otherwise be reported as "employee 00000000-… not found",
        // an answer about the wrong thing.
        if (dto.EmployeeId == Guid.Empty)
            throw new ArgumentException("Name the employee who is leaving.");

        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{dto.EmployeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} has already left — their staff status is Terminated.");

        // One open separation at a time. Without this an employee could be resigning and retiring
        // simultaneously, each with its own clearance run and its own settlement.
        var open = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId
                        && !s.IsDeleted
                        && s.EmployeeId == dto.EmployeeId
                        && s.Status != SeparationStatus.Completed
                        && s.Status != SeparationStatus.Cancelled
                        && s.Status != SeparationStatus.Rejected, cancellationToken);

        if (open)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} already has a separation in progress. "
                + "Cancel it before raising another.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var initiatedOn = dto.InitiatedOn ?? today;

        if (initiatedOn > today)
            throw new InvalidOperationException("A separation cannot be raised with a future initiation date.");

        if (employee.DateEmployed is { } employed && initiatedOn < employed)
            throw new InvalidOperationException(
                $"The separation is dated {initiatedOn:yyyy-MM-dd}, before this employee joined on {employed:yyyy-MM-dd}.");

        if (dto.LastWorkingDay is { } lastDay && dto.EffectiveDate is { } effective && lastDay > effective)
            throw new InvalidOperationException(
                "The last working day cannot fall after the date employment ends.");

        var settings = await _policyProvider.GetAsync(cancellationToken);
        var noticeDays = dto.NoticeDays ?? DefaultNoticeDays(dto.SeparationType, settings);

        if (noticeDays is < 0)
            throw new InvalidOperationException("Notice days cannot be negative.");

        // FR-HR-093: compulsory retirement takes effect ON THE BIRTHDAY. The date is not a choice,
        // so it is computed here and a contradicting one is refused rather than accepted and
        // quietly overwritten — somebody who typed a date deserves to know it was wrong.
        var effectiveDate = dto.EffectiveDate;
        if (dto.SeparationType == EmployeeTerminationType.CompulsoryRetirement)
        {
            var due = HrPolicyCalculations.RetirementDate(settings, employee);

            if (due is null)
                throw new InvalidOperationException(
                    $"{employee.FirstName} {employee.LastName} has no date of birth and no retirement "
                    + "date on record, so the retirement date cannot be worked out. Record one before "
                    + "raising a compulsory retirement.");

            if (effectiveDate is { } supplied && supplied != due.Value)
                throw new InvalidOperationException(
                    $"A compulsory retirement takes effect on the employee's birthday — "
                    + $"{due.Value:yyyy-MM-dd} for this employee (FR-HR-093). The date supplied was "
                    + $"{supplied:yyyy-MM-dd}. Leave it blank to use the computed date, or raise a "
                    + "voluntary retirement if the employee is leaving on a different date.");

            effectiveDate = due.Value;

            // Re-checked against the COMPUTED date, not the supplied one. The guard above ran
            // before the birthday was worked out, so a last working day after the retirement date
            // would otherwise slip through on exactly the route that computes its own end date.
            if (dto.LastWorkingDay is { } lastDayOfService && lastDayOfService > effectiveDate)
                throw new InvalidOperationException(
                    $"The last working day ({lastDayOfService:yyyy-MM-dd}) falls after this employee's "
                    + $"retirement date ({effectiveDate:yyyy-MM-dd}).");
        }

        // A contract expiry ends on the day the contract ends — by definition of the route. An exit
        // on some other date is a different kind of separation, not a contract expiring.
        //
        // ⚠ The rule bites only where a contract end date exists, and on the live tenant **none
        // does**: 202 active contracts, 0 with an EndDate. So today this is inert for everybody,
        // like the retirement rule was before fixtures. Where the data arrives, it applies.
        if (dto.SeparationType == EmployeeTerminationType.ContractExpiry)
        {
            var (contractEnd, contractNumber) = await ActiveContractEndAsync(
                tenantId, employee.Id, cancellationToken);

            if (contractEnd is { } ends)
            {
                if (effectiveDate is { } supplied && supplied != ends)
                    throw new InvalidOperationException(
                        $"A contract expiry ends on the day the contract ends — {ends:yyyy-MM-dd} on "
                        + $"contract {contractNumber}. The date supplied was {supplied:yyyy-MM-dd}. "
                        + "Leave it blank to use the contract's date, or raise a different separation "
                        + "type if the employee is leaving early.");

                effectiveDate = ends;
            }
        }

        var entity = new EmployeeSeparation
        {
            // Set explicitly: the DbContext auto-stamp is dead in this codebase, and a missing
            // TenantId surfaces as a foreign-key 547 on save rather than as anything readable.
            TenantId = tenantId,
            SeparationNumber = await NextNumberAsync(tenantId, initiatedOn, cancellationToken),
            EmployeeId = employee.Id,
            SeparationType = dto.SeparationType,
            Status = SeparationStatus.Draft,
            ReasonCategory = dto.ReasonCategory,
            ReasonNotes = string.IsNullOrWhiteSpace(dto.ReasonNotes) ? null : dto.ReasonNotes.Trim(),
            InitiatedOn = initiatedOn,
            NoticeGivenOn = dto.NoticeGivenOn,
            NoticeDays = noticeDays,
            LastWorkingDay = dto.LastWorkingDay,
            EffectiveDate = effectiveDate,
            InitiatedById = actorEmployeeId,
            IsSystemInitiated = actorEmployeeId is null,
            IsEligibleForRehire = dto.IsEligibleForRehire,
            EligibleForRehireDate = dto.EligibleForRehireDate,
            RehireRestrictions = string.IsNullOrWhiteSpace(dto.RehireRestrictions) ? null : dto.RehireRestrictions.Trim(),
            DisciplinaryActionId = dto.DisciplinaryActionId,
            AbsenceDays = dto.AbsenceDays,
        };

        await _unitOfWork.Repository<EmployeeSeparation>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {Number} raised for employee {EmployeeId} ({Type})",
            entity.SeparationNumber, entity.EmployeeId, entity.SeparationType);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> UpdateAsync(
        Guid id, UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {entity.Status} and can no longer be amended. "
                + "Cancel it and raise a new one if the details are wrong.");

        if (dto.SeparationType is { } type) entity.SeparationType = type;
        // ⚠ A board's finding supports a medical retirement and nothing else (lane K-II-a); left
        // behind on another route out it would read as evidence for something it never said.
        if (entity.SeparationType != EmployeeTerminationType.MedicalRetirement) entity.MedicalBoardId = null;
        if (dto.ReasonCategory is { } reason) entity.ReasonCategory = reason;
        if (dto.ReasonNotes is not null)
            entity.ReasonNotes = string.IsNullOrWhiteSpace(dto.ReasonNotes) ? null : dto.ReasonNotes.Trim();
        if (dto.NoticeGivenOn is { } noticeGiven) entity.NoticeGivenOn = noticeGiven;
        if (dto.NoticeDays is { } noticeDays)
        {
            if (noticeDays < 0) throw new InvalidOperationException("Notice days cannot be negative.");
            entity.NoticeDays = noticeDays;
        }
        if (dto.LastWorkingDay is { } lastDay) entity.LastWorkingDay = lastDay;
        if (dto.EffectiveDate is { } effective) entity.EffectiveDate = effective;
        if (dto.IsEligibleForRehire is { } rehire) entity.IsEligibleForRehire = rehire;
        if (dto.EligibleForRehireDate is { } rehireDate) entity.EligibleForRehireDate = rehireDate;
        if (dto.AbsenceDays is { } absence)
        {
            if (absence < 0) throw new InvalidOperationException("Days of absence cannot be negative.");
            entity.AbsenceDays = absence;
        }
        if (dto.RehireRestrictions is not null)
            entity.RehireRestrictions = string.IsNullOrWhiteSpace(dto.RehireRestrictions) ? null : dto.RehireRestrictions.Trim();

        if (entity.LastWorkingDay is { } lwd && entity.EffectiveDate is { } eff && lwd > eff)
            throw new InvalidOperationException("The last working day cannot fall after the date employment ends.");

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CancelAsync(
        Guid id, CancelEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Give a reason for cancelling the separation.");

        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status == SeparationStatus.Completed)
            throw new InvalidOperationException(
                "This separation has completed — the employee has left and been paid. It cannot be cancelled.");

        if (entity.Status == SeparationStatus.Cancelled)
            throw new InvalidOperationException("This separation is already cancelled.");

        entity.Status = SeparationStatus.Cancelled;
        entity.CancelledOn = DateTime.UtcNow;
        entity.CancelledById = actorEmployeeId;
        entity.CancellationReason = dto.Reason.Trim();

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Separation {Number} cancelled", entity.SeparationNumber);
        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>⚠ <b>This closes a column nothing could set.</b> <c>MedicalBoardId</c> went in with the
    /// residue plan's G4 and no screen, endpoint or service ever wrote it (lane K-II-a).</para>
    ///
    /// <para>⚠ <b>A decided finding, not a pending board</b> — unlike leave, which lets a request
    /// name the board it is waiting on and enforces the finding at its gate. Here the link IS the
    /// evidence (submission accepts it in place of the medical report), so it is checked when made.
    /// A board can find somebody unfit for their post and fit for redeployment; only a case that
    /// recommends retirement can carry one.</para>
    ///
    /// <para>The board stays the thing linked, as on leave: an employee is before a board at most
    /// once, so the board and this separation's employee name exactly one case.</para>
    /// </remarks>
    public async Task<EmployeeSeparationDetailDto> LinkMedicalBoardAsync(
        Guid id, Guid? medicalBoardId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {entity.Status} and can no longer be amended. The board it rests on "
                + "was fixed when it was submitted.");

        if (medicalBoardId is Guid boardId)
        {
            if (entity.SeparationType != EmployeeTerminationType.MedicalRetirement)
                throw new InvalidOperationException(
                    "Only a medical retirement rests on a medical board's finding.");

            var board = await _unitOfWork.Repository<MedicalBoard>().GetQueryable()
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.Id == boardId)
                .Select(b => new { b.BoardNumber })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"Medical board '{boardId}' not found.");

            var onBoard = await _unitOfWork.Repository<MedicalBoardCase>().GetQueryable()
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.BoardId == boardId && c.EmployeeId == entity.EmployeeId)
                .Select(c => new { c.Status, c.RecommendsMedicalRetirement })
                .FirstOrDefaultAsync(cancellationToken);

            if (onBoard is null)
                throw new InvalidOperationException(
                    $"Medical board {board.BoardNumber} has no case about this employee, so a retirement "
                    + "cannot rest on it.");

            if (onBoard.Status != MedicalBoardCaseStatus.Concluded)
                throw new InvalidOperationException(onBoard.Status == MedicalBoardCaseStatus.Withdrawn
                    ? $"Medical board {board.BoardNumber} withdrew this employee's case without deciding it."
                    : $"Medical board {board.BoardNumber} has not yet decided this employee's case. Link it "
                      + "once it has.");

            if (!onBoard.RecommendsMedicalRetirement)
                throw new InvalidOperationException(
                    $"Medical board {board.BoardNumber}'s finding on this employee does not recommend "
                    + "medical retirement, so a medical retirement cannot rest on it.");
        }

        entity.MedicalBoardId = medicalBoardId;

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status == SeparationStatus.Completed)
            throw new InvalidOperationException(
                "A completed separation is the record of someone's exit and its settlement. It cannot be deleted.");

        await _unitOfWork.Repository<EmployeeSeparation>().DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> SubmitAsync(
        Guid id, SubmitEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {entity.Status}; only a draft can be submitted.");

        // A resignation IS its letter, and its date is where the notice clock starts. Without it
        // there is no way to tell notice served from notice owed, and FR-HR-184's notice pay is
        // computed from exactly that difference. Other routes carry no notice date by nature —
        // nobody serves notice on a bereavement — so this is asked of resignation alone.
        if (entity.SeparationType == EmployeeTerminationType.VoluntaryResignation && entity.NoticeGivenOn is null)
            throw new InvalidOperationException(
                "Record the date notice was given before submitting a resignation — the notice "
                + "period, and any shortfall to be paid, are both counted from it.");

        // A medical retirement or a death asserts something a file should be able to evidence.
        await RequireSupportingEvidenceAsync(tenantId, entity, cancellationToken);

        // Derive what follows rather than demanding it twice. The last working day is the day
        // notice runs out unless someone says otherwise.
        if (entity.LastWorkingDay is null && entity.NoticeGivenOn is { } given && entity.NoticeDays is { } days)
            entity.LastWorkingDay = given.AddDays(days);

        entity.EffectiveDate ??= entity.LastWorkingDay;

        if (entity.EffectiveDate is null)
            throw new InvalidOperationException(
                "This separation has no end date and none can be worked out from the notice given. "
                + "Set the effective date, or the notice date and period, before submitting.");

        if (entity.LastWorkingDay is { } lwd && entity.EffectiveDate is { } eff && lwd > eff)
            throw new InvalidOperationException("The last working day cannot fall after the date employment ends.");

        // FR-HR-092's exception is decided here and frozen, not evaluated at approval time. The
        // threshold is tenant policy and policy can change; who was entitled to sign a separation
        // must not change underneath it after it was queued.
        var settings = await _policyProvider.GetAsync(cancellationToken);
        entity.IsProcedural =
            entity.AbsenceDays is { } absent
            && settings.ProceduralAbsenceDays > 0
            && absent >= settings.ProceduralAbsenceDays;

        entity.SubmittedById = actorEmployeeId;

        // ⚠ The status is NOT set here. Submitting starts the FR-HR-092 approval on the generic
        // workflow engine, and the adapter writes whatever the engine's outcome means — normally
        // PendingApproval, but Approved outright if the published definition routes this exit
        // straight through. Setting it here as well would be a second opinion about a decision the
        // engine owns, and the two would eventually disagree.
        //
        // Everything above this line stays in the service: the resignation-needs-a-notice-date rule,
        // the evidence a medical retirement or a death must carry, the derived dates, and the
        // freezing of IsProcedural. Those are facts about the RECORD, not routing choices, and they
        // must refuse before an approver is ever troubled with the exit.
        // ⚠ SAVED BEFORE THE HAND-OFF, and this is load-bearing. The engine builds its routing
        // context by READING the separation back, so anything set above but not yet persisted is
        // invisible to it. IsProcedural is exactly that: set a few lines up, and the one fact the
        // FR-HR-092 routing branches on. Handing off first meant every procedural termination was
        // read as non-procedural and routed to the Managing Director — and because the condition
        // evaluator answers false on anything it cannot resolve, the misrouting was silent.
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ Submitting must never approve, and here that is the whole of FR-HR-092.
        //
        // With no published definition the engine returns WorkflowOutcome.Approved, and the
        // adapter maps it to SeparationStatus.Approved — so Submit ENDED SOMEONE'S EMPLOYMENT with
        // nobody having signed it. Worse, it did so by never reaching ApproveAsync, which is where
        // RequireDecisionAuthority lives: the Managing Director's signature was not refused, it was
        // never asked for. That is the single most consequential instance of this defect in HR.
        //
        // Defence in depth — an EMPLOYEE_SEPARATION definition IS seeded (mdOnly), so this bites
        // only on a tenant where seeding has not run. See HrWorkflowFallbackAuthority.
        var (submitted, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflow, EntityType, entity.Id);
        if (!submitted.ExecutionResult.Success)
            throw new InvalidOperationException(
                submitted.ExecutionResult.Message ?? "Failed to start the separation approval workflow.");

        _workflowAdapters.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            entity.ReasonNotes = string.IsNullOrWhiteSpace(entity.ReasonNotes)
                ? dto.Notes.Trim()
                : $"{entity.ReasonNotes}\n\n{dto.Notes.Trim()}";
        }

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── G-3.6: an ANTICIPATED vacancy, which nothing in the solution had ever written ────────
        // PositionVacancyStatus.Anticipated was set by no service, IsAnticipated was only ever
        // assigned false, and ExpectedVacancyDate was never written at all — so the establishment
        // screen's Anticipated tile read 0 unless a human had manually overridden a row into it,
        // despite the entity carrying two fields for exactly this.
        //
        // Notice given with a future end date is precisely the condition those fields describe: the
        // seat is not empty yet, and HR knows the day it will be. Logging it here — at submission,
        // when the date is first derived and frozen — is what gives succession and recruitment the
        // lead time the register exists to provide. ApplySeparationOutcomeAsync later logs the real
        // departure, and PositionVacancyLog promotes this row from Anticipated to Open rather than
        // opening a second one.
        //
        // Deliberately not gated on approval: an exit that is later rejected leaves an anticipated
        // row that reconcile closes once the seat is seen to be occupied, which is the cheaper
        // error. Telling HR late about a departure is the expensive one.
        // EffectiveDate is a DateOnly; PositionVacancy dates the register in DateTime, so the
        // comparison is made in DateOnly and the value converted once, at the call.
        if (entity.EffectiveDate is { } expectedVacancy &&
            expectedVacancy > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            var expectedVacancyOn = expectedVacancy.ToDateTime(TimeOnly.MinValue);

            var separatingEmployee = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
                .Where(e => e.Id == entity.EmployeeId && e.TenantId == tenantId)
                .Select(e => new { e.PositionId })
                .FirstOrDefaultAsync(cancellationToken);

            if (separatingEmployee is not null)
            {
                await PositionVacancyLog.LogDepartureAsync(
                    _unitOfWork,
                    tenantId,
                    entity.EmployeeId,
                    separatingEmployee.PositionId,
                    VacancyReasonForSeparation(entity.SeparationType),
                    expectedVacancyOn,
                    _currentUserProvider.UserId,
                    isAnticipated: true,
                    expectedVacancyDate: expectedVacancyOn,
                    note: $"Notified by separation {entity.SeparationNumber}.",
                    cancellationToken: cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        _logger.LogInformation(
            "Separation {Number} submitted for approval (effective {Effective:yyyy-MM-dd})",
            entity.SeparationNumber, entity.EffectiveDate);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <summary>
    /// Translates the separation type into why the seat will be empty (G-3.4 / G-3.6).
    /// </summary>
    /// <remarks>
    /// The three involuntary types and the two dismissals all map to <c>Termination</c>: the
    /// establishment register records that the post emptied by termination, not how gravely. Only
    /// redundancy is different in kind, because a redundant post is a restructure from the
    /// establishment's point of view rather than a seat to refill — and classifying it that way is
    /// what stops reconcile treating an abolished post as a gap.
    /// </remarks>
    private static VacancyReason VacancyReasonForSeparation(EmployeeTerminationType type) => type switch
    {
        EmployeeTerminationType.VoluntaryResignation   => VacancyReason.Resignation,
        EmployeeTerminationType.VoluntaryRetirement    => VacancyReason.Retirement,
        EmployeeTerminationType.CompulsoryRetirement   => VacancyReason.Retirement,
        EmployeeTerminationType.MedicalRetirement      => VacancyReason.Retirement,
        EmployeeTerminationType.Death                  => VacancyReason.Death,
        EmployeeTerminationType.InvoluntaryRedundancy  => VacancyReason.Restructure,
        EmployeeTerminationType.InvoluntaryForCause    => VacancyReason.Termination,
        EmployeeTerminationType.InvoluntaryPerformance => VacancyReason.Termination,
        EmployeeTerminationType.SummaryDismissal       => VacancyReason.Termination,
        EmployeeTerminationType.MutualAgreement        => VacancyReason.Termination,
        EmployeeTerminationType.ContractExpiry         => VacancyReason.Termination,
        _                                              => VacancyReason.Other,
    };

    // ── FR-HR-092: the decision ───────────────────────────────────────────────

    /// <summary>
    /// Whether the caller holds the Managing Director role, under either of its two seeded
    /// spellings.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>SuperAdmin is deliberately not here.</b> FR-HR-092 exists to put a named officer's
    /// signature on the ending of someone's employment; a technical superuser signing it is the
    /// thing the control is for, not an exemption from it. SuperAdmin can still read and
    /// administer, and can grant somebody the role.
    /// </remarks>
    private bool IsManagingDirector =>
        _currentUserProvider.Roles.Any(r =>
            string.Equals(r, Constants.Roles.ManagingDirector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.TdcManagingDirector, StringComparison.OrdinalIgnoreCase));

    private bool IsHrActor =>
        _currentUserProvider.Roles.Any(r =>
            string.Equals(r, Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.Hr, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.LegacyHrUser, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// FR-HR-092 in one place: the MD may decide any separation, HR only a procedural one.
    /// </summary>
    /// <remarks>
    /// This is read off the record rather than expressed as a permission because no permission can
    /// say "may approve this one but not that one" — and stacking a role attribute onto a policy
    /// attribute would AND them, admitting nobody. The endpoint therefore carries a plain
    /// <c>[Authorize]</c> and this decides.
    /// </remarks>
    private void RequireDecisionAuthority(EmployeeSeparation separation)
    {
        if (IsManagingDirector) return;

        if (separation.IsProcedural && IsHrActor) return;

        throw new UnauthorizedAccessException(
            separation.IsProcedural
                ? "Only HR or the Managing Director may decide a separation."
                : "Only the Managing Director may sign this separation. It is not procedural — "
                  + "under FR-HR-092 HR may approve only a termination for absence beyond the "
                  + "tenant's procedural threshold.");
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> ApproveAsync(
        Guid id, ApproveEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.PendingApproval)
            throw new InvalidOperationException(
                $"This separation is {entity.Status}; only one awaiting approval can be signed.");

        RequireDecisionAuthority(entity);

        // ⚠ The notice decision no longer rides here — see RecordNoticeDecisionAsync. Approval is a
        // yes/no plus a comment, which is all the generic workflow engine's approve action carries.

        // ⚠ TWO gates, deliberately, and in this order.
        //
        // RequireDecisionAuthority (above) is FR-HR-092: a rule about the RECORD — the Managing
        // Director may sign any exit, HR only a procedural one. It runs FIRST so a refusal explains
        // itself in terms of the separation rather than answering the generic "you are not assigned
        // as an approver", and so the rule holds even if the definition is missing or wrong.
        //
        // The engine's check is about the STEP: whether this user is the approver the published
        // definition assigned. A definition can express the same procedural split for assignment,
        // but the guarantee lives in the service.
        var actingUserId = _currentUserProvider.UserId;

        // ⚠ NO PERMISSION GATE ON THE NO-WORKFLOW BRANCH, and that is deliberate.
        //
        // Everywhere else in this programme the fallback authority is an HR.<Module>.Approve
        // permission. Here it must not be, because **RequireDecisionAuthority above already IS the
        // authority** — it is FR-HR-092 itself, it is role-based (the MD signs any exit; HR only a
        // procedural one), and its own comment says it holds "even if the definition is missing or
        // wrong". It is stronger than a permission check and it has already run.
        //
        // Adding HR.Separation.Approve on top would BLOCK THE MANAGING DIRECTOR, who holds only
        // ViewSeparation by design (ApprovalReaderGrants: "the authority to decide is not a
        // permission at all, it is read off the record"). The gate that exists is the right one;
        // a second gate here would refuse the very person the requirement names.
        WorkflowOutcome approvalOutcome;
        if (await _workflow.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflow.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
                throw new UnauthorizedAccessException(
                    "You are not assigned as an approver for the current step of this separation.");

            var approval = await _workflow.ProcessApprovalAsync(
                EntityType, entity.Id, actingUserId, "Approve", dto.Notes);
            if (!approval.ExecutionResult.Success)
                throw new InvalidOperationException(
                    approval.ExecutionResult.Message ?? "Failed to process the separation approval.");

            approvalOutcome = approval.Outcome;
        }
        else
        {
            approvalOutcome = WorkflowOutcome.Approved;
        }

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, actingUserId);

        // The engine names the ApplicationUser who acted; the record wants the Employee, because
        // ApprovedById is an Employee FK and "who signed this exit" is a person, not a login.
        entity.ApprovedById = actorEmployeeId;
        entity.ApprovedOn = DateTime.UtcNow;
        entity.ApprovalNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {Number} approved (procedural={Procedural})",
            entity.SeparationNumber, entity.IsProcedural);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> RecordNoticeDecisionAsync(
        Guid id, RecordSeparationNoticeDecisionDto dto, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        // ⚠ The real constraint is BEFORE THE SETTLEMENT EXISTS, not a particular status. The first
        // version of this allowed only PendingApproval and Approved, and deadlocked: a separation
        // that reached ClearanceCompleted with the notice undecided could not prepare its settlement
        // (the gate below refuses) and could not record the decision either (this check refused).
        // Nothing could move it. Caught by run-slice14 on its first run.
        //
        // It opens at PendingApproval because the signatory should be able to settle the notice at
        // the moment they sign — the decision left approval so the ENGINE could carry the approval,
        // not so the decision would have to wait for it.
        // ⚠ The SETTLEMENT is asked about FIRST, before the status. Once it exists it is too late to
        // change the decision: the notice-pay line was computed from the decision in force when the
        // settlement was prepared, so changing it afterwards would leave a statement disagreeing
        // with the decision behind it. Amending the line is the way to correct that.
        //
        // Asked second, this loses to the status check — a separation at SettlementPending is
        // outside the status window too, and answers "the decision is taken up until the settlement
        // is prepared" when the truthful answer is "it already was". Order the questions so the more
        // specific one answers first; StartClearanceAsync carries the same note for the same reason.
        var settlementExists = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.SeparationId == entity.Id,
                cancellationToken);
        if (settlementExists)
            throw new InvalidOperationException(
                "The settlement has already been prepared for this separation, and its notice pay "
                + "was computed from the decision in force at the time. Amend the settlement line "
                + "rather than the decision behind it.");

        if (entity.Status is not (SeparationStatus.PendingApproval
                                  or SeparationStatus.Approved
                                  or SeparationStatus.ClearanceInProgress
                                  or SeparationStatus.ClearanceCompleted))
            throw new InvalidOperationException(
                $"This separation is {entity.Status}. The notice decision is taken from the point it "
                + "is awaiting approval up until the settlement is prepared.");

        // The same authority that may sign this separation, and for the same reason: waiving notice
        // or paying it in lieu is the organisation giving something up or paying it out.
        RequireDecisionAuthority(entity);

        if (dto.WaiveNotice && dto.PayNoticeInLieu)
            throw new InvalidOperationException(
                "Notice cannot be both waived and paid in lieu — waived notice costs nothing, "
                + "notice paid in lieu is money.");

        if (dto.WaiveNotice && string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Give a reason for waiving the notice period.");

        var (_, served, shortfall) = Notice(entity);

        if (dto.WaiveNotice || dto.PayNoticeInLieu)
        {
            // Nothing to waive or pay where the notice was served in full — and nothing to reason
            // about where no notice period was ever counted, as on a retirement or a death.
            if (served is null)
                throw new InvalidOperationException(
                    "This separation has no notice period to settle, so notice cannot be waived or paid in lieu.");
            if (shortfall is 0)
                throw new InvalidOperationException(
                    "The full notice period was served, so there is no notice to waive or pay in lieu.");
        }

        entity.IsNoticeWaived = dto.WaiveNotice;
        entity.NoticeWaiverReason = dto.WaiveNotice ? dto.Reason!.Trim() : null;
        entity.IsNoticePaidInLieu = dto.PayNoticeInLieu;

        // ⚠ The stamp is the whole point of the record: it is what tells "neither applies" from
        // "nobody has looked". Recording neither is a decision, and it is stamped like any other.
        entity.NoticeDecisionOn = DateTime.UtcNow;
        entity.NoticeDecidedById = actorEmployeeId;

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notice decision recorded on separation {Number}: waived={Waived} paidInLieu={Paid} shortfall={Shortfall}",
            entity.SeparationNumber, dto.WaiveNotice, dto.PayNoticeInLieu, shortfall);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <summary>
    /// Notice was left unserved and nobody has said what happens to it — the state FR-HR-184 must
    /// not be computed through.
    /// </summary>
    private static bool NoticeDecisionOutstanding(EmployeeSeparation separation)
    {
        if (separation.NoticeDecisionOn is not null) return false;
        var (_, served, shortfall) = Notice(separation);
        return served is not null && shortfall is > 0;
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> RejectAsync(
        Guid id, RejectEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Give a reason for refusing the separation.");

        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.PendingApproval)
            throw new InvalidOperationException(
                $"This separation is {entity.Status}; only one awaiting approval can be refused.");

        RequireDecisionAuthority(entity);

        // Same two gates as approving, same order and for the same reasons — and the same absence
        // of a permission gate on the no-workflow branch, for the reason given there.
        var actingUserId = _currentUserProvider.UserId;

        WorkflowOutcome refusalOutcome;
        if (await _workflow.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflow.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
                throw new UnauthorizedAccessException(
                    "You are not assigned as an approver for the current step of this separation.");

            var refusal = await _workflow.ProcessApprovalAsync(
                EntityType, entity.Id, actingUserId, "Reject", dto.Reason);
            if (!refusal.ExecutionResult.Success)
                throw new InvalidOperationException(
                    refusal.ExecutionResult.Message ?? "Failed to process the separation refusal.");

            refusalOutcome = refusal.Outcome;
        }
        else
        {
            refusalOutcome = WorkflowOutcome.Rejected;
        }

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, refusalOutcome, actingUserId, dto.Reason);

        entity.RejectedById = actorEmployeeId;
        entity.RejectedOn = DateTime.UtcNow;
        entity.RejectionReason = dto.Reason.Trim();

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Separation {Number} refused", entity.SeparationNumber);
        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IEnumerable<EmployeeSeparationDocumentDto>> GetDocumentsAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireAsync(tenantId, separationId, cancellationToken);

        var documents = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .Include(d => d.UploadedBy)
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.SeparationId == separationId)
            .OrderByDescending(d => d.UploadedOn)
            .ThenBy(d => d.FileName)
            .ToListAsync(cancellationToken);

        return documents.Select(ToDocumentDto).ToList();
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDocumentDto> AttachDocumentAsync(
        Guid separationId,
        SeparationDocumentCategory category,
        string fileName,
        string filePath,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        string? description,
        Guid? uploadedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireAsync(tenantId, separationId, cancellationToken);

        var document = new EmployeeSeparationDocument
        {
            TenantId = tenantId,
            SeparationId = separationId,
            Category = category,
            FileName = fileName,
            FilePath = filePath,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UploadedOn = DateTime.UtcNow,
            UploadedById = uploadedByEmployeeId,
        };

        await _unitOfWork.Repository<EmployeeSeparationDocument>().AddAsync(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .Include(d => d.UploadedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken)
            ?? throw new InvalidOperationException("Document saved but could not be reloaded.");

        return ToDocumentDto(saved);
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDocument?> GetDocumentEntityAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var document = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId && !d.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Separation document with ID '{documentId}' not found.");

        var separation = await RequireAsync(tenantId, document.SeparationId, cancellationToken);

        // A mis-uploaded file can be taken back while the separation is still being prepared. Once
        // it has been submitted the attachments are part of what was approved and what the
        // settlement was computed against, so removing one silently rewrites the record. Deleting
        // the whole separation stays available to an administrator.
        if (separation.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. Its documents are part of the record and "
                + "can no longer be removed.");

        await _unitOfWork.Repository<EmployeeSeparationDocument>().DeleteAsync(documentId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── The disciplinary route joins this pipeline (decision D1) ──────────────

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto?> CreateFromDisciplinaryOutcomeAsync(
        Guid disciplinaryActionId,
        Guid employeeId,
        EmployeeTerminationType type,
        string? notes,
        Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Already linked — a second call must not mint a second exit for the same decision.
        var linked = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted
                                      && s.DisciplinaryActionId == disciplinaryActionId, cancellationToken);
        if (linked is not null)
            return ToDetailDto(await ReloadAsync(tenantId, linked.Id, cancellationToken));

        try
        {
            return await CreateAsync(new CreateEmployeeSeparationDto
            {
                EmployeeId = employeeId,
                SeparationType = type,
                ReasonCategory = type == EmployeeTerminationType.InvoluntaryRedundancy
                    ? TerminationReason.Redundancy
                    : TerminationReason.Dismissal,
                ReasonNotes = string.IsNullOrWhiteSpace(notes)
                    ? "Raised from a disciplinary outcome."
                    : $"Raised from a disciplinary outcome. {notes.Trim()}",
                // ⚠ No effective date. The disciplinary record carries none — there is no date-of-
                // termination field on it — and inventing one would put a fabricated last day on
                // somebody's employment record. HR sets it before submitting.
                EffectiveDate = null,
                DisciplinaryActionId = disciplinaryActionId,
            }, actorEmployeeId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // ⚠ Swallowed on purpose, and this is the one place in the area that swallows anything.
            // The disciplinary decision has already been recorded and must not be rolled back
            // because the exit could not be opened — an employee who already has a separation in
            // flight is the ordinary cause. The disciplinary outcome stands; the exit is picked up
            // by the orphan repair.
            _logger.LogWarning(
                "Disciplinary action {ActionId}: separation not raised — {Reason}",
                disciplinaryActionId, ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<DisciplinaryOrphanRepairDto> RepairDisciplinaryOrphansAsync(
        bool dryRun, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Every disciplinary termination that never produced an exit.
        var orphans = await _unitOfWork.Repository<StaffDisciplineTermination>().GetQueryable()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Join(_unitOfWork.Repository<StaffDisciplinaryAction>().GetQueryable().Where(a => !a.IsDeleted),
                  t => t.DisciplinaryActionId, a => a.Id,
                  (t, a) => new { Termination = t, Action = a })
            .Where(x => !_unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
                .Any(s => s.TenantId == tenantId && !s.IsDeleted
                          && s.DisciplinaryActionId == x.Action.Id))
            .ToListAsync(cancellationToken);

        var result = new DisciplinaryOrphanRepairDto
        {
            DryRun = dryRun,
            FoundCount = orphans.Count,
        };

        // ⚠ Employees who already have an exit in flight cannot be given a second one, so the
        // repair will skip them — and the DRY RUN HAS TO KNOW THAT TOO. Without this it promised 29
        // and delivered 21, because the disciplinary fixtures reuse subjects: two terminations
        // against one person can only ever produce one exit. **A dry run that misreports what will
        // happen is worse than none, because it is believed.**
        var alreadyInFlight = (await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted
                            && s.Status != SeparationStatus.Cancelled
                            && s.Status != SeparationStatus.Rejected
                            && s.Status != SeparationStatus.Completed)
                .Select(s => s.EmployeeId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var orphan in orphans)
        {
            var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == orphan.Action.EmployeeId && e.TenantId == tenantId, cancellationToken);

            var label = employee is null
                ? orphan.Action.EmployeeId.ToString()
                : $"{employee.FirstName} {employee.LastName} ({employee.EmployeeNumber})";

            if (employee is null)
            {
                result.Failures.Add($"{label}: employee not found.");
                continue;
            }

            if (employee.StaffStatus == StaffStatus.Terminated)
            {
                // Already off strength by some other route: nothing to repair, and raising an exit
                // for somebody who has already left would be worse than the gap.
                result.AlreadyTerminatedCount++;
                continue;
            }

            // Counted the same way in both modes — including exits this very run has just raised,
            // so two orphaned decisions against one person report as one raise and one skip rather
            // than two raises.
            if (alreadyInFlight.Contains(employee.Id))
            {
                result.Failures.Add($"{label}: a separation is already in progress for this employee.");
                continue;
            }

            if (dryRun)
            {
                result.WouldRaise.Add($"{label}: {orphan.Termination.Type}");
                alreadyInFlight.Add(employee.Id);
                continue;
            }

            var raised = await CreateFromDisciplinaryOutcomeAsync(
                orphan.Action.Id, employee.Id, orphan.Termination.Type,
                orphan.Termination.SeparationNotes, actorEmployeeId, cancellationToken);

            if (raised is null)
                result.Failures.Add($"{label}: a separation is already in progress for this employee.");
            else
            {
                result.RaisedCount++;
                result.Raised.Add(ToListDtoFromDetail(raised));
                alreadyInFlight.Add(employee.Id);
            }
        }

        _logger.LogInformation(
            "Disciplinary orphan repair ({Mode}): {Found} found, {Raised} raised, {Already} already terminated, {Failed} failed",
            dryRun ? "dry run" : "applied", result.FoundCount, result.RaisedCount,
            result.AlreadyTerminatedCount, result.Failures.Count);

        return result;
    }

    // ── Completion: the exit reaches the employee record ──────────────────────

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CompleteSeparationAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        // Payment is released only after Internal Audit has passed the settlement (FR-HR-185), and
        // the employee record follows the payment, not the other way round.
        if (separation.Status != SeparationStatus.SettlementApproved)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. It is completed once Internal Audit has "
                + "passed the settlement and payment can be released.");

        if (separation.EffectiveDate is not { } effective)
            throw new InvalidOperationException(
                "This separation has no effective date, so there is no date to record against the "
                + "employee. Set it before completing.");

        var reasonNote =
            $"Separation {separation.SeparationNumber} ({separation.SeparationType}), effective "
            + $"{effective:yyyy-MM-dd}."
            + (string.IsNullOrWhiteSpace(separation.ReasonNotes) ? string.Empty : $" {separation.ReasonNotes}");

        // ⚠ The whole point of this slice. Recording an exit and applying it are two different acts,
        // and until now only the first existed — which is why 29 dismissed people were still Active.
        await _employeeService.ApplySeparationOutcomeAsync(
            separation.EmployeeId,
            separation.Id,
            effective.ToDateTime(TimeOnly.MinValue),
            separation.ReasonCategory,
            reasonNote,
            cancellationToken);

        separation.Status = SeparationStatus.Completed;
        separation.EmployeeRecordUpdatedOn = DateTime.UtcNow;

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {Number} completed and applied to employee {EmployeeId}",
            separation.SeparationNumber, separation.EmployeeId);

        return ToDetailDto(await ReloadAsync(tenantId, separationId, cancellationToken));
    }

    // ── Retirement (FR-HR-093) ────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<IEnumerable<UpcomingRetirementDto>> GetUpcomingRetirementsAsync(
        int? withinDays = null, bool includeOverdue = true, CancellationToken cancellationToken = default)
        => GetUpcomingRetirementsForTenantAsync(GetTenantId(), withinDays, includeOverdue, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<UpcomingRetirementDto>> GetUpcomingRetirementsForTenantAsync(
        Guid tenantId, int? withinDays = null, bool includeOverdue = true,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required to list upcoming retirements.", nameof(tenantId));

        var settings = await _policyProvider.GetForTenantAsync(tenantId, cancellationToken);
        var horizon = withinDays ?? settings.RetirementCountdownLeadDays;

        if (horizon < 0)
            throw new InvalidOperationException("The horizon cannot be negative.");

        // Only people still on strength: chasing a retirement date for somebody who has already
        // left is noise, and the whole point of the list is who is still to be dealt with.
        var employees = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive
                        && e.StaffStatus != StaffStatus.Terminated
                        && (e.DateOfBirth != null || e.RetirementDate != null))
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var due = new List<(Employee Employee, DateOnly Date, bool Explicit)>();

        foreach (var employee in employees)
        {
            var date = HrPolicyCalculations.RetirementDate(settings, employee);
            if (date is not { } retirementDate) continue;

            var days = retirementDate.DayNumber - today.DayNumber;

            if (days > horizon) continue;                 // too far off to be anybody's problem yet
            if (days < 0 && !includeOverdue) continue;    // already past, and not asked for

            due.Add((employee, retirementDate, employee.RetirementDate.HasValue));
        }

        // One query for every separation these people already have, rather than one per employee.
        var employeeIds = due.Select(d => d.Employee.Id).ToList();
        var existing = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && employeeIds.Contains(s.EmployeeId)
                        && s.Status != SeparationStatus.Cancelled
                        && s.Status != SeparationStatus.Rejected)
            .Select(s => new { s.Id, s.EmployeeId, s.SeparationNumber, s.Status })
            .ToListAsync(cancellationToken);

        var byEmployee = existing
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        return due
            .OrderBy(d => d.Date)
            .ThenBy(d => d.Employee.LastName)
            .Select(d =>
            {
                byEmployee.TryGetValue(d.Employee.Id, out var already);
                var days = d.Date.DayNumber - today.DayNumber;

                return new UpcomingRetirementDto
                {
                    EmployeeId = d.Employee.Id,
                    EmployeeName = FullName(d.Employee),
                    EmployeeNumber = d.Employee.EmployeeNumber,
                    PositionTitle = d.Employee.Position?.Title,
                    OrganizationUnitName = d.Employee.OrganizationUnit?.Name,
                    OrganizationUnitId = d.Employee.OrganizationUnitId,
                    PositionId = d.Employee.PositionId,
                    DateOfBirth = d.Employee.DateOfBirth,
                    CurrentAge = HrPolicyCalculations.Age(d.Employee.DateOfBirth),
                    RetirementAge = HrPolicyCalculations.EffectiveRetirementAge(settings, d.Employee.Gender),
                    RetirementDate = d.Date,
                    DaysUntilRetirement = days,
                    IsOverdue = days < 0,
                    IsExplicitDate = d.Explicit,
                    ExistingSeparationId = already?.Id,
                    ExistingSeparationNumber = already?.SeparationNumber,
                    ExistingSeparationStatus = already?.Status.ToString(),
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RetirementSweepResultDto> RunRetirementSweepAsync(
        int? withinDays = null, CancellationToken cancellationToken = default)
    {
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var horizon = withinDays ?? settings.RetirementCountdownLeadDays;

        var upcoming = (await GetUpcomingRetirementsAsync(horizon, includeOverdue: true, cancellationToken)).ToList();

        var result = new RetirementSweepResultDto
        {
            HorizonDays = horizon,
            DueCount = upcoming.Count,
            SkippedExistingCount = upcoming.Count(u => u.ExistingSeparationId is not null),
        };

        foreach (var candidate in upcoming.Where(u => u.ExistingSeparationId is null))
        {
            try
            {
                // ⚠ actorEmployeeId is null ON PURPOSE. A retirement date arriving is nobody's act,
                // and stamping whoever happened to run the sweep as the initiator would be a lie the
                // audit trail could not tell from a real one. The create marks it IsSystemInitiated.
                var raised = await CreateAsync(new CreateEmployeeSeparationDto
                {
                    EmployeeId = candidate.EmployeeId,
                    SeparationType = EmployeeTerminationType.CompulsoryRetirement,
                    ReasonCategory = TerminationReason.Retirement,
                    ReasonNotes =
                        $"Raised automatically: reaches the retirement age of {candidate.RetirementAge} on "
                        + $"{candidate.RetirementDate:yyyy-MM-dd} (FR-HR-093).",
                    // Left blank deliberately — CreateAsync computes the birthday and would refuse
                    // a date supplied here that disagreed with it.
                    EffectiveDate = null,
                }, actorEmployeeId: null, cancellationToken);

                result.Raised.Add(ToListDtoFromDetail(raised));
                result.RaisedCount++;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // One employee's missing date of birth must not stop the sweep for everybody else.
                result.Failures.Add($"{candidate.EmployeeName}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "Retirement sweep over {Horizon} days: {Due} due, {Raised} raised, {Skipped} already had one, {Failed} failed",
            horizon, result.DueCount, result.RaisedCount, result.SkippedExistingCount, result.Failures.Count);

        return result;
    }

    private static EmployeeSeparationListDto ToListDtoFromDetail(EmployeeSeparationDetailDto d) => new()
    {
        Id = d.Id,
        SeparationNumber = d.SeparationNumber,
        EmployeeId = d.EmployeeId,
        EmployeeName = d.EmployeeName,
        EmployeeNumber = d.EmployeeNumber,
        PositionTitle = d.PositionTitle,
        OrganizationUnitName = d.OrganizationUnitName,
        SeparationType = d.SeparationType,
        SeparationTypeName = d.SeparationTypeName,
        Status = d.Status,
        StatusName = d.StatusName,
        InitiatedOn = d.InitiatedOn,
        LastWorkingDay = d.LastWorkingDay,
        EffectiveDate = d.EffectiveDate,
        IsProcedural = d.IsProcedural,
        IsSystemInitiated = d.IsSystemInitiated,
        IsDisciplinary = d.IsDisciplinary,
        EmployeeRecordUpdated = d.EmployeeRecordUpdated,
    };

    // ── The exit interview ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SeparationExitInterviewDto?> GetExitInterviewAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireAsync(tenantId, separationId, cancellationToken);

        var interview = await LoadInterviewAsync(tenantId, separationId, cancellationToken);
        return interview is null ? null : ToInterviewDto(interview);
    }

    /// <inheritdoc />
    public async Task<SeparationExitInterviewDto> RecordExitInterviewAsync(
        Guid separationId, RecordExitInterviewDto dto, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        // An interview is about an exit that is actually happening. Taking one against a draft or a
        // refused separation would put words in the mouth of somebody who is not leaving.
        if (separation.Status is SeparationStatus.Draft
            or SeparationStatus.PendingApproval
            or SeparationStatus.Cancelled
            or SeparationStatus.Rejected)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. An exit interview is recorded once the "
                + "separation has been approved.");

        if (dto.WasDeclined)
        {
            if (string.IsNullOrWhiteSpace(dto.DeclinedReason))
                throw new InvalidOperationException(
                    "Say why the interview was not held — declined, could not be reached, or left before it could be arranged.");
        }
        else if (dto.ConductedOn is null)
        {
            throw new InvalidOperationException(
                "Record the date the interview was held, or mark it as declined.");
        }

        if (dto.ConductedOn is { } held && held > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("An exit interview cannot be recorded as held in the future.");

        if (dto.ConductedById is { } interviewerId && interviewerId != Guid.Empty)
        {
            var exists = await _unitOfWork.Repository<Employee>().GetQueryable()
                .AnyAsync(e => e.Id == interviewerId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken);
            if (!exists)
                throw new ArgumentException($"Employee with ID '{interviewerId}' not found.");
        }

        var interview = await _unitOfWork.Repository<SeparationExitInterview>().GetQueryable()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId,
                cancellationToken);

        var isNew = interview is null;
        interview ??= new SeparationExitInterview
        {
            TenantId = tenantId,
            SeparationId = separationId,
        };

        // ⚠ A declined interview keeps NO answers. Somebody who marks an interview declined after
        // part-filling it must not leave half a set of ratings behind to be averaged later as if a
        // real interview had produced them.
        interview.WasDeclined = dto.WasDeclined;
        interview.DeclinedReason = dto.WasDeclined ? dto.DeclinedReason!.Trim() : null;
        interview.ConductedOn = dto.WasDeclined ? null : dto.ConductedOn;
        interview.ConductedById = dto.WasDeclined ? null : dto.ConductedById;
        interview.ConductedByName = dto.WasDeclined || string.IsNullOrWhiteSpace(dto.ConductedByName)
            ? null : dto.ConductedByName.Trim();
        interview.PrimaryReason = dto.WasDeclined ? null : dto.PrimaryReason;
        interview.PrimaryReasonDetail = dto.WasDeclined || string.IsNullOrWhiteSpace(dto.PrimaryReasonDetail)
            ? null : dto.PrimaryReasonDetail.Trim();
        interview.OverallExperienceRating = dto.WasDeclined ? null : dto.OverallExperienceRating;
        interview.ManagementRating = dto.WasDeclined ? null : dto.ManagementRating;
        interview.PayAndBenefitsRating = dto.WasDeclined ? null : dto.PayAndBenefitsRating;
        interview.CareerDevelopmentRating = dto.WasDeclined ? null : dto.CareerDevelopmentRating;
        interview.WouldRecommendEmployer = dto.WasDeclined ? null : dto.WouldRecommendEmployer;
        interview.WouldConsiderReturning = dto.WasDeclined ? null : dto.WouldConsiderReturning;
        interview.WhatWorkedWell = dto.WasDeclined ? null : Trimmed(dto.WhatWorkedWell);
        interview.WhatShouldChange = dto.WasDeclined ? null : Trimmed(dto.WhatShouldChange);
        interview.AdditionalComments = dto.WasDeclined ? null : Trimmed(dto.AdditionalComments);
        interview.RecordedById = actorEmployeeId;
        interview.RecordedOn = DateTime.UtcNow;

        if (isNew)
            await _unitOfWork.Repository<SeparationExitInterview>().AddAsync(interview);
        else
            await _unitOfWork.Repository<SeparationExitInterview>().UpdateAsync(interview);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Exit interview {Action} for separation {Number} ({Outcome})",
            isNew ? "recorded" : "amended", separation.SeparationNumber,
            dto.WasDeclined ? "declined" : "conducted");

        var saved = await LoadInterviewAsync(tenantId, separationId, cancellationToken)
            ?? throw new InvalidOperationException("Exit interview saved but could not be reloaded.");

        return ToInterviewDto(saved);

        static string? Trimmed(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private async Task<SeparationExitInterview?> LoadInterviewAsync(
        Guid tenantId, Guid separationId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<SeparationExitInterview>().GetQueryable()
            .Include(i => i.Separation).ThenInclude(s => s.Employee)
            .Include(i => i.ConductedBy)
            .Include(i => i.RecordedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId,
                cancellationToken);

    private static SeparationExitInterviewDto ToInterviewDto(SeparationExitInterview i) => new()
    {
        Id = i.Id,
        SeparationId = i.SeparationId,
        SeparationNumber = i.Separation?.SeparationNumber ?? string.Empty,
        EmployeeName = FullName(i.Separation?.Employee),
        WasDeclined = i.WasDeclined,
        DeclinedReason = i.DeclinedReason,
        ConductedOn = i.ConductedOn,
        ConductedById = i.ConductedById,
        // The employee link when there is one, the written name when there is not — an interviewer
        // is often not an ERP user.
        ConductedByName = i.ConductedBy != null ? FullName(i.ConductedBy) : i.ConductedByName,
        PrimaryReason = i.PrimaryReason,
        PrimaryReasonName = i.PrimaryReason?.ToString(),
        PrimaryReasonDetail = i.PrimaryReasonDetail,
        OverallExperienceRating = i.OverallExperienceRating,
        ManagementRating = i.ManagementRating,
        PayAndBenefitsRating = i.PayAndBenefitsRating,
        CareerDevelopmentRating = i.CareerDevelopmentRating,
        WouldRecommendEmployer = i.WouldRecommendEmployer,
        WouldConsiderReturning = i.WouldConsiderReturning,
        WhatWorkedWell = i.WhatWorkedWell,
        WhatShouldChange = i.WhatShouldChange,
        AdditionalComments = i.AdditionalComments,
        RecordedById = i.RecordedById,
        RecordedByName = i.RecordedBy == null ? null : FullName(i.RecordedBy),
        RecordedOn = i.RecordedOn,
    };

    /// <inheritdoc />
    public async Task<ExitInterviewThemesDto> GetExitInterviewThemesAsync(
        DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? toDate.AddYears(-1);

        if (fromDate > toDate)
            throw new InvalidOperationException("The start of the period falls after its end.");

        var separations = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.Status == SeparationStatus.Completed)
            .Select(s => new { s.Id, s.InitiatedOn, s.EffectiveDate })
            .ToListAsync(cancellationToken);

        var inWindow = separations
            .Where(s => (s.EffectiveDate ?? s.InitiatedOn) >= fromDate
                        && (s.EffectiveDate ?? s.InitiatedOn) <= toDate)
            .Select(s => s.Id)
            .ToList();

        var interviews = await _unitOfWork.Repository<SeparationExitInterview>().GetQueryable()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && inWindow.Contains(i.SeparationId))
            .ToListAsync(cancellationToken);

        var conducted = interviews.Where(i => !i.WasDeclined).ToList();
        var declined = interviews.Count(i => i.WasDeclined);

        // ⚠ Averaged over the answers ACTUALLY GIVEN, not over all interviews. A null rating means
        // the question was not asked, and counting it as anything would move the mean.
        static decimal? Mean(IEnumerable<int?> values)
        {
            var given = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return given.Count == 0 ? null : Math.Round(given.Average(v => (decimal)v), 2);
        }

        static decimal? Share(IEnumerable<bool?> values)
        {
            var asked = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return asked.Count == 0 ? null : Math.Round(asked.Count(v => v) * 100m / asked.Count, 1);
        }

        var byReason = conducted
            .Where(i => i.PrimaryReason.HasValue)
            .GroupBy(i => i.PrimaryReason!.Value)
            .Select(g => new SeparationBreakdownRowDto
            {
                Key = g.Key.ToString(),
                Label = g.Key.ToString(),
                Count = g.Count(),
            })
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Label)
            .ToList();

        var reasonTotal = byReason.Sum(r => r.Count);
        foreach (var row in byReason)
            row.Percentage = reasonTotal == 0 ? 0m : Math.Round(row.Count * 100m / reasonTotal, 1);

        return new ExitInterviewThemesDto
        {
            SeparationsInPeriod = inWindow.Count,
            InterviewsRecorded = interviews.Count,
            InterviewsConducted = conducted.Count,
            InterviewsDeclined = declined,
            CoveragePercent = inWindow.Count == 0
                ? 0m
                : Math.Round(interviews.Count * 100m / inWindow.Count, 1),
            DeclineRatePercent = interviews.Count == 0
                ? 0m
                : Math.Round(declined * 100m / interviews.Count, 1),
            ByPrimaryReason = byReason,
            AverageOverallExperience = Mean(conducted.Select(i => i.OverallExperienceRating)),
            AverageManagement = Mean(conducted.Select(i => i.ManagementRating)),
            AveragePayAndBenefits = Mean(conducted.Select(i => i.PayAndBenefitsRating)),
            AverageCareerDevelopment = Mean(conducted.Select(i => i.CareerDevelopmentRating)),
            WouldRecommendPercent = Share(conducted.Select(i => i.WouldRecommendEmployer)),
            WouldReturnPercent = Share(conducted.Select(i => i.WouldConsiderReturning)),
        };
    }

    // ── Exit analytics (slice 12) ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SeparationAnalyticsDto> GetAnalyticsAsync(
        DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = to ?? today;
        var fromDate = from ?? toDate.AddYears(-1);

        if (fromDate > toDate)
            throw new InvalidOperationException("The start of the period falls after its end.");

        var all = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new
            {
                s.Id, s.Status, s.SeparationType, s.ReasonCategory,
                s.InitiatedOn, s.EffectiveDate, s.EmployeeRecordUpdatedOn,
            })
            .ToListAsync(cancellationToken);

        // Completion is dated by the exit itself where there is one, falling back to when it was
        // raised — a completed separation with no effective date should still be counted, not lost.
        bool InWindow(DateOnly? effective, DateOnly initiated)
        {
            var when = effective ?? initiated;
            return when >= fromDate && when <= toDate;
        }

        var completed = all
            .Where(s => s.Status == SeparationStatus.Completed && InWindow(s.EffectiveDate, s.InitiatedOn))
            .ToList();

        var raised = all.Count(s => s.InitiatedOn >= fromDate && s.InitiatedOn <= toDate);

        var inFlight = all
            .Where(s => s.Status != SeparationStatus.Completed
                        && s.Status != SeparationStatus.Cancelled
                        && s.Status != SeparationStatus.Rejected)
            .ToList();

        var activeHeadcount = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive
                             && e.StaffStatus != StaffStatus.Terminated, cancellationToken);

        static List<SeparationBreakdownRowDto> Breakdown<T>(
            IEnumerable<T> source, Func<T, string> key, Func<T, string> label)
        {
            var rows = source
                .GroupBy(key)
                .Select(g => new SeparationBreakdownRowDto
                {
                    Key = g.Key,
                    Label = label(g.First()),
                    Count = g.Count(),
                })
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Label)
                .ToList();

            var total = rows.Sum(r => r.Count);
            foreach (var row in rows)
                row.Percentage = total == 0 ? 0m : Math.Round(row.Count * 100m / total, 1);

            return rows;
        }

        // The pipeline reads over everything IN FLIGHT, not the window: a separation stuck since
        // last year is precisely what a stage view is for, and a date filter would hide it.
        var pipeline = new[]
            {
                SeparationStatus.Draft, SeparationStatus.PendingApproval, SeparationStatus.Approved,
                SeparationStatus.ClearanceInProgress, SeparationStatus.ClearanceCompleted,
                SeparationStatus.SettlementPending, SeparationStatus.SettlementUnderReview,
                SeparationStatus.SettlementApproved,
            }
            .Select(status =>
            {
                var atStage = inFlight.Where(s => s.Status == status).ToList();
                return new SeparationPipelineStageDto
                {
                    Status = status.ToString(),
                    Label = status.ToString(),
                    Count = atStage.Count,
                    OldestDays = atStage.Count == 0
                        ? null
                        : atStage.Max(s => today.DayNumber - s.InitiatedOn.DayNumber),
                };
            })
            .ToList();

        // Money only from settlements Internal Audit has passed — anything earlier is a draft
        // figure, and reporting drafts as settled money would overstate what has been committed.
        var settled = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                        && x.ReviewOutcome == SettlementReviewOutcome.Approved)
            .Select(x => new { x.Id, x.CurrencyCode })
            .ToListAsync(cancellationToken);

        var settledIds = settled.Select(x => x.Id).ToList();

        var settledLines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && settledIds.Contains(l.SettlementId))
            .Select(l => new { l.IsDeduction, l.Amount })
            .ToListAsync(cancellationToken);

        var earnings = settledLines.Where(l => !l.IsDeduction).Sum(l => l.Amount ?? 0m);
        var recoveries = settledLines.Where(l => l.IsDeduction).Sum(l => l.Amount ?? 0m);

        var unvalued = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted
                        && l.Computation == SettlementLineComputation.CannotCompute)
            .Select(l => l.SettlementId)
            .Distinct()
            .CountAsync(cancellationToken);

        var settings = await _policyProvider.GetAsync(cancellationToken);

        return new SeparationAnalyticsDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            CompletedInPeriod = completed.Count,
            RaisedInPeriod = raised,
            InFlight = inFlight.Count,
            ActiveHeadcount = activeHeadcount,
            ExitRatePercent = activeHeadcount == 0
                ? 0m
                : Math.Round(completed.Count * 100m / activeHeadcount, 2),
            ByRoute = Breakdown(completed, s => s.SeparationType.ToString(), s => s.SeparationType.ToString()),
            ByReason = Breakdown(
                completed.Where(s => s.ReasonCategory != null),
                s => s.ReasonCategory!.Value.ToString(),
                s => s.ReasonCategory!.Value.ToString()),
            Pipeline = pipeline,
            SettledEarnings = earnings,
            SettledRecoveries = recoveries,
            SettledNetPayable = earnings - recoveries,
            CurrencyCode = settled.FirstOrDefault()?.CurrencyCode ?? settings.DefaultCurrencyCode ?? string.Empty,
            SettlementsWithUnvaluedLines = unvalued,
            CompletedButNotApplied = all.Count(s => s.Status == SeparationStatus.Completed
                                                    && s.EmployeeRecordUpdatedOn == null),
        };
    }

    // ── Contract expiry ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<IEnumerable<UpcomingContractExpiryDto>> GetUpcomingContractExpiriesAsync(
        int? withinDays = null, bool includeOverdue = true, CancellationToken cancellationToken = default)
        => GetUpcomingContractExpiriesForTenantAsync(GetTenantId(), withinDays, includeOverdue, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<UpcomingContractExpiryDto>> GetUpcomingContractExpiriesForTenantAsync(
        Guid tenantId, int? withinDays = null, bool includeOverdue = true,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required to list contract expiries.", nameof(tenantId));

        var settings = await _policyProvider.GetForTenantAsync(tenantId, cancellationToken);
        var horizon = withinDays ?? settings.ContractExpiryLeadDays;

        if (horizon < 0)
            throw new InvalidOperationException("The horizon cannot be negative.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = today.AddDays(horizon);

        var contracts = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .Include(c => c.Employee).ThenInclude(e => e.Position)
            .Include(c => c.Employee).ThenInclude(e => e.OrganizationUnit)
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive
                        && c.EndDate != null && c.EndDate <= limit
                        && c.Employee.IsActive && c.Employee.StaffStatus != StaffStatus.Terminated)
            .ToListAsync(cancellationToken);

        var due = contracts
            .Where(c => includeOverdue || c.EndDate!.Value >= today)
            .ToList();

        var employeeIds = due.Select(c => c.EmployeeId).Distinct().ToList();
        var existing = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && employeeIds.Contains(s.EmployeeId)
                        && s.Status != SeparationStatus.Cancelled
                        && s.Status != SeparationStatus.Rejected)
            .Select(s => new { s.Id, s.EmployeeId, s.SeparationNumber, s.Status })
            .ToListAsync(cancellationToken);

        var byEmployee = existing.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.First());

        return due
            .OrderBy(c => c.EndDate)
            .ThenBy(c => c.Employee.LastName)
            .Select(c =>
            {
                byEmployee.TryGetValue(c.EmployeeId, out var already);
                var days = c.EndDate!.Value.DayNumber - today.DayNumber;

                return new UpcomingContractExpiryDto
                {
                    EmployeeId = c.EmployeeId,
                    EmployeeName = FullName(c.Employee),
                    EmployeeNumber = c.Employee?.EmployeeNumber,
                    PositionTitle = c.Employee?.Position?.Title,
                    OrganizationUnitName = c.Employee?.OrganizationUnit?.Name,
                    ContractId = c.Id,
                    ContractNumber = c.ContractNumber,
                    ContractStartDate = c.StartDate,
                    ContractEndDate = c.EndDate.Value,
                    DaysUntilExpiry = days,
                    IsOverdue = days < 0,
                    ExistingSeparationId = already?.Id,
                    ExistingSeparationNumber = already?.SeparationNumber,
                    ExistingSeparationStatus = already?.Status.ToString(),
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ContractExpirySweepResultDto> RunContractExpirySweepAsync(
        int? withinDays = null, CancellationToken cancellationToken = default)
    {
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var horizon = withinDays ?? settings.ContractExpiryLeadDays;

        var upcoming = (await GetUpcomingContractExpiriesAsync(horizon, includeOverdue: true, cancellationToken))
            .ToList();

        var result = new ContractExpirySweepResultDto
        {
            HorizonDays = horizon,
            DueCount = upcoming.Count,
            SkippedExistingCount = upcoming.Count(u => u.ExistingSeparationId is not null),
        };

        foreach (var candidate in upcoming.Where(u => u.ExistingSeparationId is null))
        {
            try
            {
                // System-initiated, like the retirement sweep: a contract running out is nobody's
                // act either.
                var raised = await CreateAsync(new CreateEmployeeSeparationDto
                {
                    EmployeeId = candidate.EmployeeId,
                    SeparationType = EmployeeTerminationType.ContractExpiry,
                    ReasonCategory = TerminationReason.ContractExpiry,
                    ReasonNotes =
                        $"Raised automatically: contract {candidate.ContractNumber} runs to "
                        + $"{candidate.ContractEndDate:yyyy-MM-dd}.",
                    EffectiveDate = null,   // computed from the contract, see CreateAsync
                }, actorEmployeeId: null, cancellationToken);

                result.Raised.Add(ToListDtoFromDetail(raised));
                result.RaisedCount++;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                result.Failures.Add($"{candidate.EmployeeName}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "Contract-expiry sweep over {Horizon} days: {Due} due, {Raised} raised, {Skipped} already had one",
            horizon, result.DueCount, result.RaisedCount, result.SkippedExistingCount);

        return result;
    }

    /// <summary>
    /// The end date of the employee's active contract, where they have one that carries a date.
    /// </summary>
    private async Task<(DateOnly? EndDate, string? ContractNumber)> ActiveContractEndAsync(
        Guid tenantId, Guid employeeId, CancellationToken cancellationToken)
    {
        var contract = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.EmployeeId == employeeId
                        && c.IsActive && c.EndDate != null)
            .OrderByDescending(c => c.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        return (contract?.EndDate, contract?.ContractNumber);
    }

    /// <summary>
    /// Evidence a separation type cannot reasonably be submitted without.
    /// </summary>
    /// <remarks>
    /// <para>A medical retirement asserts that somebody is permanently unfit; a death asserts that
    /// somebody has died. Both end an income, and both are the kind of claim a file should carry
    /// evidence for rather than a checkbox. The document categories already exist — this is the
    /// rule that makes them mean something.</para>
    ///
    /// <para>Deliberately at <b>submission</b>, not creation: HR opens the record when it hears,
    /// and the certificate or report arrives afterwards. Requiring it up front would push people
    /// into keeping the exit out of the system until the paperwork caught up.</para>
    /// </remarks>
    private async Task RequireSupportingEvidenceAsync(
        Guid tenantId, EmployeeSeparation separation, CancellationToken cancellationToken)
    {
        // ⚠ Round 5, lane K-II-a: a medical retirement may instead rest on a medical board's decided
        // finding recommending it — the stronger form, naming who decided and when. The link was
        // checked when it was made; it is read again here because this is the gate.
        if (separation.SeparationType == EmployeeTerminationType.MedicalRetirement
            && separation.MedicalBoardId is Guid boardId)
        {
            var recommended = await _unitOfWork.Repository<MedicalBoardCase>().GetQueryable()
                .AnyAsync(c => c.TenantId == tenantId && c.BoardId == boardId
                               && c.EmployeeId == separation.EmployeeId
                               && c.Status == MedicalBoardCaseStatus.Concluded
                               && c.RecommendsMedicalRetirement, cancellationToken);
            if (recommended) return;
        }

        var required = separation.SeparationType switch
        {
            EmployeeTerminationType.MedicalRetirement =>
                (Category: SeparationDocumentCategory.MedicalReport,
                 Message: "A medical retirement needs the medical report that supports it, or a medical "
                          + "board's finding recommending it. Attach the report, or link the board, "
                          + "before submitting."),
            EmployeeTerminationType.Death =>
                (Category: SeparationDocumentCategory.DeathCertificate,
                 Message: "A separation by death needs the death certificate. Attach it to the "
                          + "separation before submitting."),
            _ => default,
        };

        if (required.Message is null) return;

        var present = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .AnyAsync(d => d.TenantId == tenantId && !d.IsDeleted
                           && d.SeparationId == separation.Id
                           && d.Category == required.Category, cancellationToken);

        if (!present)
            throw new InvalidOperationException(required.Message);
    }

    // ── Final settlement (FR-HR-184) ──────────────────────────────────────────

    /// <summary>
    /// The settlement lines that are PAY — valued by Finance, never priced by HR (leave settings
    /// audit 2, decision P3).
    /// </summary>
    /// <remarks>
    /// ⚠ HR records the facts on these — the days of notice paid in lieu, the days of annual leave
    /// owed — and Finance (<c>HR.Pay.Value</c>) puts the money on them. Until leave settings audit 2,
    /// HR worked notice pay and leave out itself (monthly × 12 ÷ 365 a day) and that figure went into
    /// Finance's books when Internal Audit released the statement; "Finance confirms the amount" was
    /// a label, not a step. Recoveries (loans, advances, property, other deductions) are balances of
    /// documents Finance already holds, and stay as HR carries them.
    /// <para>⚠ Benefit payment and other earning were added on 2026-09-27 (the user, after slice A):
    /// every line that pays the leaver is Finance's to value.</para>
    /// </remarks>
    internal static bool IsPayLine(SettlementLineCategory category) => category is
        SettlementLineCategory.UnpaidSalary
        or SettlementLineCategory.NoticePay
        or SettlementLineCategory.LeaveEncashment
        or SettlementLineCategory.GratuityOrEndOfService
        or SettlementLineCategory.BenefitPayment
        or SettlementLineCategory.PensionRelated
        or SettlementLineCategory.OtherEarning
        or SettlementLineCategory.TaxDeduction;

    /// <summary>
    /// A pay line still waiting for Finance's figure: not valued by Finance, and not a stated zero.
    /// </summary>
    /// <remarks>
    /// The one rule the queue, the blocked reason and the finalise gate share (leave settings audit 2,
    /// slice B). A pay line is settled only by Finance's figure — so a figure HR put on one, from before
    /// pay moved to Finance or on a category since added to <see cref="IsPayLine"/>, still waits for
    /// Finance rather than passing as priced; widening the categories needs no data step. A stated zero
    /// (no days owed, summary dismissal) is a fact, not a valuation.
    /// </remarks>
    internal static bool AwaitsFinance(SeparationSettlementLine line) =>
        IsPayLine(line.Category)
        && line.Computation != SettlementLineComputation.ValuedByFinance
        && !(line.Computation == SettlementLineComputation.Computed && line.Amount == 0m);

    /// <summary>
    /// A line still waiting for a figure — a pay line until Finance values it, any other line while
    /// nobody could value it. What holds a statement open.
    /// </summary>
    private static bool StillToValue(SeparationSettlementLine line) =>
        AwaitsFinance(line)
        || (!IsPayLine(line.Category) && line.Computation == SettlementLineComputation.CannotCompute);

    /// <summary>
    /// The currency a settlement is stated in: HR's configured default, validated against Finance,
    /// falling back to Finance's base currency.
    /// </summary>
    /// <remarks>
    /// Finance owns what a currency <i>is</i>; HR owns which one it uses. A code HR has configured
    /// that Finance does not hold is refused rather than silently swapped — a misconfiguration that
    /// heals itself invisibly stays broken. Same division <c>StaffTravelCurrencyBridge</c> settled
    /// for travel; when a third area needs this the two should become one HR-wide bridge.
    /// </remarks>
    private async Task<string> ResolveCurrencyAsync(
        CompanyHrPolicySettings settings, CancellationToken cancellationToken)
    {
        var configured = settings.DefaultCurrencyCode?.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var known = await _currencies.GetByCodeAsync(configured, cancellationToken);
            if (known is null)
                throw new InvalidOperationException(
                    $"HR's default currency '{configured}' is not one Finance holds. Either correct "
                    + "it in HR settings or add the currency in Finance before preparing a settlement.");

            return configured;
        }

        var baseCurrency = await _currencies.GetBaseCurrencyAsync(cancellationToken);
        if (baseCurrency is null)
            throw new InvalidOperationException(
                "No currency is configured. Set HR's default currency, or a base currency in Finance.");

        return baseCurrency.CurrencyCode;
    }

    // DailyRateAsync — HR's monthly × 12 ÷ SettlementDaysPerYear — was removed in leave settings
    // audit 2 (L-74): Finance values every pay line, so HR works out no rate.

    /// <summary>
    /// A pay line carrying a figure HR put on it — worked out or typed in, and not zero.
    /// </summary>
    /// <remarks>
    /// Only a statement from before leave settings audit 2 can hold one: HR can no longer price a pay
    /// line. The migration cleared them on statements still with HR; one finalised before it is
    /// refused at Internal Audit's approval and cleared when it is returned (<see cref="ClearHrFigure"/>).
    /// A stated zero is a fact — no days owed — not a valuation, and posts nothing.
    /// </remarks>
    private static bool IsHrPricedPayLine(SeparationSettlementLine line) =>
        IsPayLine(line.Category)
        && line.Computation is (SettlementLineComputation.Computed or SettlementLineComputation.ManuallyEntered)
        && line.Amount is { } amount && amount != 0m;

    /// <summary>The days HR wrote into a legacy line's description ("… — 56.00 day(s)").</summary>
    private static readonly Regex DaysInDescription =
        new(@"(\d+(?:\.\d+)?) day\(s\)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Turns a pay line HR priced back to unvalued, for Finance: the amount goes, the days stay, and
    /// where HR had its figure from is kept in the basis. The same change the PayValuedByFinance
    /// migration made to statements that were still with HR.
    /// </summary>
    private static void ClearHrFigure(SeparationSettlementLine line, string when)
    {
        if (line.Days is null
            && line.Category is (SettlementLineCategory.NoticePay or SettlementLineCategory.LeaveEncashment)
            && DaysInDescription.Match(line.Description) is { Success: true } match
            && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var days))
            line.Days = days;

        var from = string.IsNullOrWhiteSpace(line.SourceReference)
            ? string.Empty
            : $" HR had taken it from: {line.SourceReference.Trim()}";

        line.Amount = null;
        line.Computation = SettlementLineComputation.CannotCompute;
        line.SourceReference = null;
        line.ValuedByEmployeeId = null;
        line.ValuedOn = null;
        line.Basis = FitBasis(new[]
        {
            $"HR's figure was cleared {when}: pay is valued by Finance now (leave settings audit 2).",
            FinanceValues + from,
        });
    }

    /// <summary>The sentence every unvalued pay line ends with: what Finance is to do.</summary>
    private const string FinanceValues =
        "To be valued by Finance: the amount and its source are entered in Finance's step (Pay to value).";

    /// <summary>HR's refusal when it tries to price a pay line.</summary>
    private const string PayLineIsFinances =
        "Pay is valued by Finance, not HR: record the days and facts on this line, and Finance enters the amount "
        + "and its source in Pay to value.";

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> PrepareSettlementAsync(
        Guid separationId, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        var existing = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A settlement has already been prepared for this separation.");

        // FR-HR-091: entitlements are computed only AFTER the clearance form is complete. This is
        // the sentence that gate exists to enforce, so it is checked here as well as there.
        if (separation.Status != SeparationStatus.ClearanceCompleted)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. A settlement is prepared once clearance is "
                + "complete — FR-HR-091 requires the clearance form before entitlements are computed.");

        // ⚠ The notice decision must exist before the money is computed, not merely be absent. An
        // unserved notice with nobody's decision against it produces a settlement with NO notice pay
        // line at all — and a missing line is invisible on a statement in a way a wrong figure is
        // not. Recording "neither" clears this; refusing to look does not.
        if (NoticeDecisionOutstanding(separation))
        {
            var (_, _, owed) = Notice(separation);
            throw new InvalidOperationException(
                $"{owed} day(s) of notice were not served and no decision has been recorded. Say "
                + "whether the notice is waived, paid in lieu, or neither, before the settlement is "
                + "prepared — otherwise the statement silently omits pay that may be owed.");
        }

        var settings = await _policyProvider.GetAsync(cancellationToken);
        var currency = await ResolveCurrencyAsync(settings, cancellationToken);

        // ⚠ No daily rate (leave settings audit 2, L-74): HR records days, Finance values them. The
        // statement's DailyRate and DailyRateBasis stay empty on everything prepared from here on;
        // statements released before keep the rate they were paid on.
        var settlement = new SeparationSettlement
        {
            TenantId = tenantId,
            SeparationId = separationId,
            CurrencyCode = currency,
            PreparedById = actorEmployeeId,
            PreparedOn = DateTime.UtcNow,
        };

        await _unitOfWork.Repository<SeparationSettlement>().AddAsync(settlement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var lines = new List<SeparationSettlementLine>();
        var order = 0;

        void Add(SettlementLineCategory category, bool deduction, string description,
                 decimal? amount, SettlementLineComputation computation, string basis,
                 Guid? clearanceItemId = null, Guid? travelAdvanceId = null, decimal? days = null)
        {
            order += 10;
            lines.Add(new SeparationSettlementLine
            {
                TenantId = tenantId,
                SettlementId = settlement.Id,
                Category = category,
                IsDeduction = deduction,
                Description = description,
                Amount = amount,
                Computation = computation,
                Basis = basis,
                Days = days,
                SourceClearanceItemId = clearanceItemId,
                SourceTravelAdvanceId = travelAdvanceId,
                IsSystemGenerated = true,
                SortOrder = order,
            });
        }

        // ── Earnings — PAY, so HR records the facts and Finance values them (audit 2, P3) ────────

        // Unpaid salary: pay to the last working day. Recorded as owed and unvalued rather than
        // omitted, so nobody signs a statement that quietly forgot the last month's pay.
        Add(SettlementLineCategory.UnpaidSalary, false,
            "Unpaid salary to the last working day", null, SettlementLineComputation.CannotCompute,
            $"Pay to the last working day. {FinanceValues}");

        // Notice pay only where the notice was to be PAID rather than served or waived. The DAYS are
        // HR's fact; the money is Finance's (L-74 — HR priced them at monthly × 12 ÷ 365 a day).
        var (_, _, shortfall) = Notice(separation);
        if (separation.IsNoticePaidInLieu && shortfall is > 0)
            Add(SettlementLineCategory.NoticePay, false,
                $"Notice pay in lieu — {shortfall} day(s) not served",
                null, SettlementLineComputation.CannotCompute,
                $"{shortfall} day(s) of notice paid in lieu rather than served. {FinanceValues}",
                days: shortfall.Value);

        // Annual leave owed on exit: FR-HR-046 (cash on exit only), FR-HR-152 (the cap, a setting since
        // round 5 lane L2b) and the Labour Act (s.30: the year's share; s.30(3): none on summary dismissal).
        await AddLeaveEncashmentLineAsync(tenantId, separation, settings, Add, cancellationToken);

        // ── Deductions ────────────────────────────────────────────────────────

        var clearanceOutstanding = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId
                        && i.OutstandingAmount != null && i.OutstandingAmount > 0)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var item in clearanceOutstanding)
        {
            // ⚠ A figure in a currency this statement is not stated in cannot simply be added to
            // it - the same treatment the travel advances below get, and for the same reason.
            // Asset lines carry the currency their surcharge was assessed in (area 16 slice 10);
            // null means the settlement's own, which is what every hand-entered amount means.
            var sameCurrency = string.IsNullOrWhiteSpace(item.OutstandingCurrencyCode)
                || string.Equals(item.OutstandingCurrencyCode, currency, StringComparison.OrdinalIgnoreCase);

            Add(CategoryForClearance(item.Kind), true,
                $"{item.Name} — outstanding at clearance",
                sameCurrency ? item.OutstandingAmount : null,
                sameCurrency ? SettlementLineComputation.Computed : SettlementLineComputation.CannotCompute,
                $"Recorded on the clearance form as {item.Status}"
                + (string.IsNullOrWhiteSpace(item.SignedOffBy) ? "." : $", signed off by {item.SignedOffBy}.")
                + (sameCurrency
                    ? string.Empty
                    : $" The amount is {item.OutstandingAmount:N2} {item.OutstandingCurrencyCode}; "
                      + $"convert it to {currency} and enter the figure."),
                clearanceItemId: item.Id);
        }

        // Travel advances the employee still holds. HR's own data, and until now nothing connected
        // it to somebody leaving — an employee could walk out owing one with nothing to notice.
        var advances = await _unitOfWork.Repository<StaffTravelAdvance>().GetQueryable()
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EmployeeId == separation.EmployeeId
                        && (a.Status == TravelAdvanceStatus.Disbursed
                            || a.Status == TravelAdvanceStatus.PartiallySettled))
            .ToListAsync(cancellationToken);

        foreach (var advance in advances)
        {
            var outstanding = (advance.ApprovedAmount ?? advance.RequestedAmount) - advance.SettledAmount;
            if (outstanding <= 0) continue;

            // ⚠ A currency the settlement is not stated in cannot simply be added to it. Recorded
            // as uncomputed with the figure in the text, rather than converted here: Finance owns
            // conversion, and its rates are known to be inverted (see StaffTravelCurrencyBridge).
            var sameCurrency = string.Equals(advance.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase);

            Add(SettlementLineCategory.TravelAdvanceRecovery, true,
                $"Travel advance {advance.AdvanceNumber} outstanding",
                sameCurrency ? outstanding : null,
                sameCurrency ? SettlementLineComputation.Computed : SettlementLineComputation.CannotCompute,
                sameCurrency
                    ? $"Advance {advance.AdvanceNumber}: {currency} {(advance.ApprovedAmount ?? advance.RequestedAmount):N2} less {currency} {advance.SettledAmount:N2} settled."
                    : $"Advance {advance.AdvanceNumber} is in {advance.CurrencyCode}, not {currency} — {advance.CurrencyCode} {outstanding:N2} outstanding. Convert through Finance and enter the amount.",
                travelAdvanceId: advance.Id);
        }

        foreach (var line in lines)
            await _unitOfWork.Repository<SeparationSettlementLine>().AddAsync(line);

        separation.Status = SeparationStatus.SettlementPending;
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement prepared for separation {Number}: {Lines} lines, {Uncomputed} uncomputed",
            separation.SeparationNumber, lines.Count,
            lines.Count(l => l.Computation == SettlementLineComputation.CannotCompute));

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    /// <summary>
    /// The leaver's annual leave line — FR-HR-046 (cash for leave on exit, and only on exit), FR-HR-152
    /// (a cap on the days) and the Labour Act 2003 (round 5, lane L2).
    /// </summary>
    /// <remarks>
    /// <para><b>What is paid: annual leave owed at the last day</b>, by the working the leave owed
    /// report uses (<see cref="ILeaveOwedCalculator"/>): this leave year's share built up to the day
    /// employment ends — the accrual stops there when the policy pro-rates on exit, which is what
    /// makes <c>ProRateOnExit</c> bind — plus carried days not yet lapsed, plus adjustments, less leave
    /// taken by then and days already cashed in; less, too, requests still awaiting a decision, which
    /// would be paid twice if they are approved after the cash.</para>
    ///
    /// <para>⚠ <b>Until lane L this summed every balance of every leave type and every year</b> — sick
    /// days, casual days and closed years included — with whole-year figures. Only annual leave is
    /// owed in cash (Act 651 s.30 speaks of annual leave, and sick or casual days are not a reserve
    /// of money), and only the year the person leaves in (earlier years were carried or lapsed).</para>
    ///
    /// <para><b>Summary dismissal pays none</b> (Act 651 s.30(3)). The line is written anyway, at
    /// zero, saying so: a missing line is invisible on a statement in a way a stated zero is not.
    /// Finance's posting skips a zero line.</para>
    ///
    /// <para><b>No amount: HR records the days, Finance values them</b> (leave settings audit 2,
    /// L-74). This line used to carry an "indicative" amount HR worked out at monthly × 12 ÷ 365 a
    /// day, and that figure went into Finance's books when Internal Audit released the statement
    /// unless somebody overwrote it. It now carries the days and their working, unvalued, and holds
    /// the statement open until Finance has put the money on it.</para>
    /// </remarks>
    private async Task AddLeaveEncashmentLineAsync(
        Guid tenantId, EmployeeSeparation separation, CompanyHrPolicySettings settings,
        Action<SettlementLineCategory, bool, string, decimal?, SettlementLineComputation, string, Guid?, Guid?, decimal?> add,
        CancellationToken cancellationToken)
    {
        const SettlementLineCategory category = SettlementLineCategory.LeaveEncashment;

        if (separation.SeparationType == EmployeeTerminationType.SummaryDismissal)
        {
            // A stated zero is a FACT — no days are owed — not a valuation, so it needs no Finance step.
            add(category, false, "Annual leave on exit — none payable", 0m, SettlementLineComputation.Computed,
                "Summary dismissal: no leave pay is due on dismissal without notice (Labour Act 2003, "
                + "Act 651, s.30(3)).", null, null, 0m);
            return;
        }

        var lastDay = separation.EffectiveDate ?? separation.LastWorkingDay;
        if (lastDay is not DateOnly exit)
        {
            add(category, false, "Annual leave owed on exit", null, SettlementLineComputation.CannotCompute,
                "The separation has no end date yet, so the leave owed at it cannot be worked out. Record the "
                + $"days on this line. {FinanceValues}", null, null, null);
            return;
        }

        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .Where(e => e.Id == separation.EmployeeId && e.TenantId == tenantId)
            .Select(e => new { e.DateEmployed, StaffLevelId = e.Position != null ? e.Position.StaffLevelId : null })
            .FirstAsync(cancellationToken);

        LeaveOwedComputation owed;
        try
        {
            // ⚠ The build-up is asked of the leave year's END, with the last day on the subject, so the
            // accrual policy's ProRateOnExit decides where it stops: on, at the last day; off, the
            // whole year. Asked as at the last day, the clock would stop there whatever the policy said.
            owed = await _leaveOwed.ComputeAsync(
                exit,
                new[] { new LeaveAccrualSubject(separation.EmployeeId, employee.DateEmployed, exit, employee.StaffLevelId) },
                accrueToYearEnd: true,
                ct: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // No Annual leave type: said on the line, not thrown at whoever is preparing the statement.
            add(category, false, "Annual leave owed on exit", null, SettlementLineComputation.CannotCompute,
                $"{ex.Message} Record the days on this line. {FinanceValues}", null, null, null);
            return;
        }

        var f = owed.ByEmployee[separation.EmployeeId];
        var days = f.OwedDays - f.AwaitingApprovalDays;

        // Where the build-up stopped: the last day (the policy pro-rates on exit), or the year's end
        // (it does not) — said, because the two differ by up to a year's leave.
        var builtUpTo = f.BuiltUpTo is DateOnly to ? $" to {to:d MMM yyyy}" : " (whole entitlement)";

        // ⚠ Basis holds 500 characters. The first cut of this line wrote the working, both notes, the
        // cap and the daily rate's whole sentence, and a capped leaver's statement failed to save —
        // a 500 on "prepare", found by lane L's suite. Hence the short words and a last-resort fit.
        var parts = new List<string>
        {
            $"{owed.AnnualType.Name} {owed.Year}, last day {exit:d MMM yyyy}: built up{builtUpTo} {f.BuiltUpDays:0.##}"
            + $" + carried {f.CarriedInDays:0.##} + adjusted {f.AdjustmentDays:0.##} − taken {f.TakenDays:0.##}"
            + $" − cashed {f.CashedInDays:0.##}"
            + (f.AwaitingApprovalDays > 0 ? $" − awaiting a decision {f.AwaitingApprovalDays:0.##}" : string.Empty)
            + $" = {days:0.##} day(s).",
        };
        if (f.AwaitingApprovalDays > 0)
            parts.Add("Awaiting a decision: cancel any never taken, then correct this line.");
        if (f.BookedDays > 0)
            parts.Add($"{f.BookedDays:0.##} day(s) approved after the last day are not deducted: cancel them.");

        if (days <= 0)
        {
            // No days owed: a stated zero, which is a fact rather than a valuation.
            add(category, false, "Annual leave owed on exit — none", 0m, SettlementLineComputation.Computed,
                FitBasis(parts), null, null, 0m);
            return;
        }

        // FR-HR-152's cap, a company setting since lane L2b; empty means none. Days, so it stays HR's.
        var cap = settings.SettlementLeaveDaysCap;
        var paid = cap is int c && days > c ? c : days;
        if (paid < days)
            parts.Add($"Capped at {paid:0.##} by the settlement cap (FR-HR-152), from {days:0.##}.");

        // ⚠ The DAYS, unvalued (audit 2, L-74). The working comes first in the basis, so FitBasis
        // keeps it if anything has to give.
        parts.Add(FinanceValues);
        add(category, false, $"Annual leave owed on exit — {paid:0.##} day(s)",
            null, SettlementLineComputation.CannotCompute, FitBasis(parts), null, null, paid);
    }

    /// <summary>
    /// Joins a line's sentences and makes them fit <c>SeparationSettlementLine.Basis</c> (500).
    /// </summary>
    /// <remarks>
    /// A last resort: the leave line's sentences are written to fit, but a daily-rate source named at
    /// length could still carry it over, and an overlong basis fails the whole statement's save. The
    /// working comes first, so it is what survives.
    /// </remarks>
    private static string FitBasis(IEnumerable<string> parts)
    {
        const int max = 500;
        var text = string.Join(" ", parts);
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    /// <summary>Which settlement category a clearance line's outstanding amount belongs under.</summary>
    private static SettlementLineCategory CategoryForClearance(ClearanceItemKind kind) => kind switch
    {
        ClearanceItemKind.OutstandingLoan => SettlementLineCategory.LoanRepayment,
        ClearanceItemKind.SalaryAdvance => SettlementLineCategory.SalaryAdvanceRecovery,
        ClearanceItemKind.PayrollRecovery => SettlementLineCategory.OtherDeduction,
        _ => SettlementLineCategory.PropertyRecovery,
    };

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> GetSettlementAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var separation = await Scoped(tenantId).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == separationId, cancellationToken)
            ?? throw new ArgumentException($"Separation with ID '{separationId}' not found.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .Include(s => s.PreparedBy)
            .Include(s => s.FinalisedBy)
            .Include(s => s.ReviewedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId,
                cancellationToken)
            ?? throw new ArgumentException("No settlement has been prepared for this separation.");

        var lines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .Include(l => l.ValuedByEmployee)
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SettlementId == settlement.Id)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Description)
            .ToListAsync(cancellationToken);

        // Lines still waiting for a figure: a pay line until Finance values it (AwaitsFinance), any
        // other line while nobody could value it.
        var uncomputed = lines.Count(StillToValue);
        var earnings = lines.Where(l => !l.IsDeduction).Sum(l => l.Amount ?? 0m);
        var deductions = lines.Where(l => l.IsDeduction).Sum(l => l.Amount ?? 0m);

        // ⚠ Keyed on the separation's status, not FinalisedOn — the rule RequireEditable states. A
        // statement Internal Audit returned keeps its FinalisedOn as history, and read "finalised"
        // here until leave settings audit 2, so it could never be finalised again from the screen.
        var canFinalise = uncomputed == 0
                          && lines.Count > 0
                          && separation.Status == SeparationStatus.SettlementPending;

        return new SeparationSettlementDto
        {
            Id = settlement.Id,
            SeparationId = separationId,
            SeparationNumber = separation.SeparationNumber,
            EmployeeName = FullName(separation.Employee),
            SeparationStatus = separation.Status,
            SeparationStatusName = separation.Status.ToString(),
            CurrencyCode = settlement.CurrencyCode,
            DailyRate = settlement.DailyRate,
            DailyRateBasis = settlement.DailyRateBasis,
            Lines = lines.Select(ToSettlementLineDto).ToList(),
            GrossEarnings = earnings,
            TotalDeductions = deductions,
            NetPayable = earnings - deductions,
            UncomputedLines = uncomputed,
            IsFinalised = settlement.FinalisedOn.HasValue,
            FinalisedOn = settlement.FinalisedOn,
            FinalisedByName = settlement.FinalisedBy == null ? null : FullName(settlement.FinalisedBy),
            PreparedById = settlement.PreparedById,
            PreparedByName = settlement.PreparedBy == null ? null : FullName(settlement.PreparedBy),
            PreparedOn = settlement.PreparedOn,
            Notes = settlement.Notes,
            CanFinalise = canFinalise,
            BlockedReason = SettlementBlockedReason(separation, lines, uncomputed),
            ReviewOutcome = settlement.ReviewOutcome,
            ReviewOutcomeName = settlement.ReviewOutcome.ToString(),
            ReviewedById = settlement.ReviewedById,
            ReviewedByName = settlement.ReviewedBy == null ? null : FullName(settlement.ReviewedBy),
            ReviewedOn = settlement.ReviewedOn,
            ReviewNotes = settlement.ReviewNotes,
            ReturnCount = settlement.ReturnCount,
            IsClearedForPayment = settlement.ReviewOutcome == SettlementReviewOutcome.Approved
                                  && separation.Status == SeparationStatus.SettlementApproved,
        };
    }

    private static string? SettlementBlockedReason(
        EmployeeSeparation separation, List<SeparationSettlementLine> lines, int uncomputed)
    {
        // The separation's status, not FinalisedOn: a returned statement keeps that as history.
        if (separation.Status == SeparationStatus.SettlementUnderReview)
            return "This settlement has been finalised and is with Internal Audit.";

        if (separation.Status != SeparationStatus.SettlementPending)
            return $"This separation is {separation.Status}; a settlement is finalised while it is awaiting one.";

        if (lines.Count == 0)
            return "The settlement has no lines.";

        if (uncomputed > 0)
        {
            // Leave settings audit 2 (P2/P3): a pay line waits on Finance, not on HR.
            var awaitingFinance = lines.Count(AwaitsFinance);
            var other = uncomputed - awaitingFinance;
            var parts = new List<string>();
            if (awaitingFinance > 0)
                parts.Add($"{awaitingFinance} pay line(s) await Finance's valuation (Pay to value)");
            if (other > 0)
                parts.Add($"{other} other line(s) could not be valued — enter each amount and name its source, "
                          + "or remove the line if nothing is owed");
            return string.Join("; ", parts) + ".";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementLineDto> AddSettlementLineAsync(
        Guid separationId, AddSettlementLineDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var settlement = await RequireEditableSettlementAsync(tenantId, separationId, cancellationToken);
        await _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceSeparationSettlement, settlement.Id, "Adding a settlement line", cancellationToken);

        if (string.IsNullOrWhiteSpace(dto.Description))
            throw new InvalidOperationException("Describe what the line is for.");

        // An amount with no stated source is a number nobody can check. FR-HR-185 puts Internal
        // Audit in front of this statement; they need to know where each figure came from.
        if (dto.Amount is not null && string.IsNullOrWhiteSpace(dto.SourceReference))
            throw new InvalidOperationException(
                "Name the source of the amount — the payroll report, loan statement or letter it came from.");

        if (dto.Amount is < 0)
            throw new InvalidOperationException("A settlement amount cannot be negative. Use a deduction line instead.");

        // Leave settings audit 2 (P3): HR records a pay line's facts — its days — and Finance prices it.
        var payLine = IsPayLine(dto.Category);
        if (payLine && dto.Amount is not null)
            throw new InvalidOperationException(PayLineIsFinances);
        if (dto.Days is < 0)
            throw new InvalidOperationException("The days cannot be negative.");

        RequireDirectionMatchesCategory(dto.Category, dto.IsDeduction);

        var maxOrder = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SettlementId == settlement.Id)
            .Select(l => (int?)l.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var line = new SeparationSettlementLine
        {
            TenantId = tenantId,
            SettlementId = settlement.Id,
            Category = dto.Category,
            IsDeduction = dto.IsDeduction,
            Description = dto.Description.Trim(),
            Amount = dto.Amount,
            Days = dto.Days,
            Computation = dto.Amount is null
                ? SettlementLineComputation.CannotCompute
                : SettlementLineComputation.ManuallyEntered,
            Basis = payLine
                ? FinanceValues
                : dto.Amount is null ? "Recorded as owed; the amount is not yet known." : null,
            SourceReference = string.IsNullOrWhiteSpace(dto.SourceReference) ? null : dto.SourceReference.Trim(),
            IsSystemGenerated = false,
            SortOrder = maxOrder + 10,
        };

        await _unitOfWork.Repository<SeparationSettlementLine>().AddAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToSettlementLineDto(line);
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementLineDto> UpdateSettlementLineAsync(
        Guid lineId, UpdateSettlementLineDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var line = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Settlement line with ID '{lineId}' not found.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == line.SettlementId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("The settlement this line belongs to was not found.");
        await _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceSeparationSettlement, settlement.Id, "Changing a settlement line", cancellationToken);

        RequireEditable(await RequireAsync(tenantId, settlement.SeparationId, cancellationToken));

        if (dto.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new InvalidOperationException("Describe what the line is for.");
            line.Description = dto.Description.Trim();
        }

        // ⚠ Checked against the LINE's category, not the payload's — this DTO has no category, so
        // without this a system-generated recovery could be flipped into an earning here.
        if (dto.IsDeduction is { } deduction)
        {
            RequireDirectionMatchesCategory(line.Category, deduction);
            line.IsDeduction = deduction;
        }

        // Leave settings audit 2 (P3): on a pay line HR holds the facts, Finance the money.
        var payLine = IsPayLine(line.Category);
        if (payLine && dto.Amount is not null)
            throw new InvalidOperationException(PayLineIsFinances);

        if (dto.Days is { } newDays && newDays != line.Days)
        {
            if (newDays < 0)
                throw new InvalidOperationException("The days cannot be negative.");
            line.Days = newDays;

            // ⚠ Finance's figure was for the OLD days. Changed days mean an unvalued line again, so
            // the statement waits for Finance rather than carrying an amount nobody priced.
            if (payLine && line.Computation == SettlementLineComputation.ValuedByFinance)
            {
                line.Amount = null;
                line.Computation = SettlementLineComputation.CannotCompute;
                line.ValuedByEmployeeId = null;
                line.ValuedOn = null;
                line.SourceReference = null;
                line.Basis = $"Days changed by HR after Finance valued them: to be valued again. {FinanceValues}";
            }
        }

        if (dto.SourceReference is not null && !payLine)
            line.SourceReference = string.IsNullOrWhiteSpace(dto.SourceReference) ? null : dto.SourceReference.Trim();

        if (dto.Amount is { } amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("A settlement amount cannot be negative. Use a deduction line instead.");

            if (string.IsNullOrWhiteSpace(line.SourceReference))
                throw new InvalidOperationException(
                    "Name the source of the amount — the payroll report, loan statement or letter it came from.");

            line.Amount = amount;

            // Supplying the figure the system could not work out is what clears the block. The line
            // becomes ManuallyEntered rather than Computed: a person vouched for it, not the system.
            line.Computation = SettlementLineComputation.ManuallyEntered;
        }

        await _unitOfWork.Repository<SeparationSettlementLine>().UpdateAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToSettlementLineDto(line);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Finance's step (leave settings audit 2, P2): the holder of <c>HR.Pay.Value</c> puts the money
    /// on a pay line HR recorded in days or facts. Only while the statement is still a draft — the
    /// same window HR edits in — and never after it has posted.
    /// </remarks>
    public async Task<SeparationSettlementLineDto> ValueSettlementLineAsync(
        Guid lineId, ValueSettlementLineDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        // ⚠ Who valued it is the point of the step, so an account with no employee record is refused
        // rather than recorded as nobody.
        if (actorEmployeeId is not Guid valuer || valuer == Guid.Empty)
            throw new InvalidOperationException(
                "Valuing pay requires your user account to be linked to an employee record, so the statement can "
                + "say who valued it. Please contact your administrator.");

        var line = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Settlement line with ID '{lineId}' not found.");

        if (!IsPayLine(line.Category))
            throw new InvalidOperationException(
                "Only pay is valued by Finance — unpaid salary, notice pay, annual leave owed, gratuity, benefits, "
                + "pension, other earnings and tax. This line is a recovery HR carries as recorded.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == line.SettlementId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("The settlement this line belongs to was not found.");
        await _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceSeparationSettlement, settlement.Id, "Valuing a settlement line", cancellationToken);
        RequireEditable(await RequireAsync(tenantId, settlement.SeparationId, cancellationToken));

        if (dto.Amount < 0)
            throw new InvalidOperationException("An amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(dto.SourceReference))
            throw new InvalidOperationException(
                "Name where the figure came from — the payroll computation or worksheet it was worked out on.");

        line.Amount = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero);
        line.SourceReference = dto.SourceReference.Trim();
        line.Computation = SettlementLineComputation.ValuedByFinance;
        line.ValuedByEmployeeId = valuer;
        line.ValuedOn = DateTime.UtcNow;
        // The basis keeps HR's working (the days, how they were reached); its "to be valued" ending
        // would now be untrue.
        if (line.Basis is not null)
            line.Basis = line.Basis.Replace(FinanceValues, "Valued by Finance.");

        await _unitOfWork.Repository<SeparationSettlementLine>().UpdateAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        line.ValuedByEmployee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == valuer, cancellationToken);
        return ToSettlementLineDto(line);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayToValueItemDto>> GetSettlementsAwaitingValuationAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var payCategories = Enum.GetValues<SettlementLineCategory>().Where(IsPayLine).ToArray();

        var rows = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .AsNoTracking()
            // AwaitsFinance, as SQL: a pay line not valued by Finance and not a stated zero.
            .Where(l => l.TenantId == tenantId && !l.IsDeleted
                        && payCategories.Contains(l.Category)
                        && l.Computation != SettlementLineComputation.ValuedByFinance
                        && !(l.Computation == SettlementLineComputation.Computed && l.Amount == 0m)
                        && !l.Settlement.IsDeleted
                        && l.Settlement.Separation.Status == SeparationStatus.SettlementPending)
            .Select(l => new
            {
                l.Settlement.SeparationId,
                l.Settlement.CurrencyCode,
                l.Category,
                l.Days,
                l.SortOrder,
                l.Settlement.Separation.SeparationNumber,
                l.Settlement.Separation.Employee.FirstName,
                l.Settlement.Separation.Employee.LastName,
                l.Settlement.Separation.Employee.EmployeeNumber,
                LastDay = l.Settlement.Separation.EffectiveDate ?? l.Settlement.Separation.LastWorkingDay,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.SeparationId)
            .Select(g =>
            {
                var first = g.First();
                var days = g.Where(r => r.Days.HasValue).Select(r => r.Days!.Value).ToList();
                return new PayToValueItemDto
                {
                    Kind = "Settlement",
                    Id = g.Key,
                    Reference = first.SeparationNumber,
                    EmployeeName = $"{first.FirstName} {first.LastName}".Trim(),
                    EmployeeNumber = first.EmployeeNumber,
                    LastDay = first.LastDay,
                    CurrencyCode = first.CurrencyCode,
                    AwaitingCount = g.Count(),
                    Days = days.Count > 0 ? days.Sum() : null,
                    Summary = string.Join("; ", g.OrderBy(r => r.SortOrder).Select(r =>
                        PayLineLabel(r.Category) + (r.Days is decimal d ? $" {d:0.##} day(s)" : string.Empty))),
                };
            })
            .OrderBy(i => i.LastDay)
            .ThenBy(i => i.Reference)
            .ToList();
    }

    /// <summary>A pay line's name on Finance's queue.</summary>
    private static string PayLineLabel(SettlementLineCategory category) => category switch
    {
        SettlementLineCategory.UnpaidSalary => "Unpaid salary",
        SettlementLineCategory.NoticePay => "Notice pay",
        SettlementLineCategory.LeaveEncashment => "Annual leave owed",
        SettlementLineCategory.GratuityOrEndOfService => "Gratuity",
        SettlementLineCategory.PensionRelated => "Pension",
        SettlementLineCategory.TaxDeduction => "Tax",
        _ => category.ToString(),
    };

    /// <inheritdoc />
    public async Task<bool> DeleteSettlementLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var line = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Settlement line with ID '{lineId}' not found.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == line.SettlementId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("The settlement this line belongs to was not found.");
        await _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceSeparationSettlement, settlement.Id, "Changing a settlement line", cancellationToken);

        RequireEditable(await RequireAsync(tenantId, settlement.SeparationId, cancellationToken));

        await _unitOfWork.Repository<SeparationSettlementLine>().DeleteAsync(lineId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> FinaliseSettlementAsync(
        Guid separationId, FinaliseSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);
        var settlement = await RequireEditableSettlementAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.SettlementPending)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; a settlement is finalised while it is awaiting one.");

        var lines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SettlementId == settlement.Id)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            throw new InvalidOperationException("The settlement has no lines, so there is nothing to finalise.");

        // The rule this whole slice turns on. A statement finalised with an unvalued line would go
        // to Internal Audit, and then to payment, carrying a silent zero where a real amount was
        // owed. Zero is a claim; the block is what keeps it from being made by accident.
        var uncomputed = lines.Where(StillToValue).ToList();
        if (uncomputed.Count > 0)
        {
            // Leave settings audit 2: a pay line waits on Finance's figure, not on HR's — so the
            // refusal says which lines are whose, as the statement's blocked reason does. A pay line
            // carrying HR's figure waits too (AwaitsFinance): only Finance's figure settles it.
            static string Names(List<SeparationSettlementLine> of) =>
                string.Join(", ", of.Take(4).Select(l => l.Description))
                + (of.Count > 4 ? $" and {of.Count - 4} more" : string.Empty);

            var awaitingFinance = uncomputed.Where(AwaitsFinance).ToList();
            var other = uncomputed.Where(l => !AwaitsFinance(l)).ToList();
            var parts = new List<string>();
            if (awaitingFinance.Count > 0)
                parts.Add($"{awaitingFinance.Count} pay line(s) await Finance's valuation in Pay to value — "
                          + Names(awaitingFinance));
            if (other.Count > 0)
                parts.Add($"{other.Count} other line(s) could not be valued — {Names(other)}: enter each amount "
                          + "and name its source, or remove the line if nothing is owed");
            throw new InvalidOperationException(
                string.Join("; ", parts) + ". A settlement is not finalised with an unknown amount showing as zero.");
        }

        settlement.FinalisedOn = DateTime.UtcNow;
        settlement.FinalisedById = actorEmployeeId;
        if (!string.IsNullOrWhiteSpace(dto?.Notes)) settlement.Notes = dto.Notes.Trim();

        separation.Status = SeparationStatus.SettlementUnderReview;

        await _unitOfWork.Repository<SeparationSettlement>().UpdateAsync(settlement);
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement finalised for separation {Number}; net {Currency} {Net}",
            separation.SeparationNumber, settlement.CurrencyCode,
            lines.Where(l => !l.IsDeduction).Sum(l => l.Amount ?? 0m)
            - lines.Where(l => l.IsDeduction).Sum(l => l.Amount ?? 0m));

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    /// <summary>
    /// A settlement may be edited only while its separation is awaiting one.
    /// </summary>
    /// <remarks>
    /// ⚠ Keyed on the <b>separation's status</b>, not on <c>FinalisedOn</c>. When Internal Audit
    /// returns a statement it becomes editable again, but <c>FinalisedOn</c> is deliberately kept —
    /// it records that the statement was finalised once, and clearing it to unlock editing would
    /// erase that. The status is the live question; the timestamp is history.
    /// </remarks>
    private static void RequireEditable(EmployeeSeparation separation)
    {
        if (separation.Status == SeparationStatus.SettlementUnderReview)
            throw new InvalidOperationException(
                "This settlement has been finalised and is with Internal Audit. Its lines can no "
                + "longer be changed unless Internal Audit returns it.");

        if (separation.Status != SeparationStatus.SettlementPending)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; its settlement can only be changed while "
                + "it is awaiting one.");
    }

    private async Task<SeparationSettlement> RequireEditableSettlementAsync(
        Guid tenantId, Guid separationId, CancellationToken cancellationToken)
    {
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);
        RequireEditable(separation);

        return await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
                   .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId,
                       cancellationToken)
               ?? throw new ArgumentException("No settlement has been prepared for this separation.");
    }

    // ── FR-HR-185: Internal Audit's review, before payment is released ────────

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> ApproveSettlementReviewAsync(
        Guid separationId, ReviewSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var (separation, settlement) = await RequireSettlementUnderReviewAsync(tenantId, separationId, cancellationToken);

        // Internal Audit's approval IS the release (FR-HR-185): there is no later pay step. The
        // approval and Finance's journal commit together (HR finish plan lane 8, slice 3) — a
        // settlement Finance refuses is not released. Finalising alone posts nothing: the register
        // said "do not post on finalise", and it does not.
        var lines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .Where(l => l.TenantId == tenantId && l.SettlementId == settlement.Id && !l.IsDeleted)
            .OrderBy(l => l.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Leave settings audit 2 (P5): nothing HR priced goes into Finance's books. Only a statement
        // finalised before pay moved to Finance can carry HR's figures; returning it clears them.
        var hrPriced = lines.Where(IsHrPricedPayLine).ToList();
        if (hrPriced.Count > 0)
            throw new InvalidOperationException(
                $"{hrPriced.Count} pay line(s) on this statement carry a figure HR entered before pay moved to "
                + $"Finance — {string.Join(", ", hrPriced.Take(4).Select(l => l.Description))}. Return the "
                + "statement to HR: returning it clears those figures, and Finance values the lines in Pay to value.");

        // A deduction that recovers an asset surcharge Finance already holds as a receivable must
        // credit that receivable, not recoveries income a second time (slice 4). The line points at
        // its clearance item, the item at the surcharge, and the register says whether it posted.
        var recoveringPosted = new HashSet<Guid>();
        var clearanceItemIds = lines.Where(l => l.IsDeduction && l.SourceClearanceItemId.HasValue).Select(l => l.SourceClearanceItemId!.Value).Distinct().ToList();
        if (clearanceItemIds.Count > 0)
        {
            var surchargeByItem = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
                .Where(i => i.TenantId == tenantId && clearanceItemIds.Contains(i.Id) && i.SourceSurchargeId != null)
                .AsNoTracking()
                .Select(i => new { i.Id, SurchargeId = i.SourceSurchargeId!.Value })
                .ToListAsync(cancellationToken);
            foreach (var pair in surchargeByItem)
            {
                if (await _financePosting.IsPostedAsync(HrFinancePostingEventCatalog.AssetSurchargeApproved, pair.SurchargeId, cancellationToken))
                    foreach (var line in lines.Where(l => l.SourceClearanceItemId == pair.Id)) recoveringPosted.Add(line.Id);
            }
        }

        await _financePosting.RunAsync(async ct =>
        {
            settlement.ReviewOutcome = SettlementReviewOutcome.Approved;
            settlement.ReviewedById = actorEmployeeId;
            settlement.ReviewedOn = DateTime.UtcNow;
            settlement.ReviewNotes = string.IsNullOrWhiteSpace(dto?.Notes) ? null : dto!.Notes.Trim();

            separation.Status = SeparationStatus.SettlementApproved;

            await _unitOfWork.Repository<SeparationSettlement>().UpdateAsync(settlement);
            await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
            await _unitOfWork.SaveChangesAsync(ct);

            return HrFinancePostingCommandFactory.SeparationSettlementReleased(
                settlement, lines, separation.SeparationNumber, separation.EmployeeId, recoveringPosted);
        }, _currentUserProvider.UserId, cancellationToken);

        _logger.LogInformation(
            "Settlement for separation {Number} reviewed and approved by Internal Audit; payment may be released",
            separation.SeparationNumber);

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> ReturnSettlementAsync(
        Guid separationId, ReviewSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // A control that can refuse without saying why leaves HR guessing at what to correct, and
        // the statement comes back unchanged.
        if (string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException(
                "Set out the findings when returning a settlement — what is wrong with it, so it can be corrected.");

        var tenantId = GetTenantId();
        var (separation, settlement) = await RequireSettlementUnderReviewAsync(tenantId, separationId, cancellationToken);
        await _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceSeparationSettlement, settlement.Id, "Returning this settlement", cancellationToken);

        settlement.ReviewOutcome = SettlementReviewOutcome.Returned;
        settlement.ReviewedById = actorEmployeeId;
        settlement.ReviewedOn = DateTime.UtcNow;
        settlement.ReviewNotes = dto.Notes.Trim();
        settlement.ReturnCount += 1;

        // Back to HR, editable again. FinalisedOn is kept: it says the statement was finalised once,
        // and ReturnCount says how often Internal Audit sent it back — the question an auditor asks
        // later is "how many times was this queried before it was paid".
        separation.Status = SeparationStatus.SettlementPending;

        // Leave settings audit 2: a statement finalised before pay moved to Finance comes back with
        // HR's figures on its pay lines. They are cleared here, so Finance values them before it can
        // be finalised again — left in place, HR could neither change them nor get it approved.
        var lines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .Where(l => l.TenantId == tenantId && l.SettlementId == settlement.Id && !l.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var line in lines.Where(IsHrPricedPayLine))
        {
            ClearHrFigure(line, "when Internal Audit returned the statement");
            await _unitOfWork.Repository<SeparationSettlementLine>().UpdateAsync(line);
        }

        await _unitOfWork.Repository<SeparationSettlement>().UpdateAsync(settlement);
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement for separation {Number} returned by Internal Audit (return #{Count})",
            separation.SeparationNumber, settlement.ReturnCount);

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    private async Task<(EmployeeSeparation Separation, SeparationSettlement Settlement)>
        RequireSettlementUnderReviewAsync(Guid tenantId, Guid separationId, CancellationToken cancellationToken)
    {
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.SettlementUnderReview)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. Internal Audit reviews a settlement once HR "
                + "has finalised it.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId,
                cancellationToken)
            ?? throw new ArgumentException("No settlement has been prepared for this separation.");

        return (separation, settlement);
    }

    private static SeparationSettlementLineDto ToSettlementLineDto(SeparationSettlementLine l) => new()
    {
        Id = l.Id,
        SettlementId = l.SettlementId,
        Category = l.Category,
        CategoryName = l.Category.ToString(),
        IsDeduction = l.IsDeduction,
        Description = l.Description,
        Amount = l.Amount,
        Computation = l.Computation,
        ComputationName = l.Computation.ToString(),
        Basis = l.Basis,
        SourceReference = l.SourceReference,
        SourceClearanceItemId = l.SourceClearanceItemId,
        SourceTravelAdvanceId = l.SourceTravelAdvanceId,
        IsSystemGenerated = l.IsSystemGenerated,
        SortOrder = l.SortOrder,
        Days = l.Days,
        IsPayLine = IsPayLine(l.Category),
        AwaitingFinance = AwaitsFinance(l),
        // Needs ValuedByEmployee loaded; the statement read includes it.
        ValuedByName = l.ValuedByEmployee == null ? null : FullName(l.ValuedByEmployee),
        ValuedOn = l.ValuedOn,
    };

    // ── Clearance: the catalogue ──────────────────────────────────────────────

    /// <summary>
    /// Which way a settlement category points: <c>true</c> a deduction, <c>false</c> an earning,
    /// <c>null</c> where the category genuinely does not say.
    /// </summary>
    /// <remarks>
    /// ⚠ Added after the slice-13 audit found that a line could contradict its own category. A line
    /// categorised <c>TaxDeduction</c> was accepted with <c>IsDeduction = false</c> and raised the
    /// net payable by its amount — the leaver would have been PAID their PAYE. The same hole let a
    /// system-generated travel-advance recovery be flipped into an earning through the update path,
    /// which carries no category of its own to check against.
    ///
    /// <para>Only <c>PensionRelated</c> is left to the caller: a pension line can be a payout owed
    /// to the leaver or a contribution owed by them, and the category alone cannot tell which.
    /// Everything else is named by its direction, including <c>OtherEarning</c> and
    /// <c>OtherDeduction</c>, which is where a genuinely unusual line belongs.</para>
    /// </remarks>
    private static bool? DirectionOf(SettlementLineCategory category) => category switch
    {
        SettlementLineCategory.UnpaidSalary
            or SettlementLineCategory.NoticePay
            or SettlementLineCategory.LeaveEncashment
            or SettlementLineCategory.GratuityOrEndOfService
            or SettlementLineCategory.BenefitPayment
            or SettlementLineCategory.OtherEarning => false,

        SettlementLineCategory.LoanRepayment
            or SettlementLineCategory.SalaryAdvanceRecovery
            or SettlementLineCategory.TravelAdvanceRecovery
            or SettlementLineCategory.PropertyRecovery
            or SettlementLineCategory.TaxDeduction
            or SettlementLineCategory.OtherDeduction => true,

        _ => null,
    };

    private static void RequireDirectionMatchesCategory(
        SettlementLineCategory category, bool isDeduction)
    {
        if (DirectionOf(category) is not { } expected || expected == isDeduction)
            return;

        throw new InvalidOperationException(
            $"A '{category}' line is {(expected ? "a deduction" : "an earning")}, and cannot be "
            + $"recorded as {(isDeduction ? "a deduction" : "an earning")}. Use "
            + $"'{(isDeduction ? nameof(SettlementLineCategory.OtherDeduction) : nameof(SettlementLineCategory.OtherEarning))}' "
            + "if that is really what this line is.");
    }

    /// <summary>The kinds that can carry money, and therefore feed the FR-HR-184 settlement.</summary>
    /// <remarks>
    /// <para>⚠ <b>The two property kinds were added in area 16 slice 10, and their absence was a
    /// defect rather than a decision.</b> <c>CategoryForClearance</c> has always mapped everything
    /// it does not otherwise recognise to <c>SettlementLineCategory.PropertyRecovery</c>, and
    /// <c>DirectionOf</c> has always known that category is a deduction — but no line could ever
    /// reach it, because the only path that sets an outstanding amount refused one on any kind not
    /// listed here. A settlement category that is mapped, directed and unreachable is a hole with a
    /// lid on it: an employee could walk out owing for a written-off laptop and the statement had
    /// no way to say so.</para>
    ///
    /// <para>Nobody owes a quantity of duty-post keys, and those kinds still refuse an amount.
    /// Recording one against a kind that cannot carry it is refused rather than stored and ignored
    /// — a number the settlement will never read is worse than no number, because somebody will
    /// believe it.</para>
    /// </remarks>
    private static bool CarriesAmount(ClearanceItemKind kind)
        => kind is ClearanceItemKind.OutstandingLoan
                or ClearanceItemKind.SalaryAdvance
                or ClearanceItemKind.PayrollRecovery
                or ClearanceItemKind.CompanyProperty
                or ClearanceItemKind.OfficeEquipment;

    /// <summary>FR-HR-183's list, as a starting catalogue for a tenant that has none.</summary>
    /// <remarks>
    /// <b>"Company property" is the line the HR Assets register feeds</b>, and it is the only one —
    /// see <c>SeparationClearanceTemplate.SourcesFromAssetRegister</c> for why sourcing both
    /// property kinds would list every asset, and deduct every surcharge, twice.
    /// </remarks>
    private static readonly (string Name, ClearanceItemKind Kind, bool SourcesAssets, string Description)[] DefaultTemplates =
    {
        ("Outstanding loans", ClearanceItemKind.OutstandingLoan, false,
            "Any staff loan not yet repaid in full. The balance is recovered from the final settlement."),
        ("Salary advances", ClearanceItemKind.SalaryAdvance, false,
            "Advances drawn against salary and not yet recovered."),
        ("Company property", ClearanceItemKind.CompanyProperty, true,
            "Vehicles, phones, tools, protective equipment and anything else issued to the employee. "
            + "Everything on the HR Assets register that the employee has not given back is listed "
            + "under this line automatically; use the line itself for property that was never registered."),
        ("Office equipment", ClearanceItemKind.OfficeEquipment, false,
            "Computers, peripherals and office equipment assigned to the employee or their desk."),
        ("Duty-post keys", ClearanceItemKind.DutyPostKeys, false,
            "Keys, access cards and passes for offices, stores, gates and vehicles."),
        ("Documents and records", ClearanceItemKind.DocumentsAndRecords, false,
            "Files, drawings, contracts and records held by the employee, and the handover of work in progress."),
        ("Payroll recoveries", ClearanceItemKind.PayrollRecovery, false,
            "Any other amount due back to the organisation through payroll."),
    };

    /// <inheritdoc />
    public async Task<IEnumerable<SeparationClearanceTemplateDto>> GetClearanceTemplatesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .Include(t => t.OwningOrganizationUnit)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        var templates = await query
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(ToTemplateDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceTemplateDto> CreateClearanceTemplateAsync(
        CreateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Give the clearance line a name.");

        var name = dto.Name.Trim();

        var clash = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Name == name, cancellationToken);
        if (clash)
            throw new InvalidOperationException($"A clearance line named '{name}' already exists.");

        await RequireOrganizationUnitAsync(tenantId, dto.OwningOrganizationUnitId, cancellationToken);

        if (dto.SourcesFromAssetRegister)
            await RequireSoleAssetSourceAsync(tenantId, null, cancellationToken);

        var template = new SeparationClearanceTemplate
        {
            TenantId = tenantId,
            Name = name,
            Kind = dto.Kind,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            OwningOrganizationUnitId = dto.OwningOrganizationUnitId,
            IsMandatory = dto.IsMandatory,
            IsActive = dto.IsActive,
            SourcesFromAssetRegister = dto.SourcesFromAssetRegister,
            SortOrder = dto.SortOrder,
        };

        await _unitOfWork.Repository<SeparationClearanceTemplate>().AddAsync(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToTemplateDto(await ReloadTemplateAsync(tenantId, template.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceTemplateDto> UpdateClearanceTemplateAsync(
        Guid id, UpdateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var template = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Clearance line with ID '{id}' not found.");

        if (dto.Name is not null)
        {
            var name = dto.Name.Trim();
            if (name.Length == 0)
                throw new InvalidOperationException("Give the clearance line a name.");

            var clash = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Name == name && t.Id != id, cancellationToken);
            if (clash)
                throw new InvalidOperationException($"A clearance line named '{name}' already exists.");

            template.Name = name;
        }

        if (dto.Kind is { } kind) template.Kind = kind;
        if (dto.Description is not null)
            template.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (dto.OwningOrganizationUnitId is { } unitId)
        {
            await RequireOrganizationUnitAsync(tenantId, unitId, cancellationToken);
            template.OwningOrganizationUnitId = unitId;
        }
        if (dto.IsMandatory is { } mandatory) template.IsMandatory = mandatory;
        if (dto.IsActive is { } active) template.IsActive = active;
        if (dto.SourcesFromAssetRegister is { } sources)
        {
            if (sources && !template.SourcesFromAssetRegister)
                await RequireSoleAssetSourceAsync(tenantId, id, cancellationToken);
            template.SourcesFromAssetRegister = sources;
        }
        if (dto.SortOrder is { } order) template.SortOrder = order;

        await _unitOfWork.Repository<SeparationClearanceTemplate>().UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToTemplateDto(await ReloadTemplateAsync(tenantId, template.Id, cancellationToken));
    }

    /// <summary>
    /// Refuses a second line fed by the HR Assets register — FR-HR-183, area 16 slice 10.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>This is a money rule, not a tidiness rule.</b> Two sourced lines list every unreturned
    /// asset twice, and each copy carries the same surcharge balance into
    /// <c>SeparationClearanceItem.OutstandingAmount</c> — which the FR-HR-184 settlement then
    /// deducts twice. The leaver's final pay would be short by the value of every damaged asset
    /// they were charged for, and every figure on the statement would still add up.
    ///
    /// <para>Asked of ALL lines including retired ones, because <c>IsActive</c> is a switch: a
    /// retired sourced line reactivated later would recreate the double without passing through
    /// any check.</para>
    /// </remarks>
    private async Task RequireSoleAssetSourceAsync(
        Guid tenantId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var holder = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted
                                      && t.SourcesFromAssetRegister
                                      && (exceptId == null || t.Id != exceptId), cancellationToken);

        if (holder is not null)
            throw new InvalidOperationException(
                $"'{holder.Name}' is already the clearance line fed by the HR Assets register, and "
                + "only one line may be. Two would list every unreturned asset twice and deduct "
                + "every surcharge twice from the final settlement. Turn it off there first.");
    }

    /// <inheritdoc />
    public async Task<bool> DeleteClearanceTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var template = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Clearance line with ID '{id}' not found.");

        // Items snapshot their template and hold no foreign key to it, so deleting a catalogue line
        // cannot orphan a signed form — which is exactly why deleting is allowed at all. Retiring
        // it (IsActive = false) is usually the better move and keeps it out of future forms only.
        await _unitOfWork.Repository<SeparationClearanceTemplate>().DeleteAsync(template.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SeparationClearanceTemplateDto>> SeedDefaultClearanceTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var existing = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);
        var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ⚠ Only claim the register if nothing else already has it. Seeding runs on a tenant that
        // may have configured its own form, and a second sourced line is exactly what the write
        // path refuses — a seeder that produced a state the API rejects would leave the catalogue
        // unfixable through the API that owns it.
        var sourcedAlready = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.SourcesFromAssetRegister, cancellationToken);

        var order = 0;
        foreach (var (name, kind, sourcesAssets, description) in DefaultTemplates)
        {
            order += 10;
            if (have.Contains(name)) continue;

            var sources = sourcesAssets && !sourcedAlready;
            if (sources) sourcedAlready = true;

            await _unitOfWork.Repository<SeparationClearanceTemplate>().AddAsync(new SeparationClearanceTemplate
            {
                TenantId = tenantId,
                Name = name,
                Kind = kind,
                Description = description,
                IsMandatory = true,
                IsActive = true,
                SourcesFromAssetRegister = sources,
                SortOrder = order,
            });
        }

        // ⚠ A tenant that seeded its form BEFORE the asset register could feed it keeps every line
        // it already has — the loop above skips by name — so nothing above would ever turn the
        // feature on for them, and nothing would say so either. Adopt the company-property line
        // they already have instead.
        //
        // Deliberately HERE rather than in a data migration. This is the endpoint that owns the
        // catalogue, it is idempotent and re-runnable by an administrator, and a migration that
        // writes rows is a decision taken once, invisibly, that cannot be re-taken when the data
        // moves. Guarded by `sourcedAlready`, so a tenant that has deliberately pointed the
        // register at some other line is left exactly as it is.
        if (!sourcedAlready)
        {
            var adopt = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive
                            && t.Kind == ClearanceItemKind.CompanyProperty)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (adopt is not null)
            {
                adopt.SourcesFromAssetRegister = true;
                await _unitOfWork.Repository<SeparationClearanceTemplate>().UpdateAsync(adopt);

                _logger.LogInformation(
                    "Clearance line '{Name}' now draws from the HR Assets register (FR-HR-183)",
                    adopt.Name);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetClearanceTemplatesAsync(includeInactive: true, cancellationToken);
    }

    // ── Clearance: the run ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SeparationClearanceDto> StartClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        // ⚠ Asked BEFORE the status check, not after. A separation whose clearance has begun is no
        // longer Approved — it is ClearanceInProgress — so a status-first check answers a restart
        // with "clearance begins once the separation has been approved", which is both confusing
        // and wrong: it *is* approved. Order the questions so the more specific one answers first.
        var alreadyStarted = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AnyAsync(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId, cancellationToken);
        if (alreadyStarted)
            throw new InvalidOperationException("Clearance has already been started for this separation.");

        if (separation.Status != SeparationStatus.Approved)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. Clearance begins once the separation has "
                + "been approved.");

        var templates = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // ⚠ Refused rather than started empty. A clearance form with no lines would complete the
        // instant it began — every mandatory item satisfied because there are none — and FR-HR-091's
        // gate would report "cleared" having checked nothing. An empty catalogue is a configuration
        // gap, and it must look like one.
        if (templates.Count == 0)
            throw new InvalidOperationException(
                "No clearance lines are configured, so there is nothing to clear. Set up the "
                + "clearance form first — the FR-HR-183 defaults can be seeded in one step.");

        // FR-HR-183, area 16 slice 10. Everything the leaver has not given back, read from the HR
        // Assets register rather than remembered by whoever fills in the form. Asked once, and only
        // when some line actually claims the register.
        var custody = templates.Any(t => t.SourcesFromAssetRegister)
            ? await _assetCustody.GetOutstandingCustodyAsync(tenantId, separation.EmployeeId, cancellationToken)
            : (IReadOnlyList<AssetCustodyLine>)Array.Empty<AssetCustodyLine>();

        var sourcedLines = 0;

        foreach (var template in templates)
        {
            // The catalogue line itself is created whether or not it feeds from the register. On a
            // sourced line it becomes the place property the organisation NEVER REGISTERED is
            // recorded by hand - a badge, a toolkit, a phone bought on petty cash. Replacing it
            // with the automated list would have narrowed the form, not widened it.
            await _unitOfWork.Repository<SeparationClearanceItem>().AddAsync(
                NewClearanceItem(tenantId, separationId, template));

            if (!template.SourcesFromAssetRegister) continue;

            foreach (var held in custody)
            {
                var line = NewClearanceItem(tenantId, separationId, template);
                ApplyCustody(line, held);
                await _unitOfWork.Repository<SeparationClearanceItem>().AddAsync(line);
                sourcedLines++;
            }
        }

        separation.Status = SeparationStatus.ClearanceInProgress;
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Clearance started for separation {Number} with {Count} lines, {Sourced} of them from the asset register",
            separation.SeparationNumber, templates.Count + sourcedLines, sourcedLines);

        return await GetClearanceAsync(separationId, cancellationToken);
    }

    /// <summary>One blank line of a form, snapshotted from its catalogue entry.</summary>
    /// <remarks>
    /// Snapshotted, not read through the template: editing the catalogue afterwards must not
    /// rewrite a form somebody has already signed.
    /// </remarks>
    private static SeparationClearanceItem NewClearanceItem(
        Guid tenantId, Guid separationId, SeparationClearanceTemplate template) => new()
    {
        TenantId = tenantId,
        SeparationId = separationId,
        TemplateId = template.Id,
        Name = template.Name,
        Kind = template.Kind,
        OwningOrganizationUnitId = template.OwningOrganizationUnitId,
        IsMandatory = template.IsMandatory,
        SortOrder = template.SortOrder,
        Status = ClearanceItemStatus.Pending,
    };

    /// <summary>Turns a blank line into the line about one particular unreturned asset.</summary>
    /// <remarks>
    /// <para><b>The sort order is the template's own, deliberately unchanged.</b> Asset lines
    /// therefore group with the catalogue line that produced them however the tenant has numbered
    /// its form, and the read's secondary sort by name orders them within the group. Numbering them
    /// <c>SortOrder + 1, + 2 ...</c> looked tidier and breaks: the seeded gap is ten, so a leaver
    /// holding more than nine assets would spill into the next heading.</para>
    ///
    /// <para><b>Only a DECIDED charge becomes an amount.</b> A surcharge still in draft, with the
    /// employee or awaiting approval is not a debt - carrying it here would deduct from somebody's
    /// final pay a figure no approver has authorised, on a record whose entire point is that they
    /// were asked first (decision D9). It still stops the line being cleared; that is
    /// <c>HasUndecidedCharge</c>'s job, and it is a different question.</para>
    /// </remarks>
    private static void ApplyCustody(SeparationClearanceItem item, AssetCustodyLine held)
    {
        item.Name = held.Label;
        item.SourceAssignmentId = held.AssignmentId;
        item.SourceSurchargeId = held.SurchargeId;
        item.OutstandingAmount = held.OutstandingSurcharge;
        item.OutstandingCurrencyCode = held.SurchargeCurrencyCode;
    }

    /// <summary>The live state of every custody a form names, keyed by assignment.</summary>
    private async Task<IReadOnlyDictionary<Guid, AssetCustodyLine>> CustodyOnFormAsync(
        Guid tenantId, IEnumerable<SeparationClearanceItem> items, CancellationToken cancellationToken)
    {
        var ids = items.Where(i => i.SourceAssignmentId is not null)
            .Select(i => i.SourceAssignmentId!.Value)
            .Distinct()
            .ToList();

        return await _assetCustody.GetCustodiesAsync(tenantId, ids, cancellationToken);
    }

    private static AssetCustodyLine? Custody(
        IReadOnlyDictionary<Guid, AssetCustodyLine> map, SeparationClearanceItem item)
        => item.SourceAssignmentId is { } id && map.TryGetValue(id, out var line) ? line : null;

    /// <inheritdoc />
    public async Task<SeparationClearanceDto> GetClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await Scoped(tenantId).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == separationId, cancellationToken)
            ?? throw new ArgumentException($"Separation with ID '{separationId}' not found.");

        var items = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .Include(i => i.OwningOrganizationUnit)
            .Include(i => i.RecordedBy)
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        // The custody behind each sourced line, re-read LIVE rather than taken from the snapshot.
        // The form is drawn when the separation is approved and the whole notice period sits
        // between that and somebody signing a line, so the snapshot's "still held" is the oldest
        // fact on the page. Without this the gate below refuses a line for a reason the form itself
        // cannot show.
        var custody = await CustodyOnFormAsync(tenantId, items, cancellationToken);

        var mandatoryOutstanding = items.Count(i => i.IsMandatory && !IsSettled(i.Status));

        return new SeparationClearanceDto
        {
            SeparationId = separation.Id,
            SeparationNumber = separation.SeparationNumber,
            EmployeeName = FullName(separation.Employee),
            SeparationStatus = separation.Status,
            SeparationStatusName = separation.Status.ToString(),
            Items = items.Select(i => ToClearanceItemDto(i, Custody(custody, i))).ToList(),
            TotalItems = items.Count,
            PendingItems = items.Count(i => i.Status == ClearanceItemStatus.Pending),
            ClearedItems = items.Count(i => i.Status == ClearanceItemStatus.Cleared),
            BlockedItems = items.Count(i => i.Status == ClearanceItemStatus.Blocked),
            WaivedItems = items.Count(i => i.Status == ClearanceItemStatus.Waived),
            NotApplicableItems = items.Count(i => i.Status == ClearanceItemStatus.NotApplicable),
            MandatoryOutstanding = mandatoryOutstanding,
            TotalOutstandingAmount = items.Sum(i => i.OutstandingAmount ?? 0m),
            CanComplete = items.Count > 0
                          && mandatoryOutstanding == 0
                          && separation.Status == SeparationStatus.ClearanceInProgress,
            BlockedReason = ClearanceBlockedReason(separation, items, mandatoryOutstanding),
        };
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceItemDto> RecordClearanceItemAsync(
        Guid itemId, RecordClearanceItemDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var item = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId && !i.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Clearance item with ID '{itemId}' not found.");

        var separation = await RequireAsync(tenantId, item.SeparationId, cancellationToken);

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; clearance lines can only be answered while "
                + "clearance is in progress.");

        // Both of these are answers that need explaining: "still outstanding" and "set aside" are
        // the two that stop a clearance form being a record of nothing.
        if (item.IsMandatory && dto.Status == ClearanceItemStatus.Waived && string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException("Give a reason for waiving a mandatory clearance line.");

        if (dto.Status == ClearanceItemStatus.Blocked && string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException("Say what is outstanding when marking a clearance line blocked.");

        if (dto.OutstandingAmount is { } amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("An outstanding amount cannot be negative.");

            if (!CarriesAmount(item.Kind))
                throw new InvalidOperationException(
                    $"A '{item.Kind}' clearance line does not carry an amount. Record what is "
                    + "outstanding in the notes instead.");
        }

        // FR-HR-183's teeth - area 16 slice 10, closing area 9b's decision D4. A line the asset
        // register raised is answered by an act in HR Assets, not by a signature on this form.
        await RequireCustodySettledAsync(tenantId, item, dto.Status, cancellationToken);

        item.Status = dto.Status;
        item.SignedOffBy = string.IsNullOrWhiteSpace(dto.SignedOffBy) ? null : dto.SignedOffBy.Trim();
        item.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        item.OutstandingAmount = dto.OutstandingAmount;
        // The currency belongs to the amount. Clearing the figure clears it; overwriting the figure
        // on a sourced line keeps it, because the person adjusting it is adjusting the same claim
        // in the currency it was assessed in. A hand-entered figure on a line that never had one
        // means the settlement's own currency, which is what every loan and advance line has always
        // meant - see SeparationClearanceItem.OutstandingCurrencyCode.
        if (dto.OutstandingAmount is null) item.OutstandingCurrencyCode = null;
        item.RecordedById = actorEmployeeId;
        item.RecordedOn = DateTime.UtcNow;

        await _unitOfWork.Repository<SeparationClearanceItem>().UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .Include(i => i.OwningOrganizationUnit)
            .Include(i => i.RecordedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            ?? throw new InvalidOperationException("Clearance line saved but could not be reloaded.");

        // ⚠ The custody is read here too, so this response and the clearance read agree. Mapping it
        // on one path and not the other is the exact shape that has produced a blank field six
        // times in area 16 — and it is worse on a write response, because the screen that just
        // saved the line renders THIS.
        var custody = await CustodyOnFormAsync(tenantId, new[] { saved }, cancellationToken);

        return ToClearanceItemDto(saved, Custody(custody, saved));
    }

    /// <summary>
    /// Refuses to mark an asset-sourced clearance line <c>Cleared</c> while the asset has not come
    /// back or money is still owed on it - FR-HR-183, area 16 slice 10.
    /// </summary>
    /// <remarks>
    /// <para><b>This is the gate area 9b's decision D4 promised and could not build.</b> That
    /// slice shipped clearance as a configurable checklist with a manual signature per line,
    /// because HR Assets did not exist yet, and said so: "it becomes an enforced gate for free when
    /// 16 lands". This is that, and free it was not - what makes it work is that area 16 knows the
    /// difference between four ways of not having something back.</para>
    ///
    /// <para><b>Only <c>Cleared</c> is refused.</b> <c>Blocked</c> is how a leaver walks out still
    /// holding something the organisation intends to recover, and it is the answer that carries an
    /// amount into the settlement; <c>Waived</c> takes a written reason; <c>NotApplicable</c>
    /// records that the line was wrong about them. None of those claims the asset came back.
    /// Refusing them all would have left an unreturnable asset with no answer at all and the whole
    /// exit stuck behind it.</para>
    ///
    /// <para><b>A custody that can no longer be found does not block.</b> The register is another
    /// area's data and its rows can be deleted; a clearance form is evidence about a particular
    /// exit and must stay answerable. The line still reads as the thing that was signed, because
    /// its name was snapshotted.</para>
    /// </remarks>
    private async Task RequireCustodySettledAsync(
        Guid tenantId, SeparationClearanceItem item, ClearanceItemStatus answer,
        CancellationToken cancellationToken)
    {
        if (answer != ClearanceItemStatus.Cleared) return;
        if (item.SourceAssignmentId is not { } assignmentId) return;

        var custody = await _assetCustody.GetCustodyAsync(tenantId, assignmentId, cancellationToken);
        if (custody is null) return;

        if (custody.StillHeld)
            throw new InvalidOperationException(
                $"'{item.Name}' has not been given back - the assignment is still {custody.Status}. "
                + "Record the return in HR Assets, or report it lost or damaged there if it is not "
                + "coming back. A clearance form cannot close a custody.");

        if (custody.OutstandingSurcharge > 0)
            throw new InvalidOperationException(
                $"{custody.OutstandingSurcharge:N2} {custody.SurchargeCurrencyCode} is still outstanding on surcharge "
                + $"{custody.SurchargeNumber} for '{item.Name}'. Mark this line blocked so the "
                + "amount reaches the final settlement, or waive it with a reason - 'cleared' says "
                + "nothing further is owed.");

        if (custody.HasUndecidedCharge)
            throw new InvalidOperationException(
                $"A surcharge against '{item.Name}' has been raised and not yet decided. Approve, "
                + "reject or cancel it before clearing this line - 'cleared' says the organisation "
                + "has no further claim, at the moment it is deciding one.");
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceDto> RefreshClearanceAssetsAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; the asset lines can only be refreshed "
                + "while clearance is in progress.");

        var sourcedTemplate = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive
                                      && t.SourcesFromAssetRegister, cancellationToken);

        if (sourcedTemplate is null)
            throw new InvalidOperationException(
                "No clearance line is fed by the HR Assets register, so there is nothing to "
                + "refresh. Turn it on for one line of the clearance form first.");

        var items = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId)
            .ToListAsync(cancellationToken);

        var already = items.Where(i => i.SourceAssignmentId is not null)
            .Select(i => i.SourceAssignmentId!.Value)
            .ToHashSet();

        var custody = await _assetCustody.GetOutstandingCustodyAsync(
            tenantId, separation.EmployeeId, cancellationToken);

        var added = 0;
        foreach (var held in custody.Where(c => !already.Contains(c.AssignmentId)))
        {
            var line = NewClearanceItem(tenantId, separationId, sourcedTemplate);
            ApplyCustody(line, held);
            await _unitOfWork.Repository<SeparationClearanceItem>().AddAsync(line);
            added++;
        }

        // ⚠ Only lines nobody has answered yet. Re-reading an amount onto a line somebody has
        // already signed would rewrite the evidence, which is the one thing a clearance form must
        // never do - the same rule that makes the name a snapshot.
        var repriced = 0;
        var live = await CustodyOnFormAsync(tenantId, items, cancellationToken);
        foreach (var item in items.Where(i => i.SourceAssignmentId is not null
                                              && i.Status == ClearanceItemStatus.Pending))
        {
            if (Custody(live, item) is not { } held) continue;
            if (item.OutstandingAmount == held.OutstandingSurcharge
                && item.SourceSurchargeId == held.SurchargeId) continue;

            item.SourceSurchargeId = held.SurchargeId;
            item.OutstandingAmount = held.OutstandingSurcharge;
            item.OutstandingCurrencyCode = held.SurchargeCurrencyCode;
            await _unitOfWork.Repository<SeparationClearanceItem>().UpdateAsync(item);
            repriced++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Clearance asset lines refreshed for separation {Number}: {Added} added, {Repriced} repriced",
            separation.SeparationNumber, added, repriced);

        return await GetClearanceAsync(separationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CompleteClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; only a clearance in progress can be completed.");

        var items = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId)
            .ToListAsync(cancellationToken);

        var outstanding = items.Where(i => i.IsMandatory && !IsSettled(i.Status)).ToList();

        // FR-HR-091, enforced. This is the gate the whole area turns on: entitlements are computed
        // only after the clearance form is complete, so anything still owed is still recoverable.
        if (outstanding.Count > 0)
        {
            var names = string.Join(", ", outstanding.Take(5).Select(i => i.Name));
            var more = outstanding.Count > 5 ? $" and {outstanding.Count - 5} more" : string.Empty;
            throw new InvalidOperationException(
                $"Clearance is not complete: {outstanding.Count} mandatory line(s) are still "
                + $"outstanding or blocked — {names}{more}.");
        }

        separation.Status = SeparationStatus.ClearanceCompleted;
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Clearance completed for separation {Number}; {Amount} outstanding carried to settlement",
            separation.SeparationNumber, items.Sum(i => i.OutstandingAmount ?? 0m));

        return ToDetailDto(await ReloadAsync(tenantId, separationId, cancellationToken));
    }

    /// <summary>A clearance line with a terminal answer that does not block the gate.</summary>
    private static bool IsSettled(ClearanceItemStatus status)
        => status is ClearanceItemStatus.Cleared
                  or ClearanceItemStatus.Waived
                  or ClearanceItemStatus.NotApplicable;

    private static string? ClearanceBlockedReason(
        EmployeeSeparation separation, List<SeparationClearanceItem> items, int mandatoryOutstanding)
    {
        if (items.Count == 0)
            return "Clearance has not been started for this separation.";

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            return $"This separation is {separation.Status}; clearance can only be completed while it is in progress.";

        if (mandatoryOutstanding > 0)
            return $"{mandatoryOutstanding} mandatory clearance line(s) are still outstanding or blocked.";

        return null;
    }

    private async Task RequireOrganizationUnitAsync(Guid tenantId, Guid? unitId, CancellationToken cancellationToken)
    {
        if (unitId is not { } id || id == Guid.Empty) return;

        var exists = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
            .AnyAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);

        if (!exists)
            throw new ArgumentException($"Organisation unit with ID '{id}' not found.");
    }

    private async Task<SeparationClearanceTemplate> ReloadTemplateAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
               .Include(t => t.OwningOrganizationUnit)
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken)
           ?? throw new InvalidOperationException("Clearance line saved but could not be reloaded.");

    private static SeparationClearanceTemplateDto ToTemplateDto(SeparationClearanceTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Kind = t.Kind,
        KindName = t.Kind.ToString(),
        Description = t.Description,
        OwningOrganizationUnitId = t.OwningOrganizationUnitId,
        OwningOrganizationUnitName = t.OwningOrganizationUnit?.Name,
        IsMandatory = t.IsMandatory,
        IsActive = t.IsActive,
        SourcesFromAssetRegister = t.SourcesFromAssetRegister,
        SortOrder = t.SortOrder,
        CarriesAmount = CarriesAmount(t.Kind),
    };

    /// <param name="custody">
    /// The live state of the custody behind a sourced line, where there is one. ⚠ Passing null for
    /// a line that HAS a source is not the same as a line that has none - the first renders as
    /// "the register no longer knows about this", the second as an ordinary hand-written line -
    /// which is why <c>IsFromAssetRegister</c> is taken from the item and the three custody fields
    /// from the lookup.
    /// </param>
    private static SeparationClearanceItemDto ToClearanceItemDto(
        SeparationClearanceItem i, AssetCustodyLine? custody = null) => new()
    {
        Id = i.Id,
        SeparationId = i.SeparationId,
        TemplateId = i.TemplateId,
        Name = i.Name,
        Kind = i.Kind,
        KindName = i.Kind.ToString(),
        OwningOrganizationUnitId = i.OwningOrganizationUnitId,
        OwningOrganizationUnitName = i.OwningOrganizationUnit?.Name,
        IsMandatory = i.IsMandatory,
        SortOrder = i.SortOrder,
        Status = i.Status,
        StatusName = i.Status.ToString(),
        OutstandingAmount = i.OutstandingAmount,
        OutstandingCurrencyCode = i.OutstandingCurrencyCode,
        CarriesAmount = CarriesAmount(i.Kind),
        SourceAssignmentId = i.SourceAssignmentId,
        SourceSurchargeId = i.SourceSurchargeId,
        IsFromAssetRegister = i.SourceAssignmentId is not null,
        AssetCustodyStatusName = custody?.Status.ToString(),
        AssetOutstanding = custody?.Outstanding,
        AssetStillHeld = custody?.StillHeld,
        AssetOutstandingSurcharge = custody?.OutstandingSurcharge,
        AssetHasUndecidedCharge = custody?.HasUndecidedCharge,
        Notes = i.Notes,
        SignedOffBy = i.SignedOffBy,
        RecordedById = i.RecordedById,
        RecordedByName = i.RecordedBy == null ? null : FullName(i.RecordedBy),
        RecordedOn = i.RecordedOn,
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<EmployeeSeparation> RequireAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
               .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
           ?? throw new ArgumentException($"Separation with ID '{id}' not found.");

    private async Task<EmployeeSeparation> ReloadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await Scoped(tenantId).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw new InvalidOperationException("Separation saved but could not be reloaded.");

    /// <summary>
    /// The notice that applies to a separation type, from tenant policy.
    /// </summary>
    /// <remarks>
    /// Death, summary dismissal and contract expiry carry none — and that is a real zero, not a
    /// missing value: summary dismissal means dismissal <i>without notice</i>, a contract that
    /// expires was always going to, and nobody serves notice on a bereavement.
    /// </remarks>
    private static int DefaultNoticeDays(EmployeeTerminationType type, CompanyHrPolicySettings settings)
        => type switch
        {
            EmployeeTerminationType.Death => 0,
            EmployeeTerminationType.SummaryDismissal => 0,
            EmployeeTerminationType.ContractExpiry => 0,
            EmployeeTerminationType.VoluntaryResignation => settings.DefaultResignationNoticeDays,
            _ => settings.DefaultTerminationNoticeDays,
        };

    /// <summary>
    /// The next <c>SEP-yyyy-nnnnn</c> for the tenant and year.
    /// </summary>
    /// <remarks>
    /// ⚠ Reads <b>including soft-deleted rows</b>, and that is the point. A global query filter
    /// hides deleted rows from the ordinary queryable, and the unique index is filtered on
    /// <c>IsDeleted</c> too — so a deleted number is free again on both counts. Reissuing it would
    /// give two exits the same reference in whatever letters and payment records already quote it.
    /// Numbers are not reused.
    /// </remarks>
    private async Task<string> NextNumberAsync(Guid tenantId, DateOnly on, CancellationToken cancellationToken)
    {
        var prefix = $"SEP-{on.Year:0000}-";

        var last = await _unitOfWork.Repository<EmployeeSeparation>()
            .GetQueryableIncludingDeleted(s => s.TenantId == tenantId && s.SeparationNumber.StartsWith(prefix))
            .OrderByDescending(s => s.SeparationNumber)
            .Select(s => s.SeparationNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && int.TryParse(last[prefix.Length..], out var parsed))
            next = parsed + 1;

        return $"{prefix}{next:00000}";
    }

    private static string FullName(Employee? e)
        => e == null ? string.Empty : $"{e.FirstName} {e.LastName}".Trim();

    /// <summary>
    /// Notice required, served and short — derived, never stored.
    /// </summary>
    /// <remarks>
    /// Served notice is counted from the day notice was given to the last working day. The
    /// shortfall floors at zero: working more notice than was owed is not negative notice pay, it
    /// is just a longer handover.
    /// </remarks>
    private static (int Required, int? Served, int? Shortfall) Notice(EmployeeSeparation s)
    {
        var required = s.NoticeDays ?? 0;

        if (s.NoticeGivenOn is not { } given || s.LastWorkingDay is not { } last)
            return (required, null, null);

        var served = last.DayNumber - given.DayNumber;
        if (served < 0) served = 0;

        return (required, served, Math.Max(0, required - served));
    }

    private static EmployeeSeparationDocumentDto ToDocumentDto(EmployeeSeparationDocument d) => new()
    {
        Id = d.Id,
        SeparationId = d.SeparationId,
        Category = d.Category,
        CategoryName = d.Category.ToString(),
        FileName = d.FileName,
        Description = d.Description,
        UploadedOn = d.UploadedOn,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy == null ? null : FullName(d.UploadedBy),
        IsRegisteredInDms = d.DocumentRecordId != null,
    };

    private static EmployeeSeparationListDto ToListDto(EmployeeSeparation s) => new()
    {
        Id = s.Id,
        SeparationNumber = s.SeparationNumber,
        EmployeeId = s.EmployeeId,
        EmployeeName = FullName(s.Employee),
        EmployeeNumber = s.Employee?.EmployeeNumber,
        PositionTitle = s.Employee?.Position?.Title,
        OrganizationUnitName = s.Employee?.OrganizationUnit?.Name,
        SeparationType = s.SeparationType,
        SeparationTypeName = s.SeparationType.ToString(),
        Status = s.Status,
        StatusName = s.Status.ToString(),
        InitiatedOn = s.InitiatedOn,
        LastWorkingDay = s.LastWorkingDay,
        EffectiveDate = s.EffectiveDate,
        IsProcedural = s.IsProcedural,
        IsSystemInitiated = s.IsSystemInitiated,
        IsDisciplinary = s.DisciplinaryActionId != null,
        EmployeeRecordUpdated = s.EmployeeRecordUpdatedOn != null,
    };

    private static EmployeeSeparationDetailDto ToDetailDto(EmployeeSeparation s) => new()
    {
        Id = s.Id,
        SeparationNumber = s.SeparationNumber,
        EmployeeId = s.EmployeeId,
        EmployeeName = FullName(s.Employee),
        EmployeeNumber = s.Employee?.EmployeeNumber,
        PositionTitle = s.Employee?.Position?.Title,
        OrganizationUnitName = s.Employee?.OrganizationUnit?.Name,
        SeparationType = s.SeparationType,
        SeparationTypeName = s.SeparationType.ToString(),
        Status = s.Status,
        StatusName = s.Status.ToString(),
        InitiatedOn = s.InitiatedOn,
        LastWorkingDay = s.LastWorkingDay,
        EffectiveDate = s.EffectiveDate,
        IsProcedural = s.IsProcedural,
        IsSystemInitiated = s.IsSystemInitiated,
        IsDisciplinary = s.DisciplinaryActionId != null,
        EmployeeRecordUpdated = s.EmployeeRecordUpdatedOn != null,

        ReasonCategory = s.ReasonCategory,
        ReasonCategoryName = s.ReasonCategory?.ToString(),
        ReasonNotes = s.ReasonNotes,
        MedicalBoardId = s.MedicalBoardId,
        NoticeGivenOn = s.NoticeGivenOn,
        NoticeDays = s.NoticeDays,
        NoticeRequiredDays = Notice(s).Required,
        NoticeServedDays = Notice(s).Served,
        NoticeShortfallDays = Notice(s).Shortfall,
        SubmittedOn = s.SubmittedOn,
        SubmittedById = s.SubmittedById,
        SubmittedByName = s.SubmittedBy == null ? null : FullName(s.SubmittedBy),
        InitiatedById = s.InitiatedById,
        InitiatedByName = s.InitiatedBy == null ? null : FullName(s.InitiatedBy),
        ApprovedById = s.ApprovedById,
        ApprovedByName = s.ApprovedBy == null ? null : FullName(s.ApprovedBy),
        ApprovedOn = s.ApprovedOn,
        ApprovalNotes = s.ApprovalNotes,
        WorkflowInstanceId = s.WorkflowInstanceId,
        RejectedById = s.RejectedById,
        RejectedByName = s.RejectedBy == null ? null : FullName(s.RejectedBy),
        RejectedOn = s.RejectedOn,
        RejectionReason = s.RejectionReason,
        AbsenceDays = s.AbsenceDays,
        // The mirror of IsProcedural, said the way a client needs to hear it: who do I send this to.
        RequiresManagingDirectorSignature = !s.IsProcedural,
        IsNoticeWaived = s.IsNoticeWaived,
        NoticeWaiverReason = s.NoticeWaiverReason,
        IsNoticePaidInLieu = s.IsNoticePaidInLieu,
        NoticeDecisionOn = s.NoticeDecisionOn,
        NoticeDecidedById = s.NoticeDecidedById,
        NoticeDecidedByName = s.NoticeDecidedBy == null ? null : FullName(s.NoticeDecidedBy),
        RequiresNoticeDecision = NoticeDecisionOutstanding(s),
        IsEligibleForRehire = s.IsEligibleForRehire,
        EligibleForRehireDate = s.EligibleForRehireDate,
        RehireRestrictions = s.RehireRestrictions,
        DisciplinaryActionId = s.DisciplinaryActionId,
        EmployeeRecordUpdatedOn = s.EmployeeRecordUpdatedOn,
        CancelledOn = s.CancelledOn,
        CancelledById = s.CancelledById,
        CancelledByName = s.CancelledBy == null ? null : FullName(s.CancelledBy),
        CancellationReason = s.CancellationReason,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
