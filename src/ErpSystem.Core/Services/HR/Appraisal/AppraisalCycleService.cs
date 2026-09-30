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

#region Appraisal Cycle

public class AppraisalCycleService : IAppraisalCycleService
{
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<AppraisalCycleTarget> _targetRepository;
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<OrganizationUnit> _organizationUnitRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IGenericRepository<AppraisalReviewEvent> _reviewEventRepository;
    private readonly IGenericRepository<CheckIn> _checkInRepository;
    private readonly IEffectiveAppraisalConfigurationService _effectiveConfigService;
    private readonly IAppraisalNotificationService _notificationService;
    private readonly IAppraisalLifecycleService _lifecycle;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleService> _logger;

    public AppraisalCycleService(
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<AppraisalCycleTarget> targetRepository,
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<OrganizationUnit> organizationUnitRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalSettings> settingsRepository,
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository,
        IGenericRepository<AppraisalReviewEvent> reviewEventRepository,
        IGenericRepository<CheckIn> checkInRepository,
        IEffectiveAppraisalConfigurationService effectiveConfigService,
        IAppraisalNotificationService notificationService,
        IAppraisalLifecycleService lifecycle,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleService> logger)
    {
        _cycleRepository = cycleRepository;
        _targetRepository = targetRepository;
        _cycleTemplateRepository = cycleTemplateRepository;
        _employeeRepository = employeeRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _appraisalRepository = appraisalRepository;
        _settingsRepository = settingsRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _reviewEventRepository = reviewEventRepository;
        _checkInRepository = checkInRepository;
        _effectiveConfigService = effectiveConfigService;
        _notificationService = notificationService;
        _lifecycle = lifecycle;
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

    /// <summary>Who the cycle's active targets reach, through the one scope rule (E-c).</summary>
    private Task<AppraisalCycleScopeResolution> ResolveScopeAsync(Guid cycleId, CancellationToken cancellationToken)
        => AppraisalCycleScope.ResolveCycleAsync(
            _targetRepository.GetQueryable(), _employeeRepository.GetQueryable(), _organizationUnitRepository.GetQueryable(),
            GetTenantId(), cycleId, cancellationToken);

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

        // Review events (Custom frequency / generated). A withdrawn appraisal's open events are not
        // on the calendar: no one will hold them (performance closure E-d1).
        var reviewEvents = await _reviewEventRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AppraisalCycleId == cycleId
                        && (r.Appraisal.Status != AppraisalStatus.Withdrawn || r.Status == AppraisalReviewStatus.Completed))
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
        if (entity.ClosedDate.HasValue || entity.Status == AppraisalCycleStatus.Closed)
        {
            throw new InvalidOperationException("Cannot update a closed appraisal cycle.");
        }

        // Validate dates
        if (updateDto.EndDate <= updateDto.StartDate)
        {
            throw new InvalidOperationException("End date must be after start date.");
        }

        // What a running cycle keeps (performance closure E-c). Once it is opened, or has appraisals, its
        // settings profile is the rulebook they run by, and its year and type are what the open's overlap
        // check was made on; once it has appraisals, its period is the one they were generated with. The
        // name, the code and the phase deadlines stay HR's to move — the gates read the deadlines live.
        var hasAppraisals = await _appraisalRepository.GetQueryable()
            .AnyAsync(a => a.TenantId == tenantId && a.AppraisalCycleId == entity.Id, cancellationToken);
        if (entity.OpenedDate.HasValue || entity.Status != AppraisalCycleStatus.Draft || hasAppraisals)
        {
            var fixedFields = new List<string>();
            if (updateDto.AppraisalSettingsId != entity.AppraisalSettingsId) fixedFields.Add("settings profile");
            if (updateDto.Year != entity.Year) fixedFields.Add("year");
            if (updateDto.AppraisalType != entity.AppraisalType) fixedFields.Add("type");
            if (fixedFields.Count > 0)
                throw new InvalidOperationException(
                    $"The cycle's {JoinAnd(fixedFields)} cannot change once it has been opened or has appraisals.");
        }
        if (hasAppraisals && (updateDto.StartDate != entity.StartDate || updateDto.EndDate != entity.EndDate))
        {
            throw new InvalidOperationException(
                "The cycle's period cannot change once appraisals have been generated: each carries the dates it was generated with.");
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

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string JoinAnd(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    /// <summary>
    /// Deletes a cycle set up in error (performance closure E-d2a, D-57): never opened, and with
    /// nothing but its configuration pointing at it. Its targets (with their exclusions) and its
    /// template links go with it.
    /// </summary>
    /// <remarks>
    /// It refused only an opened cycle and soft-deleted the cycle row alone, so a Draft cycle's
    /// appraisals, goals, calibration sessions and check-ins — generation and goal setting ran on
    /// Drafts — were left pointing at a hidden cycle, with its targets and template links.
    /// </remarks>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleAsync(id);
        var tenantId = GetTenantId();

        // Check if cycle has been opened
        if (entity.OpenedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot delete an appraisal cycle that has been opened.");
        }

        // The work that points at the cycle — withdrawn appraisals included: they are records.
        var work = new List<string>();
        async Task CountAsync<T>(string label, System.Linq.Expressions.Expression<Func<T, bool>> belongs)
            where T : ErpSystem.Core.Entities.BaseEntity
        {
            var n = await _unitOfWork.Repository<T>().GetQueryable().Where(belongs).CountAsync(cancellationToken);
            if (n > 0) work.Add($"{n} {label}");
        }
        await CountAsync<PerformanceAppraisal>("appraisal(s)", a => a.TenantId == tenantId && a.AppraisalCycleId == id);
        await CountAsync<EmployeeGoal>("employee goal(s)", g => g.TenantId == tenantId && g.AppraisalCycleId == id);
        await CountAsync<UnitGoal>("unit goal(s)", g => g.TenantId == tenantId && g.AppraisalCycleId == id);
        await CountAsync<CompanyGoal>("company goal(s)", g => g.TenantId == tenantId && g.AppraisalCycleId == id);
        await CountAsync<CalibrationSession>("calibration session(s)", s => s.TenantId == tenantId && s.AppraisalCycleId == id);
        await CountAsync<CheckIn>("check-in(s)", c => c.TenantId == tenantId && c.AppraisalCycleId == id);
        await CountAsync<PerformanceJournalEntry>("journal entr(ies)", j => j.TenantId == tenantId && j.AppraisalCycleId == id);
        await CountAsync<AppraisalReviewEvent>("review event(s)", r => r.TenantId == tenantId && r.AppraisalCycleId == id);
        await CountAsync<EmployeeDevelopmentPlan>("development plan(s)", p => p.TenantId == tenantId && p.AppraisalCycleId == id);

        if (work.Count > 0)
            throw new InvalidOperationException(
                $"This cycle cannot be deleted: {JoinAnd(work)} point at it. A cycle is deleted only while " +
                "nothing but its targets and templates does.");

        // Its configuration goes with it: the targets (and their exclusions) and the template links.
        var targets = await _targetRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == id)
            .ToListAsync(cancellationToken);
        var targetIds = targets.Select(t => t.Id).ToList();
        var exclusions = await _unitOfWork.Repository<AppraisalCycleTargetExclusion>().GetQueryable()
            .Where(x => x.TenantId == tenantId && targetIds.Contains(x.AppraisalCycleTargetId))
            .ToListAsync(cancellationToken);
        var templateLinks = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ct.AppraisalCycleId == id)
            .ToListAsync(cancellationToken);

        foreach (var exclusion in exclusions)
            await _unitOfWork.Repository<AppraisalCycleTargetExclusion>().DeleteAsync(exclusion);
        foreach (var target in targets)
            await _targetRepository.DeleteAsync(target);
        foreach (var link in templateLinks)
            await _cycleTemplateRepository.DeleteAsync(link);

        await _cycleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appraisal cycle deleted: {cycleId}, with {targets} target(s), {exclusions} exclusion(s) and {links} template link(s)",
            id, targets.Count, exclusions.Count, templateLinks.Count);

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
        // open cycle of the same type and year. Skipped while the targets reach nobody yet.
        var thisScope = await ResolveScopeAsync(entity.Id, cancellationToken);
        if (thisScope.InScope.Count > 0)
        {
            // Only a cycle that is actually running reserves its people. A Draft appraises
            // nobody, may never be opened at all, and blocking on one forced the user to go
            // and delete somebody else's half-finished cycle before they could open theirs.
            // Draft overlaps are still reported — as an advisory on the coverage preview —
            // so the early warning survives without the hard block.
            var siblingCycles = await _cycleRepository.GetQueryable()
                .Where(c => c.TenantId == tenantId &&
                            c.Id != entity.Id &&
                            c.AppraisalType == entity.AppraisalType &&
                            c.Year == entity.Year &&
                            c.Status == AppraisalCycleStatus.Open)
                .ToListAsync(cancellationToken);

            var conflictDescriptions = new List<string>();
            foreach (var sibling in siblingCycles)
            {
                var siblingScope = await ResolveScopeAsync(sibling.Id, cancellationToken);
                var overlapCount = thisScope.InScope.Count(siblingScope.InScope.Contains);
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

        // Only a running cycle has deadlines to chase (performance closure E-d2a): a Closed one was
        // reminded as readily as an Open one, and the analytics page offered it.
        if (cycle.Status != AppraisalCycleStatus.Open)
            throw new InvalidOperationException(
                cycle.Status == AppraisalCycleStatus.Closed
                    ? "Reminders are sent for an open cycle, and this one is closed."
                    : "Reminders can only be sent for a cycle that has been opened.");

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
    /// Scope comes from <see cref="AppraisalCycleScope"/>, the rule the coverage preview reads.
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

        // ── 2. Resolve employees from targets (the one scope rule the coverage preview reads) ──
        var activeTargets = await AppraisalCycleScope.LoadActiveTargetsAsync(
            _targetRepository.GetQueryable(), tenantId, cycleId, cancellationToken);

        if (!activeTargets.Any())
            throw new InvalidOperationException("No active targets are configured for this cycle. Add at least one target before generating.");

        var scope = await AppraisalCycleScope.ResolveAsync(
            activeTargets, _employeeRepository.GetQueryable(), _organizationUnitRepository.GetQueryable(), tenantId, cancellationToken);
        if (scope.Excluded.Count > 0)
            _logger.LogInformation("Excluded {count} employees via target exclusion rules", scope.Excluded.Count);

        var employeeIds = scope.InScope;
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
                        evaluationsToAdd.Add(CreateEvaluation(appraisal, employee.Id, EvaluatorRole.Self, settings.SelfEvaluationWeight, cycle, generatedById));

                    if (settings.RequireManagerEvaluation)
                    {
                        if (employee.ManagerId.HasValue)
                            evaluationsToAdd.Add(CreateEvaluation(appraisal, employee.ManagerId.Value, EvaluatorRole.Manager, settings.ManagerEvaluationWeight, cycle, generatedById));
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
        decimal weight, AppraisalCycle cycle, Guid createdById)
    {
        return new EvaluatorEvaluation
        {
            Id = Guid.NewGuid(),
            AppraisalId = appraisal.Id,
            EvaluatorId = evaluatorId,
            EvaluatorRole = role,
            EvaluatorWeight = weight,
            TenantId = cycle.TenantId,
            CreatedBy = createdById.ToString(),
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Closes a cycle once its work is done (performance closure E-d2a, D-56): every appraisal
    /// finished — Completed, Closed or Withdrawn — and every appeal window lapsed. The Completed
    /// appraisals close with it, through the lifecycle; one with no score closes as it stands.
    /// </summary>
    /// <remarks>
    /// It read no appraisal: it closed a cycle whatever was in it (26 of UAT's 34 Closed cycles hold
    /// unfinished ones), cut every open appeal window, and left the Completed appraisals Completed —
    /// only the raw status routes ever moved one to Closed.
    /// </remarks>
    public async Task<bool> CloseCycleAsync(CloseAppraisalCycleDto closeDto, Guid closedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCycleAsync(closeDto.CycleId);
        var tenantId = GetTenantId();

        if (!entity.OpenedDate.HasValue)
        {
            throw new InvalidOperationException("Cannot close a cycle that hasn't been opened.");
        }

        if (entity.ClosedDate.HasValue)
        {
            throw new InvalidOperationException("Appraisal cycle is already closed.");
        }

        // Tracked: the Completed ones are closed below, in the same save as the cycle.
        var appraisals = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == entity.Id)
            .ToListAsync(cancellationToken);

        var unfinished = appraisals
            .Where(a => a.Status is not (AppraisalStatus.Completed or AppraisalStatus.Closed or AppraisalStatus.Withdrawn))
            .GroupBy(a => a.Status)
            .OrderBy(g => (int)g.Key)
            .Select(g => $"{g.Count()} {UnfinishedWord(g.Key)}")
            .ToList();
        if (unfinished.Count > 0)
            throw new InvalidOperationException(
                $"This cycle cannot be closed while appraisals are unfinished: {JoinAnd(unfinished)}. " +
                "Each is completed — or withdrawn, if it will not be — before the cycle closes.");

        var completed = appraisals.Where(a => a.Status == AppraisalStatus.Completed).ToList();

        // An appeal window still open is the employee's to use: the close waits for the last one.
        if (completed.Count > 0)
        {
            var states = await _lifecycle.GetStatesAsync(completed.Select(a => a.Id).ToList(), cancellationToken);
            var now = DateTime.UtcNow;
            var windows = states.Values
                .Select(s => AppraisalGates.CanFileAppeal(s.Facts, s.Settings, now))
                .Where(w => w.Allowed)
                .ToList();
            if (windows.Count > 0)
            {
                var lastDay = windows.Max(w => w.LastDay);
                throw new InvalidOperationException(
                    $"This cycle cannot be closed while {windows.Count} completed appraisal(s) are inside their appeal " +
                    $"window: the last one closes on {lastDay:d MMM yyyy}.");
            }
        }

        foreach (var appraisal in completed)
        {
            AppraisalLifecycle.EnsureTransition(appraisal.Status, AppraisalStatus.Closed);
            appraisal.Status = AppraisalStatus.Closed;
        }

        entity.ClosedById = closedById;
        entity.ClosedDate = DateTime.UtcNow;
        entity.Status = AppraisalCycleStatus.Closed;

        await _cycleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appraisal cycle closed: {cycleId} by user {userId}; {closed} completed appraisal(s) closed with it ({scoreless} with no score)",
            closeDto.CycleId, closedById, completed.Count, completed.Count(a => a.OverallScore == null));

        return true;
    }

    /// <summary>How the close's refusal names an unfinished status.</summary>
    private static string UnfinishedWord(AppraisalStatus status) => status switch
    {
        AppraisalStatus.Draft => "not started (Draft)",
        AppraisalStatus.Active => "in progress (Active)",
        AppraisalStatus.Governance => "in governance",
        AppraisalStatus.Appealed => "under appeal",
        _ => status.ToString(),
    };

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

        // The cycle's appraisals. A withdrawn one is out of the cycle (performance closure E-d1): it
        // leaves the phase, every progress denominator and the bottlenecks, and is counted on its
        // own. Its unsubmitted evaluations read as work no one had started.
        var all = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);
        var appraisals = all.Where(a => a.Status != AppraisalStatus.Withdrawn).ToList();

        var appraisalIds = appraisals.Select(a => a.Id).ToList();

        var evaluations = await _evaluatorEvaluationRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && appraisalIds.Contains(e.AppraisalId))
            .ToListAsync(cancellationToken);

        // Calculate progress metrics
        var totalAppraisals = appraisals.Count;
        var scope = await ResolveScopeAsync(cycleId, cancellationToken);

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
            CurrentPhase = await DetermineCurrentPhaseAsync(appraisals, cancellationToken),

            // Progress metrics
            SelfEvaluationProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.Self, totalAppraisals, settings?.RequireSelfEvaluation ?? true),
            PeerEvaluationProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.Peer, totalAppraisals, settings?.RequirePeerReviews ?? false),
            ManagerEvaluationProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.Manager, totalAppraisals, settings?.RequireManagerEvaluation ?? true),
            HRReviewProgress = CalculateEvaluationProgress(evaluations, EvaluatorRole.HR, totalAppraisals, settings?.RequireHRReview ?? false),

            // Participation coverage. Targeted: the staff the active targets reach, less those an
            // exclusion leaves out — the scope, which read the appraisal count (APC2026: 107 against
            // a scope of 102, E-d1). Excluded: those an exclusion leaves out (it was always 0).
            // Appraisals: the ones in play; Withdrawn: those taken out of the cycle.
            TotalEmployeesTargeted = scope.InScope.Count,
            TotalEmployeesExcluded = scope.Excluded.Count,
            TotalAppraisals = totalAppraisals,
            TotalWithdrawn = all.Count - totalAppraisals,
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

    /// <summary>
    /// Where most of the cycle's appraisals are: the step the gates put the largest number at, the
    /// earlier step on a tie (B1). It was read off the calendar — the first deadline not yet passed —
    /// so a cycle whose staff were all still setting goals read "Self Evaluation", and a cycle with
    /// no deadlines set read "Completed" from the day it opened.
    /// </summary>
    private async Task<string> DetermineCurrentPhaseAsync(
        IReadOnlyCollection<PerformanceAppraisal> appraisals, CancellationToken cancellationToken)
    {
        var inPlay = appraisals.Where(a => a.Status != AppraisalStatus.Withdrawn).Select(a => a.Id).ToList();
        var states = await _lifecycle.GetStatesAsync(inPlay, cancellationToken);
        if (states.Count == 0) return "Not started";

        var modal = states.Values
            .GroupBy(s => AppraisalGates.StepOf(s.SubStatus))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => (int)g.Key)
            .First()
            .Key;

        return AppraisalGates.Label(modal);
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
    /// Generates a unique appraisal number for tracking
    /// Format: APR-{Year}-{CycleCode}-{EmployeeNumber}-{SequenceNumber}
    /// </summary>
    private string GenerateAppraisalNumber(AppraisalCycle cycle, string employeeNumber, int sequenceNumber)
    {
        return $"APR-{cycle.Year}-{cycle.CycleCode}-{employeeNumber}-{sequenceNumber:D4}";
    }

    /// <summary>
    /// Who the cycle appraises — the people generation would create an appraisal for: active staff its
    /// active targets cover, less those an exclusion leaves out (<see cref="AppraisalCycleScope"/>). The
    /// open notice and the deadline reminders are addressed to them. It also took in everyone holding a
    /// post any active template was scoped to, tenant-wide, and read inactive targets and leavers
    /// (performance closure E-c).
    ///
    /// <para>Less anyone whose appraisal in the cycle was withdrawn (E-d1): the cycle no longer
    /// appraises them, and every phase reminder still reached them.</para>
    /// </summary>
    public async Task<IEnumerable<Guid>> GetEmployeesInScopeAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var scope = await ResolveScopeAsync(cycleId, cancellationToken);

        var tenantId = GetTenantId();
        var withdrawn = (await _appraisalRepository.GetQueryable()
                .Where(a => a.TenantId == tenantId && a.AppraisalCycleId == cycleId && a.Status == AppraisalStatus.Withdrawn)
                .Select(a => a.EmployeeId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return scope.InScope.Where(id => !withdrawn.Contains(id)).ToList();
    }
}

#endregion Appraisal Cycle

