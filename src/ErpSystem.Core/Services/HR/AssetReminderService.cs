using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Sweeps the asset register for anything that needs chasing and dispatches a reminder for each,
/// once. Area 16, slice 9 (AST-1) and slice 11.
/// </summary>
/// <remarks>
/// <para><b>Eight rungs over three subjects</b>, and deliberately one engine rather than three.
/// Slice 9 brought maintenance due, overdue and unscheduled; slice 11 added insurance expiring,
/// expired and undated, and returns due and overdue. A second engine would have meant a second run
/// history, a second dedupe index and a second answer to "did the sweep run last night" — and this
/// system already carries six of these. Every rung shares the escalation ladder, the backlog floor
/// and the send-once guarantee, which is the whole reason they belong together.</para>
/// </remarks>
/// <remarks>
/// <para><b>Why this exists.</b> AST-1 asks that an asset requiring maintenance can be
/// <i>monitored</i>. Slice 9's three reads are half of that; this is the half that goes and finds
/// somebody. Before it, the maintenance schedule was a column that only became visible if a person
/// thought to open a screen and ask — which, for a fire extinguisher or a standby generator, is the
/// year it does not get serviced.</para>
///
/// <para><b>Send-once.</b> Every candidate produces a dedupe key encoding the item, the kind, the
/// due date and the escalation tier. The unique <c>(TenantId, DedupeKey)</c> index is the
/// guarantee: a sweep claims the key by inserting the log row and only publishes afterwards.
/// Rescheduling an asset produces fresh keys, which re-arms the ladder — deliberate, because a
/// maintenance date that has been corrected is genuinely a new thing to chase.</para>
///
/// <para><b>Publish after commit.</b> The rule area 9 recorded and every engine since has kept: an
/// unpublished-but-claimed reminder costs one missed notification, whereas publishing first risks
/// sending the same thing twice, for ever, on every sweep.</para>
/// </remarks>
public class AssetReminderService : IAssetReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssetReminderService> _logger;

    public AssetReminderService(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ICurrentUserService currentUserService,
        ILogger<AssetReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private const string TopicEntityType = "AssetReminder";
    private const string Audience = "Internal";

    /// <summary>How far ahead scheduled maintenance starts being announced.</summary>
    /// <remarks>
    /// 30 days is the working assumption, not TDC's number, and it is the horizon the register's own
    /// <c>due-maintenance</c> read has always defaulted to — the two are deliberately the same, so
    /// what the sweep chases and what the screen shows cannot drift apart. Flagged for TDC with the
    /// other assumed windows.
    /// </remarks>
    private const int MaintenanceDueHorizonDays = 30;

    /// <summary>
    /// How far back the first sweep looks. Without this the first run on an established register
    /// queues every asset ever scheduled and never serviced — area 9's first live run queued 275, of
    /// which 242 were history. A reminder about a service missed two years ago is noise, and the
    /// unscheduled rung catches those assets anyway once their stale date is cleared.
    /// </summary>
    private const int BacklogHorizonDays = 90;

    /// <summary>How far ahead an insurance policy starts being chased for renewal.</summary>
    /// <remarks>
    /// 60 days rather than maintenance's 30, and the same number the register's own
    /// <c>insurance/expiring</c> read defaults to — the two are deliberately identical so what the
    /// sweep chases and what the screen shows cannot drift apart. A service can be booked in a
    /// fortnight; a renewal is a quotation, an approval and a payment, and a broker who is told
    /// four weeks before expiry is being told late. ⚠ Assumed, not TDC's.
    /// </remarks>
    private const int InsuranceExpiryHorizonDays = 60;

    /// <summary>How far ahead a custody starts being chased for return.</summary>
    /// <remarks>
    /// 14 days, matching <c>assignments/due-for-return</c>. Shorter than either of the others on
    /// purpose: an expected return date is an arrangement between two people, and a reminder six
    /// weeks out is a reminder nobody acts on and everybody learns to skip.
    /// </remarks>
    private const int ReturnDueHorizonDays = 14;

    // ---- the sweep ---------------------------------------------------------

    public async Task<AssetReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var run = new AssetReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
        };
        await _unitOfWork.Repository<AssetReminderRun>().AddAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var candidates = (await FindCandidatesAsync(tenantId, now, cancellationToken)).ToList();

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadySent = await _unitOfWork.Repository<AssetReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken);
        var sentSet = new HashSet<string>(alreadySent, StringComparer.Ordinal);

        var fresh = candidates.Where(c => !sentSet.Contains(c.DedupeKey)).ToList();

        foreach (var c in fresh)
        {
            await _unitOfWork.Repository<AssetReminderDispatchLog>().AddAsync(new AssetReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = c.Kind,
                ItemType = c.ItemType,
                EntityId = c.EntityId,
                AssetId = c.AssetId,
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
            _logger.LogWarning(ex, "Asset reminder sweep lost a dedupe race for tenant {TenantId}", tenantId);
            throw new InvalidOperationException(
                "Another asset reminder sweep is running for this tenant. Try again in a moment.");
        }

        await EnsureTopicsAsync(tenantId, cancellationToken);

        // Publish only after the claim commits — see the remarks on this class.
        foreach (var c in fresh)
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = c.Activity,
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
            "Asset reminder sweep for tenant {TenantId} queued {Count} reminder(s) ({Trigger}); {Skipped} already sent",
            tenantId, fresh.Count, trigger, candidates.Count - fresh.Count);

        return new AssetReminderRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Trigger = run.Trigger,
            RemindersQueued = fresh.Count,
            AlreadySent = candidates.Count - fresh.Count,
        };
    }

    public async Task<IEnumerable<AssetReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var at = asOf ?? DateTime.UtcNow;

        var candidates = (await FindCandidatesAsync(tenantId, at, cancellationToken)).ToList();
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var sent = new HashSet<string>(await _unitOfWork.Repository<AssetReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken), StringComparer.Ordinal);

        return candidates.Select(c => new AssetReminderPreviewItemDto
        {
            Kind = c.Kind, ItemType = c.ItemType, EntityId = c.EntityId, AssetId = c.AssetId,
            Reference = c.Reference, DueDate = c.DueDate, DaysRemaining = c.DaysRemaining,
            EscalationTier = c.EscalationTier, DedupeKey = c.DedupeKey,
            AlreadySent = sent.Contains(c.DedupeKey),
        }).ToList();
    }

    public async Task<IEnumerable<AssetReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        return await _unitOfWork.Repository<AssetReminderRun>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 200))
            .Select(r => new AssetReminderRunDto
            {
                Id = r.Id, StartedAt = r.StartedAt, CompletedAt = r.CompletedAt,
                Trigger = r.Trigger, TriggeredByUserId = r.TriggeredByUserId,
                RemindersQueued = r.RemindersQueued,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AssetReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        return await _unitOfWork.Repository<AssetReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= since)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new AssetReminderLogEntryDto
            {
                Id = l.Id, RunId = l.RunId, Kind = l.Kind, ItemType = l.ItemType,
                EntityId = l.EntityId, AssetId = l.AssetId, Reference = l.Reference,
                DueDate = l.DueDate, DaysRemaining = l.DaysRemaining,
                EscalationTier = l.EscalationTier, CreatedAt = l.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    // ---- candidates --------------------------------------------------------

    private sealed record Candidate(
        string Kind, string Activity, string ItemType, Guid EntityId, Guid AssetId, string Reference,
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

    /// <summary>
    /// The statuses at which an asset stops needing to be serviced.
    /// </summary>
    /// <remarks>
    /// A disposed asset is not ours and a lost one cannot be brought in, so chasing either is pure
    /// noise — and noise is what kills a reminder engine, because the reader learns to skip it.
    /// <c>Damaged</c> is deliberately NOT in this list: damaged and awaiting attention is the state
    /// maintenance exists for, and an engine that fell silent exactly when an asset broke would be
    /// the opposite of what AST-1 asks for.
    /// </remarks>
    private static readonly CompanyAssetStatus[] NotServiceable =
    {
        CompanyAssetStatus.Disposed,
        CompanyAssetStatus.LostStolen,
    };

    private async Task<IEnumerable<Candidate>> FindCandidatesAsync(
        Guid tenantId, DateTime at, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(at);
        var backlogFloor = today.AddDays(-BacklogHorizonDays);
        var horizon = today.AddDays(MaintenanceDueHorizonDays);
        var results = new List<Candidate>();

        // 1 & 2. Scheduled maintenance, due soon or already overdue. ONE query, two rungs: they
        //        select the same rows under the same rule and differ only in the sign of the gap,
        //        so two queries would let the two rungs disagree about the boundary day — and the
        //        boundary day is the one an asset is either on time or late.
        var scheduled = await _unitOfWork.Repository<CompanyAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.RequiresRegularMaintenance
                            && a.NextMaintenanceDate != null
                            && a.NextMaintenanceDate <= horizon
                            && a.NextMaintenanceDate >= backlogFloor
                            && !NotServiceable.Contains(a.Status))
            .Select(a => new { a.Id, a.AssetNumber, a.AssetName, a.NextMaintenanceDate })
            .ToListAsync(cancellationToken);

        foreach (var a in scheduled)
        {
            var days = a.NextMaintenanceDate!.Value.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            var overdue = days < 0;
            var kind = overdue ? "MaintenanceOverdue" : "MaintenanceDueSoon";

            results.Add(new Candidate(
                kind,
                overdue ? "Overdue" : "DueSoon",
                "Company asset",
                a.Id,
                a.Id,
                $"{a.AssetNumber} {a.AssetName}".Trim(),
                a.NextMaintenanceDate.Value.ToDateTime(TimeOnly.MinValue),
                days,
                tier,
                $"{kind}:{a.Id}:{a.NextMaintenanceDate:yyyy-MM-dd}:{tier}",
                $"/hr/assets/{a.Id}"));
        }

        // 3. Assets that require regular maintenance and have never been given a date.
        //    These appear on no other list in the module — every maintenance read filters on
        //    NextMaintenanceDate != null — so without this rung they are the one asset shape that
        //    can never become due, and therefore can never be chased.
        var unscheduled = await _unitOfWork.Repository<CompanyAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.RequiresRegularMaintenance
                            && a.NextMaintenanceDate == null
                            && !NotServiceable.Contains(a.Status))
            .Select(a => new { a.Id, a.AssetNumber, a.AssetName })
            .ToListAsync(cancellationToken);

        foreach (var a in unscheduled)
        {
            // ⚠ The key carries the MONTH, not a due date, because there is no due date to carry.
            // Keyed on the asset alone this would fire once in the register's lifetime and never
            // again, so an asset nobody schedules would be mentioned once and forgotten; keyed on
            // the day it would arrive every morning until somebody muted the whole engine. A month
            // is the cadence a data-quality nag can survive being ignored.
            results.Add(new Candidate(
                "MaintenanceUnscheduled",
                "Unscheduled",
                "Company asset",
                a.Id,
                a.Id,
                $"{a.AssetNumber} {a.AssetName}".Trim(),
                null,
                0,
                0,
                $"MaintenanceUnscheduled:{a.Id}:{today:yyyy-MM}",
                $"/hr/assets/{a.Id}"));
        }

        // 4 & 5. Insurance, lapsing soon or already lapsed. ONE query and two rungs for the reason
        //        the maintenance pair gives: they select the same rows under the same rule and
        //        differ only in the sign of the gap, so two queries would let them disagree about
        //        the boundary day — and the boundary day is the one a policy is either live or not.
        //
        // ⚠ Disposed is excluded and LostStolen is NOT, which is the opposite of NotServiceable.
        // Nobody renews cover on something they have sold; they very much do keep it on something
        // that was stolen, because the policy is what the claim runs against and a lapse mid-claim
        // is the loss twice over.
        var insured = await _unitOfWork.Repository<CompanyAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.IsInsured
                            && a.InsuranceExpiryDate != null
                            && a.InsuranceExpiryDate <= today.AddDays(InsuranceExpiryHorizonDays)
                            && a.InsuranceExpiryDate >= backlogFloor
                            && a.Status != CompanyAssetStatus.Disposed)
            .Select(a => new { a.Id, a.AssetNumber, a.AssetName, a.InsuranceExpiryDate })
            .ToListAsync(cancellationToken);

        foreach (var a in insured)
        {
            var days = a.InsuranceExpiryDate!.Value.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            var lapsed = days < 0;
            var kind = lapsed ? "InsuranceExpired" : "InsuranceExpiringSoon";

            results.Add(new Candidate(
                kind,
                lapsed ? "InsuranceExpired" : "InsuranceExpiring",
                "Company asset",
                a.Id,
                a.Id,
                $"{a.AssetNumber} {a.AssetName}".Trim(),
                a.InsuranceExpiryDate.Value.ToDateTime(TimeOnly.MinValue),
                days,
                tier,
                $"{kind}:{a.Id}:{a.InsuranceExpiryDate:yyyy-MM-dd}:{tier}",
                $"/hr/assets/{a.Id}"));
        }

        // 6. Assets marked insured with no expiry date — the insurance twin of the unscheduled
        //    maintenance rung, keyed on the MONTH for the same reason: there is no due date to key
        //    on, so per-asset would fire once in the register's lifetime and per-day would arrive
        //    every morning until somebody muted the engine.
        var undated = await _unitOfWork.Repository<CompanyAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.IsInsured
                            && a.InsuranceExpiryDate == null
                            && a.Status != CompanyAssetStatus.Disposed)
            .Select(a => new { a.Id, a.AssetNumber, a.AssetName })
            .ToListAsync(cancellationToken);

        foreach (var a in undated)
        {
            results.Add(new Candidate(
                "InsuranceUndated",
                "InsuranceUndated",
                "Company asset",
                a.Id,
                a.Id,
                $"{a.AssetNumber} {a.AssetName}".Trim(),
                null,
                0,
                0,
                $"InsuranceUndated:{a.Id}:{today:yyyy-MM}",
                $"/hr/assets/{a.Id}"));
        }

        // 7 & 8. Custodies coming due back, and custodies already late. The reminder goes to HR
        //        rather than to the holder: the topic recipient is the HR role, as every rung here
        //        is, and chasing the employee directly is a decision about tone that belongs with
        //        TDC rather than with a sweep.
        //
        // ⚠ Keyed on the ASSIGNMENT, not the asset, and the Reference names the asset. An employee
        // who is issued a replacement laptop under a new assignment must be chased for the new one
        // on its own dates; keyed on the asset the two custodies would share a dedupe key and the
        // second would be silently swallowed by the first.
        var custodies = await _unitOfWork.Repository<AssetAssignment>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Overdue)
                            && a.ExpectedReturnDate != null
                            && a.ExpectedReturnDate <= today.AddDays(ReturnDueHorizonDays)
                            && a.ExpectedReturnDate >= backlogFloor)
            .Select(a => new
            {
                a.Id, a.AssetId, a.ExpectedReturnDate,
                a.Asset.AssetNumber, a.Asset.AssetName,
            })
            .ToListAsync(cancellationToken);

        foreach (var c in custodies)
        {
            var days = c.ExpectedReturnDate!.Value.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            var late = days < 0;
            var kind = late ? "ReturnOverdue" : "ReturnDueSoon";

            results.Add(new Candidate(
                kind,
                late ? "ReturnOverdue" : "ReturnDueSoon",
                "Asset assignment",
                c.Id,
                c.AssetId,
                $"{c.AssetNumber} {c.AssetName}".Trim(),
                c.ExpectedReturnDate.Value.ToDateTime(TimeOnly.MinValue),
                days,
                tier,
                $"{kind}:{c.Id}:{c.ExpectedReturnDate:yyyy-MM-dd}:{tier}",
                $"/hr/assets/{c.AssetId}"));
        }

        return results;
    }

    // ---- topics ------------------------------------------------------------

    private sealed record TopicSeed(string Activity, string Name, string Description,
        string TitleTemplate, string BodyTemplate);

    /// <remarks>
    /// The templates name the asset and the date and nothing else — no serial number, no location,
    /// no value, no holder. A reminder travels further than the record it is about, and an asset
    /// register is a shopping list for anybody who can read one.
    /// </remarks>
    private static readonly TopicSeed[] TopicSeeds =
    {
        new("DueSoon", "Assets: Maintenance due soon",
            "System-seeded asset reminder — scheduled maintenance is approaching.",
            "Maintenance due soon: {{Reference}}",
            "{{ItemType}} {{Reference}} is due for maintenance on {{DueDate}} — {{Days}} day(s) remaining."),
        new("Overdue", "Assets: Maintenance overdue",
            "System-seeded asset reminder — scheduled maintenance is past due.",
            "Maintenance overdue: {{Reference}}",
            "{{ItemType}} {{Reference}} was due for maintenance on {{DueDate}} — {{Days}} day(s) overdue (tier {{EscalationTier}})."),
        new("Unscheduled", "Assets: Maintenance not scheduled",
            "System-seeded asset reminder — an asset requires regular maintenance and has no next date.",
            "No maintenance scheduled: {{Reference}}",
            "{{ItemType}} {{Reference}} is marked as requiring regular maintenance but has no next maintenance date."),

        // ── slice 11 ──────────────────────────────────────────────────────────
        new("InsuranceExpiring", "Assets: Insurance expiring",
            "System-seeded asset reminder — an asset's insurance cover is approaching its expiry date.",
            "Insurance expiring: {{Reference}}",
            "{{ItemType}} {{Reference}} has insurance cover expiring on {{DueDate}} — {{Days}} day(s) remaining."),
        new("InsuranceExpired", "Assets: Insurance expired",
            "System-seeded asset reminder — an asset's insurance cover has lapsed.",
            "Insurance expired: {{Reference}}",
            "{{ItemType}} {{Reference}} has been uninsured since {{DueDate}} — {{Days}} day(s) (tier {{EscalationTier}})."),
        new("InsuranceUndated", "Assets: Insurance has no expiry date",
            "System-seeded asset reminder — an asset is marked insured and carries no expiry date.",
            "No insurance expiry recorded: {{Reference}}",
            "{{ItemType}} {{Reference}} is marked as insured but has no expiry date, so its cover cannot be renewed on time."),
        new("ReturnDueSoon", "Assets: Return due",
            "System-seeded asset reminder — an issued asset is due back.",
            "Asset due back: {{Reference}}",
            "{{ItemType}} {{Reference}} is due back on {{DueDate}} — {{Days}} day(s) remaining."),
        new("ReturnOverdue", "Assets: Return overdue",
            "System-seeded asset reminder — an issued asset is past its expected return date.",
            "Asset return overdue: {{Reference}}",
            "{{ItemType}} {{Reference}} was due back on {{DueDate}} — {{Days}} day(s) overdue (tier {{EscalationTier}})."),
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
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }
}
