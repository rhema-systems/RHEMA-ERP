using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmployeeGoalService : IEmployeeGoalService
{
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<GoalProgressEntry> _progressRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<EmployeeGoalAppraisalAssessment> _assessmentRepository;
    private readonly IGenericRepository<CompanyGoal> _companyGoalRepository;
    private readonly IGenericRepository<UnitGoal> _unitGoalRepository;
    private readonly IGenericRepository<GoalLibrary> _libraryRepository;
    private readonly IGenericRepository<KpiDefinition> _kpiRepository;
    private readonly IGenericRepository<AppraisalReviewEvent> _reviewEventRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppraisalGoalRowService _goalRows;
    private readonly IGoalRiskSettingsProvider _riskSettingsProvider;
    private readonly IGoalRiskEvaluator _riskEvaluator;
    private readonly ILogger<EmployeeGoalService> _logger;

    public EmployeeGoalService(
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<GoalProgressEntry> progressRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<EmployeeGoalAppraisalAssessment> assessmentRepository,
        IGenericRepository<CompanyGoal> companyGoalRepository,
        IGenericRepository<UnitGoal> unitGoalRepository,
        IGenericRepository<GoalLibrary> libraryRepository,
        IGenericRepository<KpiDefinition> kpiRepository,
        IGenericRepository<AppraisalReviewEvent> reviewEventRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IAppraisalGoalRowService goalRows,
        IGoalRiskSettingsProvider riskSettingsProvider,
        IGoalRiskEvaluator riskEvaluator,
        ILogger<EmployeeGoalService> logger)
    {
        _goalRepository = goalRepository;
        _progressRepository = progressRepository;
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _appraisalRepository = appraisalRepository;
        _assessmentRepository = assessmentRepository;
        _companyGoalRepository = companyGoalRepository;
        _unitGoalRepository = unitGoalRepository;
        _libraryRepository = libraryRepository;
        _kpiRepository = kpiRepository;
        _reviewEventRepository = reviewEventRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _goalRows = goalRows;
        _riskSettingsProvider = riskSettingsProvider;
        _riskEvaluator = riskEvaluator;
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

    // A goal owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<EmployeeGoal> GetOwnedGoalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee goal with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<EmployeeGoal> BaseQuery()
    {
        var tenantId = GetTenantId();
        return _goalRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId)
            .Include(g => g.Employee)
            .Include(g => g.AppraisalCycle)
            .Include(g => g.ParentCompanyGoal)
            .Include(g => g.ParentUnitGoal)
            .Include(g => g.ParentGoal)
            .Include(g => g.LibraryItem)
            .Include(g => g.KpiDefinition)
            .Include(g => g.Manager);
    }

    /// <summary>
    /// Goals are set and moved while their cycle is Open (performance closure E-d2b, D-59): a Draft cycle has not
    /// begun — its open tells staff their goals are due — and a Closed one's goals are the year's record.
    /// </summary>
    private Task EnsureCycleOpenAsync(Guid cycleId, string action, CancellationToken cancellationToken)
        => AppraisalLiveCycle.EnsureCycleOpenAsync(_cycleRepository.GetQueryable(), GetTenantId(), cycleId, action, cancellationToken);

    /// <summary>Loads the AppraisalSettings for a cycle (1:1), or null if none is configured.</summary>
    private async Task<AppraisalSettings?> GetSettingsForCycleAsync(Guid cycleId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return (await _cycleRepository.GetQueryable(c => c.Id == cycleId && c.TenantId == tenantId)
            .Include(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(cancellationToken))?.AppraisalSettings;
    }

    // ─── CRUD ────────────────────────────────────────────────────────────────

    public async Task<EmployeeGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeGoalDto>> GetByEmployeeIdAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Where(g => g.EmployeeId == employeeId);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderBy(g => g.Status).ThenBy(g => g.DueDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<EmployeeGoalDto>> GetByAppraisalIdAsync(
        Guid appraisalId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .Select(a => new { a.EmployeeId, a.AppraisalCycleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisal == null)
            return Enumerable.Empty<EmployeeGoalDto>();

        // B2: the goal assessments carry both sides of the evaluation, and this read handed both to
        // anyone it admitted — the manager's side to the appraisee before HR's sign-off, and the
        // employee's side to the manager while it was a draft, whatever the profile's switches said.
        // Each side now follows the rule every read of an evaluation shares.
        var visibility = await AppraisalVisibility.LoadAsync(
            _appraisalRepository.GetQueryable().Where(a => a.TenantId == tenantId), appraisalId, cancellationToken);
        if (visibility == null)
            return Enumerable.Empty<EmployeeGoalDto>();
        var view = AppraisalVisibility.For(visibility, viewerEmployeeId);

        var entities = await BaseQuery()
            .Where(g => g.EmployeeId == appraisal.EmployeeId
                     && g.AppraisalCycleId == appraisal.AppraisalCycleId)
            .OrderBy(g => g.DueDate)
            .ToListAsync(cancellationToken);

        var dtos = entities.ToDtoList();

        // Merge in the persisted appraisal-scoped assessments
        var assessments = await _assessmentRepository.GetQueryable()
            .Where(a => a.PerformanceAppraisalId == appraisalId && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (assessments.Count > 0)
        {
            var assessmentMap = assessments.ToDictionary(a => a.EmployeeGoalId);
            foreach (var dto in dtos)
            {
                if (!assessmentMap.TryGetValue(dto.Id, out var a)) continue;
                if (view.SelfEntries)
                {
                    dto.SelfFinalProgressPercent  = a.SelfFinalProgressPercent;
                    dto.SelfFinalStatus           = a.SelfFinalStatus;
                    dto.SelfFinalActualValue      = a.SelfFinalActualValue;
                    dto.SelfAssessmentNotes       = a.SelfAssessmentNotes;
                    dto.SelfEvidenceLinks         = a.SelfEvidenceLinks;
                }
                if (view.ManagerScores)
                {
                    dto.ManagerFinalProgressPercent = a.ManagerFinalProgressPercent;
                    dto.ManagerFinalStatus          = a.ManagerFinalStatus;
                    dto.ManagerFinalActualValue     = a.ManagerFinalActualValue;
                    dto.ManagerAssessmentNotes      = a.ManagerAssessmentNotes;
                    dto.ManagerEvidenceLinks        = a.ManagerEvidenceLinks;
                }
            }
        }

        return dtos;
    }

    public async Task<IEnumerable<EmployeeGoalDto>> GetPendingApprovalAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Where(g => g.SubmittedToManagerId == managerId && g.Status == GoalStatus.PendingApproval);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderBy(g => g.SubmittedDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<EmployeeGoalDto>> GetPagedAsync(
        int pageNumber, int pageSize,
        Guid? employeeId = null, Guid? cycleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().AsQueryable();
        if (employeeId.HasValue)
            query = query.Where(g => g.EmployeeId == employeeId.Value);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        query = query.OrderBy(g => g.EmployeeId).ThenBy(g => g.DueDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<EmployeeGoalDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<EmployeeGoalDto> CreateAsync(CreateEmployeeGoalDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var employeeExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.EmployeeId && e.TenantId == tenantId);
        if (!employeeExists)
            throw new ArgumentException("Employee not found.");

        var cycleExists = await _cycleRepository.ExistsAsync(c => c.Id == createDto.AppraisalCycleId && c.TenantId == tenantId);
        if (!cycleExists)
            throw new ArgumentException("Appraisal cycle not found.");

        await EnsureCycleOpenAsync(createDto.AppraisalCycleId, "The goal cannot be set", cancellationToken);

        // Enforce the configured per-cycle goal ceiling (AppraisalSettings.MaxGoalsPerEmployee).
        var settings = await GetSettingsForCycleAsync(createDto.AppraisalCycleId, cancellationToken);
        if (settings?.MaxGoalsPerEmployee is int maxGoals && maxGoals > 0)
        {
            var existingGoalCount = await _goalRepository.GetQueryable(g =>
                    g.TenantId == tenantId
                    && g.EmployeeId == createDto.EmployeeId
                    && g.AppraisalCycleId == createDto.AppraisalCycleId
                    && g.Status != GoalStatus.Rejected)
                .CountAsync(cancellationToken);

            if (existingGoalCount >= maxGoals)
                throw new InvalidOperationException($"This employee already has the maximum of {maxGoals} goal(s) allowed for this cycle.");
        }

        await EnsureLinksBelongAsync(
            createDto.EmployeeId, createDto.AppraisalCycleId, goalId: null, existing: null,
            createDto.CompanyGoalId, createDto.UnitGoalId, createDto.ParentGoalId,
            createDto.GoalLibraryId, createDto.KpiDefinitionId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        // The appraisal link is the server's: this employee's appraisal in this cycle, if one
        // exists yet. It used to be taken from the payload whenever one was sent, so a goal could
        // be linked to any appraisal — someone else's included (decision D-30). Not a withdrawn
        // one (performance closure E-d1): it takes no more goals.
        var appraisal = await _appraisalRepository.FirstOrDefaultAsync(
            a => a.TenantId == tenantId
              && a.EmployeeId == entity.EmployeeId
              && a.AppraisalCycleId == entity.AppraisalCycleId
              && a.Status != AppraisalStatus.Withdrawn);
        entity.PerformanceAppraisalId = appraisal?.Id;

        await _goalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal created: {Id} for employee {EmployeeId}", entity.Id, entity.EmployeeId);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<EmployeeGoalDto> UpdateAsync(UpdateEmployeeGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGoalAsync(updateDto.Id, cancellationToken);

        if (GoalSetRules.IsLocked(entity.IsLocked, entity.Status))
            throw new InvalidOperationException("This goal is locked and cannot be edited.");

        await EnsureCycleOpenAsync(entity.AppraisalCycleId, "The goal cannot be edited", cancellationToken);

        // Decision D-30. A goal's owner and cycle are fixed when it is created — an edit could
        // move a goal into a colleague's set — and its appraisal link is the server's, so the
        // mapper no longer copies any of the three.
        if (updateDto.EmployeeId != entity.EmployeeId || updateDto.AppraisalCycleId != entity.AppraisalCycleId)
            throw new InvalidOperationException(
                "A goal's owner and cycle cannot be changed. Create a new goal for the other employee or cycle.");

        // Once the manager has agreed a goal, what it measures is part of the agreement: changing
        // it would change the score without the manager. The manager sends it back instead.
        if (GoalSetRules.IsAgreed(entity.Status) && ChangesWhatItMeasures(entity, updateDto))
            throw new InvalidOperationException(
                "This goal has been approved, so what it measures — title, measure, target, weight, period "
                + "and success criteria — cannot be changed. Ask your manager to send it back to you for changes.");

        await EnsureLinksBelongAsync(
            entity.EmployeeId, entity.AppraisalCycleId, entity.Id, entity,
            updateDto.CompanyGoalId, updateDto.UnitGoalId, updateDto.ParentGoalId,
            libraryId: null, updateDto.KpiDefinitionId, cancellationToken);

        updateDto.UpdateEntity(entity);
        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    /// <summary>
    /// Decision D-30: whether an edit changes what the goal measures — the part of an agreed goal
    /// only the manager can reopen. Description, priority, dates and alignment stay editable. Text is
    /// compared trimmed, with a blank read as none, because the two edit forms send it differently.
    /// </summary>
    private static bool ChangesWhatItMeasures(EmployeeGoal goal, UpdateEmployeeGoalDto dto) =>
           !SameText(goal.Title, dto.Title)
        || goal.KpiDefinitionId != dto.KpiDefinitionId
        || goal.MeasurementType != dto.MeasurementType
        || goal.TargetValue != dto.TargetValue
        || goal.MinValue != dto.MinValue
        || goal.MaxValue != dto.MaxValue
        || !SameText(goal.Unit, dto.Unit)
        || goal.Weight != dto.Weight
        || goal.Period != dto.Period
        || !SameText(goal.SuccessCriteria, dto.SuccessCriteria);

    private static bool SameText(string? a, string? b) => string.Equals(
        string.IsNullOrWhiteSpace(a) ? null : a.Trim(),
        string.IsNullOrWhiteSpace(b) ? null : b.Trim(),
        StringComparison.Ordinal);

    /// <summary>
    /// Decision D-72: a goal goes before it is agreed — a draft, one waiting for the manager, or one sent
    /// back. An agreed goal is part of the set the manager accepted (its weight is in the total the
    /// edit rule protects), so the manager sends it back first. The owner could delete an agreed goal,
    /// even a completed one, and the delete left its progress entries behind; they go with it now.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedGoalAsync(id, cancellationToken);

        if (GoalSetRules.IsLocked(entity.IsLocked, entity.Status))
            throw new InvalidOperationException("Locked goals cannot be deleted.");

        if (GoalSetRules.IsAgreed(entity.Status))
            throw new InvalidOperationException(
                "This goal has been agreed with the manager, so it cannot be deleted. "
                + "Ask your manager to send it back first; a goal sent back can be removed.");

        await EnsureCycleOpenAsync(entity.AppraisalCycleId, "The goal cannot be removed", cancellationToken);

        var entries = await _progressRepository
            .GetQueryable(p => p.EmployeeGoalId == id && p.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
            await _progressRepository.DeleteAsync(entry);

        await _goalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal deleted: {Id}, with {Entries} progress entries", id, entries.Count);
        return true;
    }

    /// <summary>
    /// Decision D-76: a goal's links are rows of its own tenant and year — the company and unit goal of
    /// its cycle, a parent among the same employee's goals in that cycle (never the goal itself or one of
    /// its own descendants), and a library item and KPI of the tenant. Each id was saved as sent: one
    /// that did not exist failed the foreign key as a 500, and another tenant's was stored and its titles
    /// shown. On an edit only a link that changes is checked, so an existing link is never refused.
    /// Refused as a rule (422): the caller can correct the choice.
    /// </summary>
    private async Task EnsureLinksBelongAsync(
        Guid employeeId, Guid cycleId, Guid? goalId, EmployeeGoal? existing,
        Guid? companyGoalId, Guid? unitGoalId, Guid? parentGoalId, Guid? libraryId, Guid? kpiId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        if (companyGoalId is Guid companyId && companyId != existing?.CompanyGoalId
            && !await _companyGoalRepository.ExistsAsync(c => c.Id == companyId && c.TenantId == tenantId && c.AppraisalCycleId == cycleId))
            throw new InvalidOperationException("The company goal chosen is not one of this cycle's company goals.");

        if (unitGoalId is Guid unitId && unitId != existing?.UnitGoalId
            && !await _unitGoalRepository.ExistsAsync(u => u.Id == unitId && u.TenantId == tenantId && u.AppraisalCycleId == cycleId))
            throw new InvalidOperationException("The unit goal chosen is not one of this cycle's unit goals.");

        if (parentGoalId is Guid parentId && parentId != existing?.ParentGoalId)
        {
            if (parentId == goalId)
                throw new InvalidOperationException("A goal cannot be its own parent.");

            if (!await _goalRepository.ExistsAsync(g => g.Id == parentId && g.TenantId == tenantId
                    && g.EmployeeId == employeeId && g.AppraisalCycleId == cycleId))
                throw new InvalidOperationException("The parent goal chosen is not one of this employee's goals in this cycle.");

            // Walk up from the new parent: reaching this goal would make a loop.
            if (goalId is Guid self)
            {
                Guid? step = parentId;
                for (var depth = 0; step is Guid current && depth < 50; depth++)
                {
                    step = await _goalRepository.GetQueryable(g => g.Id == current && g.TenantId == tenantId)
                        .Select(g => g.ParentGoalId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (step == self)
                        throw new InvalidOperationException("The parent goal chosen sits under this goal, so it cannot be its parent.");
                }
            }
        }

        if (libraryId is Guid library && library != existing?.GoalLibraryId
            && !await _libraryRepository.ExistsAsync(l => l.Id == library && l.TenantId == tenantId))
            throw new InvalidOperationException("The goal library item chosen was not found.");

        if (kpiId is Guid kpi && kpi != existing?.KpiDefinitionId
            && !await _kpiRepository.ExistsAsync(k => k.Id == kpi && k.TenantId == tenantId))
            throw new InvalidOperationException("The KPI chosen was not found.");
    }

    // ─── Progress Tracking ───────────────────────────────────────────────────
    //
    // Progress tracking owns the goal's EXECUTION status (InProgress, OnTrack, AtRisk,
    // Completed); GoalWorkflowCommandService owns the approval lifecycle and says so in its
    // own header. That split was documented but never implemented: a progress entry carried a
    // GoalProgressStatus that was stored on the entry and dropped on the floor, so nothing in
    // the codebase ever wrote AtRisk or OnTrack, and InProgress was only reachable from AtRisk
    // — i.e. never. Every at-risk read therefore returned an empty list for every tenant: the
    // manager's At Risk tab, the org-wide report, and the risk severity scores.
    //
    // (The only thing that ever set those statuses was a plain PUT carrying `status`, which
    // was also how a goal could be self-approved. Closing that hole is what made the gap
    // visible; ApplyProgressToGoal is the legitimate channel it should always have had.)

    /// <summary>
    /// Statuses a goal may receive progress entries in — approved and still running. The lock
    /// flag does not stop a goal's year (decision D-29), and a goal the old lock left in the Locked
    /// status reads as approved; its next entry gives it a running status again.
    /// </summary>
    private static readonly HashSet<GoalStatus> LiveExecutionStatuses = new()
    {
        GoalStatus.Approved,
        GoalStatus.InProgress,
        GoalStatus.OnTrack,
        GoalStatus.AtRisk,
        GoalStatus.Locked,
    };

    /// <summary>
    /// Carries an entry's percent and reported status onto the goal.
    ///
    /// Reaching 100% completes the goal whatever the entry claims — the number is the fact.
    /// NotStarted and Cancelled leave the status alone: the first says nothing has happened
    /// yet, and the second has no GoalStatus equivalent (cancelling is a workflow decision,
    /// not something a progress note should trigger).
    /// </summary>
    private static void ApplyProgressToGoal(EmployeeGoal goal, decimal? progressPercent, GoalProgressStatus entryStatus)
    {
        if (progressPercent.HasValue)
            goal.ProgressPercent = progressPercent.Value;

        if (progressPercent >= 100)
        {
            goal.Status = GoalStatus.Completed;
            return;
        }

        goal.Status = entryStatus switch
        {
            GoalProgressStatus.InProgress => GoalStatus.InProgress,
            GoalProgressStatus.OnTrack    => GoalStatus.OnTrack,
            GoalProgressStatus.AtRisk     => GoalStatus.AtRisk,
            GoalProgressStatus.Completed  => GoalStatus.Completed,
            _                             => goal.Status,
        };
    }

    public async Task<GoalProgressEntryDto> AddProgressEntryAsync(Guid goalId, CreateGoalProgressEntryDto dto, Guid recordedById, CancellationToken cancellationToken = default)
    {
        // GoalProgressEntry.RecordedById is a required Employee FK.
        if (recordedById == Guid.Empty)
            throw new InvalidOperationException("Unable to determine the recording employee. Please ensure your account is linked to an employee record.");

        var tenantId = GetTenantId();
        var goal = await GetOwnedGoalAsync(goalId, cancellationToken);

        if (!LiveExecutionStatuses.Contains(goal.Status))
            throw new InvalidOperationException("Progress entries can only be added to approved, on-track, in-progress, or at-risk goals.");

        await EnsureCycleOpenAsync(goal.AppraisalCycleId, "Progress cannot be recorded on this goal", cancellationToken);

        // Decision D-76: an entry logged at a review event is logged at one of this goal's appraisal's
        // events. The id was saved as sent — another appraisal's event, or another tenant's.
        if (dto.ReviewEventId is Guid reviewEventId
            && (goal.PerformanceAppraisalId is not Guid appraisalId
                || !await _reviewEventRepository.ExistsAsync(r => r.Id == reviewEventId
                        && r.TenantId == tenantId && r.PerformanceAppraisalId == appraisalId)))
            throw new InvalidOperationException("The review event is not one of this goal's appraisal's review events.");

        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.EmployeeGoalId = goalId;
        entity.EntryDate = DateTime.UtcNow;
        // An omitted status binds as 0 — not a member of GoalProgressStatus (it starts at 1) —
        // and was stored as-is, then serialized as a bare number and fed to ApplyProgressToGoal.
        // Derive from the reported progress instead; an explicit status is respected.
        if (entity.Status == default)
        {
            entity.Status = entity.ProgressPercent >= 100 ? GoalProgressStatus.Completed
                : entity.ProgressPercent > 0 ? GoalProgressStatus.InProgress
                : GoalProgressStatus.NotStarted;
        }
        // Attribution comes from the token, not the payload: a progress entry is a claim about what
        // someone did, and the body used to be free to name anyone.
        entity.RecordedById = recordedById;

        await _progressRepository.AddAsync(entity);

        ApplyProgressToGoal(goal, entity.ProgressPercent, entity.Status);

        await _goalRepository.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _progressRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.RecordedBy)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Progress entry added to goal {GoalId}: {EntryId}", goalId, entity!.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<GoalProgressEntryDto>> GetProgressEntriesAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await GetOwnedGoalAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();
        var entities = await _progressRepository.GetQueryable(p => p.EmployeeGoalId == goalId && p.TenantId == tenantId)
            .Include(p => p.RecordedBy)
            .OrderByDescending(p => p.EntryDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    /// <summary>
    /// Performance closure P8: a progress entry is a claim about what someone did, so it is
    /// corrected or withdrawn by the person who recorded it — or by the HR desk when the desk is not
    /// the goal's owner (the two-actor rule). Either party to the goal could rewrite or delete the
    /// other's entries.
    /// </summary>
    private static void EnsureMayAmendProgressEntry(GoalProgressEntry entry, EmployeeGoal goal, Guid? actorEmployeeId, bool actorIsDesk)
    {
        if (actorEmployeeId is Guid me && me == entry.RecordedById) return;
        if (actorIsDesk && !(actorEmployeeId is Guid owner && owner == goal.EmployeeId)) return;
        throw new UnauthorizedAccessException("Only the person who recorded this progress entry, or HR, can change it.");
    }

    /// <summary>
    /// Decision D-72: entries are corrected on an agreed goal only. A goal sent back keeps its entries,
    /// and correcting the latest one carried its status onto the goal — so a goal the owner had changed
    /// while it was back with them could be made to read agreed again with no manager involved.
    /// </summary>
    private static void EnsureGoalAgreedForEntryChange(EmployeeGoal goal, string verb)
    {
        if (!GoalSetRules.IsAgreed(goal.Status))
            throw new InvalidOperationException(
                $"This goal is not agreed with the manager at the moment — it is a draft, waiting for approval or sent back — "
                + $"so its progress entries cannot be {verb} until the manager approves it again.");
    }

    private async Task<List<GoalProgressEntry>> LiveEntriesNewestFirstAsync(Guid goalId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        return await _progressRepository
            .GetQueryable(p => p.EmployeeGoalId == goalId && p.TenantId == tenantId && !p.IsDeleted)
            .OrderByDescending(p => p.EntryDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Whether the goal still shows this entry's percent. A check-in also moves a goal's progress, with
    /// no entry of its own, so a goal moved by a later check-in no longer reflects its latest entry and
    /// an amendment to that entry leaves the goal alone.
    /// </summary>
    private static bool GoalReflects(EmployeeGoal goal, GoalProgressEntry entry) =>
        entry.ProgressPercent is not decimal percent || goal.ProgressPercent == percent;

    /// <summary>
    /// Re-derives the goal from its remaining entries, newest first: the latest recorded percent, and
    /// the status the latest entry reports, from the agreed base — Approved with nothing recorded,
    /// Completed at 100 % (ApplyProgressToGoal's rules, without the status it is replacing).
    /// </summary>
    private static void CarryBackToGoal(EmployeeGoal goal, IReadOnlyList<GoalProgressEntry> entriesNewestFirst)
    {
        var percent = entriesNewestFirst.FirstOrDefault(e => e.ProgressPercent.HasValue)?.ProgressPercent ?? 0m;
        goal.Status = GoalSetRules.RunningStatusFromProgress(percent, hasEntries: false);
        ApplyProgressToGoal(goal, percent, entriesNewestFirst.FirstOrDefault()?.Status ?? GoalProgressStatus.NotStarted);
    }

    public async Task<GoalProgressEntryDto> UpdateProgressEntryAsync(Guid goalId, UpdateGoalProgressEntryDto dto, Guid? actorEmployeeId, bool actorIsDesk, CancellationToken cancellationToken = default)
    {
        var goal = await GetOwnedGoalAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();
        var entity = await _progressRepository.GetQueryable()
            .Include(p => p.RecordedBy)
            .FirstOrDefaultAsync(p => p.Id == dto.Id && p.EmployeeGoalId == goalId && p.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Progress entry not found.");

        EnsureMayAmendProgressEntry(entity, goal, actorEmployeeId, actorIsDesk);
        EnsureGoalAgreedForEntryChange(goal, "changed");
        await EnsureCycleOpenAsync(goal.AppraisalCycleId, "The progress entry cannot be changed", cancellationToken);

        var entries = await LiveEntriesNewestFirstAsync(goalId, cancellationToken);
        var wasReflected = entries.Count > 0 && entries[0].Id == entity.Id && GoalReflects(goal, entity);

        dto.UpdateEntity(entity);
        await _progressRepository.UpdateAsync(entity);

        // The goal reflects its most recent entry, so correcting one has to be carried back —
        // otherwise fixing a mistyped 40% to 90% left the goal reporting 40% forever. Only the
        // latest entry counts, so editing an older one changes nothing, which is right. A locked
        // goal moves too: the lock freezes what it is, not its year (D-29).
        if (wasReflected)
            CarryBackToGoal(goal, entries);

        await _goalRepository.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress entry updated: {EntryId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteProgressEntryAsync(Guid goalId, Guid entryId, Guid? actorEmployeeId, bool actorIsDesk, CancellationToken cancellationToken = default)
    {
        var goal = await GetOwnedGoalAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();
        var entity = await _progressRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == entryId && p.EmployeeGoalId == goalId && p.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Progress entry not found.");

        EnsureMayAmendProgressEntry(entity, goal, actorEmployeeId, actorIsDesk);
        EnsureGoalAgreedForEntryChange(goal, "removed");
        await EnsureCycleOpenAsync(goal.AppraisalCycleId, "The progress entry cannot be removed", cancellationToken);

        var entries = await LiveEntriesNewestFirstAsync(goalId, cancellationToken);
        var wasReflected = entries.Count > 0 && entries[0].Id == entity.Id && GoalReflects(goal, entity);

        await _progressRepository.DeleteAsync(entity);

        // Decision D-72: removing the entry the goal reflects carries the one before it back, as a
        // correction does — the goal kept reporting the removed entry's percent and status.
        if (wasReflected)
        {
            CarryBackToGoal(goal, entries.Where(e => e.Id != entity.Id).ToList());
            await _goalRepository.UpdateAsync(goal);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress entry deleted: {EntryId}", entryId);
        return true;
    }

    // ─── Unlock ──────────────────────────────────────────────────────────────
    //
    // The lock is GoalWorkflowCommandService's (the direct manager's, with its rules); this service's
    // own LockGoalAsync had no caller and checked nothing, and went in performance closure E-f.

    public async Task<bool> UnlockGoalAsync(Guid goalId, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedGoalAsync(goalId, cancellationToken);

        // Decision D-72, the two-actor rule: the lock is the manager's hold on what the goal measures,
        // so its subject never lifts it — an HR officer included, whose desk permission let them unlock
        // their own goal. A caller with no employee record is nobody's subject.
        if (actorEmployeeId is Guid me && me == entity.EmployeeId)
            throw new UnauthorizedAccessException("You cannot unlock your own goal. Your manager or HR can.");

        // Once the goal's row in the appraisal has been scored, what it measures is part of an
        // evaluation (closure plan L2): unlocking it would let the goal change under the score.
        if (await _goalRows.IsScoredAsync(goalId, cancellationToken))
            throw new InvalidOperationException(
                "This goal has been scored in its appraisal, so it cannot be unlocked.");

        await EnsureCycleOpenAsync(entity.AppraisalCycleId, "The goal cannot be unlocked", cancellationToken);

        entity.IsLocked = false;
        entity.LockedDate = null;

        // Decision D-72: the goal resumes the status its progress gives it. The old lock set the Locked
        // status (and migration batch 1 stamped it on every goal locked at the time), and unlock always
        // turned that into Approved — a goal at 60 % read as untouched, a completed one as approved. An
        // Approved goal with progress (the demo seeder's) is put right the same way; a running status
        // is the goal's own and stays.
        if (entity.Status is GoalStatus.Locked or GoalStatus.Approved)
        {
            var hasEntries = await _progressRepository.ExistsAsync(p => p.EmployeeGoalId == goalId && p.TenantId == tenantId);
            entity.Status = GoalSetRules.RunningStatusFromProgress(entity.ProgressPercent, hasEntries);
        }

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The unlocked goal leaves the appraisal's goals section (L2).
        await _goalRows.RebuildAsync(entity.EmployeeId, entity.AppraisalCycleId, cancellationToken);

        _logger.LogInformation("Goal {GoalId} unlocked", goalId);
        return true;
    }

    // ─── Summaries ───────────────────────────────────────────────────────────

    public async Task<EmployeeGoalSummaryDto> GetGoalSummaryAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await _employeeRepository.GetQueryable()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId, cancellationToken);

        if (employee == null)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var cycle = await _cycleRepository.GetQueryable()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.TenantId == tenantId, cancellationToken);
        if (cycle == null)
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");

        var goals = await _goalRepository.GetQueryable(g =>
                g.TenantId == tenantId && g.EmployeeId == employeeId && g.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Configured minimum (defaults to 1 when unset) — drives MeetsMinGoalCount.
        var settings = await GetSettingsForCycleAsync(cycleId, cancellationToken);
        var minGoals = settings?.MinGoalsPerEmployee is int m && m > 0 ? m : 1;
        var nonRejectedCount = goals.Count(g => g.Status != GoalStatus.Rejected);

        return new EmployeeGoalSummaryDto
        {
            EmployeeId = employeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            EmployeeNumber = employee.EmployeeNumber,
            CycleId = cycleId,
            CycleName = cycle.CycleName,
            TotalGoals = goals.Count,
            DraftGoals = goals.Count(g => g.Status == GoalStatus.Draft),
            PendingApprovalGoals = goals.Count(g => g.Status == GoalStatus.PendingApproval),
            ApprovedGoals = goals.Count(g => g.Status == GoalStatus.Approved || g.Status == GoalStatus.AtRisk || g.Status == GoalStatus.InProgress),
            InProgressGoals = goals.Count(g => g.Status == GoalStatus.InProgress && g.ProgressPercent > 0 && g.ProgressPercent < 100),
            CompletedGoals = goals.Count(g => g.Status == GoalStatus.Completed),
            AtRiskGoals = await CountAtRiskAsync(goals, cancellationToken),
            OverallProgressPercent = goals.Any() ? goals.Average(g => g.ProgressPercent) : 0,
            GoalSettingComplete = goals.Any(g => g.Status == GoalStatus.Approved || g.Status == GoalStatus.InProgress),
            MeetsMinGoalCount = nonRejectedCount >= minGoals  // AppraisalSettings.MinGoalsPerEmployee (defaults to 1)
        };
    }

    public async Task<IEnumerable<TeamGoalSummaryDto>> GetTeamGoalSummaryAsync(Guid managerId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Get all direct reports' employee IDs from goals submitted to this manager
        var teamEmployeeIds = await _employeeRepository.GetQueryable(e => e.ManagerId == managerId && e.TenantId == tenantId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (!teamEmployeeIds.Any())
            return Enumerable.Empty<TeamGoalSummaryDto>();

        var allGoals = await _goalRepository.GetQueryable(
            g => g.TenantId == tenantId && teamEmployeeIds.Contains(g.EmployeeId) && g.AppraisalCycleId == cycleId)
            .Include(g => g.Employee)
                .ThenInclude(e => e.Position)
            .Include(g => g.Employee)
                .ThenInclude(e => e.Department)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<TeamGoalSummaryDto>();

        foreach (var empId in teamEmployeeIds)
        {
            var empGoals = allGoals.Where(g => g.EmployeeId == empId).ToList();
            var emp = empGoals.FirstOrDefault()?.Employee;

            if (emp == null)
            {
                emp = await _employeeRepository.GetQueryable()
                    .Include(e => e.Position)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == empId && e.TenantId == tenantId, cancellationToken);
            }

            if (emp == null) continue;

            result.Add(new TeamGoalSummaryDto
            {
                EmployeeId = empId,
                EmployeeName = $"{emp.FirstName} {emp.LastName}",
                EmployeeNumber = emp.EmployeeNumber,
                PositionName = emp.Position?.Title,
                DepartmentName = emp.Department?.Name,
                TotalGoals = empGoals.Count,
                ApprovedGoals = empGoals.Count(g => g.Status == GoalStatus.Approved || g.Status == GoalStatus.InProgress || g.Status == GoalStatus.Completed),
                PendingApprovalGoals = empGoals.Count(g => g.Status == GoalStatus.PendingApproval),
                AtRiskGoals = await CountAtRiskAsync(empGoals, cancellationToken),
                OverallProgressPercent = empGoals.Any() ? empGoals.Average(g => g.ProgressPercent) : 0,
                HasOverdueGoals = empGoals.Any(g => g.DueDate < today && g.Status != GoalStatus.Completed),
                EarliestOverdueDueDate = empGoals
                    .Where(g => g.DueDate < today && g.Status != GoalStatus.Completed)
                    .OrderBy(g => g.DueDate)
                    .Select(g => g.DueDate)
                    .Cast<DateOnly?>()
                    .FirstOrDefault()
            });
        }

        return result.OrderBy(r => r.EmployeeName);
    }

    /// <summary>
    /// Counts the goals the at-risk rules flag (performance closure D-71) — the evaluator the at-risk
    /// lists run, against the tenant's thresholds — so a tile agrees with the list it summarises. The
    /// AtRisk status alone counted only goals someone had marked at risk by hand.
    /// </summary>
    private async Task<int> CountAtRiskAsync(IEnumerable<EmployeeGoal> goals, CancellationToken cancellationToken)
    {
        var watched = goals.Where(g => Array.IndexOf(GoalSetRules.RiskWatched, g.Status) >= 0).ToList();
        if (watched.Count == 0) return 0;

        var settings = await _riskSettingsProvider.GetActiveAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;
        return watched.Count(g => _riskEvaluator.Evaluate(g, settings, utcNow).IsAtRisk);
    }
}
