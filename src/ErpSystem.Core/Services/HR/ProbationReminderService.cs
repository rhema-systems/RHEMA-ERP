using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The probation reminder engine — FR-HR-032's month-5 confirmation form and FR-HR-140's expiry
/// notice, plus the review queues that stop either being reached blind.
/// </summary>
/// <remarks>
/// <para><b>Five rules.</b> <c>ConfirmationFormDue</c> fires one month before the probation ends,
/// which is FR-HR-032's "within the 5th month of a 6-month probation" stated in a way that also
/// holds for a junior's 3-month probation. <c>ProbationEndingSoon</c> is FR-HR-140's advance notice,
/// using <c>CompanyHrPolicySettings.ProbationEndLeadDays</c>. <c>ProbationOverdue</c> chases a
/// probation whose end date has passed while it is still Active — the state where someone is
/// working on, and being paid under, terms nobody has closed. <c>ReviewOverdue</c> and
/// <c>ReviewUnacknowledged</c> chase the reviews the decision is supposed to rest on.</para>
///
/// <para><b>Send-once.</b> A dedupe key encodes the item, the kind, the due date and the escalation
/// tier, and is claimed in the same <c>SaveChanges</c> that records the run. Moving a probation's
/// end date changes the key, which re-arms the ladder — deliberately, since a rescheduled deadline
/// is a new deadline.</para>
///
/// <para><b>Who receives them is not this engine's business yet.</b> The dispatch log records the
/// item, not the person. FR-HR-032 routes the form to "the head", and 0 of 41 organisation units
/// carry one (build plan §3.10) — decision D-2 puts a named confirming authority behind slice 8, and
/// this engine gains its recipient there rather than inventing one now.</para>
/// </remarks>
public class ProbationReminderService : IProbationReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ProbationReminderService> _logger;

    /// <summary>Overdue escalation: chase, then chase harder, then stop climbing.</summary>
    private static readonly (int MinDaysOverdue, int Tier)[] OverdueTiers =
    {
        (30, 3),
        (14, 2),
        (0, 1),
    };

    public ProbationReminderService(
        IUnitOfWork unitOfWork,
        ICompanyHrPolicySettingsService policySettings,
        ICurrentUserProvider currentUserProvider,
        ILogger<ProbationReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _policySettings = policySettings;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ── The sweep ─────────────────────────────────────────────────────────────

    public async Task<ProbationReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await BuildCandidatesAsync(tenantId, now, cancellationToken);

        // Keys already claimed by an earlier sweep. This is what makes the daily host and the
        // run-now button safe to overlap: the second one finds nothing left to claim.
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadySent = await _unitOfWork.Repository<ProbationReminderDispatchLog>().GetQueryable()
            .Where(d => d.TenantId == tenantId && keys.Contains(d.DedupeKey))
            .Select(d => d.DedupeKey)
            .ToListAsync(cancellationToken);
        var claimed = alreadySent.ToHashSet(StringComparer.Ordinal);

        var fresh = candidates.Where(c => !claimed.Contains(c.DedupeKey)).ToList();

        var run = new ProbationReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = string.IsNullOrWhiteSpace(trigger) ? "Scheduled" : trigger,
            TriggeredByUserId = triggeredByUserId,
            RemindersQueued = fresh.Count,
            CompletedAt = DateTime.UtcNow,
        };
        await _unitOfWork.Repository<ProbationReminderRun>().AddAsync(run);

        foreach (var item in fresh)
        {
            await _unitOfWork.Repository<ProbationReminderDispatchLog>().AddAsync(new ProbationReminderDispatchLog
            {
                TenantId = tenantId,
                Run = run,
                Kind = item.Kind,
                ItemType = item.ItemType,
                EntityId = item.EntityId,
                ProbationPeriodId = item.ProbationPeriodId,
                Reference = item.Reference,
                DueDate = item.DueDate,
                DaysRemaining = item.DaysRemaining,
                EscalationTier = item.EscalationTier,
                DedupeKey = item.DedupeKey,
            });
        }

        // The run row and its claims land together, so a crash cannot leave keys claimed for a
        // sweep that was never recorded, or a recorded sweep with nothing claimed.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Probation reminder sweep ({Trigger}) queued {Count} reminder(s) for tenant {TenantId}",
            run.Trigger, fresh.Count, tenantId);

        return new ProbationReminderRunResultDto
        {
            RunId = run.Id,
            RemindersQueued = fresh.Count,
            ByKind = fresh.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    public async Task<IEnumerable<ProbationReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
        => await BuildCandidatesAsync(GetTenantId(), asOf ?? DateTime.UtcNow, cancellationToken);

    // ── The rules ─────────────────────────────────────────────────────────────

    private async Task<List<ProbationReminderPreviewItemDto>> BuildCandidatesAsync(
        Guid tenantId, DateTime asOf, CancellationToken cancellationToken)
    {
        var settings = await _policySettings.GetAsync(cancellationToken);
        var leadDays = settings.ProbationEndLeadDays > 0 ? settings.ProbationEndLeadDays : 30;
        var today = DateOnly.FromDateTime(asOf);
        var items = new List<ProbationReminderPreviewItemDto>();

        var probations = await _unitOfWork.Repository<ProbationPeriod>().GetQueryable()
            .Include(p => p.Employee)
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.Status == ProbationStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var probation in probations)
        {
            var who = Describe(probation);
            var end = probation.CurrentEndDate;
            var daysRemaining = end.DayNumber - today.DayNumber;

            // FR-HR-032 — the confirmation form goes out one month before the end. Stated as a
            // month rather than "the 5th month" so a junior's 3-month probation is served by the
            // same rule; for the spec's 6-month case the two are the same date.
            var formDue = end.AddMonths(-1);
            if (today >= formDue && daysRemaining >= 0)
            {
                items.Add(new ProbationReminderPreviewItemDto
                {
                    Kind = "ConfirmationFormDue",
                    ItemType = "Probation period",
                    EntityId = probation.Id,
                    ProbationPeriodId = probation.Id,
                    Reference = $"{who} — confirmation form due",
                    DueDate = formDue.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = formDue.DayNumber - today.DayNumber,
                    EscalationTier = 0,
                    DedupeKey = Key("ConfirmationFormDue", probation.Id, formDue, 0),
                });
            }

            // FR-HR-140 — advance notice of expiry, at the tenant's configured lead time.
            if (daysRemaining >= 0 && daysRemaining <= leadDays)
            {
                items.Add(new ProbationReminderPreviewItemDto
                {
                    Kind = "ProbationEndingSoon",
                    ItemType = "Probation period",
                    EntityId = probation.Id,
                    ProbationPeriodId = probation.Id,
                    Reference = $"{who} — probation ends {end:yyyy-MM-dd}",
                    DueDate = end.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = daysRemaining,
                    EscalationTier = 0,
                    DedupeKey = Key("ProbationEndingSoon", probation.Id, end, 0),
                });
            }

            // Past the end date and still Active: nobody has confirmed, extended or terminated, so
            // the employee is working under terms that have quietly expired.
            if (daysRemaining < 0)
            {
                var tier = TierFor(-daysRemaining);
                items.Add(new ProbationReminderPreviewItemDto
                {
                    Kind = "ProbationOverdue",
                    ItemType = "Probation period",
                    EntityId = probation.Id,
                    ProbationPeriodId = probation.Id,
                    Reference = $"{who} — probation ended {end:yyyy-MM-dd}, no outcome recorded",
                    DueDate = end.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = daysRemaining,
                    EscalationTier = tier,
                    DedupeKey = Key("ProbationOverdue", probation.Id, end, tier),
                });
            }
        }

        var probationIds = probations.Select(p => p.Id).ToList();
        var reviews = await _unitOfWork.Repository<ProbationReview>().GetQueryable()
            .Include(r => r.ProbationPeriod).ThenInclude(p => p.Employee)
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && probationIds.Contains(r.ProbationPeriodId))
            .ToListAsync(cancellationToken);

        foreach (var review in reviews)
        {
            var who = Describe(review.ProbationPeriod);

            // A scheduled review whose date has passed and which nobody has conducted. The
            // confirmation decision is supposed to rest on these.
            if (review.Status != ProbationReviewStatus.Completed && review.ScheduledDate < today)
            {
                var overdueBy = today.DayNumber - review.ScheduledDate.DayNumber;
                var tier = TierFor(overdueBy);
                items.Add(new ProbationReminderPreviewItemDto
                {
                    Kind = "ReviewOverdue",
                    ItemType = "Probation review",
                    EntityId = review.Id,
                    ProbationPeriodId = review.ProbationPeriodId,
                    Reference = $"{who} — review {review.ReviewNumber} due {review.ScheduledDate:yyyy-MM-dd}",
                    DueDate = review.ScheduledDate.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = -overdueBy,
                    EscalationTier = tier,
                    DedupeKey = Key("ReviewOverdue", review.Id, review.ScheduledDate, tier),
                });
            }

            // Conducted, but the employee has never signed to say they saw it. A review the subject
            // has not seen cannot support a decision about them.
            if (review.Status == ProbationReviewStatus.Completed
                && !review.EmployeeAcknowledged
                && review.ActualDate is { } actual
                && today.DayNumber - actual.DayNumber >= 7)
            {
                var waitingDays = today.DayNumber - actual.DayNumber;
                var tier = TierFor(waitingDays - 7);
                items.Add(new ProbationReminderPreviewItemDto
                {
                    Kind = "ReviewUnacknowledged",
                    ItemType = "Probation review",
                    EntityId = review.Id,
                    ProbationPeriodId = review.ProbationPeriodId,
                    Reference = $"{who} — review {review.ReviewNumber} not acknowledged",
                    DueDate = actual.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = -waitingDays,
                    EscalationTier = tier,
                    DedupeKey = Key("ReviewUnacknowledged", review.Id, actual, tier),
                });
            }
        }

        return items;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// What the notification shows. The employee and what is due — never a rating, a
    /// recommendation or a reviewer's comment.
    /// </summary>
    private static string Describe(ProbationPeriod probation)
        => probation.Employee is null
            ? "Employee"
            : $"{probation.Employee.FullName} ({probation.Employee.EmployeeNumber})";

    private static int TierFor(int daysOverdue)
        => OverdueTiers.First(t => daysOverdue >= t.MinDaysOverdue).Tier;

    /// <summary>
    /// The send-once key. Includes the DUE date, so moving a deadline re-arms the ladder, and the
    /// tier, so each rung fires once rather than the first one swallowing the rest.
    /// </summary>
    private static string Key(string kind, Guid entityId, DateOnly due, int tier)
        => $"{kind}:{entityId}:{due:yyyy-MM-dd}:{tier}";

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ProbationReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (count is < 1 or > 200) count = 20;

        return await _unitOfWork.Repository<ProbationReminderRun>().GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .Select(r => new ProbationReminderRunDto
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Trigger = r.Trigger,
                RemindersQueued = r.RemindersQueued,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProbationReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (days is < 1 or > 365) days = 14;
        var since = DateTime.UtcNow.AddDays(-days);

        return await _unitOfWork.Repository<ProbationReminderDispatchLog>().GetQueryable()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.CreatedAt >= since)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new ProbationReminderLogEntryDto
            {
                Id = d.Id,
                RunId = d.RunId,
                Kind = d.Kind,
                ItemType = d.ItemType,
                EntityId = d.EntityId,
                ProbationPeriodId = d.ProbationPeriodId,
                Reference = d.Reference,
                DueDate = d.DueDate,
                DaysRemaining = d.DaysRemaining,
                EscalationTier = d.EscalationTier,
                DispatchedAt = d.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
