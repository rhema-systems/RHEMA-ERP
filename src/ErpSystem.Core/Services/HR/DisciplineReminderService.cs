using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.StaffGrievance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// DISCIPLINE REMINDER ENGINE — area 9 slice 8.
//
// The area computes a great many deadlines and tells nobody about any of them.
// FR-HR-177's 48-hour written query, FR-HR-178's four-week investigation,
// FR-HR-180's five- and ten-working-day appeal windows, hearings coming up,
// overdue corrective actions, expiring warnings, unpaid fines, and grievances
// sitting at a rung nobody has answered — every one visible only to whoever
// opens the right screen on the right day.
//
// It matters more here than in other areas. A missed deadline in discipline is
// not an inconvenience: it is the fact that makes a sanction or a dismissal
// indefensible when it is challenged.
//
// Structure mirrors the staff-movement engine (area 8 slice 5), which mirrors
// SHE's (area 10 slice 13): all logic in this service so the daily host and the
// HR-gated run-now endpoint share exactly one code path, and a dispatch log whose
// unique (TenantId, DedupeKey) index is the send-once guarantee.
//
// ⚠ EVERY THRESHOLD COMES FROM DisciplineProcessDeadlines OR THE QUEUE ITSELF.
// Nothing here invents a number. Two definitions of "overdue" over the same
// records is how a screen and its reminder end up disagreeing, and this area
// already had that once — "overdue investigation" was a caller-supplied 30 days
// while the requirement said 28.
//
// ⚠ DAILY, NOT HOURLY. The finest thing swept is a date, and a reminder about a
// date is no more useful for being repeated hourly. Dedupe means the cadence only
// affects latency, never duplicates. Same call as the movement engine.
//
// ⚠ Platform caveat inherited from SHE: publishing to a topic with no ACTIVE row
// is a silent no-op, so the sweep self-heals its own topics per tenant first —
// create-if-missing only, because deactivating a topic is the supported way to
// mute a reminder and must not be undone on the next sweep.
// ============================================================================

public class DisciplineReminderService : IDisciplineReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<DisciplineReminderService> _logger;

    public DisciplineReminderService(
        IUnitOfWork unitOfWork,
        IHrWorkingDayCalculator workingDays,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        ILogger<DisciplineReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _workingDays = workingDays;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private const string TopicEntityType = "StaffDisciplinaryAction";
    private const string Audience = "Internal";

    /// <summary>Days a grievance may sit at a rung unanswered before it is chased.</summary>
    /// <remarks>
    /// FR-HR-181 sets no time limit on a rung — it names the route, not a clock. Five days is a
    /// working assumption, flagged like the query-response window: TDC should confirm it. Named here
    /// so there is one place to change.
    /// </remarks>
    private const int GrievanceRungChaseDays = 5;

    private static readonly int[] WarningExpiryLadder = { 30, 14, 7 };
    private static readonly int[] HearingLadder = { 7, 3, 1 };

    /// <summary>How far back an overdue obligation is still worth a reminder.</summary>
    /// <remarks>
    /// <para>⚠ THIS EXISTS BECAUSE OF WHAT THE FIRST REAL SWEEP DID. Run against TDC's data it
    /// produced 275 reminders, 242 of them written-query breaches on cases reported months ago — and
    /// the six things somebody could actually have acted on that morning were buried under them. A
    /// notification channel that opens with a flood of history is a channel people learn to ignore,
    /// and then the reminder that mattered goes unread too.</para>
    ///
    /// <para>A reminder is for a deadline you can still do something about. Ninety days past due, the
    /// written query is not going to be issued and the investigation is not going to be finished —
    /// that is a matter for a report and a conversation, not a nightly nudge. The obligation does not
    /// disappear: it stays on every queue and every case's advisories, which is where a historical
    /// breach belongs. Only the chasing stops.</para>
    ///
    /// <para>Applied to overdue reminders only. The due-soon ladders are bounded by their own
    /// thresholds and cannot reach back at all.</para>
    /// </remarks>
    private const int BacklogHorizonDays = 90;

    private static bool IsBeyondBacklogHorizon(PendingReminder p)
        => p.EscalationTier > 0 && p.DaysRemaining < -BacklogHorizonDays;

    private static int EscalationTier(int daysOverdue) => daysOverdue <= 7 ? 1 : daysOverdue <= 30 ? 2 : 3;

    /// <summary>
    /// Whole days between a due date and now, never less than one.
    /// </summary>
    /// <remarks>
    /// The floor is not cosmetic. FR-HR-177's clock runs in HOURS, so a query two hours late floors
    /// to zero days — and zero would be published as "0 day(s) overdue" and stored as a
    /// days-remaining of zero, which is the value a reminder that is NOT late carries. The one
    /// invariant a reader can rely on is that overdue reads negative and due-soon reads positive;
    /// something in its first day overdue is in its first day, not its noughth.
    /// </remarks>
    private static int DaysOverdue(DateTime due, DateTime asAt)
        => Math.Max(1, (int)Math.Floor((asAt - due).TotalDays));

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

    public async Task<IEnumerable<DisciplineReminderRunDto>> GetRecentRunsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var runs = await _unitOfWork.Repository<DisciplineReminderRun>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);

        return runs.Select(r => new DisciplineReminderRunDto
        {
            Id = r.Id,
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt,
            Trigger = r.Trigger,
            RemindersQueued = r.RemindersQueued,
        }).ToList();
    }

    public async Task<IEnumerable<DisciplineReminderLogEntryDto>> GetRecentLogAsync(int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var floor = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 90));

        var entries = await _unitOfWork.Repository<DisciplineReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= floor)
            .OrderByDescending(l => l.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entries.Select(l => new DisciplineReminderLogEntryDto
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

    /// <summary>
    /// Every sweep, evaluated as at <paramref name="now"/>. Reads only.
    /// </summary>
    /// <remarks>
    /// Split out so the live run and the preview cannot drift: a preview that answered a different
    /// question from the sweep it previews would be worse than no preview at all.
    /// </remarks>
    private async Task<List<PendingReminder>> CollectPendingAsync(
        Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var today = now.Date;
        var pending = new List<PendingReminder>();

        await SweepWrittenQueryAsync(tenantId, now, pending, cancellationToken);
        await SweepInvestigationsAsync(tenantId, today, pending, cancellationToken);
        await SweepHearingsAsync(tenantId, today, pending, cancellationToken);
        await SweepAppealWindowsAsync(tenantId, today, pending, cancellationToken);
        await SweepCorrectiveActionsAsync(tenantId, today, pending, cancellationToken);
        await SweepWarningsExpiringAsync(tenantId, today, pending, cancellationToken);
        await SweepFinesOverdueAsync(tenantId, today, pending, cancellationToken);
        await SweepGrievancesUnansweredAsync(tenantId, today, pending, cancellationToken);

        var ancient = pending.Where(IsBeyondBacklogHorizon).ToList();
        if (ancient.Count > 0)
        {
            // Never silently. A cap nobody is told about reads as "everything was covered".
            _logger.LogInformation(
                "Discipline reminder sweep for tenant {TenantId} passed over {Count} obligation(s) more than " +
                "{Days} days overdue — beyond the reminder horizon; they remain on the queues. Breakdown: {Breakdown}",
                tenantId, ancient.Count, BacklogHorizonDays,
                string.Join(", ", ancient.GroupBy(p => p.Kind).Select(g => $"{g.Key}={g.Count()}")));

            pending = pending.Where(p => !IsBeyondBacklogHorizon(p)).ToList();
        }

        return pending;
    }

    /// <summary>
    /// What a sweep run at <paramref name="asOf"/> would fire. Writes nothing, publishes nothing,
    /// claims no dedupe key.
    /// </summary>
    /// <remarks>
    /// <para>It answers a question HR actually asks — "what is this going to chase me about?" — and
    /// it is the only way to exercise the sweeps whose dates the server stamps itself. An appeal's
    /// filed date and a grievance step's reached date are both set to <c>now</c> by the services that
    /// create them, deliberately: neither is a caller's to assert. That makes their overdue rungs
    /// unreachable through the API within one run, and a rung nobody can reach is a rung nobody has
    /// checked.</para>
    ///
    /// <para>Safe against a future <paramref name="asOf"/> precisely because a dedupe key encodes the
    /// item's DUE date and rung, never the sweep date. A preview therefore cannot claim a key the
    /// real sweep will later need, because it claims nothing at all — and the keys it reports are the
    /// same ones that run will produce.</para>
    /// </remarks>
    public async Task<IEnumerable<DisciplineReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = asOf ?? DateTime.UtcNow;

        var pending = await CollectPendingAsync(tenantId, now, cancellationToken);
        var deduped = await FilterAlreadyDispatchedAsync(tenantId, pending, cancellationToken);

        return deduped
            .OrderBy(p => p.Kind).ThenBy(p => p.Reference)
            .Select(p => new DisciplineReminderPreviewItemDto
            {
                Kind = p.Kind,
                ItemType = p.ItemType,
                EntityId = p.EntityId,
                Reference = p.Reference,
                DueDate = p.DueDate,
                DaysRemaining = p.DaysRemaining,
                EscalationTier = p.EscalationTier,
                DedupeKey = p.DedupeKey,
            })
            .ToList();
    }

    public async Task<DisciplineReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to run the discipline reminder sweep.");

        var now = DateTime.UtcNow;

        await EnsureTopicsAsync(tenantId, cancellationToken);

        var pending = await CollectPendingAsync(tenantId, now, cancellationToken);
        var deduped = await FilterAlreadyDispatchedAsync(tenantId, pending, cancellationToken);

        var run = new DisciplineReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
        };
        await _unitOfWork.Repository<DisciplineReminderRun>().AddAsync(run);

        var logRepo = _unitOfWork.Repository<DisciplineReminderDispatchLog>();
        foreach (var p in deduped)
        {
            await logRepo.AddAsync(new DisciplineReminderDispatchLog
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
            _logger.LogWarning(ex, "Discipline reminder sweep lost a dedupe race for tenant {TenantId}", tenantId);
            throw new InvalidOperationException(
                "Another discipline reminder sweep is running for this tenant. Try again in a moment.");
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
            "Discipline reminder sweep for tenant {TenantId} queued {Count} reminder(s) ({Trigger})",
            tenantId, deduped.Count, trigger);

        return new DisciplineReminderRunResultDto
        {
            RunId = run.Id,
            RemindersQueued = deduped.Count,
            ByKind = deduped.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    // ── sweeps ───────────────────────────────────────────────────────────────

    private static readonly DisciplinaryStatus[] LiveCaseStatuses =
    {
        DisciplinaryStatus.Draft,
        DisciplinaryStatus.Reported,
        DisciplinaryStatus.UnderReview,
        DisciplinaryStatus.UnderInvestigation,
        DisciplinaryStatus.InvestigationComplete,
        DisciplinaryStatus.HearingScheduled,
        DisciplinaryStatus.HearingConducted,
        DisciplinaryStatus.AwaitingDecision,
        DisciplinaryStatus.DecisionMade,
        DisciplinaryStatus.UnderAppeal,
        DisciplinaryStatus.OnHold,
    };

    private IQueryable<StaffDisciplinaryAction> Cases(Guid tenantId)
        => _unitOfWork.Repository<StaffDisciplinaryAction>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted);

    private const string CasePath = "/hr/discipline";

    /// <summary>
    /// FR-HR-177 — the 48-hour written query. Only cases with no ShowCause notice at all: once one
    /// has been issued the deadline is spent, late or not, and chasing it further tells nobody
    /// anything they can act on.
    /// </summary>
    private async Task SweepWrittenQueryAsync(Guid tenantId, DateTime now, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var queried = _unitOfWork.Repository<StaffDisciplineNotification>()
            .GetQueryable(n => n.TenantId == tenantId && !n.IsDeleted
                            && n.NotificationType == DisciplineProcessDeadlines.WrittenQueryType)
            .Select(n => n.DisciplinaryActionId);

        var items = await Cases(tenantId)
            .Where(c => LiveCaseStatuses.Contains(c.Status)
                     && !c.QueryOpportunityWaivedAt.HasValue
                     && !queried.Contains(c.Id))
            .Select(c => new { c.Id, c.CaseNumber, c.ReportedDate })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = DisciplineProcessDeadlines.WrittenQueryDueAt(item.ReportedDate);
            if (now <= due) continue;

            var daysOverdue = DaysOverdue(due, now);
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                "WrittenQueryOverdue", "Disciplinary case", item.Id, item.CaseNumber,
                due, -daysOverdue, tier,
                $"WrittenQueryOverdue:{item.Id}:{due:yyyyMMdd}:t{tier}",
                tier >= 2 ? "OverdueEscalated" : "Overdue",
                $"{CasePath}/{item.Id}"));
        }
    }

    /// <summary>FR-HR-178 — investigations open past four weeks.</summary>
    private async Task SweepInvestigationsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = DisciplineProcessDeadlines.InvestigationOverdueCutoff(today);

        var items = await _unitOfWork.Repository<StaffDisciplineInvestigation>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted
                            && i.InvestigationEndDate == null
                            && i.InvestigationStartDate != null
                            && i.InvestigationStartDate <= cutoff)
            .Select(i => new { i.Id, i.DisciplinaryActionId, i.InvestigationStartDate, Number = i.DisciplinaryAction.CaseNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.InvestigationStartDate!.Value.Date.AddDays(DisciplineProcessDeadlines.InvestigationDays);
            var daysOverdue = DaysOverdue(due, today);
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                "InvestigationOverdue", "Investigation", item.DisciplinaryActionId, item.Number,
                due, -daysOverdue, tier,
                $"InvestigationOverdue:{item.Id}:{due:yyyyMMdd}:t{tier}",
                tier >= 2 ? "OverdueEscalated" : "Overdue",
                $"{CasePath}/{item.DisciplinaryActionId}"));
        }
    }

    /// <summary>Hearings coming up — the one sweep that is a diary, not a breach.</summary>
    private async Task SweepHearingsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(HearingLadder.Max());

        var items = await _unitOfWork.Repository<StaffDisciplineHearing>()
            .GetQueryable(h => h.TenantId == tenantId && !h.IsDeleted
                            && h.HearingDate != null
                            && h.HearingDate >= today
                            && h.HearingDate <= horizon)
            .Select(h => new { h.Id, h.DisciplinaryActionId, h.HearingDate, Number = h.DisciplinaryAction.CaseNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.HearingDate!.Value.Date;
            var daysRemaining = (int)(due - today).TotalDays;
            var rung = DueSoonRung(daysRemaining, HearingLadder);
            if (rung == null) continue;

            pending.Add(new PendingReminder(
                "HearingUpcoming", "Hearing", item.DisciplinaryActionId, item.Number,
                due, daysRemaining, 0,
                $"HearingUpcoming:{item.Id}:{due:yyyyMMdd}:r{rung}",
                "DueSoon",
                $"{CasePath}/{item.DisciplinaryActionId}"));
        }
    }

    /// <summary>
    /// FR-HR-180 — both appeal windows.
    /// </summary>
    /// <remarks>
    /// Working days, from <see cref="IHrWorkingDayCalculator"/>, because the requirement is in
    /// working days and counting calendar days here would contradict the very screen that shows the
    /// employee their deadline. The calculator is called per record, which is acceptable at this
    /// volume: the sets are cases decided in the last fortnight and appeals still open.
    /// </remarks>
    private async Task SweepAppealWindowsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        // Filing window about to close, on decided cases with no appeal yet.
        var appealed = _unitOfWork.Repository<StaffDisciplineAppeal>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => a.DisciplinaryActionId);

        var recentlyDecided = await Cases(tenantId)
            .Where(c => c.DecisionDate != null
                     && c.DecisionDate >= today.AddDays(-21)
                     && c.Status == DisciplinaryStatus.DecisionMade
                     && !appealed.Contains(c.Id))
            .Select(c => new { c.Id, c.CaseNumber, c.DecisionDate })
            .ToListAsync(cancellationToken);

        foreach (var item in recentlyDecided)
        {
            var closes = await _workingDays.AddWorkingDaysAsync(
                tenantId, item.DecisionDate!.Value, DisciplineProcessDeadlines.AppealFilingWorkingDays, cancellationToken);

            var daysRemaining = (int)(closes.Date - today).TotalDays;
            if (daysRemaining is < 0 or > 2) continue;

            pending.Add(new PendingReminder(
                "AppealWindowClosing", "Appeal window", item.Id, item.CaseNumber,
                closes.Date, daysRemaining, 0,
                $"AppealWindowClosing:{item.Id}:{closes:yyyyMMdd}:r{daysRemaining}",
                "DueSoon",
                $"{CasePath}/{item.Id}"));
        }

        // Appeals filed and still undecided past ten working days.
        var openAppeals = await _unitOfWork.Repository<StaffDisciplineAppeal>()
            // Dismissed is a decision, not an absence of one — it just leaves no outcome type.
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.AppealOutcome == null
                            && a.AppealStatus != DisciplineAppealStatus.Dismissed)
            .Select(a => new { a.Id, a.DisciplinaryActionId, a.FiledDate, Number = a.DisciplinaryAction.CaseNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in openAppeals)
        {
            var due = await _workingDays.AddWorkingDaysAsync(
                tenantId, item.FiledDate, DisciplineProcessDeadlines.AppealDecisionWorkingDays, cancellationToken);
            if (today <= due.Date) continue;

            var daysOverdue = DaysOverdue(due.Date, today);
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                "AppealDecisionOverdue", "Appeal", item.DisciplinaryActionId, item.Number,
                due.Date, -daysOverdue, tier,
                $"AppealDecisionOverdue:{item.Id}:{due:yyyyMMdd}:t{tier}",
                tier >= 2 ? "OverdueEscalated" : "Overdue",
                $"{CasePath}/{item.DisciplinaryActionId}"));
        }
    }

    /// <summary>Corrective actions past their review date — the queue the case detail already shows.</summary>
    private async Task SweepCorrectiveActionsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.Repository<StaffDisciplineCorrectiveAction>()
            .GetQueryable(ca => ca.TenantId == tenantId && !ca.IsDeleted
                             && ca.ReviewDate < today
                             && ca.Status != DisciplineCorrectiveActionStatus.Completed
                             && ca.Status != DisciplineCorrectiveActionStatus.Cancelled)
            .Select(ca => new { ca.Id, ca.DisciplinaryActionId, ca.ReviewDate, Number = ca.DisciplinaryAction.CaseNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.ReviewDate.Date;
            var daysOverdue = DaysOverdue(due, today);
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                "CorrectiveActionOverdue", "Corrective action", item.DisciplinaryActionId, item.Number,
                due, -daysOverdue, tier,
                $"CorrectiveActionOverdue:{item.Id}:{due:yyyyMMdd}:t{tier}",
                tier >= 2 ? "OverdueEscalated" : "Overdue",
                $"{CasePath}/{item.DisciplinaryActionId}"));
        }
    }

    /// <summary>
    /// Warnings about to expire. Not a breach — a warning lapsing changes what a later offence
    /// counts as, and HR needs to know before it happens rather than after.
    /// </summary>
    private async Task SweepWarningsExpiringAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(WarningExpiryLadder.Max());

        var items = await _unitOfWork.Repository<StaffDisciplineWarning>()
            .GetQueryable(w => w.TenantId == tenantId && !w.IsDeleted
                            && w.WarningExpiryDate != null
                            && w.WarningExpiryDate >= today
                            && w.WarningExpiryDate <= horizon)
            .Select(w => new { w.Id, w.DisciplinaryActionId, w.WarningExpiryDate, Number = w.DisciplinaryAction.CaseNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.WarningExpiryDate!.Value.Date;
            var daysRemaining = (int)(due - today).TotalDays;
            var rung = DueSoonRung(daysRemaining, WarningExpiryLadder);
            if (rung == null) continue;

            pending.Add(new PendingReminder(
                "WarningExpiring", "Warning", item.DisciplinaryActionId, item.Number,
                due, daysRemaining, 0,
                $"WarningExpiring:{item.Id}:{due:yyyyMMdd}:r{rung}",
                "DueSoon",
                $"{CasePath}/{item.DisciplinaryActionId}"));
        }
    }

    /// <summary>
    /// Fines past their due date and not fully paid.
    /// </summary>
    /// <remarks>
    /// This reminds HR that a fine is outstanding. It does NOT chase the employee and does not touch
    /// recovery — deducting a fine is payroll's, per the ownership boundary, and this area only ever
    /// records what was imposed and what has been paid.
    /// </remarks>
    private async Task SweepFinesOverdueAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.Repository<StaffDisciplineFine>()
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted
                            && f.FineDueDate != null
                            && f.FineDueDate < today
                            // The null case must be written out. FinePaymentStatus is nullable, and
                            // `!= FullyPaid` alone compiles to SQL that EXCLUDES nulls — so a fine
                            // recorded without a status, the least-tracked kind there is, would be
                            // the one the sweep never chased.
                            && (f.FinePaymentStatus == null
                                || (f.FinePaymentStatus != DisciplinaryFinePaymentStatus.FullyPaid
                                 && f.FinePaymentStatus != DisciplinaryFinePaymentStatus.Waived)))
            .Select(f => new { f.Id, f.DisciplinaryActionId, f.FineDueDate, Number = f.DisciplinaryAction.CaseNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.FineDueDate!.Value.Date;
            var daysOverdue = DaysOverdue(due, today);
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                "FineOverdue", "Fine", item.DisciplinaryActionId, item.Number,
                due, -daysOverdue, tier,
                $"FineOverdue:{item.Id}:{due:yyyyMMdd}:t{tier}",
                tier >= 2 ? "OverdueEscalated" : "Overdue",
                $"{CasePath}/{item.DisciplinaryActionId}"));
        }
    }

    /// <summary>
    /// Grievances sitting at a rung nobody has answered.
    /// </summary>
    /// <remarks>
    /// This is the sweep the grievance ladder most needs. A grievance stalls silently: the employee
    /// has done their part and is waiting, the rung it sits at may not even know it is theirs, and
    /// nothing on any screen goes looking. Escalation cannot rescue it either, because escalating
    /// requires the current rung to have ANSWERED first — so an unanswered rung stops the ladder dead.
    /// </remarks>
    private async Task SweepGrievancesUnansweredAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(-GrievanceRungChaseDays);

        var items = await _unitOfWork.Repository<StaffGrievanceStep>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted
                            && s.Outcome == GrievanceStepOutcome.AwaitingResponse
                            && s.ReachedDate <= cutoff
                            && s.Grievance.Status != GrievanceStatus.Resolved
                            && s.Grievance.Status != GrievanceStatus.Withdrawn
                            && s.Grievance.Status != GrievanceStatus.Closed)
            .Select(s => new { s.Id, s.GrievanceId, s.ReachedDate, s.Level, Number = s.Grievance.GrievanceNumber })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var due = item.ReachedDate.Date.AddDays(GrievanceRungChaseDays);
            var daysOverdue = DaysOverdue(due, today);
            var tier = EscalationTier(daysOverdue);

            pending.Add(new PendingReminder(
                "GrievanceUnanswered", $"Grievance at {item.Level}", item.GrievanceId, item.Number,
                due, -daysOverdue, tier,
                $"GrievanceUnanswered:{item.Id}:{due:yyyyMMdd}:t{tier}",
                tier >= 2 ? "OverdueEscalated" : "Overdue",
                $"/hr/grievances/{item.GrievanceId}"));
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

        var repo = _unitOfWork.Repository<DisciplineReminderDispatchLog>();
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

    /// <remarks>
    /// The templates name the case or grievance number and the deadline, and nothing else. No
    /// employee name, no allegation, no grievance subject — a notification is seen by more people
    /// than the record is, and the reader can open the record if they are entitled to.
    /// </remarks>
    private static readonly TopicSeed[] TopicSeeds =
    {
        new("DueSoon", "Discipline: Due Soon",
            "System-seeded discipline reminder — a dated obligation on a case or grievance is approaching.",
            "Discipline due soon: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} is due on {{DueDate}} — {{Days}} day(s) remaining."),
        new("Overdue", "Discipline: Overdue",
            "System-seeded discipline reminder — an obligation is past due (escalation tier 1).",
            "Discipline overdue: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} — {{Days}} day(s) overdue."),
        new("OverdueEscalated", "Discipline: Overdue (Escalated)",
            "System-seeded discipline escalation — an overdue obligation has reached tier 2 or 3.",
            "Discipline escalation (tier {{EscalationTier}}): {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} and is {{Days}} day(s) overdue — escalation tier {{EscalationTier}}.",
            EscalatesToAdmins: true),
    };

    /// <summary>
    /// Creates any missing topic for this tenant. Create-if-missing ONLY: deactivating a topic is how
    /// an administrator mutes a reminder, and a sweep that recreated or reactivated it would quietly
    /// overrule them every night.
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

            // A disciplinary deadline left unactioned for weeks is an institutional failure, not an
            // HR task — tier 2 and 3 therefore reach beyond the people who have already not acted.
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
