using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF MOVEMENT REMINDER ENGINE — area 8 slice 5.
//
// The area computed four queues and told nobody about any of them. The dashboard
// counted temporary assignments about to end, returns nobody had processed,
// approvals sitting past the five-day mark and approved movements whose effective
// date had come and gone — all of it visible only to someone who happened to open
// the right screen on the right day. A movement's whole point is that it takes
// effect on a date; a queue nobody is told about is how that date slips.
//
// Structure mirrors the SHE engine (area 10 slice 13): one sweep per tenant, all
// logic in the service so the hourly host and the HR-gated run-now endpoint share
// exactly one code path, and a dispatch log whose unique (TenantId, DedupeKey)
// index is the send-once guarantee. A key encodes item + kind + due date + ladder
// rung (or escalation tier), so each rung fires once and a rescheduled date
// re-arms the ladder by producing fresh keys.
//
// ⚠ Platform caveat inherited from SHE: publishing to a topic that has no ACTIVE
// row is a silent no-op, so the sweep self-heals its own topics per tenant first —
// create-if-missing only, because deactivating a topic is the supported way to
// mute it and must not be undone on the next sweep.
// ============================================================================

public class StaffMovementReminderService : IStaffMovementReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<StaffMovementReminderService> _logger;

    public StaffMovementReminderService(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        ILogger<StaffMovementReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private const string TopicEntityType = "StaffMovement";
    private const string Audience = "Internal";

    /// <summary>
    /// Days an approval may sit before it is called overdue. Matches the movement dashboard's own
    /// overdue rule, deliberately — two different definitions of "overdue" on the same records is
    /// how a queue and its reminder end up disagreeing.
    /// </summary>
    private const int ApprovalOverdueDays = 5;

    private static readonly int[] EndingSoonLadder = { 30, 14, 7 };
    private static readonly int[] ActingEndingLadder = { 14, 7 };

    private static int EscalationTier(int daysOverdue) => daysOverdue <= 7 ? 1 : daysOverdue <= 30 ? 2 : 3;

    /// <summary>
    /// The rung to fire for a not-yet-due item, or null when it is still beyond the widest rung.
    /// Picks the smallest threshold at or above the days remaining, so a sweep that was down for a
    /// while fires the current rung rather than back-filling stale ones.
    /// </summary>
    private static int? DueSoonRung(int daysRemaining, int[] thresholds)
    {
        var rung = thresholds.Where(t => t >= daysRemaining).DefaultIfEmpty(-1).Min();
        return rung < 0 ? null : rung;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ── read models ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<StaffMovementReminderRunDto>> GetRecentRunsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var runs = await _unitOfWork.Repository<StaffMovementReminderRun>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);

        return runs.Select(r => new StaffMovementReminderRunDto
        {
            Id = r.Id,
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt,
            Trigger = r.Trigger,
            RemindersQueued = r.RemindersQueued,
        }).ToList();
    }

    public async Task<IEnumerable<StaffMovementReminderLogEntryDto>> GetRecentLogAsync(int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var floor = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 90));

        var entries = await _unitOfWork.Repository<StaffMovementReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= floor)
            .OrderByDescending(l => l.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entries.Select(l => new StaffMovementReminderLogEntryDto
        {
            Id = l.Id,
            RunId = l.RunId,
            Kind = l.Kind,
            ItemType = l.ItemType,
            EntityId = l.EntityId,
            Reference = l.Reference,
            DueDate = l.DueDate,
            DaysRemaining = l.DaysRemaining,
            EscalationTier = l.EscalationTier,
            DispatchedAt = l.CreatedAt,
        }).ToList();
    }

    // ── the sweep ────────────────────────────────────────────────────────────

    private sealed record PendingReminder(
        string Kind,
        string ItemType,
        Guid EntityId,
        string Reference,
        DateTime? DueDate,
        int DaysRemaining,
        int EscalationTier,
        string DedupeKey,
        string Activity,
        string ActionPath);

    public async Task<StaffMovementReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to run the staff movement reminder sweep.");

        var now = DateTime.UtcNow;
        var today = now.Date;

        await EnsureTopicsAsync(tenantId, cancellationToken);

        var pending = new List<PendingReminder>();

        await SweepTemporaryEndingAsync(tenantId, today, pending, cancellationToken);
        await SweepReturnsOverdueAsync(tenantId, today, pending, cancellationToken);
        await SweepApprovalsOverdueAsync(tenantId, today, pending, cancellationToken);
        await SweepAwaitingImplementationAsync(tenantId, today, pending, cancellationToken);
        await SweepAwaitingEmployeeAsync(tenantId, today, pending, cancellationToken);
        await SweepActingEndingAsync(tenantId, today, pending, cancellationToken);

        var deduped = await FilterAlreadyDispatchedAsync(tenantId, pending, cancellationToken);

        var run = new StaffMovementReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
        };
        await _unitOfWork.Repository<StaffMovementReminderRun>().AddAsync(run);

        var logRepo = _unitOfWork.Repository<StaffMovementReminderDispatchLog>();
        foreach (var p in deduped)
        {
            await logRepo.AddAsync(new StaffMovementReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = p.Kind,
                ItemType = p.ItemType,
                EntityId = p.EntityId,
                Reference = p.Reference,
                DueDate = p.DueDate,
                DaysRemaining = p.DaysRemaining,
                EscalationTier = p.EscalationTier,
                DedupeKey = p.DedupeKey,
            });
        }

        run.RemindersQueued = deduped.Count;
        run.CompletedAt = DateTime.UtcNow;

        try
        {
            // One save claims every dedupe key and records the run together.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Staff movement reminder sweep lost a dedupe race for tenant {TenantId}", tenantId);
            throw new InvalidOperationException(
                "Another staff movement reminder sweep is running for this tenant. Try again in a moment.");
        }

        // Publish only after the claim commits: an unpublished-but-claimed reminder is a missed
        // notification, whereas publishing first risks sending the same thing twice.
        foreach (var p in deduped)
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = p.Activity,
                Audience = Audience,
                EntityId = p.EntityId,
                TriggeredByUserId = triggeredByUserId,
                Data = new Dictionary<string, object>
                {
                    ["ItemType"] = p.ItemType,
                    ["Reference"] = p.Reference,
                    ["DueDate"] = p.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["Days"] = Math.Abs(p.DaysRemaining),
                    ["EscalationTier"] = p.EscalationTier,
                    ["ActionPath"] = p.ActionPath,
                },
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Staff movement reminder sweep for tenant {TenantId} queued {Count} reminder(s) ({Trigger})",
            tenantId, deduped.Count, trigger);

        return new StaffMovementReminderRunResultDto
        {
            RunId = run.Id,
            RemindersQueued = deduped.Count,
            ByKind = deduped.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    // ── sweeps ───────────────────────────────────────────────────────────────

    private static readonly StaffMovementStatus[] AwaitingApproval =
    {
        StaffMovementStatus.Submitted,
        StaffMovementStatus.CurrentSupervisorApproval,
        StaffMovementStatus.NewSupervisorApproval,
        StaffMovementStatus.CurrentHodApproval,
        StaffMovementStatus.NewHodApproval,
        StaffMovementStatus.HrReview,
        StaffMovementStatus.ManagementApproval,
    };

    private IQueryable<StaffMovement> Movements(Guid tenantId)
        => _unitOfWork.Repository<StaffMovement>()
            .GetQueryable(m => m.TenantId == tenantId && !m.IsDeleted);

    /// <summary>A temporary assignment approaching its end — someone has to plan the return.</summary>
    private async Task SweepTemporaryEndingAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(EndingSoonLadder.Max());

        var items = await Movements(tenantId)
            .Where(m => m.IsTemporary
                     && !m.ReturnProcessed
                     && m.TemporaryEndDate != null
                     && m.TemporaryEndDate >= today
                     && m.TemporaryEndDate <= horizon
                     && m.Status == StaffMovementStatus.Implemented)
            .Select(m => new { m.Id, m.MovementNumber, m.TemporaryEndDate, m.MovementType })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.TemporaryEndDate!.Value.Date;
            var daysRemaining = (int)(due - today).TotalDays;
            var rung = DueSoonRung(daysRemaining, EndingSoonLadder);
            if (rung == null) continue;

            pending.Add(new PendingReminder(
                Kind: "TemporaryAssignmentEnding",
                ItemType: $"{item.MovementType} (temporary)",
                EntityId: item.Id,
                Reference: item.MovementNumber,
                DueDate: due,
                DaysRemaining: daysRemaining,
                EscalationTier: 0,
                DedupeKey: $"mov:ending:{item.Id:N}:{due:yyyyMMdd}:{rung}",
                Activity: "DueSoon",
                ActionPath: $"/hr/movements/{item.Id}"));
        }
    }

    /// <summary>
    /// A temporary assignment whose end date has passed with no return processed. The employee is
    /// still sitting in the host post, and every day that passes makes the record less true.
    /// </summary>
    private async Task SweepReturnsOverdueAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var items = await Movements(tenantId)
            .Where(m => m.IsTemporary
                     && !m.ReturnProcessed
                     && m.TemporaryEndDate != null
                     && m.TemporaryEndDate < today
                     && m.Status == StaffMovementStatus.Implemented)
            .Select(m => new { m.Id, m.MovementNumber, m.TemporaryEndDate, m.MovementType })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.TemporaryEndDate!.Value.Date;
            var daysOverdue = (int)(today - due).TotalDays;
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                Kind: "TemporaryReturnOverdue",
                ItemType: $"{item.MovementType} return",
                EntityId: item.Id,
                Reference: item.MovementNumber,
                DueDate: due,
                DaysRemaining: -daysOverdue,
                EscalationTier: tier,
                DedupeKey: $"mov:return-overdue:{item.Id:N}:{due:yyyyMMdd}:t{tier}",
                Activity: tier >= 2 ? "OverdueEscalated" : "Overdue",
                ActionPath: $"/hr/movements/{item.Id}"));
        }
    }

    /// <summary>An approval nobody has actioned. Escalates by how long it has been sitting.</summary>
    private async Task SweepApprovalsOverdueAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(-ApprovalOverdueDays);

        var items = await Movements(tenantId)
            .Where(m => AwaitingApproval.Contains(m.Status) && m.RequestSubmissionDate < cutoff)
            .Select(m => new { m.Id, m.MovementNumber, m.RequestSubmissionDate, m.MovementType, m.EffectiveDate })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var waitingSince = item.RequestSubmissionDate.Date;
            var daysWaiting = (int)(today - waitingSince).TotalDays;
            var tier = EscalationTier(daysWaiting - ApprovalOverdueDays);

            pending.Add(new PendingReminder(
                Kind: "ApprovalOverdue",
                ItemType: $"{item.MovementType} approval",
                EntityId: item.Id,
                Reference: item.MovementNumber,
                DueDate: item.EffectiveDate.Date,
                DaysRemaining: -daysWaiting,
                EscalationTier: tier,
                DedupeKey: $"mov:approval-overdue:{item.Id:N}:{waitingSince:yyyyMMdd}:t{tier}",
                Activity: tier >= 2 ? "OverdueEscalated" : "Overdue",
                ActionPath: $"/hr/movements/{item.Id}"));
        }
    }

    /// <summary>
    /// An approved movement whose effective date has arrived and which nobody has implemented.
    /// This is the one that costs money: the employee is doing the new job on the old record.
    /// </summary>
    private async Task SweepAwaitingImplementationAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var items = await Movements(tenantId)
            .Where(m => m.Status == StaffMovementStatus.Approved && m.EffectiveDate <= today)
            .Select(m => new { m.Id, m.MovementNumber, m.EffectiveDate, m.MovementType })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.EffectiveDate.Date;
            var daysOverdue = (int)(today - due).TotalDays;
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                Kind: "AwaitingImplementation",
                ItemType: $"{item.MovementType} implementation",
                EntityId: item.Id,
                Reference: item.MovementNumber,
                DueDate: due,
                DaysRemaining: -daysOverdue,
                EscalationTier: tier,
                DedupeKey: $"mov:implement-due:{item.Id:N}:{due:yyyyMMdd}:t{tier}",
                Activity: tier >= 2 ? "OverdueEscalated" : "Overdue",
                ActionPath: $"/hr/movements/{item.Id}"));
        }
    }

    /// <summary>
    /// A movement waiting on the employee's own acceptance. Nobody else can give it, so nothing
    /// moves until they do — and the effective date carries on approaching regardless.
    /// </summary>
    private async Task SweepAwaitingEmployeeAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(EndingSoonLadder.Max());

        var items = await Movements(tenantId)
            .Where(m => m.RequiresEmployeeAcceptance
                     && m.EmployeeAccepted == null
                     && m.Status != StaffMovementStatus.Cancelled
                     && m.Status != StaffMovementStatus.Rejected
                     && m.Status != StaffMovementStatus.Implemented
                     && m.EffectiveDate <= horizon)
            .Select(m => new { m.Id, m.MovementNumber, m.EffectiveDate, m.MovementType })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.EffectiveDate.Date;
            var daysRemaining = (int)(due - today).TotalDays;

            if (daysRemaining < 0)
            {
                var tier = EscalationTier(-daysRemaining);
                pending.Add(new PendingReminder(
                    Kind: "EmployeeAcceptanceOverdue",
                    ItemType: $"{item.MovementType} acceptance",
                    EntityId: item.Id,
                    Reference: item.MovementNumber,
                    DueDate: due,
                    DaysRemaining: daysRemaining,
                    EscalationTier: tier,
                    DedupeKey: $"mov:acceptance-overdue:{item.Id:N}:{due:yyyyMMdd}:t{tier}",
                    Activity: tier >= 2 ? "OverdueEscalated" : "Overdue",
                    ActionPath: $"/hr/movements/{item.Id}"));
                continue;
            }

            var rung = DueSoonRung(daysRemaining, EndingSoonLadder);
            if (rung == null) continue;

            pending.Add(new PendingReminder(
                Kind: "EmployeeAcceptancePending",
                ItemType: $"{item.MovementType} acceptance",
                EntityId: item.Id,
                Reference: item.MovementNumber,
                DueDate: due,
                DaysRemaining: daysRemaining,
                EscalationTier: 0,
                DedupeKey: $"mov:acceptance:{item.Id:N}:{due:yyyyMMdd}:{rung}",
                Activity: "DueSoon",
                ActionPath: $"/hr/movements/{item.Id}"));
        }
    }

    /// <summary>An acting appointment approaching its end — extend it, end it, or make it permanent.</summary>
    private async Task SweepActingEndingAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(ActingEndingLadder.Max());

        var items = await _unitOfWork.Repository<StaffActingAppointment>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted)
            .Where(a => (a.Status == StaffActingStatus.Active || a.Status == StaffActingStatus.Extended)
                     && a.EndDate != null
                     && a.EndDate >= today
                     && a.EndDate <= horizon)
            .Select(a => new { a.Id, a.AppointmentNumber, a.EndDate })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.EndDate!.Value.Date;
            var daysRemaining = (int)(due - today).TotalDays;
            var rung = DueSoonRung(daysRemaining, ActingEndingLadder);
            if (rung == null) continue;

            pending.Add(new PendingReminder(
                Kind: "ActingAppointmentEnding",
                ItemType: "Acting appointment",
                EntityId: item.Id,
                Reference: item.AppointmentNumber,
                DueDate: due,
                DaysRemaining: daysRemaining,
                EscalationTier: 0,
                DedupeKey: $"mov:acting-ending:{item.Id:N}:{due:yyyyMMdd}:{rung}",
                Activity: "DueSoon",
                ActionPath: "/hr/movements/acting"));
        }
    }

    // ── dedupe ───────────────────────────────────────────────────────────────

    private async Task<List<PendingReminder>> FilterAlreadyDispatchedAsync(
        Guid tenantId, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        if (pending.Count == 0) return pending;

        var distinct = pending
            .GroupBy(p => p.DedupeKey)
            .Select(g => g.First())
            .ToList();

        var repo = _unitOfWork.Repository<StaffMovementReminderDispatchLog>();
        var already = new HashSet<string>();

        foreach (var chunk in distinct.Select(p => p.DedupeKey).Chunk(500))
        {
            var found = await repo
                .GetQueryable(l => l.TenantId == tenantId && chunk.Contains(l.DedupeKey))
                .Select(l => l.DedupeKey)
                .ToListAsync(cancellationToken);
            foreach (var key in found) already.Add(key);
        }

        return distinct.Where(p => !already.Contains(p.DedupeKey)).ToList();
    }

    // ── topics ───────────────────────────────────────────────────────────────

    private sealed record TopicSeed(
        string Activity,
        string Name,
        string Description,
        string TitleTemplate,
        string BodyTemplate,
        bool EscalatesToAdmins = false);

    private static readonly TopicSeed[] TopicSeeds =
    {
        new("DueSoon", "Staff Movement: Due Soon",
            "System-seeded movement reminder — a dated movement obligation is approaching.",
            "Movement due soon: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} is due on {{DueDate}} — {{Days}} day(s) remaining."),
        new("Overdue", "Staff Movement: Overdue",
            "System-seeded movement reminder — a movement obligation is past due (escalation tier 1).",
            "Movement overdue: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} — {{Days}} day(s) overdue."),
        new("OverdueEscalated", "Staff Movement: Overdue (Escalated)",
            "System-seeded movement escalation — an overdue movement has reached tier 2 or 3.",
            "Movement escalation (tier {{EscalationTier}}): {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} and is {{Days}} day(s) overdue — escalation tier {{EscalationTier}}.",
            EscalatesToAdmins: true),
    };

    /// <summary>
    /// Creates any missing topic for this tenant. Create-if-missing ONLY: deactivating a topic is
    /// how an administrator mutes a reminder, and a sweep that recreated or reactivated it would
    /// quietly overrule them every hour.
    /// </summary>
    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = TopicSeeds.Select(s => $"{TopicEntityType}.{s.Activity}.{Audience}").ToArray();

        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var created = false;
        foreach (var seed in TopicSeeds)
        {
            var key = $"{TopicEntityType}.{seed.Activity}.{Audience}";
            if (existingSet.Contains(key)) continue;

            var topic = new NotificationTopic
            {
                TenantId = tenantId,
                Key = key,
                Name = seed.Name,
                Description = seed.Description,
                EntityType = TopicEntityType,
                IsSystem = true,
                IsActive = true,
                EnableInApp = true,
                EnableEmail = false,
                EnableSms = false,
                InAppTitleTemplate = seed.TitleTemplate,
                InAppBodyTemplate = seed.BodyTemplate,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);

            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            await recipientRepo.AddAsync(new NotificationTopicRecipient
            {
                TenantId = tenantId,
                TopicId = topic.Id,
                RecipientKind = "Role",
                RecipientValue = Constants.Roles.Hr,
                IsSystem = true,
                SendInApp = true,
                CreatedBy = "System",
            });

            // A movement left unactioned for weeks is an administrative failure, not an HR task —
            // tier 2 and 3 therefore reach beyond the people who already have not acted on it.
            if (seed.EscalatesToAdmins)
            {
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId,
                    TopicId = topic.Id,
                    RecipientKind = "Role",
                    RecipientValue = Constants.Roles.SuperAdmin,
                    IsSystem = true,
                    SendInApp = true,
                    CreatedBy = "System",
                });
            }

            created = true;
        }

        if (created) await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
