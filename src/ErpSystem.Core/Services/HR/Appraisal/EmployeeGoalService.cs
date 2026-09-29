using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeGoalService> _logger;

    public EmployeeGoalService(
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<GoalProgressEntry> progressRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<EmployeeGoalAppraisalAssessment> assessmentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeGoalService> logger)
    {
        _goalRepository = goalRepository;
        _progressRepository = progressRepository;
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _appraisalRepository = appraisalRepository;
        _assessmentRepository = assessmentRepository;
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

    public async Task<IEnumerable<EmployeeGoalDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .Select(a => new { a.EmployeeId, a.AppraisalCycleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisal == null)
            return Enumerable.Empty<EmployeeGoalDto>();

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
                dto.SelfFinalProgressPercent  = a.SelfFinalProgressPercent;
                dto.SelfFinalStatus           = a.SelfFinalStatus;
                dto.SelfFinalActualValue      = a.SelfFinalActualValue;
                dto.SelfAssessmentNotes       = a.SelfAssessmentNotes;
                dto.SelfEvidenceLinks         = a.SelfEvidenceLinks;
                dto.ManagerFinalProgressPercent = a.ManagerFinalProgressPercent;
                dto.ManagerFinalStatus          = a.ManagerFinalStatus;
                dto.ManagerFinalActualValue     = a.ManagerFinalActualValue;
                dto.ManagerAssessmentNotes      = a.ManagerAssessmentNotes;
                dto.ManagerEvidenceLinks        = a.ManagerEvidenceLinks;
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

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        // The appraisal link is the server's: this employee's appraisal in this cycle, if one
        // exists yet. It used to be taken from the payload whenever one was sent, so a goal could
        // be linked to any appraisal — someone else's included (decision D-30).
        var appraisal = await _appraisalRepository.FirstOrDefaultAsync(
            a => a.TenantId == tenantId
              && a.EmployeeId == entity.EmployeeId
              && a.AppraisalCycleId == entity.AppraisalCycleId);
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

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGoalAsync(id, cancellationToken);

        if (entity.IsLocked)
            throw new InvalidOperationException("Locked goals cannot be deleted.");

        await _goalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal deleted: {Id}", id);
        return true;
    }

    // ─── Approval Workflow ───────────────────────────────────────────────────

    public async Task<EmployeeGoalDto> SubmitForApprovalAsync(Guid goalId, Guid managerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedGoalAsync(goalId, cancellationToken);

        if (entity.Status != GoalStatus.Draft)
            throw new InvalidOperationException($"Only draft goals can be submitted. Current status: {entity.Status}");

        // Guard: total weights of all non-rejected goals in the cycle must equal 100
        await ValidateGoalWeightTotalAsync(entity.EmployeeId, entity.AppraisalCycleId, cancellationToken);

        var managerExists = await _employeeRepository.ExistsAsync(e => e.Id == managerId && e.TenantId == tenantId);
        if (!managerExists)
            throw new ArgumentException("Manager not found.");

        entity.Status = GoalStatus.PendingApproval;
        entity.SubmittedToManagerId = managerId;
        entity.SubmittedDate = DateTime.UtcNow;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} submitted for approval by manager {ManagerId}", goalId, managerId);
        return await GetByIdAsync(goalId, cancellationToken);
    }

    public async Task<EmployeeGoalDto> ApproveGoalAsync(Guid goalId, Guid managerId, string? feedback = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGoalAsync(goalId, cancellationToken);

        if (entity.Status != GoalStatus.PendingApproval)
            throw new InvalidOperationException("Only goals pending approval can be approved.");

        if (entity.SubmittedToManagerId != managerId)
            throw new InvalidOperationException("You are not the assigned manager for this goal.");

        // Guard: manager cannot approve if the employee's total goal weights != 100
        await ValidateGoalWeightTotalAsync(entity.EmployeeId, entity.AppraisalCycleId, cancellationToken);

        entity.Status = GoalStatus.Approved;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.ManagerFeedback = feedback;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} approved by manager {ManagerId}", goalId, managerId);
        return await GetByIdAsync(goalId, cancellationToken);
    }

    public async Task<EmployeeGoalDto> RejectGoalAsync(Guid goalId, Guid managerId, string? feedback = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGoalAsync(goalId, cancellationToken);

        if (entity.Status != GoalStatus.PendingApproval)
            throw new InvalidOperationException("Only goals pending approval can be rejected.");

        if (entity.SubmittedToManagerId != managerId)
            throw new InvalidOperationException("You are not the assigned manager for this goal.");

        entity.Status = GoalStatus.Draft;
        entity.ManagerFeedback = feedback;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} rejected by manager {ManagerId}", goalId, managerId);
        return await GetByIdAsync(goalId, cancellationToken);
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
        dto.UpdateEntity(entity);
        await _progressRepository.UpdateAsync(entity);

        // The goal reflects its most recent entry, so correcting one has to be carried back —
        // otherwise fixing a mistyped 40% to 90% left the goal reporting 40% forever. Only the
        // latest entry counts, so editing an older one changes nothing, which is right.
        var latest = await _progressRepository
            .GetQueryable(p => p.EmployeeGoalId == goalId && p.TenantId == tenantId && !p.IsDeleted)
            .OrderByDescending(p => p.EntryDate)
            .FirstOrDefaultAsync(cancellationToken);

        // A locked goal moves too: the lock freezes what it is, not its year (D-29).
        if (latest != null && latest.Id == entity.Id)
            ApplyProgressToGoal(goal, entity.ProgressPercent, entity.Status);

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

        await _progressRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress entry deleted: {EntryId}", entryId);
        return true;
    }

    // ─── Lock Management ─────────────────────────────────────────────────────

    public async Task<bool> LockGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGoalAsync(goalId, cancellationToken);

        entity.IsLocked = true;
        entity.LockedDate = DateTime.UtcNow;
        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} locked", goalId);
        return true;
    }

    public async Task<bool> UnlockGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGoalAsync(goalId, cancellationToken);

        entity.IsLocked = false;
        entity.LockedDate = null;

        // The old lock also set the Locked status and this left it behind, so an unlocked goal
        // still read as locked to the goal-setting gate and could not be locked again. It goes
        // back to Approved — the status every lock was taken from reads as that (D-29).
        if (entity.Status == GoalStatus.Locked)
            entity.Status = GoalStatus.Approved;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            AtRiskGoals = goals.Count(g => g.Status == GoalStatus.AtRisk),
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
                AtRiskGoals = empGoals.Count(g => g.Status == GoalStatus.AtRisk),
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

    // ─── Private Validation Helpers ───────────────────────────────────────

    /// <summary>
    /// Validates that the sum of all non-rejected goal weights for an employee in a
    /// given cycle equals 100. Called before Submit and Approve to enforce the rule
    /// that goal weights must be properly distributed before progressing.
    /// </summary>
    private async Task ValidateGoalWeightTotalAsync(
        Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var totalWeight = await _goalRepository
            .GetQueryable(g => g.TenantId == tenantId
                            && g.EmployeeId == employeeId
                            && g.AppraisalCycleId == cycleId
                            && g.Status != GoalStatus.Rejected)
            .SumAsync(g => (int?)g.Weight, cancellationToken) ?? 0;

        if (totalWeight != 100)
            throw new InvalidOperationException(
                $"Goal weights for this employee in the current cycle must sum to exactly 100 " +
                $"(current total: {totalWeight}). " +
                $"Adjust goal weights across all goals in this cycle before proceeding.");
    }
}
