using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Announcements;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Staff announcements. See <see cref="IHrAnnouncementService"/> for why audience membership is
/// evaluated at read time rather than frozen at publish.
/// </summary>
public class HrAnnouncementService : IHrAnnouncementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audience;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<HrAnnouncementService> _logger;

    /// <summary>
    /// The notification topic a publish raises (round 3, lane P1). Key
    /// <c>HrAnnouncement.Published.Internal</c>; seeded with in-app ON and email/SMS OFF, so an
    /// administrator turns the other channels on under Notifications → Topics — which is exactly
    /// the answer to "must staff log in to see an announcement?": no, once the channel is on.
    /// Recipients are the announcement's own resolved audience, carried on the event as user ids.
    /// </summary>
    private const string TopicEntityType = "HrAnnouncement";
    private const string PublishedActivity = "Published";
    private const string TopicAudience = "Internal";
    private const string RecipientsDataKey = "RecipientUserIds";
    public const string PublishedTopicKey = TopicEntityType + "." + PublishedActivity + "." + TopicAudience;

    public HrAnnouncementService(
        IUnitOfWork unitOfWork,
        IHrAudienceResolver audience,
        ICurrentUserProvider currentUserProvider,
        UserManager<ApplicationUser> userManager,
        IAppEventBus appEventBus,
        ILogger<HrAnnouncementService> logger)
    {
        _unitOfWork = unitOfWork;
        _audience = audience;
        _currentUserProvider = currentUserProvider;
        _userManager = userManager;
        _appEventBus = appEventBus;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<HrAnnouncement> Scoped(Guid tenantId) =>
        _unitOfWork.Repository<HrAnnouncement>()
            .GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Include(a => a.PublishedBy)
            .Include(a => a.ArchivedBy)
            .Include(a => a.Audiences);

    private static IEnumerable<HrAudienceRule> RulesOf(HrAnnouncement a) =>
        a.Audiences.Where(x => !x.IsDeleted)
            .Select(x => new HrAudienceRule(x.TargetType, x.TargetId, x.IsExclusion));

    // ── The employee's side ───────────────────────────────────────────────────

    public async Task<IEnumerable<MyAnnouncementDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        // Narrow on what SQL can decide first — status and the date window — then ask the
        // resolver only about the handful that survive. Membership cannot be pushed into the
        // query because a unit target reaches the whole subtree beneath it.
        var live = await Scoped(tenantId)
            .Where(a => a.Status == HrAnnouncementStatus.Published
                     && (a.EffectiveFrom == null || a.EffectiveFrom <= now)
                     && (a.ExpiresOn == null || a.ExpiresOn >= now))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAt)
            .ToListAsync(cancellationToken);

        var mine = new List<MyAnnouncementDto>();
        foreach (var announcement in live)
        {
            if (await _audience.IncludesAsync(RulesOf(announcement), employeeId, cancellationToken))
                mine.Add(MapMine(announcement));
        }
        return mine;
    }

    public async Task<IEnumerable<MyAnnouncementDto>> GetMineForDashboardAsync(
        Guid employeeId, int limit = 3, CancellationToken cancellationToken = default)
        => (await GetMineAsync(employeeId, cancellationToken)).Take(Math.Max(1, limit)).ToList();

    public async Task<MyAnnouncementDto?> GetMineByIdAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        // Not live, or not aimed at them: a lookup miss either way. Distinguishing the two would
        // tell an employee that an announcement exists which they were not sent.
        if (announcement is null || !announcement.IsLive) return null;
        if (!await _audience.IncludesAsync(RulesOf(announcement), employeeId, cancellationToken)) return null;

        return MapMine(announcement);
    }

    // ── The HR desk ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<HrAnnouncementDto>> GetAllAsync(
        HrAnnouncementStatus? status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = Scoped(tenantId);
        if (status is { } wanted) query = query.Where(a => a.Status == wanted);

        var rows = await query
            .OrderByDescending(a => a.Status == HrAnnouncementStatus.Draft ? 1 : 0)
            .ThenByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .ToListAsync(cancellationToken);

        var names = await TargetNamesAsync(tenantId, rows.SelectMany(r => r.Audiences), cancellationToken);
        return rows.Select(r => Map(r, names)).ToList();
    }

    public async Task<HrAnnouncementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (announcement is null) return null;

        var names = await TargetNamesAsync(tenantId, announcement.Audiences, cancellationToken);
        var dto = Map(announcement, names);
        dto.AudienceCount = await _audience.CountAsync(RulesOf(announcement), cancellationToken);
        return dto;
    }

    public async Task<HrAnnouncementDto> CreateAsync(
        CreateHrAnnouncementDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        Validate(dto);

        var announcement = new HrAnnouncement
        {
            TenantId = tenantId,
            Title = dto.Title.Trim(),
            Summary = string.IsNullOrWhiteSpace(dto.Summary) ? null : dto.Summary.Trim(),
            Body = dto.Body.Trim(),
            Category = dto.Category,
            Status = HrAnnouncementStatus.Draft,
            IsPinned = dto.IsPinned,
            EffectiveFrom = dto.EffectiveFrom,
            ExpiresOn = dto.ExpiresOn,
        };
        ApplyAudiences(announcement, dto.Audiences, tenantId);

        await _unitOfWork.Repository<HrAnnouncement>().AddAsync(announcement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RequireDtoAsync(announcement.Id, cancellationToken);
    }

    public async Task<HrAnnouncementDto> UpdateAsync(
        Guid id, UpdateHrAnnouncementDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        Validate(dto);

        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Announcement not found.");

        // A published notice is not rewritten in place: people have already read it, and
        // silently changing what they were told is worse than publishing a correction.
        if (announcement.Status != HrAnnouncementStatus.Draft)
            throw new InvalidOperationException(
                "A published announcement cannot be edited. Archive it and publish a correction.");

        announcement.Title = dto.Title.Trim();
        announcement.Summary = string.IsNullOrWhiteSpace(dto.Summary) ? null : dto.Summary.Trim();
        announcement.Body = dto.Body.Trim();
        announcement.Category = dto.Category;
        announcement.IsPinned = dto.IsPinned;
        announcement.EffectiveFrom = dto.EffectiveFrom;
        announcement.ExpiresOn = dto.ExpiresOn;

        // Replace-set semantics: the payload carries the whole audience, and omitting a rule
        // removes it (`replace-set-payload-convention`).
        //
        // ⚠ The new rows are added through their OWN repository with the FK set explicitly —
        // NOT by adding to `announcement.Audiences`. The parent here came from a query and is
        // therefore TRACKED, and `BaseEntity` pre-generates `Id`, so a child discovered through
        // a navigation collection is marked Modified rather than Added: EF emits an UPDATE for
        // a row that was never inserted, the edit silently does nothing, and no error is
        // raised. That is cross-module defect #13's exact shape (payroll's employee-profile
        // create, which has never once worked). `CreateAsync` may use the collection safely
        // because its parent is Added and the whole graph is Added with it.
        foreach (var existing in announcement.Audiences.ToList())
        {
            await _unitOfWork.Repository<HrAnnouncementAudience>().DeleteAsync(existing.Id);
        }
        announcement.Audiences.Clear();

        foreach (var rule in dto.Audiences ?? [])
        {
            await _unitOfWork.Repository<HrAnnouncementAudience>().AddAsync(new HrAnnouncementAudience
            {
                TenantId = tenantId,
                AnnouncementId = announcement.Id,
                TargetType = rule.TargetType,
                TargetId = HrAudienceTargets.NeedsTarget(rule.TargetType) ? rule.TargetId : null,
                IsExclusion = rule.IsExclusion,
            });
        }

        await _unitOfWork.Repository<HrAnnouncement>().UpdateAsync(announcement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RequireDtoAsync(id, cancellationToken);
    }

    public async Task<HrAnnouncementDto> PublishAsync(
        Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Announcement not found.");

        if (announcement.Status == HrAnnouncementStatus.Published)
            throw new InvalidOperationException("This announcement has already been published.");
        if (announcement.Status == HrAnnouncementStatus.Archived)
            throw new InvalidOperationException("An archived announcement cannot be republished.");

        // Refuse a broadcast that reaches nobody. This is the check that catches an audience
        // naming a unit with no staff in it — the announcement would look published and be
        // read by no one, which is the failure nobody notices.
        var reach = await _audience.CountAsync(RulesOf(announcement), cancellationToken);
        if (reach == 0)
            throw new InvalidOperationException(
                "This announcement reaches nobody. Check who it is addressed to before publishing.");

        announcement.Status = HrAnnouncementStatus.Published;
        announcement.PublishedAt = DateTime.UtcNow;
        announcement.PublishedById = publisherEmployeeId;

        await _unitOfWork.Repository<HrAnnouncement>().UpdateAsync(announcement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Announcement '{Title}' published to {Reach} employee(s).", announcement.Title, reach);

        // After the commit, never before it: a notification about an announcement that then
        // failed to save would be a notice about nothing.
        await NotifyAudienceAsync(tenantId, announcement, cancellationToken);

        return await RequireDtoAsync(id, cancellationToken);
    }

    // ── Notification on publish (round 3, lane P1) ────────────────────────────

    /// <summary>
    /// Raise the published topic for everyone the audience rules reach. Best-effort by design:
    /// the announcement IS published once the row says so, and the portal shows it regardless;
    /// a notification failure is logged, never surfaced as a failed publish.
    /// </summary>
    private async Task NotifyAudienceAsync(
        Guid tenantId, HrAnnouncement announcement, CancellationToken cancellationToken)
    {
        try
        {
            await EnsurePublishedTopicAsync(tenantId, cancellationToken);

            var employeeIds = await _audience.ResolveAsync(RulesOf(announcement), cancellationToken);
            var userIds = await ResolveUserIdsAsync(tenantId, employeeIds, cancellationToken);
            if (userIds.Count == 0)
            {
                _logger.LogInformation(
                    "Announcement '{Title}' reaches {Reach} employee(s) but none has a user account; nothing to notify.",
                    announcement.Title, employeeIds.Count);
                return;
            }

            var summary = string.IsNullOrWhiteSpace(announcement.Summary)
                ? Truncate(announcement.Body, 240)
                : announcement.Summary!;

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = PublishedActivity,
                Audience = TopicAudience,
                EntityId = announcement.Id,
                TriggeredByUserId = _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
                Data = new Dictionary<string, object>
                {
                    ["AnnouncementId"] = announcement.Id,
                    ["Title"] = announcement.Title,
                    ["Summary"] = summary,
                    ["Category"] = announcement.Category.ToString(),
                    ["ActionPath"] = "/me/announcements",
                    [RecipientsDataKey] = userIds,
                },
            }, cancellationToken);

            _logger.LogInformation(
                "Announcement '{Title}' raised {Topic} for {Users} user(s).",
                announcement.Title, PublishedTopicKey, userIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Announcement '{Title}' was published but its notification could not be raised.",
                announcement.Title);
        }
    }

    /// <summary>Employees → active users of this tenant linked to them, in chunks (an audience can be the whole tenant).</summary>
    private async Task<List<Guid>> ResolveUserIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken)
    {
        var result = new List<Guid>();
        foreach (var chunk in employeeIds.Chunk(500))
        {
            var ids = chunk.ToList();
            var users = await _userManager.Users
                .Where(u => u.TenantId == tenantId && u.IsActive && u.EmployeeId != null && ids.Contains(u.EmployeeId.Value))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            result.AddRange(users);
        }
        return result.Distinct().ToList();
    }

    /// <summary>
    /// One system topic, created on first publish and left to the administrator afterwards. Only
    /// in-app is on by default; the recipient rule allows every channel so that flipping the
    /// topic's switches is all it takes.
    /// </summary>
    private async Task EnsurePublishedTopicAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var exists = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.Key == PublishedTopicKey)
            .AnyAsync(cancellationToken);
        if (exists) return;

        var topic = new NotificationTopic
        {
            TenantId = tenantId,
            Key = PublishedTopicKey,
            Name = "Staff announcements: published",
            Description = "System-seeded — raised when HR publishes a staff announcement; sent to everyone in the announcement's audience. Turn email or SMS on here to reach staff who do not open the portal.",
            EntityType = TopicEntityType,
            IsSystem = true,
            IsActive = true,
            EnableInApp = true,
            EnableEmail = false,
            EnableSms = false,
            InAppTitleTemplate = "{{Title}}",
            InAppBodyTemplate = "{{Summary}}",
            SmsBodyTemplate = "{{Title}} — new staff announcement. See the staff portal for the full notice.",
            ActionUrlTemplate = "{{ActionPath}}",
            CreatedBy = "System",
        };
        await topicRepo.AddAsync(topic);
        await _unitOfWork.Repository<NotificationTopicRecipient>().AddAsync(new NotificationTopicRecipient
        {
            TenantId = tenantId,
            TopicId = topic.Id,
            RecipientKind = "UsersFromData",
            RecipientValue = RecipientsDataKey,
            IsSystem = true,
            SendInApp = true,
            SendEmail = true,
            SendSms = true,
            CreatedBy = "System",
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string text, int max)
    {
        var t = (text ?? string.Empty).Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    public async Task<HrAnnouncementDto> ArchiveAsync(
        Guid id, Guid archiverEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Announcement not found.");

        if (announcement.Status == HrAnnouncementStatus.Archived)
            throw new InvalidOperationException("This announcement has already been archived.");

        announcement.Status = HrAnnouncementStatus.Archived;
        announcement.ArchivedAt = DateTime.UtcNow;
        announcement.ArchivedById = archiverEmployeeId;

        await _unitOfWork.Repository<HrAnnouncement>().UpdateAsync(announcement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await RequireDtoAsync(id, cancellationToken);
    }

    public async Task DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Announcement not found.");

        // Only a draft is deletable. Once published, what was announced is a record — archive it.
        if (announcement.Status != HrAnnouncementStatus.Draft)
            throw new InvalidOperationException(
                "Only a draft can be deleted. A published announcement is archived instead, so that "
                + "what was announced stays findable.");

        await _unitOfWork.Repository<HrAnnouncement>().DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PreviewAudienceCountAsync(
        IEnumerable<HrAnnouncementAudienceDto> audiences, CancellationToken cancellationToken = default)
        => await _audience.CountAsync(
            (audiences ?? []).Select(a => new HrAudienceRule(a.TargetType, a.TargetId, a.IsExclusion)),
            cancellationToken);

    public async Task AttachFileAsync(
        Guid id, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var announcement = await Scoped(tenantId).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Announcement not found.");

        announcement.FileUploadRecordId = fileUploadRecordId;
        announcement.DocumentRecordId = documentRecordId;
        announcement.DocumentVersionId = documentVersionId;
        announcement.FilePath = filePath;
        announcement.FileName = fileName;
        announcement.ContentType = contentType;
        announcement.FileSize = fileSize;

        await _unitOfWork.Repository<HrAnnouncement>().UpdateAsync(announcement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static void Validate(CreateHrAnnouncementDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("Give the announcement a title.");
        if (string.IsNullOrWhiteSpace(dto.Body))
            throw new InvalidOperationException("An announcement with no body says nothing.");
        if (dto.EffectiveFrom is { } from && dto.ExpiresOn is { } until && until <= from)
            throw new InvalidOperationException("It cannot expire before it starts showing.");

        foreach (var rule in dto.Audiences ?? [])
        {
            // Everyone and Management (company-schedule D-16) name no record; every other type does.
            if (HrAudienceTargets.NeedsTarget(rule.TargetType) && rule.TargetId is null)
                throw new InvalidOperationException(
                    $"An audience rule for '{rule.TargetType}' must name which one.");
        }
    }

    private static void ApplyAudiences(
        HrAnnouncement announcement, IEnumerable<HrAnnouncementAudienceDto> audiences, Guid tenantId)
    {
        foreach (var rule in audiences ?? [])
        {
            announcement.Audiences.Add(new HrAnnouncementAudience
            {
                TenantId = tenantId,
                TargetType = rule.TargetType,
                // AllEmployees needs no id, and storing one would be a lie about the rule.
                TargetId = HrAudienceTargets.NeedsTarget(rule.TargetType) ? rule.TargetId : null,
                IsExclusion = rule.IsExclusion,
            });
        }
    }

    private async Task<HrAnnouncementDto> RequireDtoAsync(Guid id, CancellationToken ct)
        => await GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Announcement not found.");

    /// <summary>
    /// Resolves every audience row's target to a display name in one pass per entity type, so a
    /// list of announcements does not issue a lookup per rule.
    /// </summary>
    private async Task<Dictionary<Guid, string>> TargetNamesAsync(
        Guid tenantId, IEnumerable<HrAnnouncementAudience> audiences, CancellationToken ct)
    {
        var byType = (audiences ?? [])
            .Where(a => a.TargetId.HasValue)
            .GroupBy(a => a.TargetType)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TargetId!.Value).Distinct().ToList());

        var names = new Dictionary<Guid, string>();

        // Written out per type rather than generically on purpose: a generic helper taking
        // Func<T, Guid> cannot be translated to SQL, so it would have to materialise the whole
        // table and filter in memory — which for the Employee axis means pulling every employee
        // in the tenant to label a handful of audience rows.
        List<Guid> Ids(HrAudienceTargetType type) =>
            byType.TryGetValue(type, out var ids) ? ids : [];

        void Absorb(IEnumerable<(Guid Id, string Name)> rows)
        {
            foreach (var (id, name) in rows)
            {
                names[id] = name;
            }
        }

        var unitIds = Ids(HrAudienceTargetType.OrganizationUnit);
        if (unitIds.Count > 0)
        {
            Absorb((await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                    .Where(x => x.TenantId == tenantId && unitIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)));
        }

        var positionIds = Ids(HrAudienceTargetType.Position);
        if (positionIds.Count > 0)
        {
            Absorb((await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
                    .Where(x => x.TenantId == tenantId && positionIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Title }).ToListAsync(ct))
                .Select(x => (x.Id, x.Title)));
        }

        var levelIds = Ids(HrAudienceTargetType.OrganizationLevel);
        if (levelIds.Count > 0)
        {
            Absorb((await _unitOfWork.Repository<OrganizationLevel>().GetQueryable()
                    .Where(x => x.TenantId == tenantId && levelIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)));
        }

        var locationIds = Ids(HrAudienceTargetType.Location);
        if (locationIds.Count > 0)
        {
            Absorb((await _unitOfWork.Repository<Location>().GetQueryable()
                    .Where(x => x.TenantId == tenantId && locationIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name }).ToListAsync(ct))
                .Select(x => (x.Id, x.Name)));
        }

        var employeeIds = Ids(HrAudienceTargetType.Employee);
        if (employeeIds.Count > 0)
        {
            Absorb((await _unitOfWork.Repository<Employee>().GetQueryable()
                    .Where(x => x.TenantId == tenantId && employeeIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.FirstName, x.LastName }).ToListAsync(ct))
                .Select(x => (x.Id, $"{x.FirstName} {x.LastName}".Trim())));
        }

        return names;
    }

    private static HrAnnouncementDto Map(HrAnnouncement a, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Summary = a.Summary,
        Body = a.Body,
        Category = a.Category,
        Status = a.Status,
        IsPinned = a.IsPinned,
        IsLive = a.IsLive,
        PublishedAt = a.PublishedAt,
        PublishedByName = a.PublishedBy?.FullName,
        EffectiveFrom = a.EffectiveFrom,
        ExpiresOn = a.ExpiresOn,
        ArchivedAt = a.ArchivedAt,
        ArchivedByName = a.ArchivedBy?.FullName,
        HasAttachment = a.FileUploadRecordId.HasValue,
        FileName = a.FileName,
        Audiences = a.Audiences
            .Where(x => !x.IsDeleted)
            .Select(x => new HrAnnouncementAudienceDto
            {
                Id = x.Id,
                TargetType = x.TargetType,
                TargetId = x.TargetId,
                IsExclusion = x.IsExclusion,
                TargetName = !HrAudienceTargets.NeedsTarget(x.TargetType)
                    ? HrAudienceTargets.TypeLabel(x.TargetType)
                    : x.TargetId is { } t && names.TryGetValue(t, out var n) ? n : null,
            })
            .ToList(),
    };

    private static MyAnnouncementDto MapMine(HrAnnouncement a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        // The dashboard needs one line. Falling back to the body's opening beats an empty row.
        Summary = string.IsNullOrWhiteSpace(a.Summary)
            ? (a.Body.Length > 160 ? a.Body[..160].TrimEnd() + "…" : a.Body)
            : a.Summary,
        Body = a.Body,
        Category = a.Category,
        IsPinned = a.IsPinned,
        PublishedAt = a.PublishedAt ?? a.CreatedAt,
        PublishedByName = a.PublishedBy?.FullName,
        ExpiresOn = a.ExpiresOn,
        HasAttachment = a.FileUploadRecordId.HasValue,
        FileName = a.FileName,
    };
}
