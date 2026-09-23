using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Orientation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The orientation & onboarding reminder engine (round 4, lane K) — and the first HR sweep that
/// <b>delivers</b>. Every sweep before it (probation, discipline, leave, movements, assets and the
/// rest — eleven of them) records what it would have said in a dispatch log that nothing reads on to
/// anybody. This one writes an in-app notification to each person and emails it.
/// </summary>
/// <remarks>
/// <para><b>Eight rules.</b> Onboarding tasks: due soon, overdue (three escalating rungs), and
/// completed but awaiting sign-off. Orientations: due soon, overdue, assessment not attempted,
/// acknowledgement not signed. Certificates: expiring. The windows are the tenant's
/// (<c>CompanyHrPolicySettings</c>: <c>OnboardingTaskDueLeadDays</c>, <c>OrientationDueLeadDays</c>,
/// <c>OrientationCertificateExpiryLeadDays</c>, <c>OrientationChaseAfterDays</c>).</para>
///
/// <para><b>Send-once</b>, exactly as the probation engine: a dedupe key of kind, item, DUE date and
/// escalation tier, claimed in the same <c>SaveChanges</c> that records the run — and, here, writes
/// the notifications. Moving a due date re-arms the reminder; each overdue rung fires once; a preview
/// claims nothing.</para>
///
/// <para><b>One message per person per run, not one per item.</b> Measured on the demo database
/// before this was built: every one of 755 onboarding plans had the same coordinator. One
/// notification per overdue task would have handed her hundreds on the first night. Each item is
/// still claimed and logged on its own; what reaches a person is one digest listing theirs.</para>
///
/// <para><b>A backlog horizon</b> — overdue items more than 90 days past their date are history, not
/// reminders. The same demo database held 6,583 open tasks overdue by more than 90 days. Not a
/// setting, like the leave engine's: it protects the system from itself.</para>
///
/// <para><b>Routing.</b> An onboarding task goes to its assignee, else the plan's coordinator; an
/// orientation, assessment, acknowledgement or certificate goes to the participant. ⚠ When nothing
/// resolves (a plan with no coordinator) the item is still claimed and logged, with nobody to route
/// to — work that is due and has no owner is what HR most needs to see, not what to suppress.</para>
///
/// <para><b>What the email did is recorded, not assumed.</b> The templated email service never
/// throws and says only true or false, so the outcome is kept per item — <c>Sent</c>, <c>Failed</c>,
/// <c>TimedOut</c>, <c>NoAddress</c>, or <c>NoMailServer</c> when no mail server is configured
/// (checked the way the sender checks it) — and totalled on the run. On a database with no mail
/// server the in-app notification is the delivery, and the log says so.</para>
/// </remarks>
public class OnboardingOrientationReminderService : IOnboardingOrientationReminderService
{
    /// <summary>Overdue items older than this are history, not reminders.</summary>
    private const int BacklogHorizonDays = 90;

    /// <summary>Most items a digest lists before "and N more".</summary>
    private const int DigestListLimit = 25;

    /// <summary>Overdue escalation: chase, then chase harder, then stop climbing.</summary>
    private static readonly (int MinDaysOverdue, int Tier)[] OverdueTiers =
    {
        (14, 3),
        (7, 2),
        (0, 1),
    };

    private static readonly OnboardingTaskStatus[] ClosedTaskStatuses =
    {
        OnboardingTaskStatus.Completed,
        OnboardingTaskStatus.Waived,
        OnboardingTaskStatus.PendingVerification,
    };

    private static readonly OnboardingStatus[] OpenPlanStatuses =
    {
        OnboardingStatus.NotStarted,
        OnboardingStatus.InProgress,
        OnboardingStatus.Overdue,
    };

    private static readonly OrientationEnrollmentStatus[] OpenEnrollmentStatuses =
    {
        OrientationEnrollmentStatus.PendingConfirmation,
        OrientationEnrollmentStatus.Confirmed,
        OrientationEnrollmentStatus.Active,
    };

    private static readonly OrientationCompletionStatus[] OpenCompletionStatuses =
    {
        OrientationCompletionStatus.NotStarted,
        OrientationCompletionStatus.InProgress,
        OrientationCompletionStatus.PendingAssessment,
        OrientationCompletionStatus.PendingAcknowledgement,
        OrientationCompletionStatus.Overdue,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OnboardingOrientationReminderService> _logger;

    public OnboardingOrientationReminderService(
        IUnitOfWork unitOfWork,
        ICompanyHrPolicySettingsService policySettings,
        ITemplatedEmailService templatedEmail,
        IConfiguration configuration,
        ICurrentUserProvider currentUserProvider,
        ILogger<OnboardingOrientationReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _policySettings = policySettings;
        _templatedEmail = templatedEmail;
        _configuration = configuration;
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

    public async Task<OnboardingOrientationReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await BuildCandidatesAsync(tenantId, now, cancellationToken);

        // Keys already claimed by an earlier sweep — what makes the nightly host and the run-now
        // button safe to overlap: the second finds nothing left to claim.
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var claimed = (await _unitOfWork.Repository<OnboardingOrientationReminderDispatchLog>().GetQueryable()
                .Where(d => d.TenantId == tenantId && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var fresh = candidates.Where(c => !claimed.Contains(c.DedupeKey)).ToList();

        var mailServer = await MailServerConfiguredAsync();
        var run = new OnboardingOrientationReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = string.IsNullOrWhiteSpace(trigger) ? "Scheduled" : trigger,
            TriggeredByUserId = triggeredByUserId,
            RemindersQueued = fresh.Count,
            Unrouted = fresh.Count(f => f.RoutedToEmployeeId is null),
            MailServerConfigured = mailServer,
        };
        await _unitOfWork.Repository<OnboardingOrientationReminderRun>().AddAsync(run);

        // One notification per person, carrying all of their items.
        var recipientIds = fresh.Where(f => f.RoutedToEmployeeId.HasValue).Select(f => f.RoutedToEmployeeId!.Value).Distinct().ToList();
        var people = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && recipientIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmailAddress })
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var digests = new List<Digest>();
        foreach (var group in fresh.Where(f => f.RoutedToEmployeeId.HasValue).GroupBy(f => f.RoutedToEmployeeId!.Value))
        {
            people.TryGetValue(group.Key, out var person);
            var name = person is null ? "colleague" : $"{person.FirstName} {person.LastName}".Trim();
            var digest = Compose(tenantId, group.Key, name, person?.EmailAddress, group.OrderBy(i => i.DaysRemaining).ToList(), now);
            await _unitOfWork.Repository<OrientationNotification>().AddAsync(digest.Notification);
            digests.Add(digest);
        }
        run.NotificationsDelivered = digests.Count;

        var logs = new Dictionary<string, OnboardingOrientationReminderDispatchLog>(StringComparer.Ordinal);
        foreach (var item in fresh)
        {
            var carriedBy = item.RoutedToEmployeeId is { } r ? digests.First(d => d.RecipientId == r) : null;
            var log = new OnboardingOrientationReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = item.Kind,
                ItemType = item.ItemType,
                EntityId = item.EntityId,
                OnboardingPlanId = item.OnboardingPlanId,
                EmployeeOrientationId = item.EmployeeOrientationId,
                Reference = Clip(item.Reference, 300),
                DueDate = item.DueDate,
                DaysRemaining = item.DaysRemaining,
                EscalationTier = item.EscalationTier,
                RoutedToEmployeeId = item.RoutedToEmployeeId,
                NotificationId = carriedBy?.Notification.Id,
                EmailOutcome = carriedBy is null ? "NotRouted" : "Pending",
                DedupeKey = item.DedupeKey,
            };
            await _unitOfWork.Repository<OnboardingOrientationReminderDispatchLog>().AddAsync(log);
            logs[item.DedupeKey] = log;
        }

        // The run, its claims and the in-app notifications land together: a crash cannot leave keys
        // claimed for reminders nobody received, or notifications for a sweep that was never recorded.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Email after the commit, best effort: an unreachable mail server must not undo a delivery
        // that has already happened in-app. What happened is written back per item.
        foreach (var digest in digests)
        {
            var outcome = !mailServer ? "NoMailServer"
                : string.IsNullOrWhiteSpace(digest.Email) ? "NoAddress"
                : await SendAsync(digest);
            if (outcome == "Sent") run.EmailsSent++; else run.EmailsNotSent++;
            foreach (var item in digest.Items) logs[item.DedupeKey].EmailOutcome = outcome;
        }
        run.CompletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Orientation & onboarding reminder sweep ({Trigger}) claimed {Count} item(s) for tenant {TenantId}: " +
            "{Notifications} notification(s), {Sent} email(s) sent, {NotSent} not sent, {Unrouted} unrouted, mail server {Mail}",
            run.Trigger, fresh.Count, tenantId, run.NotificationsDelivered, run.EmailsSent, run.EmailsNotSent, run.Unrouted,
            mailServer ? "configured" : "NOT configured");

        return new OnboardingOrientationReminderRunResultDto
        {
            RunId = run.Id,
            RemindersQueued = run.RemindersQueued,
            NotificationsDelivered = run.NotificationsDelivered,
            EmailsSent = run.EmailsSent,
            EmailsNotSent = run.EmailsNotSent,
            Unrouted = run.Unrouted,
            MailServerConfigured = mailServer,
            ByKind = fresh.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    public async Task<IEnumerable<OnboardingOrientationReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
        => await BuildCandidatesAsync(GetTenantId(), asOf ?? DateTime.UtcNow, cancellationToken);

    // ── The rules ─────────────────────────────────────────────────────────────

    private async Task<List<OnboardingOrientationReminderPreviewItemDto>> BuildCandidatesAsync(
        Guid tenantId, DateTime asOf, CancellationToken cancellationToken)
    {
        // ⚠ BY TENANT, not by current user: the nightly host has no HTTP context.
        var settings = await _policySettings.GetForTenantAsync(tenantId, cancellationToken);
        var taskLead = Math.Max(0, settings.OnboardingTaskDueLeadDays);
        var orientationLead = Math.Max(0, settings.OrientationDueLeadDays);
        var certificateLead = Math.Max(0, settings.OrientationCertificateExpiryLeadDays);
        var chaseAfter = Math.Max(1, settings.OrientationChaseAfterDays);

        var today = DateOnly.FromDateTime(asOf);
        var horizon = today.AddDays(-BacklogHorizonDays);
        var items = new List<OnboardingOrientationReminderPreviewItemDto>();

        // ── Onboarding tasks ──────────────────────────────────────────────────
        var taskLeadEnd = today.AddDays(taskLead);
        var chaseCutoff = today.AddDays(-chaseAfter);
        var tasks = await _unitOfWork.Repository<OnboardingTask>().GetQueryable().AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Join(_unitOfWork.Repository<OnboardingPlan>().GetQueryable()
                    .Where(p => !p.IsDeleted && OpenPlanStatuses.Contains(p.Status)),
                t => t.OnboardingPlanId, p => p.Id, (t, p) => new { Task = t, Plan = p })
            .Where(x => (!ClosedTaskStatuses.Contains(x.Task.Status) && x.Task.DueDate >= horizon && x.Task.DueDate <= taskLeadEnd)
                        || (x.Task.Status == OnboardingTaskStatus.PendingVerification
                            && x.Task.CompletedDate != null && x.Task.CompletedDate <= chaseCutoff && x.Task.CompletedDate >= horizon))
            .Select(x => new
            {
                x.Task.Id,
                x.Task.TaskName,
                x.Task.Status,
                x.Task.DueDate,
                x.Task.CompletedDate,
                x.Task.AssignedToId,
                PlanId = x.Plan.Id,
                x.Plan.OnboardingCoordinatorId,
                x.Plan.EmployeeId,
            })
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(tenantId,
            tasks.Select(t => (Guid?)t.EmployeeId).Concat(tasks.Select(t => t.AssignedToId)).Concat(tasks.Select(t => t.OnboardingCoordinatorId)),
            cancellationToken);

        foreach (var t in tasks)
        {
            var hire = names.TryGetValue(t.EmployeeId, out var h) ? h : "a new hire";
            var routedTo = t.AssignedToId ?? t.OnboardingCoordinatorId;
            var routedName = routedTo is { } rid && names.TryGetValue(rid, out var rn) ? rn : null;
            const string url = "/hr/orientation/onboarding/queues";

            if (t.Status == OnboardingTaskStatus.PendingVerification)
            {
                var done = t.CompletedDate!.Value;
                items.Add(new OnboardingOrientationReminderPreviewItemDto
                {
                    Kind = "OnboardingTaskAwaitingSignOff",
                    ItemType = "Onboarding task",
                    EntityId = t.Id,
                    OnboardingPlanId = t.PlanId,
                    Reference = $"{t.TaskName} — {hire}: done, waiting to be signed off",
                    DueDate = done.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = -(today.DayNumber - done.DayNumber),
                    EscalationTier = 0,
                    RoutedToEmployeeId = t.OnboardingCoordinatorId,
                    RoutedToName = t.OnboardingCoordinatorId is { } c && names.TryGetValue(c, out var cn) ? cn : null,
                    DedupeKey = Key("OnboardingTaskAwaitingSignOff", t.Id, done, 0),
                    NavigationUrl = url,
                });
                continue;
            }

            var days = t.DueDate.DayNumber - today.DayNumber;
            if (days >= 0)
            {
                items.Add(new OnboardingOrientationReminderPreviewItemDto
                {
                    Kind = "OnboardingTaskDueSoon",
                    ItemType = "Onboarding task",
                    EntityId = t.Id,
                    OnboardingPlanId = t.PlanId,
                    Reference = $"{t.TaskName} — {hire}",
                    DueDate = t.DueDate.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = days,
                    EscalationTier = 0,
                    RoutedToEmployeeId = routedTo,
                    RoutedToName = routedName,
                    DedupeKey = Key("OnboardingTaskDueSoon", t.Id, t.DueDate, 0),
                    NavigationUrl = url,
                });
            }
            else
            {
                var tier = TierFor(-days);
                items.Add(new OnboardingOrientationReminderPreviewItemDto
                {
                    Kind = "OnboardingTaskOverdue",
                    ItemType = "Onboarding task",
                    EntityId = t.Id,
                    OnboardingPlanId = t.PlanId,
                    Reference = $"{t.TaskName} — {hire}",
                    DueDate = t.DueDate.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = days,
                    EscalationTier = tier,
                    RoutedToEmployeeId = routedTo,
                    RoutedToName = routedName,
                    DedupeKey = Key("OnboardingTaskOverdue", t.Id, t.DueDate, tier),
                    NavigationUrl = url,
                });
            }
        }

        // ── Orientations ──────────────────────────────────────────────────────
        var horizonStart = horizon.ToDateTime(TimeOnly.MinValue);
        var orientationLeadEnd = today.AddDays(orientationLead + 1).ToDateTime(TimeOnly.MinValue);
        var chaseCutoffAt = today.AddDays(-chaseAfter + 1).ToDateTime(TimeOnly.MinValue);
        var enrolments = await _unitOfWork.Repository<EmployeeOrientation>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted
                        && OpenEnrollmentStatuses.Contains(e.EnrollmentStatus)
                        && OpenCompletionStatuses.Contains(e.CompletionStatus)
                        && ((e.NextDueDate != null && e.NextDueDate >= horizonStart && e.NextDueDate < orientationLeadEnd)
                            || e.CompletionStatus == OrientationCompletionStatus.PendingAssessment
                            || e.CompletionStatus == OrientationCompletionStatus.PendingAcknowledgement))
            .Join(_unitOfWork.Repository<OrientationProgram>().GetQueryable().Where(p => !p.IsDeleted),
                e => e.ProgramId, p => p.Id, (e, p) => new
                {
                    e.Id,
                    e.EmployeeId,
                    e.ProgramId,
                    e.NextDueDate,
                    e.CompletionStatus,
                    e.AttemptCount,
                    e.AcknowledgementSigned,
                    Since = e.LastActivityAt ?? e.StartedAt ?? e.EnrolledAt,
                    p.Title,
                })
            .ToListAsync(cancellationToken);

        foreach (var e in enrolments)
        {
            var url = $"/me/orientation/{e.Id}";

            if (e.NextDueDate is { } dueAt && dueAt >= horizonStart && dueAt < orientationLeadEnd)
            {
                var due = DateOnly.FromDateTime(dueAt);
                var days = due.DayNumber - today.DayNumber;
                var overdue = days < 0;
                var tier = overdue ? TierFor(-days) : 0;
                var kind = overdue ? "OrientationOverdue" : "OrientationDueSoon";
                items.Add(new OnboardingOrientationReminderPreviewItemDto
                {
                    Kind = kind,
                    ItemType = "Orientation",
                    EntityId = e.Id,
                    EmployeeOrientationId = e.Id,
                    Reference = e.Title,
                    DueDate = due.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining = days,
                    EscalationTier = tier,
                    RoutedToEmployeeId = e.EmployeeId,
                    DedupeKey = Key(kind, e.Id, due, tier),
                    NavigationUrl = url,
                });
            }

            // Waiting on the participant: the content is done and the next step is theirs.
            if (e.Since < chaseCutoffAt && e.Since >= horizonStart)
            {
                var since = DateOnly.FromDateTime(e.Since);
                if (e.CompletionStatus == OrientationCompletionStatus.PendingAssessment && e.AttemptCount == 0)
                    items.Add(Waiting("OrientationAssessmentNotAttempted", $"{e.Title}: the assessment has not been attempted", e.Id, e.EmployeeId, since, today, url));
                if (e.CompletionStatus == OrientationCompletionStatus.PendingAcknowledgement && !e.AcknowledgementSigned)
                    items.Add(Waiting("OrientationAcknowledgementOutstanding", $"{e.Title}: the acknowledgement has not been signed", e.Id, e.EmployeeId, since, today, url));
            }
        }

        // ── Certificates ──────────────────────────────────────────────────────
        var todayAt = today.ToDateTime(TimeOnly.MinValue);
        var certificateLeadEnd = today.AddDays(certificateLead + 1).ToDateTime(TimeOnly.MinValue);
        var certificates = await _unitOfWork.Repository<OrientationCertificate>().GetQueryable().AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.Status == OrientationCertificateStatus.Active
                        && c.ExpiresAt != null && c.ExpiresAt >= todayAt && c.ExpiresAt < certificateLeadEnd)
            .Join(_unitOfWork.Repository<EmployeeOrientation>().GetQueryable().Where(e => !e.IsDeleted),
                c => c.EmployeeOrientationId, e => e.Id, (c, e) => new { c.Id, c.CertificateNumber, c.ExpiresAt, e.EmployeeId, e.ProgramId, EnrolmentId = e.Id })
            .Join(_unitOfWork.Repository<OrientationProgram>().GetQueryable(),
                x => x.ProgramId, p => p.Id, (x, p) => new { x.Id, x.CertificateNumber, x.ExpiresAt, x.EmployeeId, x.EnrolmentId, p.Title })
            .ToListAsync(cancellationToken);

        foreach (var c in certificates)
        {
            var expires = DateOnly.FromDateTime(c.ExpiresAt!.Value);
            items.Add(new OnboardingOrientationReminderPreviewItemDto
            {
                Kind = "OrientationCertificateExpiring",
                ItemType = "Certificate",
                EntityId = c.Id,
                EmployeeOrientationId = c.EnrolmentId,
                Reference = $"{c.Title}: certificate {c.CertificateNumber} expires",
                DueDate = expires.ToDateTime(TimeOnly.MinValue),
                DaysRemaining = expires.DayNumber - today.DayNumber,
                EscalationTier = 0,
                RoutedToEmployeeId = c.EmployeeId,
                DedupeKey = Key("OrientationCertificateExpiring", c.Id, expires, 0),
                NavigationUrl = $"/me/orientation/{c.EnrolmentId}",
            });
        }

        // The participant's own name, for the preview's "routed to" column.
        var participantNames = await NamesAsync(tenantId,
            items.Where(i => i.RoutedToName is null && i.RoutedToEmployeeId.HasValue).Select(i => i.RoutedToEmployeeId), cancellationToken);
        foreach (var i in items.Where(i => i.RoutedToName is null && i.RoutedToEmployeeId.HasValue))
            i.RoutedToName = participantNames.TryGetValue(i.RoutedToEmployeeId!.Value, out var n) ? n : null;

        return items;
    }

    private static OnboardingOrientationReminderPreviewItemDto Waiting(
        string kind, string reference, Guid enrolmentId, Guid employeeId, DateOnly since, DateOnly today, string url) => new()
    {
        Kind = kind,
        ItemType = "Orientation",
        EntityId = enrolmentId,
        EmployeeOrientationId = enrolmentId,
        Reference = reference,
        DueDate = since.ToDateTime(TimeOnly.MinValue),
        DaysRemaining = -(today.DayNumber - since.DayNumber),
        EscalationTier = 0,
        RoutedToEmployeeId = employeeId,
        DedupeKey = Key(kind, enrolmentId, since, 0),
        NavigationUrl = url,
    };

    // ── Delivery ──────────────────────────────────────────────────────────────

    private sealed record Digest(
        Guid RecipientId, string? Email, OrientationNotification Notification,
        List<OnboardingOrientationReminderPreviewItemDto> Items, Dictionary<string, string?> Tokens);

    /// <summary>One person's message: the notification, and the email's tokens.</summary>
    private Digest Compose(
        Guid tenantId, Guid recipientId, string name, string? email, List<OnboardingOrientationReminderPreviewItemDto> items, DateTime now)
    {
        var tasks = items.Count(i => i.ItemType == "Onboarding task");
        var single = items.Count == 1 ? items[0] : null;

        var headline = single is not null
            ? $"{single.Reference} — {When(single)}"
            : tasks == items.Count ? $"{items.Count} onboarding tasks need your attention"
            : tasks == 0 ? $"{items.Count} orientation reminders"
            : $"{items.Count} onboarding and orientation items need your attention";

        var summary = tasks == 0
            ? "Your orientation needs your attention:"
            : tasks == items.Count
                ? "These onboarding tasks are due soon, overdue or waiting on you:"
                : "These onboarding tasks and orientations are due soon, overdue or waiting on you:";

        var lines = items.Take(DigestListLimit).Select(i => $"• {i.Reference} — {When(i)}").ToList();
        if (items.Count > DigestListLimit) lines.Add($"…and {items.Count - DigestListLimit} more.");
        var list = string.Join("\n", lines);

        // One place to go: the single enrolment, the participant's list, or HR's onboarding queues.
        var path = single?.NavigationUrl
                   ?? (tasks == 0 ? "/me/orientation" : "/hr/orientation/onboarding/queues");
        var actionLabel = tasks == 0
            ? (single is not null ? "Open the orientation" : "Open my orientations")
            : "Open the onboarding queues";

        var type = items.Any(i => i.DaysRemaining < 0 && i.EscalationTier > 0) ? OrientationNotificationType.Overdue
            : items.Any(i => i.Kind.EndsWith("DueSoon", StringComparison.Ordinal) || i.Kind == "OrientationCertificateExpiring")
                ? OrientationNotificationType.DeadlineApproaching
                : OrientationNotificationType.Reminder;

        var notification = new OrientationNotification
        {
            // ⚠ Stamped here: the nightly host's context has no tenant, so the save hook cannot.
            TenantId = tenantId,
            RecipientEmployeeId = recipientId,
            Type = type,
            Subject = Clip(headline, 300),
            Message = Clip($"{summary}\n{list}", 4000),
            NavigationUrl = path,
            ProgramId = null,
            EmployeeOrientationId = single?.EmployeeOrientationId,
            IsRead = false,
            SentAt = now,
        };

        var baseUrl = (_configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["RecipientName"] = name,
            ["Headline"] = headline,
            ["Summary"] = summary,
            ["ItemList"] = list,
            ["ItemCount"] = items.Count.ToString(),
            ["ActionUrl"] = baseUrl + path,
            ["ActionLabel"] = actionLabel,
        };

        return new Digest(recipientId, email, notification, items, tokens);
    }

    /// <summary>When an item is, in words: "due tomorrow", "4 days overdue", "waiting 5 days".</summary>
    private static string When(OnboardingOrientationReminderPreviewItemDto item)
    {
        var d = item.DaysRemaining;
        if (item.Kind is "OnboardingTaskAwaitingSignOff" or "OrientationAssessmentNotAttempted" or "OrientationAcknowledgementOutstanding")
            return $"waiting {-d} day{(-d == 1 ? "" : "s")}";
        if (item.Kind == "OrientationCertificateExpiring")
            return d == 0 ? "expires today" : $"expires on {item.DueDate:d MMM yyyy}";
        return d switch
        {
            0 => "due today",
            1 => "due tomorrow",
            > 1 => $"due in {d} days ({item.DueDate:d MMM})",
            -1 => "1 day overdue",
            _ => $"{-d} days overdue",
        };
    }

    private async Task<string> SendAsync(Digest digest)
    {
        try
        {
            var send = _templatedEmail.SendAsync(
                OnboardingOrientationEmailCatalog.Module, OnboardingOrientationEmailCatalog.Events.ReminderDigest,
                digest.Email!, digest.Tokens);
            if (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10))) != send)
            {
                _logger.LogWarning("Orientation reminder email to {Email} timed out after 10 s.", digest.Email);
                return "TimedOut";
            }
            return await send ? "Sent" : "Failed";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orientation reminder email to {Email} failed.", digest.Email);
            return "Failed";
        }
    }

    /// <summary>
    /// Whether the sender could send at all — asked the way <c>SettingsService</c> answers it: the
    /// first mail settings row, with a host and a from address.
    /// </summary>
    private async Task<bool> MailServerConfiguredAsync()
    {
        var settings = await _unitOfWork.Repository<EmailSettings>().FirstOrDefaultAsync(e => true);
        return settings is not null && !string.IsNullOrWhiteSpace(settings.SmtpHost) && !string.IsNullOrWhiteSpace(settings.FromAddress);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, string>> NamesAsync(Guid tenantId, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, string>();
        return await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && list.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => (e.FirstName + " " + e.LastName).Trim(), cancellationToken);
    }

    private static int TierFor(int daysOverdue)
        => OverdueTiers.First(t => daysOverdue >= t.MinDaysOverdue).Tier;

    /// <summary>
    /// The send-once key. Includes the DUE date, so moving a deadline re-arms the reminder, and the
    /// tier, so each overdue rung fires once rather than the first one swallowing the rest.
    /// </summary>
    private static string Key(string kind, Guid entityId, DateOnly due, int tier)
        => $"{kind}:{entityId}:{due:yyyy-MM-dd}:{tier}";

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<OnboardingOrientationReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (count is < 1 or > 200) count = 20;

        return await _unitOfWork.Repository<OnboardingOrientationReminderRun>().GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .Select(r => new OnboardingOrientationReminderRunDto
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Trigger = r.Trigger,
                RemindersQueued = r.RemindersQueued,
                NotificationsDelivered = r.NotificationsDelivered,
                EmailsSent = r.EmailsSent,
                EmailsNotSent = r.EmailsNotSent,
                Unrouted = r.Unrouted,
                MailServerConfigured = r.MailServerConfigured,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<OnboardingOrientationReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (days is < 1 or > 365) days = 14;
        var since = DateTime.UtcNow.AddDays(-days);

        var rows = await _unitOfWork.Repository<OnboardingOrientationReminderDispatchLog>().GetQueryable()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.CreatedAt >= since)
            .OrderByDescending(d => d.CreatedAt)
            .Take(2000)
            .Select(d => new OnboardingOrientationReminderLogEntryDto
            {
                Id = d.Id,
                RunId = d.RunId,
                Kind = d.Kind,
                ItemType = d.ItemType,
                EntityId = d.EntityId,
                OnboardingPlanId = d.OnboardingPlanId,
                EmployeeOrientationId = d.EmployeeOrientationId,
                Reference = d.Reference,
                DueDate = d.DueDate,
                DaysRemaining = d.DaysRemaining,
                EscalationTier = d.EscalationTier,
                RoutedToEmployeeId = d.RoutedToEmployeeId,
                NotificationId = d.NotificationId,
                EmailOutcome = d.EmailOutcome,
                DispatchedAt = d.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(tenantId, rows.Select(r => r.RoutedToEmployeeId), cancellationToken);
        foreach (var r in rows)
            r.RoutedToName = r.RoutedToEmployeeId is { } id && names.TryGetValue(id, out var n) ? n : null;
        return rows;
    }
}
