using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Sweeps staff travel for dates that need chasing and dispatches a reminder for each, once.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Travel is full of dates that matter and nothing was watching any
/// of them. The endpoints were already there — expiring documents, expiring visas, overdue advance
/// settlements, upcoming departures — returning their rows to nobody. Three other HR areas had a
/// sweep; travel did not, which is why an expired passport could sit unnoticed until somebody was
/// turned away at a gate.</para>
///
/// <para><b>Send-once.</b> Every candidate produces a dedupe key encoding the item, the kind, the
/// date and the escalation tier. The unique <c>(TenantId, DedupeKey)</c> index is the guarantee: a
/// sweep claims the key by inserting the log row, and only publishes afterwards. Moving a date
/// produces fresh keys, which re-arms the ladder — that is deliberate, because a passport whose
/// expiry has been corrected genuinely is a new thing to chase.</para>
///
/// <para><b>Publish after commit.</b> The same rule area 9 records: an unpublished-but-claimed
/// reminder is one missed notification, whereas publishing first risks sending the same thing
/// twice, forever, on every sweep.</para>
/// </remarks>
public class StaffTravelReminderService : IStaffTravelReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<StaffTravelReminderService> _logger;

    public StaffTravelReminderService(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        ILogger<StaffTravelReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private const string TopicEntityType = "StaffTravelReminder";
    private const string Audience = "Internal";

    /// <summary>How far ahead a document or visa expiry starts being chased.</summary>
    /// <remarks>
    /// 90 days is the working assumption, not TDC's number. It is the shortest notice that still
    /// allows a passport renewal in Ghana, which is the constraint that actually matters — a
    /// 30-day warning about a document that takes six weeks to replace is not a warning. Flagged
    /// for TDC alongside the other assumed windows.
    /// </remarks>
    private const int DocumentExpiryHorizonDays = 90;

    /// <summary>How far ahead an approved trip is announced to the desk.</summary>
    private const int DepartureHorizonDays = 14;

    /// <summary>
    /// How far back the first sweep looks. Without this the first run on an established database
    /// queues every historical breach at once — area 9's first live run queued 275, of which 242
    /// were history. A reminder about something that expired two years ago is noise.
    /// </summary>
    private const int BacklogHorizonDays = 90;

    // ---- the sweep ---------------------------------------------------------

    public async Task<StaffTravelReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var run = new StaffTravelReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
        };
        await _unitOfWork.Repository<StaffTravelReminderRun>().AddAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var candidates = (await FindCandidatesAsync(tenantId, now, cancellationToken)).ToList();

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadySent = await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken);
        var sentSet = new HashSet<string>(alreadySent, StringComparer.Ordinal);

        var fresh = candidates.Where(c => !sentSet.Contains(c.DedupeKey)).ToList();

        foreach (var c in fresh)
        {
            await _unitOfWork.Repository<StaffTravelReminderDispatchLog>().AddAsync(new StaffTravelReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = c.Kind,
                ItemType = c.ItemType,
                EntityId = c.EntityId,
                Reference = c.Reference,
                DueDate = c.DueDate,
                DaysRemaining = c.DaysRemaining,
                EscalationTier = c.EscalationTier,
                DedupeKey = c.DedupeKey,
            });
        }

        run.RemindersQueued = fresh.Count;
        run.CompletedAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The unique index did its job: another sweep claimed the same keys first.
            _logger.LogWarning(ex, "Travel reminder sweep lost a dedupe race for tenant {TenantId}", tenantId);
            throw new InvalidOperationException(
                "Another travel reminder sweep is running for this tenant. Try again in a moment.");
        }

        await EnsureTopicsAsync(tenantId, cancellationToken);

        // Publish only after the claim commits — see the remarks on this class.
        foreach (var c in fresh)
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = c.EscalationTier > 0 ? "Overdue" : "DueSoon",
                Audience = Audience,
                EntityId = c.EntityId,
                TriggeredByUserId = triggeredByUserId,
                Data = new Dictionary<string, object>
                {
                    ["ItemType"] = c.ItemType,
                    ["Reference"] = c.Reference,
                    ["DueDate"] = c.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["Days"] = Math.Abs(c.DaysRemaining),
                    ["EscalationTier"] = c.EscalationTier,
                    ["ActionPath"] = c.ActionPath,
                },
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Travel reminder sweep for tenant {TenantId} queued {Count} reminder(s) ({Trigger}); {Skipped} already sent",
            tenantId, fresh.Count, trigger, candidates.Count - fresh.Count);

        return new StaffTravelReminderRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Trigger = run.Trigger,
            RemindersQueued = fresh.Count,
            AlreadySent = candidates.Count - fresh.Count,
        };
    }

    public async Task<IEnumerable<StaffTravelReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var at = asOf ?? DateTime.UtcNow;

        var candidates = (await FindCandidatesAsync(tenantId, at, cancellationToken)).ToList();
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var sent = new HashSet<string>(await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken), StringComparer.Ordinal);

        return candidates.Select(c => new StaffTravelReminderPreviewItemDto
        {
            Kind = c.Kind, ItemType = c.ItemType, EntityId = c.EntityId, Reference = c.Reference,
            DueDate = c.DueDate, DaysRemaining = c.DaysRemaining, EscalationTier = c.EscalationTier,
            DedupeKey = c.DedupeKey, AlreadySent = sent.Contains(c.DedupeKey),
        }).ToList();
    }

    public async Task<IEnumerable<StaffTravelReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        return await _unitOfWork.Repository<StaffTravelReminderRun>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 200))
            .Select(r => new StaffTravelReminderRunDto
            {
                Id = r.Id, StartedAt = r.StartedAt, CompletedAt = r.CompletedAt,
                Trigger = r.Trigger, TriggeredByUserId = r.TriggeredByUserId,
                RemindersQueued = r.RemindersQueued,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<StaffTravelReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        return await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= since)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new StaffTravelReminderLogEntryDto
            {
                Id = l.Id, RunId = l.RunId, Kind = l.Kind, ItemType = l.ItemType,
                EntityId = l.EntityId, Reference = l.Reference, DueDate = l.DueDate,
                DaysRemaining = l.DaysRemaining, EscalationTier = l.EscalationTier,
                CreatedAt = l.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    // ---- candidates --------------------------------------------------------

    private sealed record Candidate(
        string Kind, string ItemType, Guid EntityId, string Reference,
        DateTime? DueDate, int DaysRemaining, int EscalationTier, string DedupeKey, string ActionPath);

    /// <summary>
    /// Escalation ladder shared by every kind: due-soon is tier 0, then 1/2/3 as an overdue item
    /// ages. Expressed once so the kinds cannot drift apart.
    /// </summary>
    private static int TierFor(int daysRemaining) => daysRemaining switch
    {
        >= 0 => 0,
        >= -7 => 1,
        >= -30 => 2,
        _ => 3,
    };

    private async Task<IEnumerable<Candidate>> FindCandidatesAsync(
        Guid tenantId, DateTime at, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(at);
        var backlogFloor = today.AddDays(-BacklogHorizonDays);
        var results = new List<Candidate>();

        // 1. Travel documents — passports and the rest — expiring or expired.
        var docHorizon = today.AddDays(DocumentExpiryHorizonDays);
        var documents = await _unitOfWork.Repository<StaffTravelDocument>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted
                            && d.ExpiryDate != null
                            && d.ExpiryDate <= docHorizon
                            && d.ExpiryDate >= backlogFloor)
            .Select(d => new { d.Id, d.DocumentType, d.ExpiryDate })
            .ToListAsync(cancellationToken);
        foreach (var d in documents)
        {
            var days = d.ExpiryDate!.Value.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            results.Add(new Candidate(
                "TravelDocumentExpiring", d.DocumentType.ToString(), d.Id,
                d.DocumentType.ToString(),
                d.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue), days, tier,
                $"TravelDocumentExpiring:{d.Id}:{d.ExpiryDate:yyyy-MM-dd}:{tier}",
                // Area 25 slice 7: /hr/travel/compliance/documents/{id} never existed as a route,
                // and travel documents are employee-level with no dedicated desk register — the
                // travel dashboard is the closest real surface until one is built.
                "/hr/travel/dashboard"));
        }

        // 2. Visa applications whose visa expires within the horizon.
        var visas = await _unitOfWork.Repository<StaffTravelVisaApplication>()
            .GetQueryable(v => v.TenantId == tenantId && !v.IsDeleted
                            && v.ExpiryDate != null
                            && v.ExpiryDate <= docHorizon
                            && v.ExpiryDate >= backlogFloor)
            .Select(v => new { v.Id, v.ExpiryDate, v.StaffTravelRequestId })
            .ToListAsync(cancellationToken);
        foreach (var v in visas)
        {
            var days = v.ExpiryDate!.Value.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            results.Add(new Candidate(
                "VisaExpiring", "Visa", v.Id, "Visa",
                v.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue), days, tier,
                $"VisaExpiring:{v.Id}:{v.ExpiryDate:yyyy-MM-dd}:{tier}",
                $"/hr/travel/{v.StaffTravelRequestId}"));
        }

        // 3. Advances past their settlement deadline with money still outstanding.
        //    Slice 4 made UnsettledAmount mean something; before that every disbursed advance
        //    would have appeared here for ever.
        var advances = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.UnsettledAmount > 0m
                            && a.SettlementDeadline != null
                            && a.SettlementDeadline < today
                            && a.SettlementDeadline >= backlogFloor
                            && (a.Status == TravelAdvanceStatus.Disbursed
                                || a.Status == TravelAdvanceStatus.PartiallySettled))
            .Select(a => new { a.Id, a.AdvanceNumber, a.SettlementDeadline, a.StaffTravelRequestId })
            .ToListAsync(cancellationToken);
        foreach (var a in advances)
        {
            var days = a.SettlementDeadline!.Value.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            results.Add(new Candidate(
                "AdvanceSettlementOverdue", "Travel advance", a.Id, a.AdvanceNumber ?? string.Empty,
                a.SettlementDeadline.Value.ToDateTime(TimeOnly.MinValue), days, tier,
                $"AdvanceSettlementOverdue:{a.Id}:{a.SettlementDeadline:yyyy-MM-dd}:{tier}",
                // Area 25 slice 7: /hr/travel/finance/advances/{id} never existed — the advance is
                // worked from its request's Finance tab.
                $"/hr/travel/{a.StaffTravelRequestId}"));
        }

        // 4. Approved trips about to depart.
        var departureHorizon = today.AddDays(DepartureHorizonDays);
        var departures = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && r.TravelStartDate >= today
                            && r.TravelStartDate <= departureHorizon
                            && (r.Status == StaffTravelRequestStatus.Approved
                                || r.Status == StaffTravelRequestStatus.InProgress))
            .Select(r => new { r.Id, r.RequestNumber, r.TravelStartDate })
            .ToListAsync(cancellationToken);
        foreach (var r in departures)
        {
            var days = r.TravelStartDate.DayNumber - today.DayNumber;
            results.Add(new Candidate(
                "TripDeparting", "Travel request", r.Id, r.RequestNumber ?? string.Empty,
                r.TravelStartDate.ToDateTime(TimeOnly.MinValue), days, 0,
                $"TripDeparting:{r.Id}:{r.TravelStartDate:yyyy-MM-dd}",
                $"/hr/travel/{r.Id}"));
        }

        return results;
    }

    // ---- topics ------------------------------------------------------------

    private sealed record TopicSeed(string Activity, string Name, string Description,
        string TitleTemplate, string BodyTemplate);

    /// <remarks>
    /// The templates name the item type and the date and nothing else — no passport number, no visa
    /// number, no amount. A reminder travels further than the record it is about.
    /// </remarks>
    private static readonly TopicSeed[] TopicSeeds =
    {
        new("DueSoon", "Travel: Due soon",
            "System-seeded travel reminder — a dated travel obligation is approaching.",
            "Travel due soon: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} is due on {{DueDate}} — {{Days}} day(s) remaining."),
        new("Overdue", "Travel: Overdue",
            "System-seeded travel reminder — a travel obligation is past due.",
            "Travel overdue: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} — {{Days}} day(s) overdue (tier {{EscalationTier}})."),
    };

    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = TopicSeeds.Select(s => $"{TopicEntityType}.{s.Activity}.{Audience}").ToArray();

        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (existingSet.Count == TopicSeeds.Length) return;

        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
        foreach (var seed in TopicSeeds)
        {
            var key = $"{TopicEntityType}.{seed.Activity}.{Audience}";
            if (existingSet.Contains(key)) continue;

            var topic = new NotificationTopic
            {
                TenantId = tenantId, Key = key, Name = seed.Name, Description = seed.Description,
                EntityType = TopicEntityType, IsSystem = true, IsActive = true,
                EnableInApp = true, EnableEmail = false, EnableSms = false,
                InAppTitleTemplate = seed.TitleTemplate,
                InAppBodyTemplate = seed.BodyTemplate,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);
            await recipientRepo.AddAsync(new NotificationTopicRecipient
            {
                TenantId = tenantId, TopicId = topic.Id,
                RecipientKind = "Role", RecipientValue = Constants.Roles.Hr,
                IsSystem = true, SendInApp = true, CreatedBy = "System",
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireTenant()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }
}
