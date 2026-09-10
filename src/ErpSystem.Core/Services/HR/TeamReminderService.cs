using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc />
public class TeamReminderService : ITeamReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TeamReminderService> _logger;

    /// <summary>How far ahead the terms-of-reference expiry warning starts. Plan § 6.6.2.</summary>
    private const int TermsExpiryLeadDays = 30;

    public TeamReminderService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<TeamReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>One reminder the sweep has decided to send, before it is written down.</summary>
    private sealed record Candidate(
        string Kind,
        string ItemType,
        Guid EntityId,
        Guid TeamId,
        string Reference,
        DateOnly? DueDate,
        int DaysRemaining,
        Guid? RoutedToEmployeeId)
    {
        /// <summary>
        /// ⚠ The due date is IN the key. Moving a deadline therefore re-arms the reminder, which is
        /// what a moved deadline should do; without it, a task pushed back a month would stay quiet
        /// because it had already been chased once.
        /// </summary>
        public string DedupeKey => $"{Kind}:{EntityId}:{DueDate?.ToString("yyyy-MM-dd") ?? "none"}";
    }

    public async Task<TeamReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var leadDays = await GetTaskLeadDaysAsync(tenantId, cancellationToken);

        var candidates = new List<Candidate>();
        candidates.AddRange(await SweepTasksAsync(tenantId, leadDays, cancellationToken));
        candidates.AddRange(await SweepObjectivesAsync(tenantId, cancellationToken));
        candidates.AddRange(await SweepMeetingsAsync(tenantId, cancellationToken));
        candidates.AddRange(await SweepTermsAsync(tenantId, cancellationToken));

        // ── Send-once ────────────────────────────────────────────────────────
        // ⚠ The already-sent set is read ONCE and the claims are written in the same SaveChanges
        // that records the run, so the nightly host and the run-now button cannot double-send even
        // if they overlap. The unique (TenantId, DedupeKey) index is the backstop.
        var keys = candidates.Select(c => c.DedupeKey).Distinct().ToList();
        var alreadySent = keys.Count == 0
            ? new HashSet<string>()
            : (await _unitOfWork.Repository<TeamReminderDispatchLog>().GetQueryable().AsNoTracking()
                .Where(d => d.TenantId == tenantId && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(cancellationToken))
              .ToHashSet();

        var fresh = candidates
            .Where(c => !alreadySent.Contains(c.DedupeKey))
            .GroupBy(c => c.DedupeKey)
            .Select(g => g.First())
            .ToList();

        var run = new TeamReminderRun
        {
            // Explicit: the context auto-stamp is inert on a background scope, where there is no
            // authenticated user at all.
            TenantId = tenantId,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
            RemindersQueued = fresh.Count,
        };
        await _unitOfWork.Repository<TeamReminderRun>().AddAsync(run);

        foreach (var c in fresh)
        {
            await _unitOfWork.Repository<TeamReminderDispatchLog>().AddAsync(new TeamReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = c.Kind,
                ItemType = c.ItemType,
                EntityId = c.EntityId,
                TeamId = c.TeamId,
                Reference = c.Reference,
                DueDate = c.DueDate,
                DaysRemaining = c.DaysRemaining,
                RoutedToEmployeeId = c.RoutedToEmployeeId,
                DedupeKey = c.DedupeKey,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Team reminder sweep for tenant {TenantId}: {Queued} queued of {Found} found ({Trigger}).",
            tenantId, fresh.Count, candidates.Count, trigger);

        return new TeamReminderRunResultDto
        {
            RunId = run.Id,
            RemindersQueued = fresh.Count,
            TeamsSwept = candidates.Select(c => c.TeamId).Distinct().Count(),
            ByKind = fresh.GroupBy(c => c.Kind).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    /// <remarks>
    /// ⚠ Falls back to 3 rather than 0 when no settings row exists. Zero would mean "warn only on
    /// the day", which reads as the sweep working while it quietly warns about nothing — the exact
    /// failure the migration's <c>DEFAULT (3)</c> exists to prevent, guarded a second time here
    /// because a tenant created outside the seed has no row at all.
    /// </remarks>
    private async Task<int> GetTaskLeadDaysAsync(Guid tenantId, CancellationToken ct)
        => await _unitOfWork.Repository<CompanyHrPolicySettings>().GetQueryable().AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => (int?)s.TeamTaskReminderLeadDays)
            .FirstOrDefaultAsync(ct) ?? 3;

    /// <summary>Tasks due within the lead time, and tasks already overdue.</summary>
    /// <remarks>
    /// ⚠ Routed to the ASSIGNEE for a due-soon, and to the team's LEAD for an overdue. Chasing the
    /// person who is late is the first move; escalating to the person accountable is the second.
    /// An unassigned overdue task routes to nobody and says so — that is the case a lead most needs
    /// surfaced, not the one to suppress.
    /// </remarks>
    private async Task<List<Candidate>> SweepTasksAsync(Guid tenantId, int leadDays, CancellationToken ct)
    {
        var today = Today;
        var horizon = today.AddDays(leadDays);

        var rows = await _unitOfWork.Repository<TeamTask>().GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted
                     && t.DueDate != null
                     && t.Status != TeamTaskStatus.Completed
                     && t.Status != TeamTaskStatus.Cancelled
                     && t.DueDate <= horizon)
            .Include(t => t.Team)
            .Include(t => t.AssigneeMember)
            .Select(t => new
            {
                t.Id, t.TeamId, t.Title, t.DueDate,
                TeamName = t.Team.Name,
                TeamLeadId = t.Team.TeamLeadId,
                AssigneeEmployeeId = t.AssigneeMember != null ? (Guid?)t.AssigneeMember.EmployeeId : null,
            })
            .ToListAsync(ct);

        var result = new List<Candidate>();
        foreach (var t in rows)
        {
            var due = t.DueDate!.Value;
            var days = due.DayNumber - today.DayNumber;
            var overdue = days < 0;

            result.Add(new Candidate(
                Kind: overdue ? "TaskOverdue" : "TaskDueSoon",
                ItemType: "Team task",
                EntityId: t.Id,
                TeamId: t.TeamId,
                // ⚠ The team, the task and the date — and nothing about what the work IS. A reminder
                // travels further than the record it is about.
                Reference: $"{t.TeamName}: {Trim(t.Title, 120)}",
                DueDate: due,
                DaysRemaining: days,
                RoutedToEmployeeId: overdue ? t.TeamLeadId ?? t.AssigneeEmployeeId : t.AssigneeEmployeeId));
        }

        return result;
    }

    /// <summary>Objectives past their due date and still open.</summary>
    private async Task<List<Candidate>> SweepObjectivesAsync(Guid tenantId, CancellationToken ct)
    {
        var today = Today;

        var rows = await _unitOfWork.Repository<TeamObjective>().GetQueryable().AsNoTracking()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted
                     && o.DueDate != null && o.DueDate < today
                     && o.Status != TeamObjectiveStatus.Completed
                     && o.Status != TeamObjectiveStatus.Cancelled)
            .Include(o => o.Team)
            .Select(o => new
            {
                o.Id, o.TeamId, o.Title, o.DueDate, o.ProgressPercent,
                TeamName = o.Team.Name,
                TeamLeadId = o.Team.TeamLeadId,
            })
            .ToListAsync(ct);

        return rows.Select(o => new Candidate(
            Kind: "ObjectiveOverdue",
            ItemType: "Team objective",
            EntityId: o.Id,
            TeamId: o.TeamId,
            Reference: $"{o.TeamName}: {Trim(o.Title, 110)} ({o.ProgressPercent}%)",
            DueDate: o.DueDate,
            DaysRemaining: o.DueDate!.Value.DayNumber - today.DayNumber,
            RoutedToEmployeeId: o.TeamLeadId)).ToList();
    }

    /// <summary>Meetings happening tomorrow, to everyone expected at them.</summary>
    /// <remarks>
    /// ⚠ One reminder PER ATTENDEE, not one per meeting: the point is that each person is told, and
    /// a single dispatch row addressed to a meeting would have nobody to route to. The dedupe key
    /// therefore carries the attendee, which is why the key is built from the ATTENDEE row's id.
    /// </remarks>
    private async Task<List<Candidate>> SweepMeetingsAsync(Guid tenantId, CancellationToken ct)
    {
        var today = Today;
        var tomorrowStart = DateTime.UtcNow.Date.AddDays(1);
        var tomorrowEnd = tomorrowStart.AddDays(1);

        var rows = await _unitOfWork.Repository<TeamMeetingAttendee>().GetQueryable().AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted
                     && a.Meeting.Status == TeamMeetingStatus.Scheduled
                     && a.Meeting.ScheduledAt >= tomorrowStart
                     && a.Meeting.ScheduledAt < tomorrowEnd)
            .Select(a => new
            {
                AttendeeId = a.Id,
                a.Meeting.Id,
                a.Meeting.TeamId,
                a.Meeting.Title,
                a.Meeting.ScheduledAt,
                TeamName = a.Meeting.Team.Name,
                EmployeeId = (Guid?)a.Member.EmployeeId,
            })
            .ToListAsync(ct);

        return rows.Select(m => new Candidate(
            Kind: "MeetingTomorrow",
            ItemType: "Team meeting",
            EntityId: m.AttendeeId,
            TeamId: m.TeamId,
            Reference: $"{m.TeamName}: {Trim(m.Title, 110)} at {m.ScheduledAt:HH:mm}",
            DueDate: DateOnly.FromDateTime(m.ScheduledAt),
            DaysRemaining: DateOnly.FromDateTime(m.ScheduledAt).DayNumber - today.DayNumber,
            RoutedToEmployeeId: m.EmployeeId)).ToList();
    }

    /// <summary>Approved terms of reference lapsing within thirty days, or already lapsed.</summary>
    /// <remarks>
    /// ⚠ This is the rule that catches a committee quietly operating without a charter. It is not
    /// suppressed once the terms have lapsed — an expired charter is more urgent than an expiring
    /// one, not less.
    /// </remarks>
    private async Task<List<Candidate>> SweepTermsAsync(Guid tenantId, CancellationToken ct)
    {
        var today = Today;
        var horizon = today.AddDays(TermsExpiryLeadDays);

        var rows = await _unitOfWork.Repository<TeamTermsOfReference>().GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted
                     && t.Status == TeamTorStatus.Approved
                     && t.EffectiveTo != null && t.EffectiveTo <= horizon)
            .Include(t => t.Team)
            .Select(t => new
            {
                t.Id, t.TeamId, t.Version, t.EffectiveTo,
                TeamName = t.Team.Name,
                TeamLeadId = t.Team.TeamLeadId,
            })
            .ToListAsync(ct);

        return rows.Select(t => new Candidate(
            Kind: "TermsExpiring",
            ItemType: "Terms of reference",
            EntityId: t.Id,
            TeamId: t.TeamId,
            Reference: $"{t.TeamName}: terms of reference v{t.Version}",
            DueDate: t.EffectiveTo,
            DaysRemaining: t.EffectiveTo!.Value.DayNumber - today.DayNumber,
            RoutedToEmployeeId: t.TeamLeadId)).ToList();
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    // ── Reads, for the run-now screen and for proving the sweep fired ──────────

    public async Task<IEnumerable<TeamReminderRunDto>> GetRunsAsync(
        int take = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<TeamReminderRun>().GetQueryable().AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(r => new TeamReminderRunDto
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Trigger = r.Trigger,
                RemindersQueued = r.RemindersQueued,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TeamReminderDispatchDto>> GetDispatchesAsync(
        Guid runId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<TeamReminderDispatchLog>().GetQueryable().AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.RunId == runId)
            .OrderBy(d => d.Kind).ThenBy(d => d.DueDate)
            .Select(d => new TeamReminderDispatchDto
            {
                Id = d.Id,
                RunId = d.RunId,
                Kind = d.Kind,
                ItemType = d.ItemType,
                EntityId = d.EntityId,
                TeamId = d.TeamId,
                Reference = d.Reference,
                DueDate = d.DueDate,
                DaysRemaining = d.DaysRemaining,
                RoutedToEmployeeId = d.RoutedToEmployeeId,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }
}
