using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Sweeps staff travel for dates that need chasing and tells the people who act on each, once.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Travel is full of dates that matter and nothing was watching any
/// of them. The endpoints were already there — expiring documents, expiring visas, overdue advance
/// settlements, upcoming departures — returning their rows to nobody. Three other HR areas had a
/// sweep; travel did not, which is why an expired passport could sit unnoticed until somebody was
/// turned away at a gate.</para>
///
/// <para><b>Who is told (travel final closure, lane 8, slice 8b — D-4, D-49, D-50, F2).</b> Every reminder went to the
/// HR role, in the app only, and a document got one notice at 90 days and the next once it had expired. Now each kind
/// reaches the people who act on it through <see cref="StaffTravelNotices"/> — the traveller (in the app and by email, or
/// by email alone without a login, or the desk told to tell them), the approvers a waiting request's current stage is
/// asking, the desk — and documents and visas are chased at 90, 30 and 7 days. New kinds: a visa missing, a request
/// waiting (and still waiting near departure, O-11), a risk briefing unacknowledged, a claim window closing and one
/// closed with money open.</para>
///
/// <para><b>Send-once.</b> Every candidate produces a dedupe key encoding the item, the kind, the
/// date and the rung or escalation tier. The unique <c>(TenantId, DedupeKey)</c> index is the guarantee: a
/// sweep claims the key by inserting the log row, and only publishes afterwards. Moving a date
/// produces fresh keys, which re-arms the ladder — that is deliberate, because a passport whose
/// expiry has been corrected genuinely is a new thing to chase.</para>
///
/// <para><b>Publish after commit.</b> An unpublished-but-claimed reminder is one missed notification, whereas
/// publishing first risks sending the same thing twice. Since slice 8b the row says so: <c>PublishedAt</c> is written
/// once the publish call has returned, and a key claimed but never published (a sweep that died between the two) is
/// published by the next run. The platform's bus swallows its handlers' failures, so <c>PublishedAt</c> records that
/// the call was made, not that anyone received it — the notices written are the proof (U2).</para>
///
/// <para><b>Tenant-explicit.</b> The nightly host has nobody signed in, so nothing here may read the tenant or the
/// actor from the current user — the leave sweep's first scheduled run died of exactly that. Only the admin reads
/// (preview, runs, log) take the tenant from the caller.</para>
/// </remarks>
public class StaffTravelReminderService : IStaffTravelReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly StaffTravelNotices _notices;
    private readonly IStaffTravelFleetService _fleet;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<StaffTravelReminderService> _logger;

    public StaffTravelReminderService(
        IUnitOfWork unitOfWork,
        StaffTravelNotices notices,
        IStaffTravelFleetService fleet,
        UserManager<ApplicationUser> userManager,
        ICurrentUserProvider currentUserProvider,
        ILogger<StaffTravelReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _notices = notices;
        _fleet = fleet;
        _userManager = userManager;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>Who the sweep's own writes are stamped by — the advances it marks overdue, the trips and groups it moves.</summary>
    private const string SweepActor = "staff-travel-sweep";

    // ---- the windows (D-50) --------------------------------------------------
    //
    // Constants, each a TDC question in the closure plan's § 6. The 90 days are the working assumption — the
    // shortest notice that still allows a Ghanaian passport renewal; a 30-day warning about a document that takes
    // six weeks to replace is not a warning.

    /// <summary>How far ahead a document or visa expiry starts being chased, and the rungs inside it.</summary>
    private const int DocumentExpiryHorizonDays = 90;
    private const int DocumentSecondRungDays = 30;
    private const int DocumentLastRungDays = 7;

    /// <summary>How far ahead the traveller of an approved trip is told it is departing.</summary>
    private const int DepartureHorizonDays = 14;

    /// <summary>How far ahead an approved trip needing a visa, with none approved, is chased.</summary>
    private const int VisaMissingHorizonDays = 14;

    /// <summary>How long a request may wait for a decision before its approvers are reminded.</summary>
    private const int ApprovalWaitingDays = 5;

    /// <summary>How close to departure a request still waiting for approval goes to HR (O-11).</summary>
    private const int ApprovalEscalationDays = 3;

    /// <summary>How far ahead the traveller is chased to acknowledge the risk briefing.</summary>
    private const int BriefingHorizonDays = 7;

    /// <summary>How long before the claim window's last day the traveller is told (D-49).</summary>
    private const int ClaimWindowWarningDays = 7;

    /// <summary>
    /// How far back the first sweep looks. Without this the first run on an established database
    /// queues every historical breach at once — area 9's first live run queued 275, of which 242
    /// were history. A reminder about something that expired two years ago is noise.
    /// </summary>
    private const int BacklogHorizonDays = 90;

    // ---- the kinds, as the dispatch log names them ---------------------------

    private const string KindDocument = "TravelDocumentExpiring";
    private const string KindVisa = "VisaExpiring";
    private const string KindAdvance = "AdvanceSettlementOverdue";
    private const string KindDeparting = "TripDeparting";
    private const string KindVisaMissing = "VisaMissing";
    private const string KindApprovalWaiting = "ApprovalWaiting";
    private const string KindApprovalEscalated = "ApprovalEscalated";
    private const string KindBriefing = "BriefingUnacknowledged";
    private const string KindClaimWindowClosing = "ClaimWindowClosing";
    private const string KindClaimWindowPassed = "ClaimWindowPassed";
    private const string KindFleetReturned = "FleetReturned";
    private const string KindFleetIncident = "FleetIncident";

    // The sweep's moves (slice 8c), logged beside the reminders so the page shows what it did and when.
    private const string KindTripStarted = "TripStarted";
    private const string KindTripCompleted = "TripCompleted";
    private const string KindTripClosed = "TripClosed";
    private const string KindGroupStarted = "GroupStarted";
    private const string KindGroupCompleted = "GroupCompleted";

    private static readonly string[] TransitionKinds =
        { KindTripStarted, KindTripCompleted, KindTripClosed, KindGroupStarted, KindGroupCompleted };

    // Who a reminder reaches, as the preview reports it.
    private const string AudienceTraveller = "Traveller";
    private const string AudienceDesk = "Desk";
    private const string AudienceApprovers = "Approvers";
    private const string AudienceLineManager = "LineManager";

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

        var markedOverdue = await MarkOverdueAdvancesAsync(tenantId, DateOnly.FromDateTime(now), cancellationToken);

        // Lane 8 (D-45, 8b): every run ensures the travel topics, so the retired ones — the workflow engine's three among
        // them, which its own seeders switch back on — are off again by the next morning.
        await _notices.EnsureTopicsAsync(tenantId, cancellationToken);

        // Slice 8c (D-6, D-47, D-51): the trips and groups move first, so the reminders below read them as they now stand.
        var plan = await PlanTransitionsAsync(tenantId, now, cancellationToken);
        await ApplyTransitionsAsync(tenantId, run.Id, now, plan, cancellationToken);

        var candidates = (await FindCandidatesAsync(tenantId, now, plan, cancellationToken)).ToList();

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var logged = await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .ToListAsync(cancellationToken);
        var rows = logged.ToDictionary(l => l.DedupeKey, StringComparer.Ordinal);

        var fresh = candidates.Where(c => !rows.ContainsKey(c.DedupeKey)).ToList();
        // A key a run claimed and never published — it died between the two: published now, under its old row.
        var retry = candidates.Where(c => rows.TryGetValue(c.DedupeKey, out var row) && row.PublishedAt is null).ToList();

        foreach (var c in fresh)
        {
            var row = new StaffTravelReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = c.Kind,
                ItemType = c.ItemType,
                EntityId = c.EntityId,
                Reference = Clip(c.Reference, 250),
                DueDate = c.DueDate,
                DaysRemaining = c.DaysRemaining,
                EscalationTier = c.EscalationTier,
                DedupeKey = c.DedupeKey,
            };
            await _unitOfWork.Repository<StaffTravelReminderDispatchLog>().AddAsync(row);
            rows[c.DedupeKey] = row;
        }

        // The moves were claimed with the moves themselves (above), so here they are all "retry": their notices go out below.
        var reminders = candidates.Where(c => !c.Transition).ToList();
        var queued = fresh.Count(c => !c.Transition);
        var retried = retry.Count(c => !c.Transition);
        run.RemindersQueued = queued;

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

        // Publish only after the claim commits — see the remarks on this class. Each row says it was published as soon
        // as it was, so a run that dies part-way leaves only the rest for the next run.
        foreach (var c in fresh.Concat(retry))
        {
            await c.Send(cancellationToken);
            rows[c.DedupeKey].PublishedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        run.CompletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Travel reminder sweep for tenant {TenantId} sent {Count} reminder(s) and {Retried} left unsent before ({Trigger}); {Skipped} already sent; " +
            "trips started {Started}, completed {Completed}, closed {Closed}; groups moved {Groups}",
            tenantId, queued, retried, trigger, reminders.Count - queued - retried,
            plan.Started.Count, plan.Completed.Count, plan.Closed.Count, plan.Groups.Count);

        return new StaffTravelReminderRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Trigger = run.Trigger,
            RemindersQueued = queued,
            Retried = retried,
            AlreadySent = reminders.Count - queued - retried,
            AdvancesMarkedOverdue = markedOverdue,
            TripsStarted = plan.Started.Count,
            TripsCompleted = plan.Completed.Count,
            TripsClosed = plan.Closed.Count,
            GroupsUpdated = plan.Groups.Count,
        };
    }

    /// <summary>
    /// Gives <see cref="TravelAdvanceStatus.Overdue"/> its writer (travel final closure, lane 3; D-6): an advance with
    /// cash out whose settlement deadline has passed. Every reader of cash out accepts Overdue
    /// (<see cref="StaffTravelAdvanceRules.CashOut"/>), so marking one changes no money — it is what the register and
    /// the desk's overdue queue show, and it bars the traveller from a new advance until it is settled.
    /// </summary>
    private async Task<int> MarkOverdueAdvancesAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken)
    {
        var due = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.Status != TravelAdvanceStatus.Overdue
                            && a.SettlementDeadline != null && a.SettlementDeadline < today)
            .Where(StaffTravelAdvanceRules.CashOut)
            .ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;

        var at = DateTime.UtcNow;
        foreach (var advance in due)
        {
            advance.Status = TravelAdvanceStatus.Overdue;
            advance.UpdatedAt = at;
            advance.UpdatedBy = SweepActor;
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel reminder sweep marked {Count} advance(s) overdue for tenant {TenantId}", due.Count, tenantId);
        return due.Count;
    }

    // ---- the moves (slice 8c — D-6, D-47, D-51; the groups are lane 1's slice 1c) -----------------------------------

    /// <summary>What a run moves, decided before anything is: each open trip's status once moved, and Fleet's word on them.</summary>
    private sealed class TransitionPlan
    {
        public List<(StaffTravelRequest Trip, string Why)> Started { get; } = new();
        public List<StaffTravelRequest> Completed { get; } = new();
        public List<StaffTravelRequest> Closed { get; } = new();
        public List<(StaffGroupTravel Group, GroupTravelStatus To)> Groups { get; } = new();

        /// <summary>Every approved, under-way and completed trip, by id.</summary>
        public Dictionary<Guid, StaffTravelRequest> Trips { get; } = new();

        /// <summary>Each of <see cref="Trips"/>' status once the moves are made — a preview's trips still hold the old one.</summary>
        public Dictionary<Guid, StaffTravelRequestStatus> Projected { get; } = new();

        /// <summary>The claim window's last day of each trip that is completed once moved.</summary>
        public Dictionary<Guid, DateOnly> LastDays { get; } = new();

        public StaffTravelFleetSignals Fleet { get; set; } =
            new(Array.Empty<StaffTravelFleetTripSignal>(), Array.Empty<StaffTravelFleetIncidentSignal>());
    }

    /// <summary>
    /// Decides the moves (D-6). An approved trip goes under way on its departure date, or before it when Fleet dispatches one
    /// of its company vehicles (D-29); a trip under way is completed the day after it ends (D-47) — an approved one already
    /// past its end takes both steps at once; a completed trip is closed once its claim window has passed and nothing is
    /// left to settle (D-51 — the window, so a trip is never closed under a traveller still entitled to claim; the settled
    /// test is the Close verb's own, <see cref="StaffTravelLifecycleRules.OpenItemAsync"/>). A group trip is under way once
    /// any of its places is, completed once every place is completed or closed. Nothing is written here.
    /// </summary>
    private async Task<TransitionPlan> PlanTransitionsAsync(
        Guid tenantId, DateTime at, CancellationToken cancellationToken, bool track = true)
    {
        var today = DateOnly.FromDateTime(at);
        var plan = new TransitionPlan();

        var query = _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && (r.Status == StaffTravelRequestStatus.Approved
                                || r.Status == StaffTravelRequestStatus.InProgress
                                || r.Status == StaffTravelRequestStatus.Completed));
        var trips = await (track ? query : query.AsNoTracking())
            .OrderBy(r => r.RequestNumber)
            .ToListAsync(cancellationToken);
        foreach (var trip in trips)
        {
            plan.Trips[trip.Id] = trip;
            plan.Projected[trip.Id] = trip.Status;
        }
        if (trips.Count > 0)
            plan.Fleet = await _fleet.GetSweepSignalsAsync(
                tenantId, trips.Select(t => t.Id).ToList(),
                today.AddDays(-BacklogHorizonDays).ToDateTime(TimeOnly.MinValue), cancellationToken);
        var dispatched = plan.Fleet.Trips
            .Where(s => s.Status is FleetTripStatuses.Dispatched or FleetTripStatuses.Completed)
            .GroupBy(s => s.RequestId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.DispatchedAt ?? DateTime.MaxValue).First());

        foreach (var trip in trips)
        {
            if (plan.Projected[trip.Id] == StaffTravelRequestStatus.Approved)
            {
                if (trip.TravelStartDate <= today)
                    plan.Started.Add((trip, $"its departure date, {StaffTravelNotices.Date(trip.TravelStartDate)}"));
                else if (dispatched.TryGetValue(trip.Id, out var fleetTrip))
                    plan.Started.Add((trip, "Fleet dispatched its company vehicle" +
                        (fleetTrip.DispatchedAt is DateTime d ? $" on {StaffTravelNotices.Date(DateOnly.FromDateTime(d))}" : string.Empty)));
                else
                    continue;
                plan.Projected[trip.Id] = StaffTravelRequestStatus.InProgress;
            }

            if (plan.Projected[trip.Id] == StaffTravelRequestStatus.InProgress && today > trip.TravelEndDate)
            {
                plan.Completed.Add(trip);
                plan.Projected[trip.Id] = StaffTravelRequestStatus.Completed;
            }

            if (plan.Projected[trip.Id] == StaffTravelRequestStatus.Completed)
            {
                var lastDay = await StaffTravelLifecycleRules.ClaimWindowLastDayAsync(_unitOfWork, trip, cancellationToken);
                plan.LastDays[trip.Id] = lastDay;
                if (lastDay < today && await StaffTravelLifecycleRules.OpenItemAsync(_unitOfWork, trip, cancellationToken) is null)
                {
                    plan.Closed.Add(trip);
                    plan.Projected[trip.Id] = StaffTravelRequestStatus.Closed;
                }
            }
        }

        await PlanGroupsAsync(tenantId, plan, track, cancellationToken);
        return plan;
    }

    /// <summary>
    /// A group's under way and completed had no writer (lane 1, slice 1c): they are read off its places — the travellers'
    /// trips not cancelled or rejected — as they stand once moved. A group only moves forward; one called off stays so.
    /// </summary>
    private async Task PlanGroupsAsync(Guid tenantId, TransitionPlan plan, bool track, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Repository<StaffGroupTravel>()
            .GetQueryable(g => g.TenantId == tenantId && !g.IsDeleted
                            && (g.Status == GroupTravelStatus.Planning || g.Status == GroupTravelStatus.Open
                                || g.Status == GroupTravelStatus.Closed || g.Status == GroupTravelStatus.InProgress));
        var groups = await (track ? query : query.AsNoTracking()).ToListAsync(cancellationToken);
        if (groups.Count == 0) return;

        var groupIds = groups.Select(g => g.Id).ToList();
        var places = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && r.GroupTravelId != null && groupIds.Contains(r.GroupTravelId.Value)
                            && r.Status != StaffTravelRequestStatus.Cancelled && r.Status != StaffTravelRequestStatus.Rejected)
            .Select(r => new { r.Id, GroupId = r.GroupTravelId!.Value, r.Status })
            .ToListAsync(cancellationToken);

        foreach (var group in groups.OrderBy(g => g.GroupName))
        {
            var statuses = places
                .Where(p => p.GroupId == group.Id)
                .Select(p => plan.Projected.TryGetValue(p.Id, out var moved) ? moved : p.Status)
                .ToList();
            if (statuses.Count == 0) continue;

            GroupTravelStatus? to =
                statuses.All(s => s is StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed)
                    ? GroupTravelStatus.Completed
                : statuses.Any(s => s is StaffTravelRequestStatus.InProgress or StaffTravelRequestStatus.Completed
                                        or StaffTravelRequestStatus.Closed)
                    ? GroupTravelStatus.InProgress
                : (GroupTravelStatus?)null;
            if (to is { } target && target != group.Status)
                plan.Groups.Add((group, target));
        }
    }

    /// <summary>
    /// Makes the moves and logs each in the same save — so no move is made without its row, and the notice a completion
    /// owes the traveller goes out with this run or, if it stops first, with the next (<see cref="UnpublishedTransitionsAsync"/>).
    /// </summary>
    private async Task ApplyTransitionsAsync(
        Guid tenantId, Guid runId, DateTime at, TransitionPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Started.Count + plan.Completed.Count + plan.Closed.Count + plan.Groups.Count == 0) return;

        foreach (var (trip, why) in plan.Started)
        {
            trip.Status = StaffTravelRequestStatus.InProgress;
            Stamp(trip, at);
            _logger.LogInformation("Travel sweep moved {Reference} under way: {Why}", trip.RequestNumber, why);
        }
        foreach (var trip in plan.Completed)
        {
            trip.Status = StaffTravelRequestStatus.Completed;
            trip.CompletedAt = at;
            Stamp(trip, at);
        }
        foreach (var trip in plan.Closed)
        {
            trip.Status = StaffTravelRequestStatus.Closed;
            trip.ClosedAt = at;
            trip.ClosedById = null; // the sweep closed it: no employee did
            Stamp(trip, at);
        }
        foreach (var (group, to) in plan.Groups)
        {
            group.Status = to;
            group.UpdatedAt = at;
            group.UpdatedBy = SweepActor;
        }

        var moves = TransitionCandidates(plan, DateOnly.FromDateTime(at));
        var keys = moves.Select(m => m.DedupeKey).ToList();
        var logged = new HashSet<string>(await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken), StringComparer.Ordinal);
        foreach (var m in moves.Where(m => !logged.Contains(m.DedupeKey)))
            await _unitOfWork.Repository<StaffTravelReminderDispatchLog>().AddAsync(new StaffTravelReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = runId,
                Kind = m.Kind,
                ItemType = m.ItemType,
                EntityId = m.EntityId,
                Reference = Clip(m.Reference, 250),
                DueDate = m.DueDate,
                DaysRemaining = m.DaysRemaining,
                EscalationTier = m.EscalationTier,
                DedupeKey = m.DedupeKey,
            });

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Travel reminder sweep lost a dedupe race moving trips for tenant {TenantId}", tenantId);
            throw new InvalidOperationException(
                "Another travel reminder sweep is running for this tenant. Try again in a moment.");
        }

        _logger.LogInformation(
            "Travel sweep for tenant {TenantId}: {Started} trip(s) under way, {Completed} completed, {Closed} closed, {Groups} group(s) moved",
            tenantId, plan.Started.Count, plan.Completed.Count, plan.Closed.Count, plan.Groups.Count);
    }

    private static void Stamp(StaffTravelRequest trip, DateTime at)
    {
        trip.UpdatedAt = at;
        trip.UpdatedBy = SweepActor;
    }

    public async Task<IEnumerable<StaffTravelReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var at = asOf ?? DateTime.UtcNow;

        // The moves a run would make are listed too (slice 8c) — read only, nothing tracked.
        var plan = await PlanTransitionsAsync(tenantId, at, cancellationToken, track: false);
        var candidates = (await FindCandidatesAsync(tenantId, at, plan, cancellationToken)).ToList();
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        // Sent means published: a key claimed and never published goes out with the next run.
        var sent = new HashSet<string>(await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey) && l.PublishedAt != null)
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken), StringComparer.Ordinal);

        return candidates.Select(c => new StaffTravelReminderPreviewItemDto
        {
            Kind = c.Kind, ItemType = c.ItemType, EntityId = c.EntityId, Reference = c.Reference,
            DueDate = c.DueDate, DaysRemaining = c.DaysRemaining, EscalationTier = c.EscalationTier,
            DedupeKey = c.DedupeKey, AlreadySent = sent.Contains(c.DedupeKey), SentTo = c.SentTo.ToList(),
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
                CreatedAt = l.CreatedAt, PublishedAt = l.PublishedAt,
            })
            .ToListAsync(cancellationToken);
    }

    // ---- candidates --------------------------------------------------------

    /// <summary>One reminder: what the log records, whom the preview says it reaches, and how it is sent.</summary>
    /// <param name="Transition">One of the sweep's moves (slice 8c), logged when it is made; its notice, if any, follows.</param>
    private sealed record Candidate(
        string Kind, string ItemType, Guid EntityId, string Reference,
        DateTime? DueDate, int DaysRemaining, int EscalationTier, string DedupeKey,
        IReadOnlyList<string> SentTo, Func<CancellationToken, Task> Send, bool Transition = false);

    private static readonly Func<CancellationToken, Task> Nothing = _ => Task.CompletedTask;

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
    /// The rung of an expiry chase (slice 8b, F2): before the date, the 90-, 30- and 7-day notices — an item first seen
    /// inside a rung gets that rung's notice, not the ones it missed; after it, the escalation tier.
    /// </summary>
    private static string RungFor(int daysRemaining) => daysRemaining switch
    {
        > DocumentSecondRungDays => $"r{DocumentExpiryHorizonDays}",
        > DocumentLastRungDays => $"r{DocumentSecondRungDays}",
        >= 0 => $"r{DocumentLastRungDays}",
        _ => $"t{TierFor(daysRemaining)}",
    };

    private static string Expiry(DateOnly date, int days) => days switch
    {
        > 0 => $"expires on {StaffTravelNotices.Date(date)}, in {days} day(s)",
        0 => $"expires today, {StaffTravelNotices.Date(date)}",
        _ => $"expired on {StaffTravelNotices.Date(date)}, {-days} day(s) ago",
    };

    private static string Spaced(string name) => Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;

    private async Task<IEnumerable<Candidate>> FindCandidatesAsync(
        Guid tenantId, DateTime at, TransitionPlan plan, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(at);
        var backlogFloor = today.AddDays(-BacklogHorizonDays);
        var results = new List<Candidate>();

        results.AddRange(TransitionCandidates(plan, today));
        await UnpublishedTransitionsAsync(tenantId, today, backlogFloor, results, cancellationToken);
        await FleetAsync(tenantId, plan, today, backlogFloor, results, cancellationToken);
        await DocumentsAsync(tenantId, today, backlogFloor, results, cancellationToken);
        await VisasAsync(tenantId, today, backlogFloor, results, cancellationToken);
        await AdvancesAsync(tenantId, today, backlogFloor, results, cancellationToken);
        await DeparturesAsync(tenantId, today, results, cancellationToken);
        await ApprovalsAsync(tenantId, at, today, backlogFloor, results, cancellationToken);
        await BriefingsAsync(tenantId, today, results, cancellationToken);
        await ClaimWindowsAsync(tenantId, today, backlogFloor, results, cancellationToken);

        return results;
    }

    /// <summary>Trips by id, untracked: the notices read their facts and nothing here changes them.</summary>
    private async Task<Dictionary<Guid, StaffTravelRequest>> TripsAsync(Guid tenantId, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, StaffTravelRequest>();
        return await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && wanted.Contains(r.Id))
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Id, cancellationToken);
    }

    private Func<CancellationToken, Task> ToTraveller(StaffTravelRequest trip, string evt, string? tab, Dictionary<string, object> data)
        => ct => _notices.TellTravellerAsync(trip, evt, StaffTravelNotices.TravellerTrip(trip.Id, tab), null, null, data, ct);

    private Func<CancellationToken, Task> ToDesk(StaffTravelRequest trip, string evt, string actionPath, Dictionary<string, object> data)
        => ct => _notices.TellDeskAsync(trip, evt, actionPath, null, null, data, onlyWhenActorOutsideDesk: false, ct);

    private static Func<CancellationToken, Task> Both(params Func<CancellationToken, Task>[] sends)
        => async ct => { foreach (var send in sends) await send(ct); };

    // 0. The sweep's own moves (slice 8c), as the log records them. Only a completion tells anyone: the traveller, with
    //    the last day to claim while it is still ahead (D-47). Starting tells nobody — the traveller was told it is
    //    departing — and nor does closing (D-46).
    private List<Candidate> TransitionCandidates(TransitionPlan plan, DateOnly today)
    {
        var results = new List<Candidate>();
        foreach (var (trip, _) in plan.Started)
            results.Add(TripMove(KindTripStarted, trip, trip.TravelStartDate, today, Array.Empty<string>(), Nothing));
        foreach (var trip in plan.Completed)
            results.Add(CompletedMove(trip, plan.LastDays[trip.Id], today));
        foreach (var trip in plan.Closed)
            results.Add(TripMove(KindTripClosed, trip, plan.LastDays[trip.Id], today, Array.Empty<string>(), Nothing));
        foreach (var (group, to) in plan.Groups)
        {
            var kind = to == GroupTravelStatus.Completed ? KindGroupCompleted : KindGroupStarted;
            var on = to == GroupTravelStatus.Completed ? group.TravelEndDate : group.TravelStartDate;
            results.Add(new Candidate(
                kind, "Group trip", group.Id, group.GroupName, on.ToDateTime(TimeOnly.MinValue), on.DayNumber - today.DayNumber, 0,
                $"{kind}:{group.Id}", Array.Empty<string>(), Nothing, Transition: true));
        }
        return results;
    }

    private static Candidate TripMove(
        string kind, StaffTravelRequest trip, DateOnly on, DateOnly today, IReadOnlyList<string> sentTo, Func<CancellationToken, Task> send)
        => new(kind, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty, on.ToDateTime(TimeOnly.MinValue),
               on.DayNumber - today.DayNumber, 0, $"{kind}:{trip.Id}", sentTo, send, Transition: true);

    private Candidate CompletedMove(StaffTravelRequest trip, DateOnly lastDay, DateOnly today)
        => lastDay >= today
            ? TripMove(KindTripCompleted, trip, trip.TravelEndDate, today, new[] { AudienceTraveller },
                ToTraveller(trip, StaffTravelNotices.TripCompleted, "money",
                    new Dictionary<string, object> { ["LastDay"] = StaffTravelNotices.Date(lastDay) }))
            : TripMove(KindTripCompleted, trip, trip.TravelEndDate, today, Array.Empty<string>(), Nothing);

    /// <summary>
    /// A move logged by a run that stopped before its notice went out. Only a completion has one, owed while the trip is
    /// still completed and its claim window open; the row is stamped published with this run either way.
    /// </summary>
    private async Task UnpublishedTransitionsAsync(
        Guid tenantId, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        var floor = backlogFloor.ToDateTime(TimeOnly.MinValue);
        var have = new HashSet<string>(results.Select(r => r.DedupeKey), StringComparer.Ordinal);
        var rows = (await _unitOfWork.Repository<StaffTravelReminderDispatchLog>()
                .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.PublishedAt == null && l.CreatedAt >= floor
                                && TransitionKinds.Contains(l.Kind))
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(r => !have.Contains(r.DedupeKey))
            .ToList();
        if (rows.Count == 0) return;

        var trips = await TripsAsync(tenantId, rows.Where(r => r.Kind == KindTripCompleted).Select(r => r.EntityId), cancellationToken);
        foreach (var row in rows)
        {
            if (row.Kind == KindTripCompleted && trips.TryGetValue(row.EntityId, out var trip)
                && trip.Status == StaffTravelRequestStatus.Completed)
                results.Add(CompletedMove(trip, await StaffTravelLifecycleRules.ClaimWindowLastDayAsync(_unitOfWork, trip, cancellationToken), today));
            else
                results.Add(new Candidate(
                    row.Kind, row.ItemType, row.EntityId, row.Reference, row.DueDate, row.DaysRemaining, row.EscalationTier,
                    row.DedupeKey, Array.Empty<string>(), Nothing, Transition: true));
        }
    }

    // 1. Travel documents — passports and the rest — at 90, 30 and 7 days, and once lapsed. The owner is told
    //    (slice 8b); the desk only when nobody can tell them. An employee who has left is not chased.
    private async Task DocumentsAsync(Guid tenantId, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(DocumentExpiryHorizonDays);
        var active = _unitOfWork.Repository<Employee>().GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive).Select(e => e.Id);
        var documents = await _unitOfWork.Repository<StaffTravelDocument>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted
                            && d.ExpiryDate != null && d.ExpiryDate <= horizon && d.ExpiryDate >= backlogFloor
                            && active.Contains(d.EmployeeId))
            .Select(d => new { d.Id, d.EmployeeId, d.DocumentType, d.ExpiryDate })
            .ToListAsync(cancellationToken);
        foreach (var d in documents)
        {
            var expiry = d.ExpiryDate!.Value;
            var days = expiry.DayNumber - today.DayNumber;
            var type = Spaced(d.DocumentType.ToString());
            var data = new Dictionary<string, object> { ["DocumentType"] = type, ["Expiry"] = Expiry(expiry, days) };
            var employeeId = d.EmployeeId;
            var documentId = d.Id;
            results.Add(new Candidate(
                KindDocument, type, d.Id, type,
                expiry.ToDateTime(TimeOnly.MinValue), days, days >= 0 ? 0 : TierFor(days),
                $"{KindDocument}:{d.Id}:{expiry:yyyy-MM-dd}:{RungFor(days)}",
                new[] { AudienceTraveller },
                ct => _notices.TellDocumentOwnerAsync(tenantId, employeeId, documentId, data, ct)));
        }
    }

    // 2. Visas recorded on a trip still to happen or under way, at the same rungs — the traveller is told.
    private async Task VisasAsync(Guid tenantId, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(DocumentExpiryHorizonDays);
        var visas = await _unitOfWork.Repository<StaffTravelVisaApplication>()
            .GetQueryable(v => v.TenantId == tenantId && !v.IsDeleted
                            && v.ExpiryDate != null && v.ExpiryDate <= horizon && v.ExpiryDate >= backlogFloor)
            .Select(v => new { v.Id, v.ExpiryDate, v.StaffTravelRequestId })
            .ToListAsync(cancellationToken);
        var trips = await TripsAsync(tenantId, visas.Select(v => v.StaffTravelRequestId), cancellationToken);
        foreach (var v in visas)
        {
            if (!trips.TryGetValue(v.StaffTravelRequestId, out var trip)
                || trip.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected
                    or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed)
                continue;
            var expiry = v.ExpiryDate!.Value;
            var days = expiry.DayNumber - today.DayNumber;
            results.Add(new Candidate(
                KindVisa, "Visa", v.Id, trip.RequestNumber ?? "Visa",
                expiry.ToDateTime(TimeOnly.MinValue), days, days >= 0 ? 0 : TierFor(days),
                $"{KindVisa}:{v.Id}:{expiry:yyyy-MM-dd}:{RungFor(days)}",
                new[] { AudienceTraveller },
                ToTraveller(trip, StaffTravelNotices.VisaExpiring, "before",
                    new Dictionary<string, object> { ["Expiry"] = Expiry(expiry, days) })));
        }
    }

    // 3. Advances past their settlement deadline with money still out — the traveller and the desk. Cash out includes
    //    Overdue (lane 3, N1): the sweep above marks them so.
    private async Task AdvancesAsync(Guid tenantId, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        var advances = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.SettlementDeadline != null
                            && a.SettlementDeadline < today
                            && a.SettlementDeadline >= backlogFloor)
            .Where(StaffTravelAdvanceRules.CashOut)
            .Select(a => new { a.Id, a.AdvanceNumber, a.SettlementDeadline, a.StaffTravelRequestId, a.CurrencyCode, a.UnsettledAmount })
            .ToListAsync(cancellationToken);
        var trips = await TripsAsync(tenantId, advances.Select(a => a.StaffTravelRequestId), cancellationToken);
        foreach (var a in advances)
        {
            if (!trips.TryGetValue(a.StaffTravelRequestId, out var trip)) continue;
            var deadline = a.SettlementDeadline!.Value;
            var days = deadline.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            var data = new Dictionary<string, object>
            {
                ["Number"] = a.AdvanceNumber ?? string.Empty,
                ["Amount"] = StaffTravelNotices.Money(a.CurrencyCode, a.UnsettledAmount),
                ["Deadline"] = StaffTravelNotices.Date(deadline),
            };
            results.Add(new Candidate(
                KindAdvance, "Travel advance", a.Id, a.AdvanceNumber ?? string.Empty,
                deadline.ToDateTime(TimeOnly.MinValue), days, tier,
                $"{KindAdvance}:{a.Id}:{deadline:yyyy-MM-dd}:{tier}",
                new[] { AudienceTraveller, AudienceDesk },
                Both(ToTraveller(trip, StaffTravelNotices.SettlementOverdue, "money", data),
                     ToDesk(trip, StaffTravelNotices.SettlementOverdue, "/hr/travel/advances", data))));
        }
    }

    // 4 and 5. Approved trips about to depart: the traveller is told once; one that needs a visa and has none approved
    //    (the ticket's own rule — Approved or Not required) is chased with the traveller and the desk. Since slice 8c a trip
    //    is moved under way on its departure day, or before it on Fleet's dispatch, so an under-way one counts too.
    private async Task DeparturesAsync(Guid tenantId, DateOnly today, List<Candidate> results, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(Math.Max(DepartureHorizonDays, VisaMissingHorizonDays));
        var trips = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && (r.Status == StaffTravelRequestStatus.Approved || r.Status == StaffTravelRequestStatus.InProgress)
                            && r.TravelStartDate >= today && r.TravelStartDate <= horizon)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (trips.Count == 0) return;
        var ids = trips.Select(t => t.Id).ToList();
        var visaInHand = new HashSet<Guid>(await _unitOfWork.Repository<StaffTravelVisaApplication>()
            .GetQueryable(v => v.TenantId == tenantId && !v.IsDeleted && ids.Contains(v.StaffTravelRequestId)
                            && (v.Status == VisaApplicationStatus.Approved || v.Status == VisaApplicationStatus.NotRequired))
            .Select(v => v.StaffTravelRequestId)
            .ToListAsync(cancellationToken));

        foreach (var trip in trips)
        {
            var days = trip.TravelStartDate.DayNumber - today.DayNumber;
            var start = trip.TravelStartDate.ToDateTime(TimeOnly.MinValue);
            var data = new Dictionary<string, object>
            {
                ["Days"] = days,
                ["StartDate"] = StaffTravelNotices.Date(trip.TravelStartDate),
            };
            if (days <= DepartureHorizonDays)
                results.Add(new Candidate(
                    KindDeparting, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty, start, days, 0,
                    $"{KindDeparting}:{trip.Id}:{trip.TravelStartDate:yyyy-MM-dd}",
                    new[] { AudienceTraveller },
                    ToTraveller(trip, StaffTravelNotices.Departing, "plan", data)));

            if (days <= VisaMissingHorizonDays && !visaInHand.Contains(trip.Id)
                && await StaffTravelComplianceRules.NeedsVisaAsync(_unitOfWork, trip, cancellationToken))
                results.Add(new Candidate(
                    KindVisaMissing, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty, start, days, 0,
                    $"{KindVisaMissing}:{trip.Id}:{trip.TravelStartDate:yyyy-MM-dd}",
                    new[] { AudienceTraveller, AudienceDesk },
                    Both(ToTraveller(trip, StaffTravelNotices.VisaMissing, "before", data),
                         ToDesk(trip, StaffTravelNotices.VisaMissing, StaffTravelNotices.DeskTrip(trip.Id, "compliance"), data))));
        }
    }

    // 6. Requests waiting for a decision: after five days the people its current stage is asking — never the traveller —
    //    or HR when nobody can be; and, waiting or not, HR once it is three days from departure or past it (O-11).
    private async Task ApprovalsAsync(Guid tenantId, DateTime at, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        var floor = backlogFloor.ToDateTime(TimeOnly.MinValue);
        var trips = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && r.Status == StaffTravelRequestStatus.Submitted
                            && r.SubmittedAt != null && r.SubmittedAt >= floor)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (trips.Count == 0) return;

        var waitingSince = at.AddDays(-ApprovalWaitingDays);
        var waiting = trips.Where(t => t.SubmittedAt <= waitingSince).ToList();
        var asked = await HrPendingApprovers.ForEntitiesAsync(
            _unitOfWork, _userManager, tenantId, waiting.Select(t => t.Id).ToList(), cancellationToken);

        foreach (var trip in waiting)
        {
            var waited = (int)(at - trip.SubmittedAt!.Value).TotalDays;
            var tier = TierFor(-waited);
            var approvers = asked.TryGetValue(trip.Id, out var users)
                ? users.Where(u => u.EmployeeId != trip.EmployeeId).Select(u => u.UserId).Distinct().ToList()
                : new List<Guid>();
            var key = $"{KindApprovalWaiting}:{trip.Id}:{trip.SubmittedAt:yyyy-MM-dd}:{tier}";
            var due = trip.SubmittedAt.Value;
            if (approvers.Count > 0)
                results.Add(new Candidate(
                    KindApprovalWaiting, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty, due, -waited, tier, key,
                    new[] { AudienceApprovers },
                    ct => _notices.TellApproversAsync(trip, approvers, StaffTravelNotices.ApprovalWaiting,
                        StaffTravelNotices.DeskTrip(trip.Id), new Dictionary<string, object> { ["Waited"] = waited }, ct)));
            else
                results.Add(new Candidate(
                    KindApprovalWaiting, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty, due, -waited, tier, key,
                    new[] { AudienceDesk },
                    ToDesk(trip, StaffTravelNotices.ApprovalWaiting, StaffTravelNotices.DeskTrip(trip.Id), new Dictionary<string, object>
                    {
                        ["Why"] = $"has waited {waited} day(s) for a decision, and nobody else can be asked to make it — no one its " +
                                  "current approval stage names has a login.",
                    })));
        }

        var escalateBy = today.AddDays(ApprovalEscalationDays);
        foreach (var trip in trips.Where(t => t.TravelStartDate <= escalateBy && t.TravelStartDate >= backlogFloor))
        {
            var days = trip.TravelStartDate.DayNumber - today.DayNumber;
            var why = days >= 0
                ? $"departs on {StaffTravelNotices.Date(trip.TravelStartDate)}, in {days} day(s), and is still waiting for approval."
                : $"was to depart on {StaffTravelNotices.Date(trip.TravelStartDate)}, {-days} day(s) ago, and is still waiting for approval.";
            results.Add(new Candidate(
                KindApprovalEscalated, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty,
                trip.TravelStartDate.ToDateTime(TimeOnly.MinValue), days, TierFor(days),
                $"{KindApprovalEscalated}:{trip.Id}:{trip.TravelStartDate:yyyy-MM-dd}",
                new[] { AudienceDesk },
                ToDesk(trip, StaffTravelNotices.ApprovalWaiting, StaffTravelNotices.DeskTrip(trip.Id),
                    new Dictionary<string, object> { ["Why"] = why })));
        }
    }

    // 7. A trip about to depart whose risk assessment — the latest still valid at departure, as the ticket reads it
    //    (D-37) — the traveller has not acknowledged: they are chased once per assessment. Under way counts too (8c).
    private async Task BriefingsAsync(Guid tenantId, DateOnly today, List<Candidate> results, CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(BriefingHorizonDays);
        var trips = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && (r.Status == StaffTravelRequestStatus.Approved || r.Status == StaffTravelRequestStatus.InProgress)
                            && r.TravelStartDate >= today && r.TravelStartDate <= horizon)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (trips.Count == 0) return;
        var ids = trips.Select(t => t.Id).ToList();
        var assessments = await _unitOfWork.Repository<StaffTravelRiskAssessment>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.StaffTravelRequestId))
            .Select(a => new { a.Id, a.StaffTravelRequestId, a.ValidUntil, a.AssessedAt, a.CreatedAt, a.EmployeeAcknowledged, a.RiskLevel })
            .ToListAsync(cancellationToken);
        foreach (var trip in trips)
        {
            var latest = assessments
                .Where(a => a.StaffTravelRequestId == trip.Id && (a.ValidUntil == null || a.ValidUntil >= trip.TravelStartDate))
                .OrderByDescending(a => a.AssessedAt ?? a.CreatedAt)
                .FirstOrDefault();
            if (latest is null || latest.EmployeeAcknowledged) continue;
            var days = trip.TravelStartDate.DayNumber - today.DayNumber;
            results.Add(new Candidate(
                KindBriefing, "Risk assessment", trip.Id, trip.RequestNumber ?? string.Empty,
                trip.TravelStartDate.ToDateTime(TimeOnly.MinValue), days, 0,
                $"{KindBriefing}:{trip.Id}:{latest.Id}",
                new[] { AudienceTraveller },
                ToTraveller(trip, StaffTravelNotices.BriefingUnacknowledged, "before", new Dictionary<string, object>
                {
                    ["Days"] = days,
                    ["RiskLevel"] = latest.RiskLevel.ToString(),
                })));
        }
    }

    // 8 and 9. The claim window (D-49). On a completed trip whose approved policy has one: the traveller is told a week
    //    before its last day, unless a claim has been submitted; once it has closed with a claim not submitted or cash
    //    still out, the desk is told once — the trip cannot close. A trip with nothing open is not chased.
    private async Task ClaimWindowsAsync(Guid tenantId, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        var policies = _unitOfWork.Repository<StaffTravelPolicy>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted && p.ApprovedById != null && p.ExpenseSubmissionDays > 0);
        var rows = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && r.Status == StaffTravelRequestStatus.Completed && r.PolicyId != null)
            .AsNoTracking()
            .Join(policies, r => r.PolicyId, p => (Guid?)p.Id, (r, p) => new { Trip = r, p.ExpenseSubmissionDays })
            .ToListAsync(cancellationToken);
        var inScope = rows
            .Select(x => (x.Trip, LastDay: x.Trip.TravelEndDate.AddDays(x.ExpenseSubmissionDays)))
            .Where(x => x.LastDay >= backlogFloor && x.LastDay.AddDays(-ClaimWindowWarningDays) <= today)
            .ToList();
        if (inScope.Count == 0) return;

        var ids = inScope.Select(x => x.Trip.Id).ToList();
        var claims = await _unitOfWork.Repository<StaffTravelExpenseClaim>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && ids.Contains(c.StaffTravelRequestId))
            .Select(c => new { c.StaffTravelRequestId, c.ClaimNumber, c.Status, c.SubmittedAt })
            .ToListAsync(cancellationToken);
        var cashOut = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.StaffTravelRequestId))
            .Where(StaffTravelAdvanceRules.CashOut)
            .Select(a => new { a.StaffTravelRequestId, a.AdvanceNumber, a.CurrencyCode, a.UnsettledAmount })
            .ToListAsync(cancellationToken);

        foreach (var (trip, lastDay) in inScope)
        {
            var mine = claims.Where(c => c.StaffTravelRequestId == trip.Id).ToList();
            var days = lastDay.DayNumber - today.DayNumber;
            var data = new Dictionary<string, object> { ["LastDay"] = StaffTravelNotices.Date(lastDay) };
            if (days >= 0)
            {
                if (mine.Any(c => c.SubmittedAt != null)) continue;
                results.Add(new Candidate(
                    KindClaimWindowClosing, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty,
                    lastDay.ToDateTime(TimeOnly.MinValue), days, 0,
                    $"{KindClaimWindowClosing}:{trip.Id}:{lastDay:yyyy-MM-dd}",
                    new[] { AudienceTraveller },
                    ToTraveller(trip, StaffTravelNotices.ClaimWindowClosing, "money", data)));
                continue;
            }

            var open = mine
                .Where(c => c.Status is TravelClaimStatus.Draft or TravelClaimStatus.Returned)
                .Select(c => c.Status == TravelClaimStatus.Draft
                    ? $"claim {c.ClaimNumber} never submitted"
                    : $"claim {c.ClaimNumber} returned and not sent back")
                .Concat(cashOut.Where(a => a.StaffTravelRequestId == trip.Id)
                    .Select(a => $"advance {a.AdvanceNumber} still out ({StaffTravelNotices.Money(a.CurrencyCode, a.UnsettledAmount)})"))
                .ToList();
            if (open.Count == 0) continue;
            data["Open"] = string.Join("; ", open);
            results.Add(new Candidate(
                KindClaimWindowPassed, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty,
                lastDay.ToDateTime(TimeOnly.MinValue), days, TierFor(days),
                $"{KindClaimWindowPassed}:{trip.Id}:{lastDay:yyyy-MM-dd}",
                new[] { AudienceDesk },
                ToDesk(trip, StaffTravelNotices.ClaimWindowPassed, StaffTravelNotices.DeskTrip(trip.Id, "finance"), data)));
        }
    }

    // 10 and 11. Fleet's word on a trip's company vehicles (D-29), read through travel's seam — Fleet's code unchanged.
    //    Every vehicle back while the trip is still under way: the desk is told once, to mark it completed if the traveller
    //    is back too (the sweep does so the day after it ends). An incident Fleet records on the vehicle of a trip not yet
    //    closed: the desk, and the traveller's nearest line authority with a login, once per incident.
    private async Task FleetAsync(
        Guid tenantId, TransitionPlan plan, DateOnly today, DateOnly backlogFloor, List<Candidate> results, CancellationToken cancellationToken)
    {
        foreach (var signals in plan.Fleet.Trips.GroupBy(s => s.RequestId))
        {
            if (!plan.Trips.TryGetValue(signals.Key, out var trip)
                || plan.Projected[trip.Id] != StaffTravelRequestStatus.InProgress)
                continue;
            var live = signals.Where(s => s.Status is not (FleetTripStatuses.Cancelled or FleetTripStatuses.Rejected)).ToList();
            if (live.Count == 0 || live.Any(s => s.Status != FleetTripStatuses.Completed)) continue;
            var last = live.OrderBy(s => s.ReturnedAt ?? DateTime.MinValue).ThenBy(s => s.FleetTripId).Last();
            results.Add(new Candidate(
                KindFleetReturned, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty,
                trip.TravelEndDate.ToDateTime(TimeOnly.MinValue), trip.TravelEndDate.DayNumber - today.DayNumber, 0,
                $"{KindFleetReturned}:{trip.Id}:{last.FleetTripId}",
                new[] { AudienceDesk },
                ToDesk(trip, StaffTravelNotices.FleetReturned, StaffTravelNotices.DeskTrip(trip.Id), new Dictionary<string, object>
                {
                    ["ReturnedOn"] = last.ReturnedAt is DateTime back
                        ? StaffTravelNotices.Date(DateOnly.FromDateTime(back))
                        : "a date Fleet does not record",
                    ["EndDate"] = StaffTravelNotices.Date(trip.TravelEndDate),
                })));
        }

        var floor = backlogFloor.ToDateTime(TimeOnly.MinValue);
        foreach (var incident in plan.Fleet.Incidents.Where(i => i.OccurredAtUtc >= floor).OrderBy(i => i.OccurredAtUtc))
        {
            if (!plan.Trips.TryGetValue(incident.RequestId, out var trip)
                || plan.Projected[trip.Id] is not (StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress
                    or StaffTravelRequestStatus.Completed))
                continue;
            var occurred = DateOnly.FromDateTime(incident.OccurredAtUtc);
            var data = new Dictionary<string, object>
            {
                ["Severity"] = incident.Severity,
                ["IncidentType"] = Spaced(incident.IncidentType).ToLowerInvariant(),
                ["OccurredOn"] = StaffTravelNotices.Date(occurred),
                ["Vehicle"] = incident.Vehicle,
                ["Title"] = incident.Title,
            };
            var send = ToDesk(trip, StaffTravelNotices.FleetIncident, StaffTravelNotices.DeskTrip(trip.Id, "compliance"), data);
            var sentTo = new[] { AudienceDesk };
            if ((await HrLineAuthority.GetLineApproversAsync(
                    _unitOfWork, _userManager.Users, tenantId, trip.EmployeeId, 1, cancellationToken)).FirstOrDefault() is { } manager)
            {
                send = Both(send, ct => _notices.TellLineManagerAsync(
                    trip, manager.UserId, StaffTravelNotices.FleetIncident, StaffTravelNotices.DeskTrip(trip.Id), data, ct));
                sentTo = new[] { AudienceDesk, AudienceLineManager };
            }
            results.Add(new Candidate(
                KindFleetIncident, "Travel request", trip.Id, trip.RequestNumber ?? string.Empty,
                incident.OccurredAtUtc, occurred.DayNumber - today.DayNumber, 0,
                $"{KindFleetIncident}:{incident.IncidentId}", sentTo, send));
        }
    }

    private Guid RequireTenant()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }
}
