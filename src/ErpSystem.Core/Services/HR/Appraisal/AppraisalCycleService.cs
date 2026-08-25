using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Appraisal Cycle

public class AppraisalCycleService : IAppraisalCycleService
{
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<AppraisalCycleTarget> _targetRepository;
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<OrganizationUnit> _organizationUnitRepository;
    private readonly IGenericRepository<EmployeePosition> _positionRepository;
    private readonly IGenericRepository<AppraisalTemplate> _templateRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IGenericRepository<AppraisalReviewEvent> _reviewEventRepository;
    private readonly IGenericRepository<CheckIn> _checkInRepository;
    private readonly IEffectiveAppraisalConfigurationService _effectiveConfigService;
    private readonly IAppraisalNotificationService _notificationService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleService> _logger;

    public AppraisalCycleService(
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<AppraisalCycleTarget> targetRepository,
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<OrganizationUnit> organizationUnitRepository,
        IGenericRepository<EmployeePosition> positionRepository,
        IGenericRepository<AppraisalTemplate> templateRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalSettings> settingsRepository,
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository,
        IGenericRepository<AppraisalReviewEvent> reviewEventRepository,
        IGenericRepository<CheckIn> checkInRepository,
        IEffectiveAppraisalConfigurationService effectiveConfigService,
        IAppraisalNotificationService notificationService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleService> logger)
    {
        _cycleRepository = cycleRepository;
        _targetRepository = targetRepository;
        _cycleTemplateRepository = cycleTemplateRepository;
        _employeeRepository = employeeRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _positionRepository = positionRepository;
        _templateRepository = templateRepository;
        _appraisalRepository = appraisalRepository;
        _settingsRepository = settingsRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _reviewEventRepository = reviewEventRepository;
        _checkInRepository = checkInRepository;
        _effectiveConfigService = effectiveConfigService;
        _notificationService = notificationService;
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

    // An appraisal cycle owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalCycle> GetOwnedCycleAsync(Guid id)
    {
        var entity = await _cycleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal cycle with ID '{id}' not found.");
        return entity;
    }

    private async Task<AppraisalSettings> GetOwnedSettingsAsync(Guid id)
    {
        var entity = await _settingsRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal settings with ID '{id}' not found.");
        return entity;
    }

    private async Task<AppraisalCycleTarget> GetOwnedCycleTargetAsync(Guid cycleId, Guid targetId)
    {
        var entity = await _targetRepository.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == targetId && t.AppraisalCycleId == cycleId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Cycle target not found.");
        return entity;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AppraisalCalendarEventDto>> GetCalendarAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var cycle = await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var events = new List<AppraisalCalendarEventDto>();

        void Add(DateOnly? date, string title, string category, string phase)
        {
            if (date.HasValue)
                events.Add(new AppraisalCalendarEventDto
                {
                    Date = date.Value,
                    Title = title,
                    Category = category,
                    Phase = phase,
                    CycleId = cycle.Id,
                    CycleName = cycle.CycleName
                });
        }

        // Cycle span
        Add(cycle.StartDate, "Cycle starts", "Opens", "Cycle");
        Add(cycle.EndDate, "Cycle ends", "Deadline", "Cycle");

        // Goal setting
        Add(cycle.GoalSettingOpenDate, "Goal setting opens", "Opens", "Goal Setting");
        Add(cycle.GoalSettingDeadline, "Goal setting deadline", "Deadline", "Goal Setting");

        // Interim reviews
        Add(cycle.Q1ReviewOpenDate, "Q1 review opens", "Opens", "Q1 Review");
        Add(cycle.Q1ReviewDeadline, "Q1 review deadline", "Deadline", "Q1 Review");
        Add(cycle.MidYearOpenDate, "Mid-year review opens", "Opens", "Mid-Year Review");
        Add(cycle.MidYearDeadline, "Mid-year review deadline", "Deadline", "Mid-Year Review");
        Add(cycle.Q3ReviewOpenDate, "Q3 review opens", "Opens", "Q3 Review");
        Add(cycle.Q3ReviewDeadline, "Q3 review deadline", "Deadline", "Q3 Review");

        // Year-end pipeline
        Add(cycle.PeerNominationDeadline, "Peer nomination deadline", "Deadline", "Peer Nomination");
        Add(cycle.SelfEvaluationOpenDate, "Self-evaluation opens", "Opens", "Self-Evaluation");
        Add(cycle.SelfEvaluationDeadline, "Self-evaluation deadline", "Deadline", "Self-Evaluation");
        Add(cycle.PeerEvaluationOpenDate, "Peer evaluation opens", "Opens", "Peer Evaluation");
        Add(cycle.PeerEvaluationDeadline, "Peer evaluation deadline", "Deadline", "Peer Evaluation");
        Add(cycle.ManagerEvaluationOpenDate, "Manager evaluation opens", "Opens", "Manager Evaluation");
        Add(cycle.ManagerEvaluationDeadline, "Manager evaluation deadline", "Deadline", "Manager Evaluation");
        Add(cycle.CalibrationOpenDate, "Calibration opens", "Opens", "Calibration");
        Add(cycle.CalibrationDeadline, "Calibration deadline", "Deadline", "Calibration");
        Add(cycle.HRReviewOpenDate, "HR review opens", "Opens", "HR Review");
        Add(cycle.HRReviewDeadline, "HR review deadline", "Deadline", "HR Review");
        Add(cycle.EmployeeAcknowledgeDeadline, "Employee acknowledgment deadline", "Deadline", "Acknowledgment");
        Add(cycle.FinalConversationDeadline, "Final conversation deadline", "Deadline", "Final Conversation");

        // Review events (Custom frequency / generated)
        var reviewEvents = await _reviewEventRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);
        foreach (var re in reviewEvents)
            events.Add(new AppraisalCalendarEventDto
            {
                Date = re.EventDate,
                Title = $"{re.Type} review event{(re.IsLightTouch ? " (light-touch)" : "")}",
                Category = "Review",
                Phase = "Interim Review",
                CycleId = cycle.Id,
                CycleName = cycle.CycleName
            });

        // Check-ins
        var checkIns = await _checkInRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);
        foreach (var ci in checkIns)
            events.Add(new AppraisalCalendarEventDto
            {
                Date = DateOnly.FromDateTime(ci.ScheduledDate),
                Title = string.IsNullOrWhiteSpace(ci.Title) ? "Check-in" : ci.Title,
                Category = "Check-in",
                Phase = "Check-in",
                CycleId = cycle.Id,
                CycleName = cycle.CycleName
            });

        return events.OrderBy(e => e.Date).ThenBy(e => e.Phase).ToList();
    }

    public async Task<AppraisalCycleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _cycleRepository.GetQueryable()
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .Include(c => c.ClosedBy)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, cancellationToken);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal cycle with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _cycleRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .Include(c => c.ClosedBy)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalCycleDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _cycleRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .Include(c => c.ClosedBy);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(c => c.Year)
                            .ThenByDescending(c => c.StartDate)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        return new PagedResult<AppraisalCycleDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AppraisalCycleDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _cycleRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.Year == year)
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .Include(c => c.ClosedBy)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalCycleDto>> GetByTypeAsync(AppraisalType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _cycleRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.AppraisalType == type)
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .Include(c => c.ClosedBy)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalCycleDto>> GetActiveCyclesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenantId = GetTenantId();

        var entities = await _cycleRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId
                     && c.OpenedDate != null
                     && c.ClosedDate == null
                     && c.StartDate <= today
                     && c.EndDate >= today)
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleDto> CreateAsync(CreateAppraisalCycleDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate dates
        if (createDto.EndDate <= createDto.StartDate)
        {
            throw new InvalidOperationException("End date must be after start date.");
        }

        await GetOwnedSettingsAsync(createDto.AppraisalSettingsId);

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _cycleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle created successfully: {cycleId}", entity.Id);

        // Reload with includes
        entity = await _cycleRepository.GetQueryable()
            .Include(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(c => c.Id == entity.Id && c.TenantId == entity.TenantId, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<AppraisalCycleDto> UpdateAsync(UpdateAppraisalCycleDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _cycleRepository.GetQueryable()
            .Include(c => c.AppraisalSettings)
            .Include(c => c.OpenedBy)
            .Include(c => c.ClosedBy)
            .FirstOrDefaultAsync(c => c.Id == updateDto.Id && c.TenantId == tenantId, cancellationToken);
        
        if (entity == null)
            throw new ArgumentException($"Appraisal cycle with ID '{updateDto.Id}' not found.");

        // Don't allow updates if cycle is closed
        if (entity.ClosedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot update a closed appraisal cycle.");
        }

        // Validate dates
        if (updateDto.EndDate <= updateDto.StartDate)
        {
            throw new InvalidOperationException("End date must be after start date.");
        }

        var settingsChanged = entity.AppraisalSettingsId != updateDto.AppraisalSettingsId;
        if (settingsChanged)
            await GetOwnedSettingsAsync(updateDto.AppraisalSettingsId);

        updateDto.UpdateEntity(entity);

        await _cycleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle updated successfully: {cycleId}", entity.Id);

        // Switching settings profiles leaves the loaded navigation pointing at the old one,
        // so the response would name the profile that was just replaced. Re-read instead.
        if (settingsChanged)
            return await GetByIdAsync(entity.Id, cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleAsync(id);

        // Check if cycle has been opened
        if (entity.OpenedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot delete an appraisal cycle that has been opened.");
        }

        await _cycleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle deleted: {cycleId}", id);

        return true;
    }

    public async Task<bool> OpenCycleAsync(OpenAppraisalCycleDto openDto, Guid openedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleAsync(openDto.CycleId);
        var tenantId = GetTenantId();

        if (entity.OpenedDate.HasValue)
        {
            throw new InvalidOperationException("Appraisal cycle has already been opened.");
        }

        // Scope-aware overlap check: block opening if any employee is already covered by another
        // non-closed cycle of the same type and year.  Skipped when no targets are configured yet.
        var thisTargets = await _targetRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == entity.Id && t.IsActive && !t.IsDeleted)
            .Include(t => t.Exclusions)
            .ToListAsync(cancellationToken);

        if (thisTargets.Any())
        {
            var thisScopeEmployees = await ResolveEmployeesFromTargetsAsync(thisTargets, cancellationToken);

            if (thisScopeEmployees.Any())
            {
                // Only a cycle that is actually running reserves its people. A Draft appraises
                // nobody, may never be opened at all, and blocking on one forced the user to go
                // and delete somebody else's half-finished cycle before they could open theirs.
                // Draft overlaps are still reported — as an advisory on the coverage preview —
                // so the early warning survives without the hard block.
                var siblingsQuery = _cycleRepository.GetQueryable()
                    .Where(c => c.TenantId == tenantId &&
                                c.Id != entity.Id &&
                                c.AppraisalType == entity.AppraisalType &&
                                c.Year == entity.Year &&
                                (c.Status == AppraisalCycleStatus.Open ||
                                 c.Status == AppraisalCycleStatus.InProgress));

                var siblingCycles = await siblingsQuery.ToListAsync(cancellationToken);

                var conflictDescriptions = new List<string>();
                foreach (var sibling in siblingCycles)
                {
                    var siblingTargets = await _targetRepository.GetQueryable()
                        .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == sibling.Id && t.IsActive && !t.IsDeleted)
                        .Include(t => t.Exclusions)
                        .ToListAsync(cancellationToken);

                    if (!siblingTargets.Any()) continue;

                    var siblingScope = await ResolveEmployeesFromTargetsAsync(siblingTargets, cancellationToken);
                    var overlapCount = thisScopeEmployees.Count(id => siblingScope.Contains(id));
                    if (overlapCount > 0)
                        conflictDescriptions.Add($"'{sibling.CycleName}' ({overlapCount} shared employee(s))");
                }

                if (conflictDescriptions.Any())
                {
                    throw new InvalidOperationException(
                        $"Cannot open this cycle — its employee scope overlaps with: {string.Join(", ", conflictDescriptions)}. " +
                        "Adjust the target groups so each employee is covered by only one active cycle.");
                }
            }
        }

        // Update cycle status — generation happens separately via GenerateAppraisalsAsync
        entity.OpenedById = openedById;
        entity.OpenedDate = DateTime.UtcNow;
        entity.Status = AppraisalCycleStatus.Open;
        await _cycleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle opened: {cycleId} by user {userId}", openDto.CycleId, openedById);

        await NotifyCycleOpenedAsync(entity, cancellationToken);

        return true;
    }

    /// <summary>
    /// Tells everyone in scope that the cycle is live and, when a goal-setting deadline is
    /// configured, by when their goals are due.
    ///
    /// Best-effort: the cycle is already open and saved by the time this runs, so a failure
    /// here is logged rather than thrown. Opening a cycle must not fail because a
    /// notification could not be written.
    /// </summary>
    private async Task NotifyCycleOpenedAsync(AppraisalCycle cycle, CancellationToken cancellationToken)
    {
        try
        {
            var recipients = (await GetEmployeesInScopeAsync(cycle.Id, cancellationToken)).ToList();
            if (recipients.Count == 0) return;

            var due = cycle.GoalSettingDeadline.HasValue
                ? $" Goals are due by {cycle.GoalSettingDeadline.Value:dd MMM yyyy}."
                : string.Empty;

            var requests = recipients.Select(employeeId => new AppraisalNotificationRequest(
                RecipientEmployeeId: employeeId,
                Type: AppraisalNotificationType.ActionRequired,
                Title: $"{cycle.CycleName} is open",
                Message: $"The {cycle.AppraisalType} appraisal cycle {cycle.CycleCode} is now open.{due}",
                CycleName: cycle.CycleName,
                NavigationUrl: "/me/performance/goals",
                Urgency: NotificationUrgency.Normal));

            var raised = await _notificationService.RaiseAsync(requests, cancellationToken);
            _logger.LogInformation("Cycle {CycleId} opened: notified {Count} of {Total} in-scope employees",
                cycle.Id, raised, recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cycle {CycleId} was opened but its notifications could not be raised", cycle.Id);
        }
    }

    /// <inheritdoc />
    public async Task<int> SendDeadlineRemindersAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var cycle = await GetOwnedCycleAsync(cycleId);

        if (cycle.Status == AppraisalCycleStatus.Draft)
            throw new InvalidOperationException("Reminders can only be sent for a cycle that has been opened.");

        // Risk bands are a tenant policy, held on the cycle's settings profile. Falling back
        // to the DTO defaults keeps this working for a settings row saved before they existed.
        var settings = await _settingsRepository.GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == cycle.AppraisalSettingsId && s.TenantId == cycle.TenantId, cancellationToken);
        var highDays = settings is { DeadlineRiskHighDays: > 0 } ? settings.DeadlineRiskHighDays : 2;
        var lowDays = settings is { DeadlineRiskLowDays: > 0 } ? settings.DeadlineRiskLowDays : 7;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var phases = new (DateOnly? Deadline, string Phase, string NavigationUrl)[]
        {
            // Area 25 slice 5: employee-directed nudges land in the portal; the manager one stays desk.
            (cycle.GoalSettingDeadline,           "Goal setting",         "/me/performance/goals"),
            (cycle.SelfEvaluationDeadline,        "Self-evaluation",      "/me/performance/appraisals"),
            (cycle.PeerNominationDeadline,        "Peer nomination",      "/me/performance/appraisals"),
            (cycle.PeerEvaluationDeadline,        "Peer evaluation",      "/me/performance/peer-reviews"),
            (cycle.ManagerEvaluationDeadline,     "Manager evaluation",   "/hr/performance/team-goals"),
            (cycle.EmployeeAcknowledgeDeadline,   "Acknowledgment",       "/me/performance/appraisals"),
            (cycle.FinalConversationDeadline,     "Final conversation",   "/me/performance/appraisals"),
        };

        // Only phases that are live now: already past, or close enough to be worth a nudge.
        // A deadline further out than the low-risk band is not news.
        var actionable = phases
            .Where(p => p.Deadline.HasValue)
            .Select(p => (p.Phase, p.NavigationUrl, Deadline: p.Deadline!.Value,
                          DaysRemaining: p.Deadline!.Value.DayNumber - today.DayNumber))
            .Where(p => p.DaysRemaining <= lowDays)
            .ToList();

        if (actionable.Count == 0) return 0;

        var recipients = (await GetEmployeesInScopeAsync(cycleId, cancellationToken)).ToList();
        if (recipients.Count == 0) return 0;

        var requests = new List<AppraisalNotificationRequest>();
        foreach (var phase in actionable)
        {
            var (type, urgency, headline) = phase.DaysRemaining switch
            {
                < 0 => (AppraisalNotificationType.DeadlinePassed, NotificationUrgency.Urgent,
                        $"{phase.Phase} is overdue"),
                var d when d <= highDays => (AppraisalNotificationType.DeadlineImminent, NotificationUrgency.Urgent,
                        $"{phase.Phase} closes {(d == 0 ? "today" : d == 1 ? "tomorrow" : $"in {d} days")}"),
                var d => (AppraisalNotificationType.DeadlineApproaching, NotificationUrgency.Warning,
                        $"{phase.Phase} closes in {d} days"),
            };

            requests.AddRange(recipients.Select(employeeId => new AppraisalNotificationRequest(
                RecipientEmployeeId: employeeId,
                Type: type,
                Title: $"{headline} — {cycle.CycleCode}",
                Message: $"The {phase.Phase.ToLowerInvariant()} deadline for {cycle.CycleName} " +
                         $"{(phase.DaysRemaining < 0 ? "passed on" : "is")} {phase.Deadline:dd MMM yyyy}.",
                CycleName: cycle.CycleName,
                NavigationUrl: phase.NavigationUrl,
                Urgency: urgency)));
        }

        var raised = await _notificationService.RaiseAsync(requests, cancellationToken);
        _logger.LogInformation("Cycle {CycleId}: raised {Raised} deadline reminder(s) across {Phases} phase(s)",
            cycleId, raised, actionable.Count);
        return raised;
    }

    /// <summary>
    /// Generates appraisal instances for all employees in scope.
    /// Can be called on Draft or Open cycles — blocked only on Closed.
    /// This allows HR to generate (and review) appraisals before officially opening the cycle.
    /// Uses target-only scope resolution (aligned with the coverage preview).
    /// Assigns the resolved template to each appraisal. Throws if any employee has
    /// a template conflict or no template — run the Coverage Preview first to fix issues.
    /// </summary>
    public async Task<(int Created, int EvaluationsCreated, int ReviewEventsCreated)> GenerateAppraisalsAsync(
        Guid cycleId, Guid generatedById, CancellationToken cancellationToken = default)
    {
        var cycle = await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        if (cycle.Status == AppraisalCycleStatus.Closed)
            throw new InvalidOperationException("Cannot generate appraisals for a closed cycle.");

        var settings = await GetOwnedSettingsAsync(cycle.AppraisalSettingsId);

        // ── 1. Load active template assignments with scope nav-props ──
        var activeTemplates = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ct.AppraisalCycleId == cycleId && ct.IsActive && !ct.IsDeleted)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .OrderByDescending(ct => ct.Priority)
            .ToListAsync(cancellationToken);

        if (!activeTemplates.Any())
            throw new InvalidOperationException("No active templates are configured for this cycle. Assign at least one template before generating.");

        // ── 2. Resolve employees from targets (target-only scope) ──
        var activeTargets = await _targetRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId && t.IsActive && !t.IsDeleted)
            .Include(t => t.Exclusions)
            .ToListAsync(cancellationToken);

        if (!activeTargets.Any())
            throw new InvalidOperationException("No active targets are configured for this cycle. Add at least one target before generating.");

        var employeeIds = await ResolveEmployeesFromTargetsAsync(activeTargets, cancellationToken);
        if (employeeIds.Count == 0)
            throw new InvalidOperationException("No active employees found in the configured target groups.");

        // ── 3. Filter out any already-generated employees ──
        var alreadyGenerated = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == cycleId)
            .Select(a => a.EmployeeId)
            .ToListAsync(cancellationToken);

        var pendingIds = employeeIds.Except(alreadyGenerated).ToList();
        if (!pendingIds.Any())
            throw new InvalidOperationException("All employees in scope already have appraisals generated for this cycle.");

        // ── 4. Load employee details + resolve templates ──
        var employees = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && pendingIds.Contains(e.Id) && !e.IsDeleted)
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.OrganizationLevel)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var conflicts   = new List<string>();
        var noTemplates = new List<string>();

        var resolutions = employees
            .Select(emp =>
            {
                var (templateId, status, conflictNames) = ResolveTemplate(emp, activeTemplates);
                if (status == EmployeeCoverageStatus.Conflict)
                    conflicts.Add($"{emp.FullName} ({emp.EmployeeNumber}): conflicting templates [{string.Join(", ", conflictNames)}]");
                else if (status == EmployeeCoverageStatus.NoTemplate)
                    noTemplates.Add($"{emp.FullName} ({emp.EmployeeNumber})");
                return (emp, templateId);
            })
            .ToList();

        if (conflicts.Any() || noTemplates.Any())
        {
            var sb = new System.Text.StringBuilder();
            if (conflicts.Any())
                sb.AppendLine($"Template conflicts ({conflicts.Count}): {string.Join("; ", conflicts)}");
            if (noTemplates.Any())
                sb.AppendLine($"No template assigned ({noTemplates.Count}): {string.Join("; ", noTemplates)}");
            throw new InvalidOperationException(
                $"Cannot generate appraisals — coverage issues must be resolved first:\n{sb}");
        }

        // ── 5. Batch-create appraisals (500 per save), all inside ONE transaction so an appraisal can
        //       never persist without its criterion-config snapshot / evaluations / review events. ──
        var totalCreated = 0;
        var totalEvaluationsCreated = 0;
        var totalReviewEventsCreated = 0;
        const int batchSize = 500;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Retry-safety: the retrying execution strategy may re-invoke this delegate after a
            // rolled-back attempt — drop entities tracked by the previous attempt and recount from zero.
            _unitOfWork.ClearChangeTracker();
            totalCreated = 0;
            totalEvaluationsCreated = 0;
            totalReviewEventsCreated = 0;

            for (int i = 0; i < resolutions.Count; i += batchSize)
            {
                var batch = resolutions.Skip(i).Take(batchSize).ToList();
                var appraisalsToAdd = new List<PerformanceAppraisal>();

                foreach (var (employee, resolvedTemplateId) in batch)
                {
                    var sequenceNumber = alreadyGenerated.Count + i + batch.IndexOf((employee, resolvedTemplateId)) + 1;
                    appraisalsToAdd.Add(new PerformanceAppraisal
                    {
                        Id = Guid.NewGuid(),
                        AppraisalCycleId = cycleId,
                        EmployeeId = employee.Id,
                        AppraisalTemplateId = resolvedTemplateId,
                        AppraisalNumber = GenerateAppraisalNumber(cycle, employee.EmployeeNumber, sequenceNumber),
                        Year = cycle.Year,
                        StartDate = cycle.StartDate,
                        EndDate = cycle.EndDate,
                        Status = AppraisalStatus.Draft,
                        PeerEvaluatorsCount = 0,
                        TenantId = cycle.TenantId,
                        CreatedBy = generatedById.ToString(),
                        CreatedById = generatedById,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                foreach (var appraisal in appraisalsToAdd)
                    await _appraisalRepository.AddAsync(appraisal);
                await _unitOfWork.SaveChangesAsync(ct);

                // Snapshot criterion configs — anchored to the template resolved above. A failure here
                // now ROLLS BACK the whole generation (no orphan appraisals without configs).
                foreach (var appraisal in appraisalsToAdd)
                {
                    await _effectiveConfigService.SnapshotConfigAsync(
                        appraisal.Id, appraisal.EmployeeId, cycleId, appraisal.AppraisalTemplateId, ct);
                }

                var evaluationsToAdd = new List<EvaluatorEvaluation>();
                foreach (var appraisal in appraisalsToAdd)
                {
                    var employee = batch.First(r => r.emp.Id == appraisal.EmployeeId).emp;

                    if (settings.RequireSelfEvaluation)
                        evaluationsToAdd.Add(CreateEvaluation(appraisal, employee.Id, EvaluatorRole.Self, settings.SelfEvaluationWeight, false, cycle, generatedById));

                    if (settings.RequireManagerEvaluation)
                    {
                        if (employee.ManagerId.HasValue)
                            evaluationsToAdd.Add(CreateEvaluation(appraisal, employee.ManagerId.Value, EvaluatorRole.Manager, settings.ManagerEvaluationWeight, settings.IsManagerAuthoritative, cycle, generatedById));
                        else
                            _logger.LogWarning("Employee {EmployeeId} has no manager assigned; skipping manager evaluation for cycle {CycleId}", employee.Id, cycleId);
                    }
                }

                foreach (var ev in evaluationsToAdd)
                    await _evaluatorEvaluationRepository.AddAsync(ev);
                await _unitOfWork.SaveChangesAsync(ct);

                // ── 6. Create interim review event records from cycle dates ──
                var reviewEventsToAdd = new List<AppraisalReviewEvent>();
                if (settings.ReviewFrequency != ReviewFrequency.None && settings.ReviewFrequency != ReviewFrequency.Custom)
                {
                    foreach (var appraisal in appraisalsToAdd)
                        reviewEventsToAdd.AddRange(BuildReviewEvents(appraisal, cycle, settings.ReviewFrequency, settings.InterimReviewDepth));

                    foreach (var re in reviewEventsToAdd)
                        await _reviewEventRepository.AddAsync(re);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                totalCreated += appraisalsToAdd.Count;
                totalEvaluationsCreated += evaluationsToAdd.Count;
                totalReviewEventsCreated += reviewEventsToAdd.Count;
                _logger.LogInformation("GenerateAppraisals batch {Batch}: {A} appraisals, {E} evaluations, {R} review events for cycle {CycleId}",
                    (i / batchSize) + 1, appraisalsToAdd.Count, evaluationsToAdd.Count, reviewEventsToAdd.Count, cycleId);
            }
        }, cancellationToken);

        _logger.LogInformation("GenerateAppraisals complete — cycle {CycleId}: {Total} appraisals, {Evals} evaluations, {Events} review events",
            cycleId, totalCreated, totalEvaluationsCreated, totalReviewEventsCreated);
        return (totalCreated, totalEvaluationsCreated, totalReviewEventsCreated);
    }

    // ── Template resolution (mirrors CycleCoverageService.ResolveEmployeeCoverage) ──
    private static (Guid? TemplateId, EmployeeCoverageStatus Status, List<string> ConflictNames)
        ResolveTemplate(Employee emp, List<AppraisalCycleTemplate> assignments)
    {
        var positionMatches = assignments
            .Where(ct => ct.AppraisalTemplate.PositionId.HasValue && ct.AppraisalTemplate.PositionId == emp.PositionId)
            .ToList();
        var unitMatches = assignments
            .Where(ct => ct.AppraisalTemplate.OrganizationUnitId.HasValue
                      && ct.AppraisalTemplate.OrganizationUnitId == emp.OrganizationUnitId
                      && ct.AppraisalTemplate.PositionId == null)
            .ToList();
        var levelMatches = assignments
            .Where(ct => ct.AppraisalTemplate.OrganizationLevelId.HasValue
                      && ct.AppraisalTemplate.OrganizationUnitId == null
                      && ct.AppraisalTemplate.PositionId == null)
            .ToList();
        var globalMatches = assignments
            .Where(ct => ct.AppraisalTemplate.OrganizationLevelId == null
                      && ct.AppraisalTemplate.OrganizationUnitId == null
                      && ct.AppraisalTemplate.PositionId == null)
            .ToList();

        var bestMatches = positionMatches.Count > 0 ? positionMatches
                        : unitMatches.Count > 0    ? unitMatches
                        : levelMatches.Count > 0   ? levelMatches
                        : globalMatches.Count > 0  ? globalMatches
                        : null;

        if (bestMatches == null)
            return (null, EmployeeCoverageStatus.NoTemplate, new List<string>());

        var topPriority = bestMatches.Max(m => m.Priority);
        var topTied = bestMatches.Where(m => m.Priority == topPriority).ToList();

        if (topTied.Count > 1)
            return (null, EmployeeCoverageStatus.Conflict, topTied.Select(m => m.AppraisalTemplate.TemplateName).ToList());

        return (topTied[0].AppraisalTemplateId, EmployeeCoverageStatus.Covered, new List<string>());
    }

    // ── Scope resolution from targets (mirrors CycleCoverageService, target-only) ──
    private async Task<HashSet<Guid>> ResolveEmployeesFromTargetsAsync(
        List<AppraisalCycleTarget> targets, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var set = new HashSet<Guid>();
        foreach (var target in targets)
        {
            switch (target.TargetType)
            {
                case AppraisalTargetType.Position:
                    if (target.PositionId.HasValue)
                    {
                        var ids = await _employeeRepository.GetQueryable()
                            .Where(e => e.TenantId == tenantId && e.PositionId == target.PositionId && !e.IsDeleted && e.IsActive)
                            .Select(e => e.Id).ToListAsync(cancellationToken);
                        foreach (var id in ids) set.Add(id);
                    }
                    break;

                case AppraisalTargetType.OrganizationUnit:
                    if (target.OrganizationUnitId.HasValue)
                    {
                        var childIds = await GetChildUnitIdsForGenerationAsync(target.OrganizationUnitId.Value, cancellationToken);
                        childIds.Add(target.OrganizationUnitId.Value);
                        var ids = await _employeeRepository.GetQueryable()
                            .Where(e => e.TenantId == tenantId
                                     && e.OrganizationUnitId.HasValue
                                     && childIds.Contains(e.OrganizationUnitId.Value)
                                     && !e.IsDeleted
                                     && e.IsActive)
                            .Select(e => e.Id).ToListAsync(cancellationToken);
                        foreach (var id in ids) set.Add(id);
                    }
                    break;

                case AppraisalTargetType.OrganizationLevel:
                    if (target.OrganizationLevelId.HasValue)
                    {
                        var ids = await _employeeRepository.GetQueryable()
                            .Where(e => e.TenantId == tenantId && e.OrganizationLevelId == target.OrganizationLevelId && !e.IsDeleted && e.IsActive)
                            .Select(e => e.Id).ToListAsync(cancellationToken);
                        foreach (var id in ids) set.Add(id);
                    }
                    break;
            }
        }

        // Apply all active exclusions across all targets.
        var allExclusions = targets.SelectMany(t => t.Exclusions).ToList();
        var beforeCount = set.Count;
        await ApplyExclusionsAsync(set, allExclusions, cancellationToken);
        var excluded = beforeCount - set.Count;
        if (excluded > 0)
            _logger.LogInformation("Excluded {count} employees via target exclusion rules", excluded);

        return set;
    }

    private async Task<HashSet<Guid>> GetChildUnitIdsForGenerationAsync(
        Guid parentUnitId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentUnitId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await _organizationUnitRepository.GetQueryable()
                .Where(u => u.TenantId == tenantId && u.ParentUnitId == current && !u.IsDeleted)
                .Select(u => u.Id).ToListAsync(cancellationToken);
            foreach (var child in children)
                if (result.Add(child)) queue.Enqueue(child);
        }
        return result;
    }

    // ── Review event factory ──────────────────────────────────────────────────
    private static IEnumerable<AppraisalReviewEvent> BuildReviewEvents(
        PerformanceAppraisal appraisal, AppraisalCycle cycle, ReviewFrequency frequency, InterimReviewDepth interimReviewDepth)
    {
        // Theme 7 wiring: the InterimReviewDepth setting decides whether generated interim events are
        // full appraisals or light-touch reviews. Keep IsLightTouch as the strict inverse of IsFullAppraisal.
        var isFullAppraisal = interimReviewDepth == InterimReviewDepth.FullAppraisal;

        var windows = frequency == ReviewFrequency.Quarterly
            ? new[]
            {
                (Type: ReviewEventType.QuarterlyQ1,     Open: cycle.Q1ReviewOpenDate,   Deadline: cycle.Q1ReviewDeadline),
                (Type: ReviewEventType.MidYearReview,   Open: cycle.MidYearOpenDate,    Deadline: cycle.MidYearDeadline),
                (Type: ReviewEventType.QuarterlyQ3,     Open: cycle.Q3ReviewOpenDate,   Deadline: cycle.Q3ReviewDeadline),
            }
            : new[]
            {
                (Type: ReviewEventType.MidYearReview,   Open: cycle.MidYearOpenDate,    Deadline: cycle.MidYearDeadline),
            };

        foreach (var (type, open, deadline) in windows)
        {
            // EventDate = deadline if set, else open date, else a proportional fallback
            var eventDate = deadline
                ?? open
                ?? FallbackEventDate(type, cycle.StartDate, cycle.EndDate);

            yield return new AppraisalReviewEvent
            {
                Id                      = Guid.NewGuid(),
                AppraisalCycleId        = cycle.Id,
                PerformanceAppraisalId  = appraisal.Id,
                Type                    = type,
                EventDate               = eventDate,
                Status                  = AppraisalReviewStatus.Pending,
                IsFullAppraisal         = isFullAppraisal,
                IsLightTouch            = !isFullAppraisal,
                TenantId                = cycle.TenantId,
                CreatedBy               = appraisal.CreatedBy,
                CreatedById             = appraisal.CreatedById,
                CreatedAt               = DateTime.UtcNow,
            };
        }
    }

    /// <summary>
    /// Proportional fallback date when the cycle has no explicit window dates configured.
    /// Q1 → 25% through, MidYear → 50%, Q3 → 75%.
    /// </summary>
    private static DateOnly FallbackEventDate(ReviewEventType type, DateOnly start, DateOnly end)
    {
        var totalDays = end.DayNumber - start.DayNumber;
        var fraction = type switch
        {
            ReviewEventType.QuarterlyQ1   => 0.25,
            ReviewEventType.MidYearReview => 0.50,
            ReviewEventType.QuarterlyQ3   => 0.75,
            _                             => 0.50,
        };
        return start.AddDays((int)(totalDays * fraction));
    }

    // ── Evaluation factory ───────────────────────────────────────────────────
    private static EvaluatorEvaluation CreateEvaluation(
        PerformanceAppraisal appraisal, Guid evaluatorId, EvaluatorRole role,
        decimal weight, bool isAuthoritative, AppraisalCycle cycle, Guid createdById)
    {
        return new EvaluatorEvaluation
        {
            Id = Guid.NewGuid(),
            AppraisalId = appraisal.Id,
            EvaluatorId = evaluatorId,
            EvaluatorRole = role,
            EvaluatorWeight = weight,
            IsAuthoritative = isAuthoritative,
            TenantId = cycle.TenantId,
            CreatedBy = createdById.ToString(),
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> CloseCycleAsync(CloseAppraisalCycleDto closeDto, Guid closedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleAsync(closeDto.CycleId);

        if (!entity.OpenedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot close a cycle that hasn't been opened.");
        }

        if (entity.ClosedDate.HasValue)
        {
            throw new InvalidOperationException("Appraisal cycle is already closed.");
        }

        entity.ClosedById = closedById;
        entity.ClosedDate = DateTime.UtcNow;
        entity.Status = AppraisalCycleStatus.Closed;

        await _cycleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle closed: {cycleId} by user {userId}", closeDto.CycleId, closedById);

        return true;
    }

    /// <summary>
    /// Gets comprehensive progress metrics for an appraisal cycle dashboard
    /// </summary>
    public async Task<AppraisalCycleProgressDto> GetCycleProgressAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Get cycle with settings
        var cycle = await _cycleRepository.GetQueryable()
            .Include(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.TenantId == tenantId, cancellationToken);

        if (cycle == null)
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");

        var settings = cycle.AppraisalSettings;

        // Get all appraisals for this cycle with evaluations
        var appraisals = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);

        var appraisalIds = appraisals.Select(a => a.Id).ToList();

        var evaluations = await _evaluatorEvaluationRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && appraisalIds.Contains(e.AppraisalId))
            .ToListAsync(cancellationToken);

        // Calculate progress metrics
        var totalAppraisals = appraisals.Count;

        var progress = new AppraisalCycleProgressDto
        {
            // Cycle Snapshot
            CycleId = cycle.Id,
            CycleName = cycle.CycleName,
            CycleCode = cycle.CycleCode,
            Year = cycle.Year,
            StartDate = cycle.StartDate,
            EndDate = cycle.EndDate,
            Status = cycle.Status,
            AppraisalSettingsName = settings?.SettingsName,
            RequirePeerReviews = settings?.RequirePeerReviews ?? false,
            RequireHRReview = settings?.RequireHRReview ?? false,
            
            // Deadlines
            SelfEvaluationDeadline = cycle.SelfEvaluationDeadline,
            PeerEvaluationDeadline = cycle.PeerEvaluationDeadline,
            ManagerEvaluationDeadline = cycle.ManagerEvaluationDeadline,
            HRReviewDeadline = cycle.HRReviewDeadline,
            EmployeeAcknowledgeDeadline = cycle.EmployeeAcknowledgeDeadline,

            // Calculate current phase
            CurrentPhase = DetermineCurrentPhase(cycle),

            // Progress metrics
            SelfEvaluationProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.Self, totalAppraisals, settings?.RequireSelfEvaluation ?? true),
            PeerEvaluationProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.Peer, totalAppraisals, settings?.RequirePeerReviews ?? false),
            ManagerEvaluationProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.Manager, totalAppraisals, settings?.RequireManagerEvaluation ?? true),
            HRReviewProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.HR, totalAppraisals, settings?.RequireHRReview ?? false),

            // Participation coverage
            TotalEmployeesTargeted = totalAppraisals,
            TotalEmployeesExcluded = await CalculateExcludedEmployees(cycleId, cancellationToken),
            TargetBreakdown = await CalculateTargetBreakdown(cycleId, cancellationToken)
        };

        // Calculate bottlenecks
        progress.EmployeesNotStartedSelfEvaluation = CalculateNotStartedCount(evaluations, EvaluatorRole.Self);
        progress.PeerReviewsPendingPastMidpoint = CalculatePeerReviewsAtRisk(cycle, evaluations);
        progress.ManagersWithHighWorkload = CalculateManagersWithHighWorkload(evaluations, settings?.ManagerWorkloadThreshold ?? 10);

        // Calculate deadline risks
        progress.DeadlineRisks = CalculateDeadlineRisks(cycle,
            settings?.DeadlineRiskHighDays ?? 2,
            settings?.DeadlineRiskMediumDays ?? 5,
            settings?.DeadlineRiskLowDays ?? 7);

        // Calculate top bottlenecks
        progress.TopBottlenecks = await CalculateTopBottlenecks(cycle, appraisals, evaluations, cancellationToken);

        return progress;
    }

    private string DetermineCurrentPhase(AppraisalCycle cycle)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (cycle.SelfEvaluationDeadline.HasValue && today <= cycle.SelfEvaluationDeadline.Value)
            return "Self Evaluation";

        if (cycle.PeerEvaluationDeadline.HasValue && today <= cycle.PeerEvaluationDeadline.Value)
            return "Peer Evaluation";

        if (cycle.ManagerEvaluationDeadline.HasValue && today <= cycle.ManagerEvaluationDeadline.Value)
            return "Manager Evaluation";

        if (cycle.HRReviewDeadline.HasValue && today <= cycle.HRReviewDeadline.Value)
            return "HR Review";

        if (cycle.EmployeeAcknowledgeDeadline.HasValue && today <= cycle.EmployeeAcknowledgeDeadline.Value)
            return "Employee Acknowledgment";

        return "Completed";
    }

    private ProgressMetricDto CalculateEvaluationProgress(List<EvaluatorEvaluation> evaluations, EvaluatorRole role, int totalAppraisals, bool isRequired)
    {
        var roleEvaluations = evaluations.Where(e => e.EvaluatorRole == role).ToList();
        var completed = roleEvaluations.Count(e => e.SubmittedDate.HasValue);
        var total = role == EvaluatorRole.Peer ? roleEvaluations.Count : totalAppraisals;

        return new ProgressMetricDto
        {
            Completed = completed,
            Total = total,
            PercentageCompleted = total > 0 ? Math.Round((decimal)completed / total * 100, 2) : 0,
            IsRequired = isRequired
        };
    }

    private Task<int> CalculateExcludedEmployees(Guid cycleId, CancellationToken cancellationToken)
    {
        // Exclusions are now managed via AppraisalCycleTargetExclusion — not counted here.
        return Task.FromResult(0);
    }

    private async Task<TargetBreakdownDto> CalculateTargetBreakdown(Guid cycleId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var targets = await _targetRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);

        return new TargetBreakdownDto
        {
            OrganizationLevelTargets = targets.Count(t => t.TargetType == AppraisalTargetType.OrganizationLevel),
            OrganizationUnitTargets = targets.Count(t => t.TargetType == AppraisalTargetType.OrganizationUnit),
            PositionTargets = targets.Count(t => t.TargetType == AppraisalTargetType.Position),
            IndividualEmployeeTargets = targets.Count(t => t.TargetType == AppraisalTargetType.Employee)
        };
    }

    private int CalculateNotStartedCount(List<EvaluatorEvaluation> evaluations, EvaluatorRole role)
    {
        return evaluations.Count(e => e.EvaluatorRole == role && !e.StartedDate.HasValue);
    }

    private int CalculatePeerReviewsAtRisk(AppraisalCycle cycle, List<EvaluatorEvaluation> evaluations)
    {
        if (!cycle.PeerEvaluationDeadline.HasValue)
            return 0;

        var midpoint = cycle.StartDate.AddDays((cycle.PeerEvaluationDeadline.Value.DayNumber - cycle.StartDate.DayNumber) / 2);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (today < midpoint)
            return 0;

        return evaluations.Count(e => e.EvaluatorRole == EvaluatorRole.Peer && !e.SubmittedDate.HasValue);
    }

    private int CalculateManagersWithHighWorkload(List<EvaluatorEvaluation> evaluations, int threshold)
    {
        var managerWorkloads = evaluations
            .Where(e => e.EvaluatorRole == EvaluatorRole.Manager && !e.SubmittedDate.HasValue)
            .GroupBy(e => e.EvaluatorId)
            .Select(g => new { ManagerId = g.Key, PendingCount = g.Count() })
            .Where(m => m.PendingCount >= threshold)
            .ToList();

        return managerWorkloads.Count;
    }

    private List<DeadlineRiskDto> CalculateDeadlineRisks(AppraisalCycle cycle, int highDays, int mediumDays, int lowDays)
    {
        var risks = new List<DeadlineRiskDto>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        void AddRisk(string phase, DateOnly? deadline)
        {
            if (!deadline.HasValue) return;

            var daysUntil = deadline.Value.DayNumber - today.DayNumber;
            var isOverdue = daysUntil < 0;

            var riskLevel =
                  daysUntil < 0          ? RiskLevel.Critical
                : daysUntil <= highDays  ? RiskLevel.High
                : daysUntil <= mediumDays ? RiskLevel.Medium
                : daysUntil <= lowDays   ? RiskLevel.Low
                : RiskLevel.None;

            var relativeTime = Math.Abs(daysUntil) switch
            {
                0 => "today",
                1 => isOverdue ? "overdue by 1 day" : "in 1 day",
                _ => isOverdue ? $"overdue by {Math.Abs(daysUntil)} days" : $"in {daysUntil} days"
            };

            if (riskLevel != RiskLevel.None || isOverdue)
            {
                risks.Add(new DeadlineRiskDto
                {
                    Phase = phase,
                    Deadline = deadline.Value,
                    DaysUntilDeadline = daysUntil,
                    RelativeTime = relativeTime,
                    IsOverdue = isOverdue,
                    RiskLevel = riskLevel
                });
            }
        }

        AddRisk("Self Evaluation", cycle.SelfEvaluationDeadline);
        AddRisk("Peer Evaluation", cycle.PeerEvaluationDeadline);
        AddRisk("Manager Evaluation", cycle.ManagerEvaluationDeadline);
        AddRisk("HR Review", cycle.HRReviewDeadline);
        AddRisk("Employee Acknowledgment", cycle.EmployeeAcknowledgeDeadline);

        return risks.OrderBy(r => r.DaysUntilDeadline).ToList();
    }

    private async Task<List<BottleneckDto>> CalculateTopBottlenecks(AppraisalCycle cycle, List<PerformanceAppraisal> appraisals, List<EvaluatorEvaluation> evaluations, CancellationToken cancellationToken)
    {
        var bottlenecks = new List<BottleneckDto>();

        // Not started self-evaluations
        var notStartedSelf = evaluations.Count(e => e.EvaluatorRole == EvaluatorRole.Self && !e.StartedDate.HasValue);
        if (notStartedSelf > 0)
        {
            bottlenecks.Add(new BottleneckDto
            {
                Category = "Self Evaluation",
                Description = $"{notStartedSelf} employees haven't started",
                Count = notStartedSelf,
                Icon = "⚠️",
                Severity = notStartedSelf > appraisals.Count * 0.5m ? RiskLevel.High : RiskLevel.Medium
            });
        }

        // Pending manager evaluations
        var pendingManager = evaluations.Count(e => e.EvaluatorRole == EvaluatorRole.Manager && !e.SubmittedDate.HasValue);
        if (pendingManager > 0)
        {
            bottlenecks.Add(new BottleneckDto
            {
                Category = "Manager Evaluation",
                Description = $"{pendingManager} pending manager evaluations",
                Count = pendingManager,
                Icon = "📋",
                Severity = pendingManager > appraisals.Count * 0.3m ? RiskLevel.High : RiskLevel.Medium
            });
        }

        // Employees without managers
        var tenantId = GetTenantId();
        var withoutManagers = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId
                     && appraisals.Select(a => a.EmployeeId).Contains(e.Id)
                     && !e.ManagerId.HasValue)
            .CountAsync(cancellationToken);

        if (withoutManagers > 0)
        {
            bottlenecks.Add(new BottleneckDto
            {
                Category = "Configuration",
                Description = $"{withoutManagers} employees without assigned managers",
                Count = withoutManagers,
                Icon = "⚙️",
                Severity = RiskLevel.Medium
            });
        }

        return bottlenecks.OrderByDescending(b => b.Severity).ThenByDescending(b => b.Count).Take(5).ToList();
    }

    /// <summary>
    /// Creates appraisal instances for all employees in scope when a cycle is opened
    /// Efficiently handles bulk creation with batching for large employee sets
    /// </summary>
    private async Task CreateAppraisalInstancesAsync(AppraisalCycle cycle, AppraisalSettings settings, CancellationToken cancellationToken)
    {
        // Get all employees in scope
        var employeeIds = (await GetEmployeesInScopeAsync(cycle.Id, cancellationToken)).ToList();
        
        if (!employeeIds.Any())
        {
            _logger.LogWarning("No employees found in scope for cycle {cycleId}. No appraisal instances created.", cycle.Id);
            return;
        }

        _logger.LogInformation("Creating appraisal instances for {count} employees in cycle {cycleId}", employeeIds.Count, cycle.Id);

        // Check if any appraisals already exist for this cycle (shouldn't happen, but safety check)
        var tenantId = cycle.TenantId;
        var existingAppraisals = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == cycle.Id)
            .Select(a => a.EmployeeId)
            .ToListAsync(cancellationToken);

        if (existingAppraisals.Any())
        {
            _logger.LogWarning("Found {count} existing appraisals for cycle {cycleId}. Filtering out duplicates.", existingAppraisals.Count, cycle.Id);
            employeeIds = employeeIds.Except(existingAppraisals).ToList();
        }

        if (!employeeIds.Any())
        {
            _logger.LogInformation("All employees already have appraisals for cycle {cycleId}. No new instances created.", cycle.Id);
            return;
        }

        // Fetch employee details with their managers in batches to avoid memory issues
        const int batchSize = 500;
        var totalCreated = 0;
        var totalEvaluationsCreated = 0;

        for (int i = 0; i < employeeIds.Count; i += batchSize)
        {
            var batchEmployeeIds = employeeIds.Skip(i).Take(batchSize).ToList();
            
            // Fetch employee details with manager info
            var employees = await _employeeRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId && batchEmployeeIds.Contains(e.Id))
                .Select(e => new 
                { 
                    e.Id, 
                    e.EmployeeNumber,
                    e.ManagerId 
                })
                .ToListAsync(cancellationToken);

            var appraisalsToAdd = new List<PerformanceAppraisal>();

            // Generate a counter for appraisal numbers in this batch
            var batchStartNumber = i + 1;

            foreach (var employee in employees)
            {
                var appraisalNumber = GenerateAppraisalNumber(cycle, employee.EmployeeNumber, batchStartNumber + employees.IndexOf(employee));

                var appraisal = new PerformanceAppraisal
                {
                    Id = Guid.NewGuid(),
                    AppraisalCycleId = cycle.Id,
                    EmployeeId = employee.Id,
                    AppraisalNumber = appraisalNumber,
                    Year = cycle.Year,
                    StartDate = cycle.StartDate,
                    EndDate = cycle.EndDate,
                    Status = AppraisalStatus.Draft,
                    PeerEvaluatorsCount = 0,
                    TenantId = cycle.TenantId,
                    CreatedBy = cycle.OpenedById?.ToString(),
                    CreatedById = cycle.OpenedById,
                    CreatedAt = DateTime.UtcNow
                };

                appraisalsToAdd.Add(appraisal);
            }

            // Bulk add and save appraisals first
            foreach (var appraisal in appraisalsToAdd)
            {
                await _appraisalRepository.AddAsync(appraisal);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Snapshot criterion config for each appraisal (freezes weights + grade bands at generation time)
            foreach (var appraisal in appraisalsToAdd)
            {
                try
                {
                    await _effectiveConfigService.SnapshotConfigAsync(appraisal.Id, appraisal.EmployeeId, cycle.Id, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to snapshot criterion config for appraisal {appraisalId} (employee {employeeId}). Continuing generation.",
                        appraisal.Id, appraisal.EmployeeId);
                }
            }

            // Now create evaluations based on the saved appraisals
            var evaluationsToAdd = new List<EvaluatorEvaluation>();
            
            foreach (var employee in employees)
            {
                // Find the saved appraisal for this employee
                var appraisal = appraisalsToAdd.FirstOrDefault(a => a.EmployeeId == employee.Id);
                if (appraisal == null) continue;

                // Create evaluator evaluation records based on settings
                
                // 1. Self-evaluation (if required)
                if (settings.RequireSelfEvaluation)
                {
                    evaluationsToAdd.Add(new EvaluatorEvaluation
                    {
                        Id = Guid.NewGuid(),
                        AppraisalId = appraisal.Id,
                        EvaluatorId = employee.Id,
                        EvaluatorRole = EvaluatorRole.Self,
                        EvaluatorWeight = settings.SelfEvaluationWeight,
                        IsAuthoritative = false,
                        TenantId = cycle.TenantId,
                        CreatedBy = cycle.OpenedById?.ToString(),
                        CreatedById = cycle.OpenedById,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 2. Manager evaluation (if required and employee has a manager)
                if (settings.RequireManagerEvaluation && employee.ManagerId.HasValue)
                {
                    evaluationsToAdd.Add(new EvaluatorEvaluation
                    {
                        Id = Guid.NewGuid(),
                        AppraisalId = appraisal.Id,
                        EvaluatorId = employee.ManagerId.Value,
                        EvaluatorRole = EvaluatorRole.Manager,
                        EvaluatorWeight = settings.ManagerEvaluationWeight,
                        IsAuthoritative = settings.IsManagerAuthoritative,
                        TenantId = cycle.TenantId,
                        CreatedBy = cycle.OpenedById?.ToString(),
                        CreatedById = cycle.OpenedById,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else if (settings.RequireManagerEvaluation && !employee.ManagerId.HasValue)
                {
                    _logger.LogWarning("Employee {employeeId} has no manager assigned but manager evaluation is required for cycle {cycleId}", 
                        employee.Id, cycle.Id);
                }

                // Note: Peer evaluations are typically nominated later, not created upfront
                // HR evaluation will be created when HR review stage is reached
            }

            // Now add evaluations (they reference the committed appraisals)
            foreach (var evaluation in evaluationsToAdd)
            {
                await _evaluatorEvaluationRepository.AddAsync(evaluation);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            totalCreated += appraisalsToAdd.Count;
            totalEvaluationsCreated += evaluationsToAdd.Count;

            _logger.LogInformation("Batch {batchNum}: Created {appraisalCount} appraisals and {evaluationCount} evaluations for cycle {cycleId}", 
                (i / batchSize) + 1, appraisalsToAdd.Count, evaluationsToAdd.Count, cycle.Id);
        }

        _logger.LogInformation("Successfully created {totalAppraisals} appraisal instances and {totalEvaluations} evaluator evaluations for cycle {cycleId}", 
            totalCreated, totalEvaluationsCreated, cycle.Id);
    }

    /// <summary>
    /// Generates a unique appraisal number for tracking
    /// Format: APR-{Year}-{CycleCode}-{EmployeeNumber}-{SequenceNumber}
    /// </summary>
    private string GenerateAppraisalNumber(AppraisalCycle cycle, string employeeNumber, int sequenceNumber)
    {
        return $"APR-{cycle.Year}-{cycle.CycleCode}-{employeeNumber}-{sequenceNumber:D4}";
    }

    /// <summary>
    /// Gets all employees in scope for the appraisal cycle.
    /// Uses auto-discovery (position criteria + KPI targets) with optional manual additions/exclusions.
    /// </summary>
    public async Task<IEnumerable<Guid>> GetEmployeesInScopeAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var cycle = await GetOwnedCycleAsync(cycleId);

        // Step 1: Auto-discover employees with criteria or KPI targets
        var autoDiscovered = await GetAutoDiscoveredEmployeesAsync(cycle, cancellationToken);
        _logger.LogInformation("Auto-discovered {count} employees for cycle {cycleId}", autoDiscovered.Count, cycleId);

        var finalEmployees = new HashSet<Guid>(autoDiscovered);

        // Step 2: Get manual targets with their exclusions
        var tenantId = GetTenantId();
        var targets = await _targetRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId)
            .Include(t => t.Exclusions)
            .ToListAsync(cancellationToken);

        // Step 3: Apply manual targets (inclusions)
        var manualAdditions = 0;
        foreach (var target in targets)
        {
            var employeeIds = await ResolveTargetEmployeesAsync(target, cancellationToken);
            var added = employeeIds.Count(id => finalEmployees.Add(id));
            manualAdditions += added;
        }

        if (manualAdditions > 0)
            _logger.LogInformation("Manually added {count} employees to cycle {cycleId}", manualAdditions, cycleId);

        // Step 4: Apply exclusions — removes employees regardless of which target added them.
        var allExclusions = targets.SelectMany(t => t.Exclusions).ToList();
        var beforeExclusion = finalEmployees.Count;
        await ApplyExclusionsAsync(finalEmployees, allExclusions, cancellationToken);
        var excluded = beforeExclusion - finalEmployees.Count;
        if (excluded > 0)
            _logger.LogInformation("Excluded {count} employees via target exclusion rules for cycle {cycleId}", excluded, cycleId);

        _logger.LogInformation("Final scope for cycle {cycleId}: {total} employees (auto: {auto}, added: {added}, excluded: {excluded})",
            cycleId, finalEmployees.Count, autoDiscovered.Count, manualAdditions, excluded);

        return finalEmployees;
    }

    /// <summary>
    /// Auto-discovers employees who should be included in the appraisal cycle based on
    /// positions that have an active appraisal template scoped to them.
    /// </summary>
    private async Task<HashSet<Guid>> GetAutoDiscoveredEmployeesAsync(AppraisalCycle cycle, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var employeeIds = new HashSet<Guid>();

        // Find positions that have an active position-scoped appraisal template.
        var positionsWithTemplate = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive && t.PositionId.HasValue)
            .Select(t => t.PositionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get employees in those positions
        var employeesWithTemplate = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && positionsWithTemplate.Contains(e.PositionId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in employeesWithTemplate)
            employeeIds.Add(id);

        _logger.LogDebug("Found {count} employees with a position-scoped appraisal template", employeesWithTemplate.Count);

        return employeeIds;
    }

    private async Task<IEnumerable<Guid>> ResolveTargetEmployeesAsync(AppraisalCycleTarget target, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        switch (target.TargetType)
        {
            case AppraisalTargetType.Employee:
                // EmployeeId is no longer stored on AppraisalCycleTarget (deprecated in entity redesign).
                break;

            case AppraisalTargetType.Position:
                if (target.PositionId.HasValue)
                {
                    return await _employeeRepository.GetQueryable()
                        .Where(e => e.TenantId == tenantId && e.PositionId == target.PositionId && !e.IsDeleted)
                        .Select(e => e.Id)
                        .ToListAsync(cancellationToken);
                }
                break;

            case AppraisalTargetType.OrganizationUnit:
                if (target.OrganizationUnitId.HasValue)
                {
                    var unitIds = new List<Guid> { target.OrganizationUnitId.Value };

                    // Always include child units
                    var childUnits = await GetChildUnitsAsync(target.OrganizationUnitId.Value, cancellationToken);
                    unitIds.AddRange(childUnits);

                    return await _employeeRepository.GetQueryable()
                        .Where(e => e.TenantId == tenantId
                                 && e.OrganizationUnitId.HasValue
                                 && unitIds.Contains(e.OrganizationUnitId.Value)
                                 && !e.IsDeleted)
                        .Select(e => e.Id)
                        .ToListAsync(cancellationToken);
                }
                break;

            case AppraisalTargetType.OrganizationLevel:
                if (target.OrganizationLevelId.HasValue)
                {
                    var unitsInLevel = await _organizationUnitRepository.GetQueryable()
                        .Where(u => u.TenantId == tenantId && u.OrganizationLevelId == target.OrganizationLevelId && !u.IsDeleted)
                        .Select(u => u.Id)
                        .ToListAsync(cancellationToken);

                    return await _employeeRepository.GetQueryable()
                        .Where(e => e.TenantId == tenantId
                                 && e.OrganizationUnitId.HasValue
                                 && unitsInLevel.Contains(e.OrganizationUnitId.Value)
                                 && !e.IsDeleted)
                        .Select(e => e.Id)
                        .ToListAsync(cancellationToken);
                }
                break;
        }

        return Enumerable.Empty<Guid>();
    }

    private async Task<List<Guid>> GetChildUnitsAsync(Guid parentUnitId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var childUnits = new List<Guid>();
        var directChildren = await _organizationUnitRepository.GetQueryable()
            .Where(u => u.TenantId == tenantId && u.ParentUnitId == parentUnitId && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        childUnits.AddRange(directChildren);

        foreach (var childId in directChildren)
        {
            var grandChildren = await GetChildUnitsAsync(childId, cancellationToken);
            childUnits.AddRange(grandChildren);
        }

        return childUnits;
    }

    /// <summary>
    /// Removes employees from <paramref name="employeeSet"/> that match any active exclusion rule.
    /// Exclusion rules are applied globally (an employee excluded by any target rule is removed
    /// from the final scope, regardless of which other target included them).
    /// Supports four exclusion scope types: individual Employee, Position, OrganizationUnit
    /// (including child units), and OrganizationLevel.
    /// </summary>
    private async Task ApplyExclusionsAsync(
        HashSet<Guid> employeeSet,
        IEnumerable<AppraisalCycleTargetExclusion> exclusions,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var active = exclusions.Where(e => e.IsActive && !e.IsDeleted).ToList();
        if (active.Count == 0) return;

        // The exclusion modal uses cascading scope selectors (Level → Unit → Position → Employee).
        // A single-employee exclusion therefore stores PositionId (and possibly UnitId/LevelId) as
        // navigation context alongside EmployeeId. We must honour only the most-specific scope:
        //   EmployeeId present            → direct employee exclusion only
        //   PositionId, no EmployeeId     → exclude everyone in that position
        //   UnitId, no EmployeeId/Pos     → exclude everyone in that unit (and children)
        //   LevelId only                  → exclude everyone at that org level

        // 1. Direct employee exclusions — most specific, no DB query needed.
        foreach (var ex in active.Where(e => e.EmployeeId.HasValue))
            employeeSet.Remove(ex.EmployeeId!.Value);

        // 2. Position exclusions (only when no EmployeeId — not a narrowing-selector value).
        var excludedPositionIds = active
            .Where(e => e.PositionId.HasValue && !e.EmployeeId.HasValue)
            .Select(e => e.PositionId!.Value)
            .Distinct().ToList();
        if (excludedPositionIds.Count > 0)
        {
            var ids = await _employeeRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId && excludedPositionIds.Contains(e.PositionId) && !e.IsDeleted)
                .Select(e => e.Id).ToListAsync(cancellationToken);
            foreach (var id in ids) employeeSet.Remove(id);
        }

        // 3. OrganizationUnit exclusions (only when no EmployeeId or PositionId).
        var excludedUnitIds = active
            .Where(e => e.OrganizationUnitId.HasValue && !e.EmployeeId.HasValue && !e.PositionId.HasValue)
            .Select(e => e.OrganizationUnitId!.Value)
            .Distinct().ToList();
        if (excludedUnitIds.Count > 0)
        {
            var allUnitIds = new HashSet<Guid>(excludedUnitIds);
            foreach (var unitId in excludedUnitIds)
            {
                var children = await GetChildUnitIdsForGenerationAsync(unitId, cancellationToken);
                foreach (var child in children) allUnitIds.Add(child);
            }
            var ids = await _employeeRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId
                         && e.OrganizationUnitId.HasValue
                         && allUnitIds.Contains(e.OrganizationUnitId.Value)
                         && !e.IsDeleted)
                .Select(e => e.Id).ToListAsync(cancellationToken);
            foreach (var id in ids) employeeSet.Remove(id);
        }

        // 4. OrganizationLevel exclusions (only when no more-specific scope is set).
        var excludedLevelIds = active
            .Where(e => e.OrganizationLevelId.HasValue
                     && !e.EmployeeId.HasValue
                     && !e.PositionId.HasValue
                     && !e.OrganizationUnitId.HasValue)
            .Select(e => e.OrganizationLevelId!.Value)
            .Distinct().ToList();
        if (excludedLevelIds.Count > 0)
        {
            var ids = await _employeeRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId
                         && e.OrganizationLevelId.HasValue
                         && excludedLevelIds.Contains(e.OrganizationLevelId.Value)
                         && !e.IsDeleted)
                .Select(e => e.Id).ToListAsync(cancellationToken);
            foreach (var id in ids) employeeSet.Remove(id);
        }
    }

    #region AppraisalCycleTarget Operations

    public async Task<AppraisalCycleTargetDto> AddCycleTargetAsync(Guid cycleId, CreateAppraisalCycleTargetDto createDto, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        // Validate that exactly one target type is specified
        var targetCount = new[] { createDto.OrganizationLevelId, createDto.OrganizationUnitId, createDto.PositionId }
            .Count(id => id.HasValue);

        if (targetCount != 1)
        {
            throw new InvalidOperationException("Exactly one target type must be specified (OrganizationLevel, OrganizationUnit, or Position).");
        }

        var entity = createDto.ToEntity();
        entity.AppraisalCycleId = cycleId;
        entity.TenantId = tenantId;

        await _targetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .FirstOrDefaultAsync(t => t.Id == entity.Id && t.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Cycle target added: {targetId} to cycle {cycleId}", entity!.Id, cycleId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetCycleTargetsAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var entities = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId)
            .OrderBy(t => t.TargetType)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleTargetDto> UpdateCycleTargetAsync(Guid cycleId, UpdateAppraisalCycleTargetDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleTargetAsync(cycleId, updateDto.Id);

        // Validate that exactly one target type is specified
        var targetCount = new[] { updateDto.OrganizationLevelId, updateDto.OrganizationUnitId, updateDto.PositionId }
            .Count(id => id.HasValue);

        if (targetCount != 1)
        {
            throw new InvalidOperationException("Exactly one target type must be specified.");
        }

        updateDto.UpdateEntity(entity);

        await _targetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle target updated: {targetId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> RemoveCycleTargetAsync(Guid cycleId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleTargetAsync(cycleId, targetId);

        await _targetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle target removed: {targetId} from cycle {cycleId}", targetId, cycleId);

        return true;
    }

    #endregion
}

#endregion Appraisal Cycle

