using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementCalendarService : IProcurementCalendarService, IProcurementCalendarProcessor
{
    private const string SourceType = "ProcurementCalendar";
    private const string EventType = "ProcurementCalendarLifecycle";
    private const string ViewPermission = "procurement.calendar.view";
    private const string ManagePermission = "procurement.calendar.manage";
    private const string RunPermission = "procurement.calendar.run";
    private const string ActionUrl = "/procurement/planning/calendar";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationService _notifications;
    private readonly ILogger<ProcurementCalendarService> _logger;

    public ProcurementCalendarService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        INotificationService notifications,
        ILogger<ProcurementCalendarService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    private IGenericRepository<ProcurementCalendarProfile> Profiles => _unitOfWork.Repository<ProcurementCalendarProfile>();
    private IGenericRepository<ProcurementCalendarRule> Rules => _unitOfWork.Repository<ProcurementCalendarRule>();
    private IGenericRepository<ProcurementCalendarOccurrence> Occurrences => _unitOfWork.Repository<ProcurementCalendarOccurrence>();
    private IGenericRepository<ProcurementCalendarRun> Runs => _unitOfWork.Repository<ProcurementCalendarRun>();
    private IGenericRepository<ProcurementResponsibilityAssignment> Assignments => _unitOfWork.Repository<ProcurementResponsibilityAssignment>();

    public async Task<ProcurementCalendarSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ViewPermission, "SUMMARY", string.Empty, cancellationToken);
        var tenantId = TenantId;
        var open = new[]
        {
            ProcurementCalendarOccurrenceStatus.Upcoming,
            ProcurementCalendarOccurrenceStatus.Due,
            ProcurementCalendarOccurrenceStatus.Acknowledged,
            ProcurementCalendarOccurrenceStatus.Escalated
        };
        var occurrenceQuery = Occurrences.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted);
        return new ProcurementCalendarSummaryDto
        {
            ProfileFamilies = await Profiles.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => item.ProfileKey).Distinct().CountAsync(cancellationToken),
            DraftProfiles = await Profiles.CountAsync(item => item.TenantId == tenantId && item.Status == ProcurementCalendarProfileStatus.Draft),
            PublishedProfiles = await Profiles.CountAsync(item => item.TenantId == tenantId && item.Status == ProcurementCalendarProfileStatus.Published),
            OpenTasks = await occurrenceQuery.CountAsync(item => open.Contains(item.Status), cancellationToken),
            DueTasks = await occurrenceQuery.CountAsync(item => item.Status == ProcurementCalendarOccurrenceStatus.Due, cancellationToken),
            EscalatedTasks = await occurrenceQuery.CountAsync(item => item.Status == ProcurementCalendarOccurrenceStatus.Escalated, cancellationToken),
            CompletedTasks = await occurrenceQuery.CountAsync(item => item.Status == ProcurementCalendarOccurrenceStatus.Completed, cancellationToken),
            NextDueAtUtc = await occurrenceQuery.Where(item => open.Contains(item.Status) && item.DueAtUtc >= DateTime.UtcNow)
                .OrderBy(item => item.DueAtUtc).Select(item => (DateTime?)item.DueAtUtc).FirstOrDefaultAsync(cancellationToken),
            LastRunAtUtc = await Runs.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderByDescending(item => item.StartedAtUtc).Select(item => (DateTime?)item.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
        };
    }

    public async Task<IReadOnlyList<ProcurementCalendarProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ViewPermission, "PROFILES", string.Empty, cancellationToken);
        var rows = await Profiles.GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.Rules.Where(rule => !rule.IsDeleted)).AsNoTracking()
            .OrderBy(item => item.ProfileCode).ThenByDescending(item => item.Version).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var effectiveIds = rows
            .Where(item => item.Status == ProcurementCalendarProfileStatus.Published && item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now))
            .GroupBy(item => item.ProfileKey)
            .Select(group => group.OrderByDescending(item => item.Version).First().Id)
            .ToHashSet();
        return rows.Select(item => MapProfile(item, effectiveIds.Contains(item.Id))).ToList();
    }

    public async Task<ProcurementCalendarProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ViewPermission, id.ToString(), string.Empty, cancellationToken);
        return await MapProfileWithEffectiveStateAsync(await LoadProfileAsync(id, false, cancellationToken), cancellationToken);
    }

    public async Task<ProcurementCalendarProfileDto> CreateProfileAsync(
        SaveProcurementCalendarProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ManagePermission, request.ProfileCode, correlationId, cancellationToken);
        ValidateRequest(request, requireAllEvents: false);
        var code = NormalizeCode(request.ProfileCode);
        if (await Profiles.GetQueryableIncludingDeleted(item => item.TenantId == TenantId && item.ProfileCode == code)
                .AnyAsync(cancellationToken))
            throw new ProcurementCalendarConflictException("A calendar profile family with this code already exists. Clone its latest version instead.");
        var now = DateTime.UtcNow;
        var entity = new ProcurementCalendarProfile
        {
            TenantId = TenantId,
            ProfileKey = Guid.NewGuid(),
            ProfileCode = code,
            Version = 1,
            Status = ProcurementCalendarProfileStatus.Draft,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            CreatedAt = now
        };
        ApplyRequest(entity, request);
        ApplyRules(entity, request.Rules, []);
        await Profiles.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(entity, "Created", request.ChangeSummary, null, Snapshot(entity), correlationId, cancellationToken);
        return await MapProfileWithEffectiveStateAsync(await LoadProfileAsync(entity.Id, false, cancellationToken), cancellationToken);
    }

    public async Task<ProcurementCalendarProfileDto> UpdateProfileAsync(
        Guid id, SaveProcurementCalendarProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ManagePermission, id.ToString(), correlationId, cancellationToken);
        ValidateRequest(request, requireAllEvents: false);
        var entity = await LoadProfileAsync(id, true, cancellationToken);
        EnsureDraft(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        if (!string.Equals(entity.ProfileCode, NormalizeCode(request.ProfileCode), StringComparison.Ordinal))
            throw new ProcurementCalendarValidationException("PROFILE_CODE_IMMUTABLE", "ProfileCode is immutable within a calendar family.");
        var before = Snapshot(entity);
        var existing = entity.Rules.Where(item => !item.IsDeleted).ToList();
        await Rules.HardDeleteRangeAsync(existing);
        entity.Rules.Clear();
        ApplyRequest(entity, request);
        ApplyRules(entity, request.Rules, existing);
        Touch(entity);
        await Profiles.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(entity, "Updated", request.ChangeSummary, before, Snapshot(entity), correlationId, cancellationToken);
        return await MapProfileWithEffectiveStateAsync(await LoadProfileAsync(entity.Id, false, cancellationToken), cancellationToken);
    }

    public async Task<ProcurementCalendarProfileDto> CloneProfileAsync(
        Guid id, CloneProcurementCalendarProfileRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ManagePermission, id.ToString(), correlationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.ChangeSummary))
            throw new ProcurementCalendarValidationException("CHANGE_SUMMARY_REQUIRED", "ChangeSummary is required.");
        ValidatePeriod(request.EffectiveFromUtc, request.EffectiveToUtc);
        var source = await LoadProfileAsync(id, false, cancellationToken);
        if (source.Status == ProcurementCalendarProfileStatus.Draft)
            throw new ProcurementCalendarConflictException("Clone a Published or Retired calendar profile, not a Draft.");
        var version = await Profiles.GetQueryableIncludingDeleted(item => item.TenantId == TenantId && item.ProfileKey == source.ProfileKey)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? 0;
        var draftExists = await Profiles.ExistsAsync(item => item.TenantId == TenantId && item.ProfileKey == source.ProfileKey &&
            item.Status == ProcurementCalendarProfileStatus.Draft);
        if (draftExists) throw new ProcurementCalendarConflictException("This calendar family already has a Draft version.");
        var clone = new ProcurementCalendarProfile
        {
            TenantId = TenantId,
            ProfileKey = source.ProfileKey,
            ProfileCode = source.ProfileCode,
            Name = source.Name,
            Description = source.Description,
            Version = version + 1,
            Status = ProcurementCalendarProfileStatus.Draft,
            TimeZoneId = source.TimeZoneId,
            GenerationHorizonDays = source.GenerationHorizonDays,
            CatchUpDays = source.CatchUpDays,
            EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc),
            EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null,
            ChangeSummary = request.ChangeSummary.Trim(),
            SupersedesProfileId = source.Id,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        ApplyRules(clone, source.Rules.Where(item => !item.IsDeleted).Select(item => new ProcurementCalendarRuleRequest
        {
            RuleKey = item.RuleKey,
            EventType = item.EventType,
            Title = item.Title,
            Description = item.Description,
            DueMonth = item.DueMonth,
            DueDay = item.DueDay,
            DueLocalTime = item.DueLocalTime,
            ReminderLeadDays = item.ReminderLeadDays,
            EscalationAfterDays = item.EscalationAfterDays,
            OwnerUserId = item.OwnerUserId,
            OwnerRoleName = item.OwnerRoleName,
            EscalationUserId = item.EscalationUserId,
            EscalationRoleName = item.EscalationRoleName,
            StatutoryReference = item.StatutoryReference,
            IsEnabled = item.IsEnabled
        }).ToList(), []);
        await Profiles.AddAsync(clone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(clone, "Cloned", request.ChangeSummary, Snapshot(source), Snapshot(clone), correlationId, cancellationToken);
        return await MapProfileWithEffectiveStateAsync(await LoadProfileAsync(clone.Id, false, cancellationToken), cancellationToken);
    }

    public async Task<ProcurementCalendarProfileDto> PublishProfileAsync(
        Guid id, ProcurementCalendarLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ManagePermission, id.ToString(), correlationId, cancellationToken);
        var entity = await LoadProfileAsync(id, true, cancellationToken);
        EnsureDraft(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureReason(request.Reason);
        if (string.IsNullOrWhiteSpace(request.ApprovalReference))
            throw new ProcurementCalendarValidationException("APPROVAL_REFERENCE_REQUIRED",
                "An approved decision, workflow, or evidence reference is required before statutory dates can be published.");
        if (entity.CreatedById == _currentUser.UserId)
            throw new ProcurementCalendarAuthorizationException("The Draft creator cannot publish the same calendar version.");
        var issues = await ValidateForPublicationAsync(entity, cancellationToken);
        if (issues.Count != 0)
            throw new ProcurementCalendarValidationException(issues[0].Code, issues[0].Message);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementCalendarProfileStatus.Published;
        entity.ApprovalReference = request.ApprovalReference.Trim();
        entity.PublishedAtUtc = now;
        entity.PublishedById = _currentUser.UserId;
        entity.PublishedByName = ActorName;
        Touch(entity, now);
        await Profiles.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(entity, "Published", request.Reason, before, Snapshot(entity), correlationId, cancellationToken);
        await ProcessTenantAsync(TenantId, now, entity.GenerationHorizonDays, ProcurementCalendarRunTrigger.Publication,
            _currentUser.UserId, ActorName, request.Reason, correlationId, cancellationToken);
        return await MapProfileWithEffectiveStateAsync(await LoadProfileAsync(entity.Id, false, cancellationToken), cancellationToken);
    }

    public async Task<ProcurementCalendarProfileDto> RetireProfileAsync(
        Guid id, ProcurementCalendarLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ManagePermission, id.ToString(), correlationId, cancellationToken);
        var entity = await LoadProfileAsync(id, true, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureReason(request.Reason);
        if (entity.Status != ProcurementCalendarProfileStatus.Published)
            throw new ProcurementCalendarConflictException("Only a Published calendar profile can be retired.");
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementCalendarProfileStatus.Retired;
        entity.RetiredAtUtc = now;
        entity.RetiredById = _currentUser.UserId;
        entity.RetiredByName = ActorName;
        Touch(entity, now);
        await Profiles.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(entity, "Retired", request.Reason, before, Snapshot(entity), correlationId, cancellationToken);
        return await MapProfileWithEffectiveStateAsync(await LoadProfileAsync(entity.Id, false, cancellationToken), cancellationToken);
    }

    public async Task DeleteDraftAsync(Guid id, ProcurementCalendarLifecycleRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ManagePermission, id.ToString(), correlationId, cancellationToken);
        var entity = await LoadProfileAsync(id, true, cancellationToken);
        EnsureDraft(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureReason(request.Reason);
        var before = Snapshot(entity);
        foreach (var rule in entity.Rules.Where(item => !item.IsDeleted)) await Rules.DeleteAsync(rule);
        await Profiles.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(entity, "DraftDeleted", request.Reason, before, new { entity.Id, IsDeleted = true }, correlationId, cancellationToken);
    }

    public async Task<ProcurementCalendarOccurrencePageDto> SearchOccurrencesAsync(
        ProcurementCalendarOccurrenceSearchRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ViewPermission, "OCCURRENCES", string.Empty, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = Occurrences.GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.Profile).Include(item => item.Rule).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.Title.Contains(search) || item.OccurrenceKey.Contains(search) ||
                item.OwnerName.Contains(search) || item.StatutoryReference.Contains(search));
        }
        if (request.EventType.HasValue) query = query.Where(item => item.EventType == request.EventType);
        if (request.Status.HasValue) query = query.Where(item => item.Status == request.Status);
        if (request.OwnerUserId.HasValue) query = query.Where(item => item.OwnerUserId == request.OwnerUserId);
        if (request.FromUtc.HasValue) query = query.Where(item => item.DueAtUtc >= EnsureUtc(request.FromUtc.Value));
        if (request.ToUtc.HasValue) query = query.Where(item => item.DueAtUtc <= EnsureUtc(request.ToUtc.Value));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.DueAtUtc).ThenBy(item => item.Title)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new ProcurementCalendarOccurrencePageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(MapOccurrence).ToList()
        };
    }

    public async Task<ProcurementCalendarOccurrenceDto> GetOccurrenceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ViewPermission, id.ToString(), string.Empty, cancellationToken);
        return MapOccurrence(await LoadOccurrenceAsync(id, false, cancellationToken));
    }

    public Task<ProcurementCalendarOccurrenceDto> AcknowledgeAsync(Guid id, ProcurementCalendarOccurrenceActionRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        ActAsync(id, request, "Acknowledged", ProcurementCalendarOccurrenceStatus.Acknowledged, false, correlationId, cancellationToken);

    public Task<ProcurementCalendarOccurrenceDto> CompleteAsync(Guid id, ProcurementCalendarOccurrenceActionRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        ActAsync(id, request, "Completed", ProcurementCalendarOccurrenceStatus.Completed, false, correlationId, cancellationToken);

    public Task<ProcurementCalendarOccurrenceDto> CancelAsync(Guid id, ProcurementCalendarOccurrenceActionRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        ActAsync(id, request, "Cancelled", ProcurementCalendarOccurrenceStatus.Cancelled, true, correlationId, cancellationToken);

    public async Task<ProcurementCalendarRunDto> RunAsync(ProcurementCalendarRunRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(RunPermission, "MANUAL-RUN", correlationId, cancellationToken);
        EnsureReason(request.Reason);
        return await ProcessTenantAsync(TenantId, request.EvaluationAtUtc ?? DateTime.UtcNow, request.HorizonDays,
            ProcurementCalendarRunTrigger.Manual, _currentUser.UserId, ActorName, request.Reason, correlationId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementCalendarRunDto>> GetRunsAsync(int take = 50,
        CancellationToken cancellationToken = default)
    {
        await EnsureCapabilityAsync(ViewPermission, "RUNS", string.Empty, cancellationToken);
        return (await Runs.GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted).AsNoTracking()
            .OrderByDescending(item => item.StartedAtUtc).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken))
            .Select(MapRun).ToList();
    }

    public IReadOnlyList<string> GetTimeZones() => TimeZoneInfo.GetSystemTimeZones().Select(item => item.Id).ToList();

    public async Task<ProcurementCalendarRunDto> ProcessTenantAsync(
        Guid tenantId, DateTime evaluationAtUtc, int? horizonDays, ProcurementCalendarRunTrigger trigger,
        Guid? requestedById, string requestedByName, string reason, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ProcurementCalendarValidationException("TENANT_REQUIRED", "Tenant is required.");
        var now = EnsureUtc(evaluationAtUtc);
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var runKey = trigger == ProcurementCalendarRunTrigger.Scheduled
            ? $"calendar:{tenantId:N}:scheduled:{now:yyyyMMddHH}"
            : $"calendar:{tenantId:N}:{trigger.ToString().ToLowerInvariant()}:{normalizedCorrelation}";
        var existingRun = await Runs.GetQueryableIncludingDeleted(item => item.TenantId == tenantId && item.RunKey == runKey && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingRun?.Status is ProcurementCalendarRunStatus.Completed or ProcurementCalendarRunStatus.CompletedWithWarnings)
        {
            await RecordRunControlEventAsync(existingRun, tenantId, requestedById, reason, normalizedCorrelation,
                cancellationToken);
            return MapRun(existingRun);
        }

        var profiles = await Profiles.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == ProcurementCalendarProfileStatus.Published && item.EffectiveFromUtc <= now)
            .Include(item => item.Rules.Where(rule => !rule.IsDeleted && rule.IsEnabled)).AsNoTracking()
            .OrderByDescending(item => item.Version).ToListAsync(cancellationToken);
        var maxCatchUp = profiles.Count == 0 ? 0 : profiles.Max(item => item.CatchUpDays);
        var maxHorizon = horizonDays ?? (profiles.Count == 0 ? 365 : profiles.Max(item => item.GenerationHorizonDays));
        var run = existingRun ?? new ProcurementCalendarRun
        {
            TenantId = tenantId,
            RunKey = runKey,
            Trigger = trigger,
            RequestedById = requestedById,
            RequestedByName = Truncate(requestedByName, 300),
            AttemptCount = 0
        };
        run.Status = ProcurementCalendarRunStatus.Running;
        run.AttemptCount++;
        run.EvaluationAtUtc = now;
        run.WindowStartUtc = now.AddDays(-maxCatchUp);
        run.WindowEndUtc = now.AddDays(Math.Clamp(maxHorizon, 1, 730));
        run.StartedAtUtc = DateTime.UtcNow;
        run.CompletedAtUtc = null;
        run.ProfilesEvaluated = profiles.Count;
        run.RulesEvaluated = 0;
        run.CreatedCount = run.RescheduledCount = run.ReminderCount = run.DueCount = run.EscalationCount = run.FailedCount = 0;
        run.ErrorSummary = null;
        if (existingRun is null) await Runs.AddAsync(run); else await Runs.UpdateAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var errors = new List<string>();
        try
        {
            var candidates = new List<OccurrenceCandidate>();
            foreach (var profile in profiles)
            {
                var timeZone = ResolveTimeZone(profile.TimeZoneId);
                var localStart = TimeZoneInfo.ConvertTimeFromUtc(run.WindowStartUtc, timeZone);
                var localEnd = TimeZoneInfo.ConvertTimeFromUtc(run.WindowEndUtc, timeZone);
                foreach (var rule in profile.Rules.Where(item => item.IsEnabled))
                {
                    run.RulesEvaluated++;
                    for (var year = localStart.Year; year <= localEnd.Year; year++)
                    {
                        var localDue = CreateLocalDue(year, rule);
                        var dueUtc = TimeZoneInfo.ConvertTimeToUtc(localDue, timeZone);
                        if (dueUtc < run.WindowStartUtc || dueUtc > run.WindowEndUtc ||
                            dueUtc < profile.EffectiveFromUtc || (profile.EffectiveToUtc.HasValue && dueUtc > profile.EffectiveToUtc.Value)) continue;
                        candidates.Add(new OccurrenceCandidate(profile, rule, year, localDue, dueUtc));
                    }
                }
            }

            foreach (var candidate in candidates
                .GroupBy(item => new { item.Profile.ProfileKey, item.Rule.RuleKey, item.Year })
                .Select(group => group.OrderByDescending(item => item.Profile.Version).First())
                .OrderBy(item => item.DueUtc))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var owner = await ResolveOwnerAsync(tenantId, candidate.Rule.OwnerUserId, candidate.Rule.OwnerRoleName,
                        candidate.DueUtc, cancellationToken);
                    var escalation = await ResolveOwnerAsync(tenantId, candidate.Rule.EscalationUserId,
                        candidate.Rule.EscalationRoleName, candidate.DueUtc, cancellationToken);
                    var key = CreateOccurrenceKey(candidate.Profile.ProfileKey, candidate.Rule.RuleKey, candidate.Year);
                    var occurrence = await Occurrences.GetQueryable(item => item.TenantId == tenantId &&
                        item.OccurrenceKey == key && !item.IsDeleted).SingleOrDefaultAsync(cancellationToken);
                    if (occurrence is null)
                    {
                        occurrence = new ProcurementCalendarOccurrence
                        {
                            TenantId = tenantId,
                            OccurrenceKey = key,
                            ProfileId = candidate.Profile.Id,
                            ProfileKey = candidate.Profile.ProfileKey,
                            RuleId = candidate.Rule.Id,
                            RuleKey = candidate.Rule.RuleKey,
                            ProfileVersion = candidate.Profile.Version,
                            EventType = candidate.Rule.EventType,
                            CalendarYear = candidate.Year,
                            GeneratedAtUtc = DateTime.UtcNow,
                            Status = now >= candidate.DueUtc ? ProcurementCalendarOccurrenceStatus.Due : ProcurementCalendarOccurrenceStatus.Upcoming,
                            RowVersion = Guid.NewGuid().ToByteArray()
                        };
                        ApplyCandidate(occurrence, candidate, owner, escalation);
                        await Occurrences.AddAsync(occurrence);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        run.CreatedCount++;
                    }
                    else if (IsOpen(occurrence.Status) && (occurrence.DueAtUtc != candidate.DueUtc ||
                                 occurrence.ProfileId != candidate.Profile.Id || occurrence.OwnerUserId != owner.UserId ||
                                 occurrence.EscalationUserId != escalation.UserId))
                    {
                        var priorDue = occurrence.DueAtUtc;
                        ApplyCandidate(occurrence, candidate, owner, escalation);
                        occurrence.RescheduledAtUtc = DateTime.UtcNow;
                        occurrence.RescheduleReason = $"Effective calendar profile changed from due date {priorDue:O}.";
                        occurrence.RowVersion = Guid.NewGuid().ToByteArray();
                        await Occurrences.UpdateAsync(occurrence);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        run.RescheduledCount++;
                    }
                    await ProcessNotificationsAsync(occurrence, now, run, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    run.FailedCount++;
                    errors.Add($"{candidate.Rule.RuleCode}/{candidate.Year}: {exception.Message}");
                    _logger.LogWarning(exception, "Procurement calendar occurrence processing failed for tenant {TenantId}", tenantId);
                }
            }

            run.Status = errors.Count == 0 ? ProcurementCalendarRunStatus.Completed : ProcurementCalendarRunStatus.CompletedWithWarnings;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            run.Status = ProcurementCalendarRunStatus.Failed;
            run.FailedCount++;
            errors.Add(exception.Message);
            _logger.LogError(exception, "Procurement calendar run failed for tenant {TenantId}", tenantId);
        }

        run.CompletedAtUtc = DateTime.UtcNow;
        run.ErrorSummary = errors.Count == 0 ? null : Truncate(string.Join(" | ", errors), 4000);
        await Runs.UpdateAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordRunControlEventAsync(run, tenantId, requestedById, reason, normalizedCorrelation,
            cancellationToken);
        return MapRun(run);
    }

    private async Task RecordRunControlEventAsync(ProcurementCalendarRun run, Guid tenantId, Guid? requestedById,
        string reason, string normalizedCorrelation, CancellationToken cancellationToken)
    {
        var controlRequest = new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("calendar-run", tenantId, run.RunKey, run.AttemptCount, run.Status),
            EventType = EventType,
            Action = "GenerationRun",
            Result = run.Status == ProcurementCalendarRunStatus.Failed ? ProcurementControlEventResult.Failed :
                run.Status == ProcurementCalendarRunStatus.CompletedWithWarnings ? ProcurementControlEventResult.Warning : ProcurementControlEventResult.Succeeded,
            RuleCode = "TDC-0108",
            DecisionKeys = ["DEC-009"],
            SourceType = SourceType,
            SourceId = run.Id,
            SourceReference = run.RunKey,
            Reason = reason,
            ResultValues = MapRun(run),
            CorrelationId = normalizedCorrelation,
            OccurredAtUtc = run.CompletedAtUtc ?? DateTime.UtcNow
        };
        if (requestedById.HasValue && _currentUser.IsAuthenticated && _currentUser.TenantId == tenantId)
            await _controlEvents.RecordAsync(controlRequest, cancellationToken);
        else
            await _controlEvents.RecordSystemAsync(tenantId, "Procurement calendar scheduler", controlRequest, cancellationToken);
    }

    private async Task<ProcurementCalendarOccurrenceDto> ActAsync(
        Guid id, ProcurementCalendarOccurrenceActionRequest request, string action,
        ProcurementCalendarOccurrenceStatus target, bool manageOnly, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        EnsureReason(request.Reason);
        var entity = await LoadOccurrenceAsync(id, true, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        if (!IsOpen(entity.Status)) throw new ProcurementCalendarConflictException("Only an open calendar task can be changed.");
        if (target == ProcurementCalendarOccurrenceStatus.Acknowledged &&
            entity.Status is not (ProcurementCalendarOccurrenceStatus.Upcoming or ProcurementCalendarOccurrenceStatus.Due))
            throw new ProcurementCalendarConflictException("Only an upcoming or due calendar task can be acknowledged.");
        if (manageOnly || (_currentUser.UserId != entity.OwnerUserId && _currentUser.UserId != entity.EscalationUserId))
            await EnsureCapabilityAsync(ManagePermission, entity.OccurrenceKey, correlationId, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = target;
        entity.ActionReason = request.Reason.Trim();
        if (target == ProcurementCalendarOccurrenceStatus.Acknowledged)
        {
            entity.AcknowledgedAtUtc = now;
            entity.AcknowledgedById = _currentUser.UserId;
            entity.AcknowledgedByName = ActorName;
        }
        else if (target == ProcurementCalendarOccurrenceStatus.Completed)
        {
            entity.CompletedAtUtc = now;
            entity.CompletedById = _currentUser.UserId;
            entity.CompletedByName = ActorName;
        }
        else
        {
            entity.CancelledAtUtc = now;
            entity.CancelledById = _currentUser.UserId;
            entity.CancelledByName = ActorName;
        }
        entity.RowVersion = Guid.NewGuid().ToByteArray();
        await Occurrences.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserOccurrenceEventAsync(entity, action, request.Reason, before, Snapshot(entity), correlationId, cancellationToken);
        return MapOccurrence(await LoadOccurrenceAsync(entity.Id, false, cancellationToken));
    }

    private async Task ProcessNotificationsAsync(ProcurementCalendarOccurrence occurrence, DateTime now,
        ProcurementCalendarRun run, CancellationToken cancellationToken)
    {
        if (!IsOpen(occurrence.Status)) return;
        occurrence.LastEvaluatedAtUtc = DateTime.UtcNow;
        occurrence.ProcessingAttemptCount++;
        try
        {
            if (!occurrence.ReminderSentAtUtc.HasValue && now >= occurrence.DueAtUtc.AddDays(-occurrence.ReminderLeadDays))
            {
                occurrence.ReminderNotificationId = await CreateNotificationAsync(occurrence, occurrence.OwnerUserId,
                    "Procurement deadline reminder", $"{occurrence.Title} is due {FormatDue(occurrence)}.", "Normal", cancellationToken);
                occurrence.ReminderSentAtUtc = DateTime.UtcNow;
                run.ReminderCount++;
            }
            if (!occurrence.DueNotificationSentAtUtc.HasValue && now >= occurrence.DueAtUtc)
            {
                if (occurrence.Status == ProcurementCalendarOccurrenceStatus.Upcoming)
                    occurrence.Status = ProcurementCalendarOccurrenceStatus.Due;
                occurrence.DueNotificationId = await CreateNotificationAsync(occurrence, occurrence.OwnerUserId,
                    "Procurement task is due", $"{occurrence.Title} is now due. Acknowledge or complete the task in the procurement calendar.",
                    "High", cancellationToken);
                occurrence.DueNotificationSentAtUtc = DateTime.UtcNow;
                run.DueCount++;
            }
            if (!occurrence.EscalatedAtUtc.HasValue && now >= occurrence.DueAtUtc.AddDays(occurrence.EscalationAfterDays))
            {
                occurrence.Status = ProcurementCalendarOccurrenceStatus.Escalated;
                occurrence.EscalationNotificationId = await CreateNotificationAsync(occurrence, occurrence.EscalationUserId,
                    "Escalated procurement deadline", $"{occurrence.Title} assigned to {occurrence.OwnerName} remains incomplete after its deadline.",
                    "Critical", cancellationToken);
                occurrence.EscalatedAtUtc = DateTime.UtcNow;
                run.EscalationCount++;
            }
            occurrence.LastProcessingError = null;
        }
        catch (Exception exception)
        {
            occurrence.LastProcessingError = Truncate(exception.Message, 2000);
            throw;
        }
        finally
        {
            occurrence.RowVersion = Guid.NewGuid().ToByteArray();
            await Occurrences.UpdateAsync(occurrence);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Guid> CreateNotificationAsync(ProcurementCalendarOccurrence occurrence, Guid recipientId,
        string title, string message, string priority, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var notification = await _notifications.CreateNotificationAsync(new CreateNotificationDto
        {
            RecipientId = recipientId,
            Type = "ProcurementCalendar",
            Title = title,
            Message = message,
            Priority = priority,
            EntityType = nameof(ProcurementCalendarOccurrence),
            EntityId = occurrence.Id,
            ActionUrl = ActionUrl,
            Metadata = new Dictionary<string, object>
            {
                ["occurrenceKey"] = occurrence.OccurrenceKey,
                ["eventType"] = occurrence.EventType.ToString(),
                ["dueAtUtc"] = occurrence.DueAtUtc,
                ["timeZoneId"] = occurrence.TimeZoneId
            }
        }, Guid.Empty, occurrence.TenantId);
        return notification.Id;
    }

    private async Task<List<ProcurementCalendarValidationIssueDto>> ValidateForPublicationAsync(
        ProcurementCalendarProfile profile, CancellationToken cancellationToken)
    {
        var issues = ValidateProfile(profile);
        foreach (var rule in profile.Rules.Where(item => !item.IsDeleted && item.IsEnabled))
        {
            try
            {
                var zone = ResolveTimeZone(profile.TimeZoneId);
                var effectiveLocal = TimeZoneInfo.ConvertTimeFromUtc(profile.EffectiveFromUtc, zone);
                var localDue = CreateLocalDue(effectiveLocal.Year, rule);
                var dueUtc = TimeZoneInfo.ConvertTimeToUtc(localDue, zone);
                await ResolveOwnerAsync(profile.TenantId, rule.OwnerUserId, rule.OwnerRoleName, dueUtc, cancellationToken);
                await ResolveOwnerAsync(profile.TenantId, rule.EscalationUserId, rule.EscalationRoleName, dueUtc, cancellationToken);
            }
            catch (ProcurementCalendarValidationException exception)
            {
                issues.Add(Issue(exception.Code, rule.EventType.ToString(), exception.Message));
            }
        }
        return issues;
    }

    private static List<ProcurementCalendarValidationIssueDto> ValidateProfile(ProcurementCalendarProfile profile)
    {
        var issues = new List<ProcurementCalendarValidationIssueDto>();
        var active = profile.Rules.Where(item => !item.IsDeleted && item.IsEnabled).ToList();
        foreach (var eventType in Enum.GetValues<ProcurementCalendarEventType>())
        {
            var count = active.Count(item => item.EventType == eventType);
            if (count != 1) issues.Add(Issue("CALENDAR_EVENT_REQUIRED", eventType.ToString(),
                $"Exactly one enabled {eventType} rule is required before publication."));
        }
        try { ResolveTimeZone(profile.TimeZoneId); }
        catch (ProcurementCalendarValidationException exception) { issues.Add(Issue(exception.Code, nameof(profile.TimeZoneId), exception.Message)); }
        return issues;
    }

    private async Task<ResolvedOwner> ResolveOwnerAsync(Guid tenantId, Guid? userId, string? roleName,
        DateTime effectiveAtUtc, CancellationToken cancellationToken)
    {
        var effectiveAt = EnsureUtc(effectiveAtUtc);
        var query = Assignments.GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.EffectiveFrom <= effectiveAt && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= effectiveAt))
            .Include(item => item.User).AsNoTracking();
        if (userId.HasValue) query = query.Where(item => item.UserId == userId.Value);
        else if (!string.IsNullOrWhiteSpace(roleName)) query = query.Where(item => item.RoleName == roleName.Trim());
        else throw new ProcurementCalendarValidationException("CALENDAR_OWNER_REQUIRED", "An owner user or procurement responsibility role is required.");
        var row = await query.OrderBy(item => item.EffectiveFrom).ThenBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementCalendarValidationException("CALENDAR_OWNER_UNAVAILABLE",
                $"No active tenant responsibility assignment resolves '{roleName ?? userId?.ToString()}' at the configured due date.");
        if (!row.User.IsActive)
            throw new ProcurementCalendarValidationException("CALENDAR_OWNER_INACTIVE", "The resolved calendar owner is inactive.");
        var name = string.Join(' ', new[] { row.User.FirstName, row.User.LastName }.Where(item => !string.IsNullOrWhiteSpace(item)));
        if (string.IsNullOrWhiteSpace(name)) name = row.User.UserName ?? row.User.Email ?? row.UserId.ToString();
        return new ResolvedOwner(row.UserId, Truncate(name, 300), row.RoleName);
    }

    private async Task EnsureCapabilityAsync(string permission, string sourceReference, string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = SourceType,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? "CALENDAR" : sourceReference
        }, string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId, cancellationToken);
        if (!decision.Allowed) throw new ProcurementCalendarAuthorizationException(decision.Message);
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementCalendarAuthorizationException("An authenticated tenant context is required.");
    }

    private async Task<ProcurementCalendarProfile> LoadProfileAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ProcurementCalendarProfile> query = Profiles.GetQueryable(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.Rules.Where(rule => !rule.IsDeleted));
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementCalendarNotFoundException("The calendar profile was not found in the current tenant.");
    }

    private async Task<ProcurementCalendarOccurrence> LoadOccurrenceAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ProcurementCalendarOccurrence> query = Occurrences.GetQueryable(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.Profile).Include(item => item.Rule);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementCalendarNotFoundException("The calendar occurrence was not found in the current tenant.");
    }

    private static void ApplyRequest(ProcurementCalendarProfile entity, SaveProcurementCalendarProfileRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = TrimOrNull(request.Description, 1000);
        entity.TimeZoneId = request.TimeZoneId.Trim();
        entity.GenerationHorizonDays = request.GenerationHorizonDays;
        entity.CatchUpDays = request.CatchUpDays;
        entity.EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc);
        entity.EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null;
        entity.ChangeSummary = request.ChangeSummary.Trim();
    }

    private static void ApplyRules(ProcurementCalendarProfile entity, IReadOnlyCollection<ProcurementCalendarRuleRequest> requests,
        IReadOnlyCollection<ProcurementCalendarRule> prior)
    {
        foreach (var request in requests)
        {
            var priorRule = prior.FirstOrDefault(item => item.EventType == request.EventType);
            entity.Rules.Add(new ProcurementCalendarRule
            {
                TenantId = entity.TenantId,
                ProfileId = entity.Id,
                RuleKey = request.RuleKey ?? priorRule?.RuleKey ?? Guid.NewGuid(),
                RuleCode = RuleCode(request.EventType),
                EventType = request.EventType,
                Title = request.Title.Trim(),
                Description = TrimOrNull(request.Description, 1000),
                DueMonth = request.DueMonth,
                DueDay = request.DueDay,
                DueLocalTime = request.DueLocalTime,
                ReminderLeadDays = request.ReminderLeadDays,
                EscalationAfterDays = request.EscalationAfterDays,
                OwnerUserId = request.OwnerUserId,
                OwnerRoleName = TrimOrNull(request.OwnerRoleName, 100),
                EscalationUserId = request.EscalationUserId,
                EscalationRoleName = TrimOrNull(request.EscalationRoleName, 100),
                StatutoryReference = request.StatutoryReference.Trim(),
                IsEnabled = request.IsEnabled
            });
        }
    }

    private static void ApplyCandidate(ProcurementCalendarOccurrence entity, OccurrenceCandidate candidate,
        ResolvedOwner owner, ResolvedOwner escalation)
    {
        entity.ProfileId = candidate.Profile.Id;
        entity.ProfileKey = candidate.Profile.ProfileKey;
        entity.ProfileVersion = candidate.Profile.Version;
        entity.RuleId = candidate.Rule.Id;
        entity.RuleKey = candidate.Rule.RuleKey;
        entity.EventType = candidate.Rule.EventType;
        entity.CalendarYear = candidate.Year;
        entity.Title = candidate.Rule.Title;
        entity.Description = candidate.Rule.Description;
        entity.DueAtUtc = candidate.DueUtc;
        entity.DueLocal = candidate.LocalDue;
        entity.TimeZoneId = candidate.Profile.TimeZoneId;
        entity.ReminderLeadDays = candidate.Rule.ReminderLeadDays;
        entity.EscalationAfterDays = candidate.Rule.EscalationAfterDays;
        entity.OwnerUserId = owner.UserId;
        entity.OwnerName = owner.Name;
        entity.OwnerRoleName = owner.RoleName;
        entity.EscalationUserId = escalation.UserId;
        entity.EscalationOwnerName = escalation.Name;
        entity.EscalationRoleName = escalation.RoleName;
        entity.StatutoryReference = candidate.Rule.StatutoryReference;
    }

    private static void ValidateRequest(SaveProcurementCalendarProfileRequest request, bool requireAllEvents)
    {
        if (string.IsNullOrWhiteSpace(request.ProfileCode)) throw new ProcurementCalendarValidationException("PROFILE_CODE_REQUIRED", "ProfileCode is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ProcurementCalendarValidationException("PROFILE_NAME_REQUIRED", "Name is required.");
        if (string.IsNullOrWhiteSpace(request.ChangeSummary)) throw new ProcurementCalendarValidationException("CHANGE_SUMMARY_REQUIRED", "ChangeSummary is required.");
        ResolveTimeZone(request.TimeZoneId);
        ValidatePeriod(request.EffectiveFromUtc, request.EffectiveToUtc);
        if (request.Rules.GroupBy(item => item.EventType).Any(group => group.Count() > 1))
            throw new ProcurementCalendarValidationException("CALENDAR_EVENT_DUPLICATE", "Only one rule per calendar event type is allowed in a profile.");
        if (requireAllEvents && request.Rules.Count(item => item.IsEnabled) != Enum.GetValues<ProcurementCalendarEventType>().Length)
            throw new ProcurementCalendarValidationException("CALENDAR_EVENTS_REQUIRED", "All required annual event families must be enabled.");
        foreach (var rule in request.Rules)
        {
            if (!Enum.IsDefined(rule.EventType)) throw new ProcurementCalendarValidationException("CALENDAR_EVENT_INVALID", "EventType is invalid.");
            if (string.IsNullOrWhiteSpace(rule.Title)) throw new ProcurementCalendarValidationException("CALENDAR_TITLE_REQUIRED", $"A title is required for {rule.EventType}.");
            if (string.IsNullOrWhiteSpace(rule.StatutoryReference)) throw new ProcurementCalendarValidationException("STATUTORY_REFERENCE_REQUIRED", $"An approved source reference is required for {rule.EventType}.");
            if (rule.DueLocalTime < TimeSpan.Zero || rule.DueLocalTime >= TimeSpan.FromDays(1))
                throw new ProcurementCalendarValidationException("DUE_TIME_INVALID", "DueLocalTime must fall within one local day.");
            try { _ = new DateTime(2024, rule.DueMonth, rule.DueDay); }
            catch (ArgumentOutOfRangeException) { throw new ProcurementCalendarValidationException("DUE_DATE_INVALID", $"The due month/day for {rule.EventType} is invalid."); }
            var ownerCount = (rule.OwnerUserId.HasValue ? 1 : 0) + (!string.IsNullOrWhiteSpace(rule.OwnerRoleName) ? 1 : 0);
            var escalationCount = (rule.EscalationUserId.HasValue ? 1 : 0) + (!string.IsNullOrWhiteSpace(rule.EscalationRoleName) ? 1 : 0);
            if (ownerCount != 1) throw new ProcurementCalendarValidationException("CALENDAR_OWNER_INVALID", $"Select exactly one owner user or role for {rule.EventType}.");
            if (escalationCount != 1) throw new ProcurementCalendarValidationException("CALENDAR_ESCALATION_INVALID", $"Select exactly one escalation user or role for {rule.EventType}.");
        }
    }

    private async Task RecordUserEventAsync(ProcurementCalendarProfile entity, string action, string reason,
        object? before, object? after, string correlationId, CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(CreateEvent(entity.Id, $"{entity.ProfileCode}/v{entity.Version}", action, reason,
            before, after, correlationId), cancellationToken);

    private async Task RecordUserOccurrenceEventAsync(ProcurementCalendarOccurrence entity, string action, string reason,
        object? before, object? after, string correlationId, CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(CreateEvent(entity.Id, entity.OccurrenceKey, action, reason, before, after,
            correlationId), cancellationToken);

    private static ProcurementControlEventWriteRequest CreateEvent(Guid id, string reference, string action, string reason,
        object? before, object? after, string correlationId) => new()
    {
        EventKey = ProcurementControlEventKey.Create("calendar", id, action, NormalizeCorrelation(correlationId)),
        EventType = EventType,
        Action = action,
        Result = ProcurementControlEventResult.Succeeded,
        RuleCode = "TDC-0108",
        DecisionKeys = ["DEC-009"],
        SourceType = SourceType,
        SourceId = id,
        SourceReference = reference,
        Reason = reason,
        Before = before,
        After = after,
        CorrelationId = NormalizeCorrelation(correlationId),
        OccurredAtUtc = DateTime.UtcNow
    };

    private async Task<ProcurementCalendarProfileDto> MapProfileWithEffectiveStateAsync(
        ProcurementCalendarProfile item, CancellationToken cancellationToken)
    {
        var result = MapProfile(item);
        if (!result.IsEffective) return result;
        result.IsEffective = !await Profiles.GetQueryable(candidate => candidate.TenantId == item.TenantId &&
                candidate.ProfileKey == item.ProfileKey && !candidate.IsDeleted &&
                candidate.Status == ProcurementCalendarProfileStatus.Published && candidate.Version > item.Version &&
                candidate.EffectiveFromUtc <= DateTime.UtcNow &&
                (!candidate.EffectiveToUtc.HasValue || candidate.EffectiveToUtc.Value >= DateTime.UtcNow))
            .AnyAsync(cancellationToken);
        return result;
    }

    private static ProcurementCalendarProfileDto MapProfile(ProcurementCalendarProfile item, bool? isEffective = null)
    {
        var now = DateTime.UtcNow;
        return new ProcurementCalendarProfileDto
        {
            Id = item.Id,
            ProfileKey = item.ProfileKey,
            ProfileCode = item.ProfileCode,
            Name = item.Name,
            Description = item.Description,
            Version = item.Version,
            Status = item.Status,
            TimeZoneId = item.TimeZoneId,
            GenerationHorizonDays = item.GenerationHorizonDays,
            CatchUpDays = item.CatchUpDays,
            EffectiveFromUtc = item.EffectiveFromUtc,
            EffectiveToUtc = item.EffectiveToUtc,
            IsEffective = isEffective ?? (item.Status == ProcurementCalendarProfileStatus.Published && item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now)),
            ChangeSummary = item.ChangeSummary,
            ApprovalReference = item.ApprovalReference,
            SupersedesProfileId = item.SupersedesProfileId,
            PublishedAtUtc = item.PublishedAtUtc,
            PublishedByName = item.PublishedByName,
            RetiredAtUtc = item.RetiredAtUtc,
            RetiredByName = item.RetiredByName,
            CreatedAtUtc = item.CreatedAt,
            RowVersion = Convert.ToBase64String(item.RowVersion),
            Rules = item.Rules.Where(rule => !rule.IsDeleted).OrderBy(rule => rule.EventType).Select(rule => new ProcurementCalendarRuleDto
            {
                Id = rule.Id,
                RuleKey = rule.RuleKey,
                RuleCode = rule.RuleCode,
                EventType = rule.EventType,
                Title = rule.Title,
                Description = rule.Description,
                DueMonth = rule.DueMonth,
                DueDay = rule.DueDay,
                DueLocalTime = rule.DueLocalTime,
                ReminderLeadDays = rule.ReminderLeadDays,
                EscalationAfterDays = rule.EscalationAfterDays,
                OwnerUserId = rule.OwnerUserId,
                OwnerRoleName = rule.OwnerRoleName,
                EscalationUserId = rule.EscalationUserId,
                EscalationRoleName = rule.EscalationRoleName,
                StatutoryReference = rule.StatutoryReference,
                IsEnabled = rule.IsEnabled
            }).ToList(),
            ValidationIssues = ValidateProfile(item)
        };
    }

    private static ProcurementCalendarOccurrenceDto MapOccurrence(ProcurementCalendarOccurrence item) => new()
    {
        Id = item.Id,
        OccurrenceKey = item.OccurrenceKey,
        ProfileId = item.ProfileId,
        ProfileVersion = item.ProfileVersion,
        ProfileCode = item.Profile?.ProfileCode ?? string.Empty,
        RuleCode = item.Rule?.RuleCode ?? RuleCode(item.EventType),
        EventType = item.EventType,
        CalendarYear = item.CalendarYear,
        Title = item.Title,
        Description = item.Description,
        DueAtUtc = item.DueAtUtc,
        DueLocal = item.DueLocal,
        TimeZoneId = item.TimeZoneId,
        OwnerUserId = item.OwnerUserId,
        OwnerName = item.OwnerName,
        OwnerRoleName = item.OwnerRoleName,
        EscalationUserId = item.EscalationUserId,
        EscalationOwnerName = item.EscalationOwnerName,
        EscalationRoleName = item.EscalationRoleName,
        StatutoryReference = item.StatutoryReference,
        Status = item.Status,
        GeneratedAtUtc = item.GeneratedAtUtc,
        ReminderSentAtUtc = item.ReminderSentAtUtc,
        DueNotificationSentAtUtc = item.DueNotificationSentAtUtc,
        EscalatedAtUtc = item.EscalatedAtUtc,
        AcknowledgedAtUtc = item.AcknowledgedAtUtc,
        CompletedAtUtc = item.CompletedAtUtc,
        CancelledAtUtc = item.CancelledAtUtc,
        ActionReason = item.ActionReason,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private static ProcurementCalendarRunDto MapRun(ProcurementCalendarRun item) => new()
    {
        Id = item.Id,
        RunKey = item.RunKey,
        Trigger = item.Trigger,
        Status = item.Status,
        AttemptCount = item.AttemptCount,
        EvaluationAtUtc = EnsureUtc(item.EvaluationAtUtc),
        WindowStartUtc = EnsureUtc(item.WindowStartUtc),
        WindowEndUtc = EnsureUtc(item.WindowEndUtc),
        StartedAtUtc = EnsureUtc(item.StartedAtUtc),
        CompletedAtUtc = item.CompletedAtUtc.HasValue ? EnsureUtc(item.CompletedAtUtc.Value) : null,
        RequestedByName = item.RequestedByName,
        ProfilesEvaluated = item.ProfilesEvaluated,
        RulesEvaluated = item.RulesEvaluated,
        CreatedCount = item.CreatedCount,
        RescheduledCount = item.RescheduledCount,
        ReminderCount = item.ReminderCount,
        DueCount = item.DueCount,
        EscalationCount = item.EscalationCount,
        FailedCount = item.FailedCount,
        ErrorSummary = item.ErrorSummary
    };

    private static object Snapshot(ProcurementCalendarProfile item) => new
    {
        item.Id, item.ProfileKey, item.ProfileCode, item.Version, item.Status, item.Name, item.Description,
        item.TimeZoneId, item.GenerationHorizonDays, item.CatchUpDays, item.EffectiveFromUtc, item.EffectiveToUtc,
        item.ChangeSummary, item.ApprovalReference, item.SupersedesProfileId, item.PublishedAtUtc, item.PublishedById,
        item.RetiredAtUtc, item.RetiredById,
        Rules = item.Rules.Where(rule => !rule.IsDeleted).OrderBy(rule => rule.EventType).Select(rule => new
        {
            rule.RuleKey, rule.RuleCode, rule.EventType, rule.Title, rule.DueMonth, rule.DueDay, rule.DueLocalTime,
            rule.ReminderLeadDays, rule.EscalationAfterDays, rule.OwnerUserId, rule.OwnerRoleName,
            rule.EscalationUserId, rule.EscalationRoleName, rule.StatutoryReference, rule.IsEnabled
        })
    };

    private static object Snapshot(ProcurementCalendarOccurrence item) => new
    {
        item.Id, item.OccurrenceKey, item.ProfileId, item.ProfileVersion, item.RuleId, item.EventType, item.CalendarYear,
        item.DueAtUtc, item.DueLocal, item.TimeZoneId, item.OwnerUserId, item.EscalationUserId, item.Status,
        item.ReminderSentAtUtc, item.DueNotificationSentAtUtc, item.EscalatedAtUtc, item.AcknowledgedAtUtc,
        item.CompletedAtUtc, item.CancelledAtUtc, item.ActionReason
    };

    private void Touch(ProcurementCalendarProfile entity, DateTime? now = null)
    {
        entity.UpdatedAt = now ?? DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.Username;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private static void EnsureDraft(ProcurementCalendarProfile entity)
    {
        if (entity.Status != ProcurementCalendarProfileStatus.Draft)
            throw new ProcurementCalendarConflictException("Published and Retired calendar profiles are immutable; clone a new Draft.");
    }

    private static void EnsureRowVersion(byte[] current, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) throw new ProcurementCalendarValidationException("ROW_VERSION_REQUIRED", "RowVersion is required.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw new ProcurementCalendarValidationException("ROW_VERSION_INVALID", "RowVersion must be valid Base64."); }
        if (!current.SequenceEqual(parsed)) throw new ProcurementCalendarConflictException("The record changed after it was loaded. Refresh and retry.");
    }

    private static void EnsureReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ProcurementCalendarValidationException("REASON_REQUIRED", "A reason is required.");
    }

    private static void ValidatePeriod(DateTime fromUtc, DateTime? toUtc)
    {
        if (fromUtc == default) throw new ProcurementCalendarValidationException("EFFECTIVE_FROM_REQUIRED", "EffectiveFromUtc is required.");
        if (toUtc.HasValue && EnsureUtc(toUtc.Value) < EnsureUtc(fromUtc))
            throw new ProcurementCalendarValidationException("EFFECTIVE_PERIOD_INVALID", "EffectiveToUtc cannot precede EffectiveFromUtc.");
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ProcurementCalendarValidationException("TIME_ZONE_REQUIRED", "TimeZoneId is required.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); }
        catch (TimeZoneNotFoundException) { throw new ProcurementCalendarValidationException("TIME_ZONE_INVALID", $"Time zone '{id}' is not installed on this server."); }
        catch (InvalidTimeZoneException) { throw new ProcurementCalendarValidationException("TIME_ZONE_INVALID", $"Time zone '{id}' is invalid."); }
    }

    private static DateTime CreateLocalDue(int year, ProcurementCalendarRule rule)
    {
        var day = Math.Min(rule.DueDay, DateTime.DaysInMonth(year, rule.DueMonth));
        return DateTime.SpecifyKind(new DateTime(year, rule.DueMonth, day).Add(rule.DueLocalTime), DateTimeKind.Unspecified);
    }

    private static bool IsOpen(ProcurementCalendarOccurrenceStatus status) => status is
        ProcurementCalendarOccurrenceStatus.Upcoming or ProcurementCalendarOccurrenceStatus.Due or
        ProcurementCalendarOccurrenceStatus.Acknowledged or ProcurementCalendarOccurrenceStatus.Escalated;

    private static string CreateOccurrenceKey(Guid profileKey, Guid ruleKey, int year) =>
        $"PCO-{profileKey:N}-{ruleKey:N}-{year}";

    private static string RuleCode(ProcurementCalendarEventType eventType) => eventType switch
    {
        ProcurementCalendarEventType.AppPreparation => "APP-PREPARATION",
        ProcurementCalendarEventType.AppSubmission => "APP-SUBMISSION",
        ProcurementCalendarEventType.MidYearReview => "MID-YEAR-REVIEW",
        ProcurementCalendarEventType.CycleCount => "CYCLE-COUNT",
        ProcurementCalendarEventType.YearEndClose => "YEAR-END-CLOSE",
        ProcurementCalendarEventType.Renewal => "RENEWAL",
        ProcurementCalendarEventType.GhanepsDeadline => "GHANEPS-DEADLINE",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType))
    };

    private static ProcurementCalendarValidationIssueDto Issue(string code, string field, string message) =>
        new() { Code = code, Field = field, Message = message };

    private static string FormatDue(ProcurementCalendarOccurrence item) =>
        $"on {item.DueLocal:yyyy-MM-dd HH:mm} ({item.TimeZoneId})";

    private Guid TenantId { get { EnsureAuthenticatedTenant(); return _currentUser.TenantId; } }
    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string? TrimOrNull(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), length);

    private sealed record ResolvedOwner(Guid UserId, string Name, string RoleName);
    private sealed record OccurrenceCandidate(ProcurementCalendarProfile Profile, ProcurementCalendarRule Rule,
        int Year, DateTime LocalDue, DateTime DueUtc);
}
