using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Orientation;

/// <summary>Which session states are "live" — the ones a notice about the session is worth sending for.</summary>
public static class OrientationSessionLife
{
    /// <summary>
    /// Published and later, short of the end. A Draft has nobody on it to tell; a Completed or
    /// Cancelled session has nothing left to change.
    /// </summary>
    public static bool IsLive(OrientationSessionStatus status) => status is
        OrientationSessionStatus.Published
        or OrientationSessionStatus.EnrollmentOpen
        or OrientationSessionStatus.EnrollmentClosed
        or OrientationSessionStatus.InProgress
        or OrientationSessionStatus.Postponed;

    /// <summary>The enrolment states of somebody still expected at a session.</summary>
    public static readonly OrientationEnrollmentStatus[] OnSession =
    {
        OrientationEnrollmentStatus.PendingConfirmation,
        OrientationEnrollmentStatus.Confirmed,
        OrientationEnrollmentStatus.Waitlisted,
        OrientationEnrollmentStatus.Active,
    };
}

/// <summary>
/// Composes and stages the lifecycle notices (round 4, lane K-b). See
/// <see cref="IOnboardingOrientationNotices"/> for the contract: stage, never save, never throw.
/// </summary>
/// <remarks>
/// <para><b>One notice, two deliveries.</b> The row is the in-app notice — My Notifications reads it
/// straight away — and it carries its own email, queued: the event key, the tokens as they stood at
/// the event, and <c>EmailStatus = Queued</c>. <c>OrientationNoticeEmailDispatcher</c> sends it
/// after the save. The email's words are the notice's words: <c>Headline</c> and <c>Body</c> are
/// composed here once.</para>
///
/// <para><b>Links only where the reader can go.</b> A participant's notice opens their enrolment; a
/// facilitator's carries none, because the session page is HR's and a facilitator is often not.</para>
///
/// <para>⚠ <b>The tenant is stamped from the record</b>, never read from the caller: the audience-rule
/// sweep runs on a host with no tenant context.</para>
/// </remarks>
public sealed class OnboardingOrientationNoticeService : IOnboardingOrientationNotices
{
    private const string Actor = "system:orientation-notices";

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingOrientationNoticeService> _logger;

    public OnboardingOrientationNoticeService(IUnitOfWork unitOfWork, ILogger<OnboardingOrientationNoticeService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Enrolment ─────────────────────────────────────────────────────────────

    public async Task EnrolledAsync(
        EmployeeOrientation enrolment, OrientationNoticeProgramme programme, OrientationSession? session, string? reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The programme's "Send reminders" switch reads "enrolment, deadline and overdue notices".
            if (!programme.EnableReminders) return;

            var waitlisted = session is not null && enrolment.EnrollmentStatus == OrientationEnrollmentStatus.Waitlisted;
            var headline = waitlisted
                ? $"You are on the waiting list for {programme.Title}"
                : $"You have been enrolled in {programme.Title}";

            var lines = new List<string>();
            if (enrolment.NextDueDate is { } due) lines.Add($"Complete it by {Day(due)}.");
            if (session is not null)
            {
                lines.Add(waitlisted
                    ? $"The session \"{session.Title}\" ({When(session)}) is full: you are number {enrolment.WaitlistPosition} on its waiting list."
                    : $"Your session: {session.Title} — {When(session)}, {Where(session)}.");
                if (!waitlisted && !string.IsNullOrWhiteSpace(session.ParticipantInstructions))
                    lines.Add(session.ParticipantInstructions.Trim());
            }

            var why = reason ?? enrolment.EnrollmentSource switch
            {
                OrientationEnrollmentSource.HrAssigned => "Enrolled by HR.",
                OrientationEnrollmentSource.ManagerAssigned => "Enrolled by your manager.",
                _ => null,
            };
            if (why is not null) lines.Add(why);
            if (lines.Count == 0) lines.Add("You can start it from My Orientations.");

            await StageAsync(enrolment.TenantId, enrolment.EmployeeId, OrientationNotificationType.Enrollment,
                OnboardingOrientationEmailCatalog.Events.Enrolled, programme.Id, enrolment.Id, headline, lines,
                $"/me/orientation/{enrolment.Id}", "Open the orientation",
                new()
                {
                    ["ProgrammeTitle"] = programme.Title,
                    ["DueDate"] = enrolment.NextDueDate is { } d ? Day(d) : null,
                    ["SessionTitle"] = session?.Title,
                    ["SessionWhen"] = session is null ? null : When(session),
                    ["SessionWhere"] = session is null ? null : Where(session),
                    ["WaitlistPosition"] = waitlisted ? enrolment.WaitlistPosition?.ToString(En) : null,
                    ["Reason"] = why,
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Enrolment notice for {EnrolmentId} could not be composed; the enrolment stands.", enrolment.Id);
        }
    }

    public async Task PlacedOnSessionAsync(
        EmployeeOrientation enrolment, OrientationSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            var programme = await ProgrammeTitleAsync(session.ProgramId, cancellationToken);
            var waitlisted = enrolment.EnrollmentStatus == OrientationEnrollmentStatus.Waitlisted;

            var lines = new List<string> { $"{session.Title} — {When(session)}, {Where(session)}." };
            if (waitlisted)
                lines.Add($"The session is full: you are number {enrolment.WaitlistPosition} on its waiting list.");
            else if (!string.IsNullOrWhiteSpace(session.ParticipantInstructions))
                lines.Add(session.ParticipantInstructions.Trim());

            await StageAsync(enrolment.TenantId, enrolment.EmployeeId, OrientationNotificationType.SessionScheduled,
                OnboardingOrientationEmailCatalog.Events.SessionScheduled, session.ProgramId, enrolment.Id,
                waitlisted
                    ? $"You are on the waiting list for a {programme} session"
                    : $"Your {programme} session: {DayOrTbc(session.ScheduledStartAt)}",
                lines, $"/me/orientation/{enrolment.Id}", "Open the orientation",
                SessionFacts(programme, session, "participant"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Session notice for enrolment {EnrolmentId} could not be composed; the change stands.", enrolment.Id);
        }
    }

    // ── Sessions ──────────────────────────────────────────────────────────────

    public async Task FacilitatorsScheduledAsync(
        OrientationSession session, IReadOnlyCollection<Guid> facilitatorEmployeeIds, CancellationToken cancellationToken = default)
    {
        try
        {
            if (facilitatorEmployeeIds.Count == 0) return;
            var programme = await ProgrammeTitleAsync(session.ProgramId, cancellationToken);

            foreach (var employeeId in facilitatorEmployeeIds.Distinct())
                await StageAsync(session.TenantId, employeeId, OrientationNotificationType.SessionScheduled,
                    OnboardingOrientationEmailCatalog.Events.SessionScheduled, session.ProgramId, null,
                    $"You are facilitating {session.Title}: {DayOrTbc(session.ScheduledStartAt)}",
                    new[] { $"{programme} — {When(session)}, {Where(session)}." },
                    null, null, SessionFacts(programme, session, "facilitator"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Facilitator notices for session {SessionId} could not be composed; the change stands.", session.Id);
        }
    }

    public async Task SessionRescheduledAsync(
        OrientationSession session, OrientationSessionSnapshot before, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = OrientationSessionSnapshot.Of(session);
            if (now.SameWhenAndWhereAs(before) || !OrientationSessionLife.IsLive(session.Status)) return;

            var programme = await ProgrammeTitleAsync(session.ProgramId, cancellationToken);
            var headline = before.StartAt != session.ScheduledStartAt
                ? $"{session.Title} has moved to {DayOrTbc(session.ScheduledStartAt)}"
                : $"{session.Title} has changed";
            var lines = new[]
            {
                $"Was: {When(before.StartAt, before.EndAt)}, {Where(before.Venue, before.MeetingUrl)}.",
                $"Now: {When(session)}, {Where(session)}.",
            };

            await TellEveryoneOnAsync(session, OrientationNotificationType.SessionRescheduled,
                OnboardingOrientationEmailCatalog.Events.SessionRescheduled, headline,
                role => lines,
                role => new Dictionary<string, string?>(SessionFacts(programme, session, role), StringComparer.OrdinalIgnoreCase)
                {
                    ["WasWhen"] = When(before.StartAt, before.EndAt),
                    ["WasWhere"] = Where(before.Venue, before.MeetingUrl),
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Reschedule notices for session {SessionId} could not be composed; the change stands.", session.Id);
        }
    }

    public async Task SessionCalledOffAsync(
        OrientationSession session, OrientationSessionStatus newStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            if (newStatus is not (OrientationSessionStatus.Cancelled or OrientationSessionStatus.Postponed)) return;
            var programme = await ProgrammeTitleAsync(session.ProgramId, cancellationToken);
            var cancelled = newStatus == OrientationSessionStatus.Cancelled;

            var headline = cancelled
                ? $"{session.Title} on {DayOrTbc(session.ScheduledStartAt)} is cancelled"
                : $"{session.Title} is postponed";

            IReadOnlyList<string> Lines(string role) => cancelled
                ? role == "participant"
                    ? new[] { $"You are still enrolled in {programme}; you will be told if you are placed on another session." }
                    : new[] { "You were to facilitate it; nothing more is needed from you for it." }
                : new[] { $"It was to run {When(session)}. A new date will follow." };

            await TellEveryoneOnAsync(session,
                cancelled ? OrientationNotificationType.Cancellation : OrientationNotificationType.SessionPostponed,
                cancelled ? OnboardingOrientationEmailCatalog.Events.SessionCancelled : OnboardingOrientationEmailCatalog.Events.SessionPostponed,
                headline, Lines,
                role => new Dictionary<string, string?>(SessionFacts(programme, session, role), StringComparer.OrdinalIgnoreCase)
                {
                    ["WasWhen"] = When(session),
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{Status} notices for session {SessionId} could not be composed; the change stands.", newStatus, session.Id);
        }
    }

    /// <summary>
    /// One notice to each participant still expected at the session (their enrolment linked) and one to
    /// each employee facilitating it (no link). Somebody who is both hears once, as a participant.
    /// </summary>
    private async Task TellEveryoneOnAsync(
        OrientationSession session, OrientationNotificationType type, string eventKey, string headline,
        Func<string, IReadOnlyList<string>> lines, Func<string, Dictionary<string, string?>> facts,
        CancellationToken cancellationToken)
    {
        var participants = await _unitOfWork.Repository<EmployeeOrientation>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == session.TenantId && e.SessionId == session.Id && !e.IsDeleted
                        && OrientationSessionLife.OnSession.Contains(e.EnrollmentStatus))
            .Select(e => new { e.Id, e.EmployeeId })
            .ToListAsync(cancellationToken);

        var facilitators = await InternalFacilitatorsAsync(session, cancellationToken);

        var told = new HashSet<Guid>();
        foreach (var p in participants)
        {
            if (!told.Add(p.EmployeeId)) continue;
            await StageAsync(session.TenantId, p.EmployeeId, type, eventKey, session.ProgramId, p.Id, headline,
                lines("participant"), $"/me/orientation/{p.Id}", "Open the orientation", facts("participant"));
        }
        foreach (var f in facilitators)
        {
            if (!told.Add(f)) continue;
            await StageAsync(session.TenantId, f, type, eventKey, session.ProgramId, null, headline,
                lines("facilitator"), null, null, facts("facilitator"));
        }
    }

    /// <summary>The employees facilitating a session — an external facilitator has no inbox here.</summary>
    private async Task<List<Guid>> InternalFacilitatorsAsync(OrientationSession session, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<OrientationSessionFacilitator>().GetQueryable().AsNoTracking()
            .Where(f => f.TenantId == session.TenantId && f.SessionId == session.Id && !f.IsDeleted && f.EmployeeId != null)
            .Select(f => f.EmployeeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    // ── Completion and certificates ──────────────────────────────────────────

    public async Task CompletedAsync(
        EmployeeOrientation enrolment, OrientationProgram programme, OrientationCertificate? certificate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var completedOn = enrolment.CompletedAt ?? DateTime.UtcNow;
            var lines = new List<string> { $"Completed on {Day(completedOn)}." };
            if (certificate is not null) lines.Add(CertificateLine(certificate));

            string? opens = null;
            if (programme.IsRecurring && programme.RecurrenceFrequency is { } frequency)
            {
                var day = OrientationTriggerWindows.OpensOn(DateOnly.FromDateTime(completedOn), frequency, programme.CompletionDeadlineDays);
                opens = Day(day.ToDateTime(TimeOnly.MinValue));
                lines.Add($"{programme.Title} recurs {OrientationTriggerWindows.Describe(frequency)}: your next cycle is due to open on {opens}.");
            }

            await StageAsync(enrolment.TenantId, enrolment.EmployeeId, OrientationNotificationType.Completion,
                OnboardingOrientationEmailCatalog.Events.Completed, programme.Id, enrolment.Id,
                $"You have completed {programme.Title}", lines,
                $"/me/orientation/{enrolment.Id}", certificate is null ? "Open the orientation" : "View your certificate",
                new()
                {
                    ["ProgrammeTitle"] = programme.Title,
                    ["CompletedOn"] = Day(completedOn),
                    ["CertificateNumber"] = certificate?.CertificateNumber,
                    ["CertificateExpires"] = certificate?.ExpiresAt is { } x ? Day(x) : null,
                    ["NextCycleOpens"] = opens,
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Completion notice for {EnrolmentId} could not be composed; the completion stands.", enrolment.Id);
        }
    }

    public async Task CertificateIssuedAsync(
        EmployeeOrientation enrolment, OrientationProgram programme, OrientationCertificate certificate, bool reissue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var lines = new List<string> { $"Issued on {Day(certificate.IssuedAt)}.", CertificateLine(certificate) };
            if (reissue) lines.Add("It replaces your earlier certificate for this programme, which no longer stands.");

            await StageAsync(enrolment.TenantId, enrolment.EmployeeId, OrientationNotificationType.CertificateIssued,
                OnboardingOrientationEmailCatalog.Events.CertificateIssued, programme.Id, enrolment.Id,
                reissue
                    ? $"Your {programme.Title} certificate has been reissued: {certificate.CertificateNumber}"
                    : $"Your {programme.Title} certificate: {certificate.CertificateNumber}",
                lines, $"/me/orientation/{enrolment.Id}", "View your certificate",
                new()
                {
                    ["ProgrammeTitle"] = programme.Title,
                    ["CertificateNumber"] = certificate.CertificateNumber,
                    ["CertificateIssuedOn"] = Day(certificate.IssuedAt),
                    ["CertificateExpires"] = certificate.ExpiresAt is { } x ? Day(x) : null,
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Certificate notice for {CertificateId} could not be composed; the certificate stands.", certificate.Id);
        }
    }

    private static string CertificateLine(OrientationCertificate certificate) => certificate.ExpiresAt is { } expires
        ? $"Your certificate is {certificate.CertificateNumber}, valid until {Day(expires)}."
        : $"Your certificate is {certificate.CertificateNumber}; it does not expire.";

    // ── Onboarding (lane K-b2) ────────────────────────────────────────────────
    //
    // The new hire, their buddy and anybody given a task are sent to "My onboarding" in the portal —
    // built in this lane so that every one of these notices has somewhere to go. The coordinator is
    // sent to the plan on HR's screen, where coordinating happens.

    private const string MyOnboardingPath = "/me/onboarding";

    private static readonly OnboardingStatus[] ClosedPlanStatuses = { OnboardingStatus.Completed, OnboardingStatus.Cancelled };

    public async Task PlanAssignedAsync(
        OnboardingPlan plan, IReadOnlyCollection<OnboardingTask> tasks, CancellationToken cancellationToken = default)
    {
        try
        {
            var names = await NamesAsync(plan.TenantId, new[] { plan.EmployeeId, plan.OnboardingCoordinatorId, plan.AssignedBuddyId }, cancellationToken);
            var hire = NameOf(names, plan.EmployeeId) ?? "a new hire";
            var coordinator = NameOf(names, plan.OnboardingCoordinatorId);
            var buddy = NameOf(names, plan.AssignedBuddyId);
            var begins = plan.StartDate >= DateOnly.FromDateTime(DateTime.UtcNow) ? "starts" : "began";
            var start = Day(plan.StartDate);

            var welcome = new List<string> { coordinator is null ? "HR is coordinating it." : $"{coordinator} is coordinating it." };
            if (buddy is not null) welcome.Add($"{buddy} is your onboarding buddy — somebody to ask anything.");
            if (tasks.Count > 0) welcome.Add($"{tasks.Count} tasks are planned. See them, and anything that is yours to do, under My onboarding.");
            await StageAsync(plan.TenantId, plan.EmployeeId, OrientationNotificationType.OnboardingPlanAssigned,
                OnboardingOrientationEmailCatalog.Events.OnboardingWelcome, null, null,
                $"Welcome — your onboarding {begins} on {start}", welcome, MyOnboardingPath, "Open my onboarding",
                new()
                {
                    ["NewHireName"] = hire, ["StartDate"] = start, ["CoordinatorName"] = coordinator,
                    ["BuddyName"] = buddy, ["TaskCount"] = tasks.Count.ToString(En),
                });

            if (plan.OnboardingCoordinatorId is { } coordinatorId && coordinatorId != plan.EmployeeId)
                await CoordinatorAsync(plan, coordinatorId, hire, buddy, tasks.Count, tasks.Select(t => (DateOnly?)t.DueDate).Min(), takingOver: false);

            if (plan.AssignedBuddyId is { } buddyId && buddyId != plan.EmployeeId)
                await BuddyAsync(plan, buddyId, hire, coordinator);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Onboarding notices for plan {PlanId} could not be composed; the plan stands.", plan.Id);
        }
    }

    public async Task PlanRolesChangedAsync(
        OnboardingPlan plan, Guid? previousCoordinatorId, Guid? previousBuddyId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (ClosedPlanStatuses.Contains(plan.Status)) return;
            var coordinatorChanged = plan.OnboardingCoordinatorId is { } c && c != previousCoordinatorId && c != plan.EmployeeId;
            var buddyChanged = plan.AssignedBuddyId is { } b && b != previousBuddyId && b != plan.EmployeeId;
            if (!coordinatorChanged && !buddyChanged) return;

            var names = await NamesAsync(plan.TenantId, new[] { plan.EmployeeId, plan.OnboardingCoordinatorId, plan.AssignedBuddyId }, cancellationToken);
            var hire = NameOf(names, plan.EmployeeId) ?? "a new hire";
            var open = await _unitOfWork.Repository<OnboardingTask>().GetQueryable().AsNoTracking()
                .Where(t => t.OnboardingPlanId == plan.Id && !t.IsDeleted
                            && t.Status != OnboardingTaskStatus.Completed && t.Status != OnboardingTaskStatus.Waived)
                .Select(t => t.DueDate)
                .ToListAsync(cancellationToken);

            if (coordinatorChanged)
                await CoordinatorAsync(plan, plan.OnboardingCoordinatorId!.Value, hire, NameOf(names, plan.AssignedBuddyId),
                    open.Count, open.Count == 0 ? (DateOnly?)null : open.Min(), takingOver: true);
            if (buddyChanged)
                await BuddyAsync(plan, plan.AssignedBuddyId!.Value, hire, NameOf(names, plan.OnboardingCoordinatorId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Onboarding role notices for plan {PlanId} could not be composed; the change stands.", plan.Id);
        }
    }

    private async Task CoordinatorAsync(
        OnboardingPlan plan, Guid coordinatorId, string hire, string? buddy, int taskCount, DateOnly? firstDue, bool takingOver)
    {
        var lines = new List<string>
        {
            $"It {(plan.StartDate >= DateOnly.FromDateTime(DateTime.UtcNow) ? "starts" : "began")} on {Day(plan.StartDate)}.",
            taskCount == 0
                ? "It has no open tasks yet."
                : $"{taskCount} {(takingOver ? "open " : "")}task{(taskCount == 1 ? "" : "s")}; the first due on {Day(firstDue!.Value)}.",
        };
        if (buddy is not null) lines.Add($"{buddy} is their onboarding buddy.");

        await StageAsync(plan.TenantId, coordinatorId, OrientationNotificationType.OnboardingPlanAssigned,
            OnboardingOrientationEmailCatalog.Events.OnboardingCoordinatorAssigned, null, null,
            takingOver ? $"You are now coordinating {hire}'s onboarding" : $"You are coordinating {hire}'s onboarding",
            lines, $"/hr/orientation/onboarding/{plan.Id}", "Open the plan",
            new()
            {
                ["NewHireName"] = hire, ["StartDate"] = Day(plan.StartDate),
                ["TaskCount"] = taskCount.ToString(En), ["FirstDue"] = firstDue is { } d ? Day(d) : null,
            });
    }

    private async Task BuddyAsync(OnboardingPlan plan, Guid buddyId, string hire, string? coordinator)
    {
        var lines = new List<string>
        {
            $"{hire} {(plan.StartDate >= DateOnly.FromDateTime(DateTime.UtcNow) ? "starts" : "started")} on {Day(plan.StartDate)}.",
        };
        if (coordinator is not null) lines.Add($"{coordinator} is coordinating their onboarding.");
        lines.Add("Be somebody they can ask anything.");

        await StageAsync(plan.TenantId, buddyId, OrientationNotificationType.OnboardingPlanAssigned,
            OnboardingOrientationEmailCatalog.Events.OnboardingBuddyAssigned, null, null,
            $"You are {hire}'s onboarding buddy", lines, MyOnboardingPath, "Open my onboarding",
            new() { ["NewHireName"] = hire, ["StartDate"] = Day(plan.StartDate), ["CoordinatorName"] = coordinator });
    }

    public async Task TaskAssignedAsync(OnboardingTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            if (task.AssignedToId is not { } assigneeId) return;
            if (task.Status is OnboardingTaskStatus.Completed or OnboardingTaskStatus.Waived or OnboardingTaskStatus.PendingVerification) return;

            var plan = await _unitOfWork.Repository<OnboardingPlan>().GetQueryable().AsNoTracking()
                .Where(p => p.Id == task.OnboardingPlanId)
                .Select(p => new { p.EmployeeId, p.OnboardingCoordinatorId, p.StartDate, p.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (plan is null || ClosedPlanStatuses.Contains(plan.Status)) return;

            var names = await NamesAsync(task.TenantId, new[] { plan.EmployeeId, plan.OnboardingCoordinatorId }, cancellationToken);
            var hire = NameOf(names, plan.EmployeeId) ?? "a new hire";
            var coordinator = NameOf(names, plan.OnboardingCoordinatorId);
            var theirOwn = assigneeId == plan.EmployeeId;

            var lines = new List<string> { $"Due {Day(task.DueDate)}." };
            if (!string.IsNullOrWhiteSpace(task.Description)) lines.Add(task.Description.Trim());
            if (!theirOwn)
                lines.Add(coordinator is null
                    ? $"{hire} {(plan.StartDate >= DateOnly.FromDateTime(DateTime.UtcNow) ? "starts" : "started")} on {Day(plan.StartDate)}."
                    : $"{hire} {(plan.StartDate >= DateOnly.FromDateTime(DateTime.UtcNow) ? "starts" : "started")} on {Day(plan.StartDate)}; {coordinator} is coordinating.");
            lines.Add(task.RequiresVerification
                ? $"Mark it done under My onboarding when it is; {coordinator ?? "HR"} then signs it off."
                : "Mark it done under My onboarding when it is.");

            await StageAsync(task.TenantId, assigneeId, OrientationNotificationType.OnboardingTaskAssigned,
                OnboardingOrientationEmailCatalog.Events.OnboardingTaskAssigned, null, null,
                theirOwn ? $"Your onboarding task: {task.TaskName}" : $"Onboarding task for {hire}: {task.TaskName}",
                lines, MyOnboardingPath, "Open my onboarding tasks",
                new()
                {
                    ["TaskName"] = task.TaskName, ["NewHireName"] = hire, ["DueDate"] = Day(task.DueDate),
                    ["Description"] = task.Description, ["CoordinatorName"] = coordinator,
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Task notice for onboarding task {TaskId} could not be composed; the task stands.", task.Id);
        }
    }

    public async Task TaskAwaitingSignOffAsync(
        OnboardingTask task, Guid doneByEmployeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (task.Status != OnboardingTaskStatus.PendingVerification) return;

            var plan = await _unitOfWork.Repository<OnboardingPlan>().GetQueryable().AsNoTracking()
                .Where(p => p.Id == task.OnboardingPlanId)
                .Select(p => new { p.Id, p.EmployeeId, p.OnboardingCoordinatorId })
                .FirstOrDefaultAsync(cancellationToken);
            // Nobody to tell, or the coordinator did it themselves — and cannot sign off their own work.
            if (plan is null || plan.OnboardingCoordinatorId is not { } coordinatorId || coordinatorId == doneByEmployeeId) return;

            var names = await NamesAsync(task.TenantId, new Guid?[] { plan.EmployeeId, doneByEmployeeId }, cancellationToken);
            var hire = NameOf(names, plan.EmployeeId) ?? "a new hire";
            var doneBy = NameOf(names, doneByEmployeeId) ?? "the person it was given to";

            var lines = new List<string> { $"{doneBy} marked it done on {Day(DateTime.UtcNow)}." };
            if (!string.IsNullOrWhiteSpace(task.CompletionNotes)) lines.Add($"Their note: {task.CompletionNotes.Trim()}");

            await StageAsync(task.TenantId, coordinatorId, OrientationNotificationType.Completion,
                OnboardingOrientationEmailCatalog.Events.OnboardingTaskDone, null, null,
                $"{task.TaskName} for {hire} is done — waiting for your sign-off",
                lines, $"/hr/orientation/onboarding/{plan.Id}", "Open the plan",
                new()
                {
                    ["TaskName"] = task.TaskName, ["NewHireName"] = hire, ["DoneByName"] = doneBy,
                    ["CompletionNotes"] = task.CompletionNotes,
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Sign-off notice for onboarding task {TaskId} could not be composed; the task stands.", task.Id);
        }
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(Guid tenantId, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, string>();
        return await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && wanted.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => (e.FirstName + " " + e.LastName).Trim(), cancellationToken);
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id is { } value && names.TryGetValue(value, out var name) ? name : null;

    // ── Staging ───────────────────────────────────────────────────────────────

    private async Task StageAsync(
        Guid tenantId, Guid recipientId, OrientationNotificationType type, string eventKey,
        Guid? programId, Guid? enrolmentId, string headline, IEnumerable<string> lines,
        string? path, string? actionLabel, Dictionary<string, string?> facts)
    {
        var body = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        var tokens = new Dictionary<string, string?>(facts, StringComparer.OrdinalIgnoreCase)
        {
            ["Headline"] = headline,
            ["Body"] = body,
            ["ActionLabel"] = path is null ? null : actionLabel,
        };

        var notice = new OrientationNotification
        {
            TenantId = tenantId,
            RecipientEmployeeId = recipientId,
            Type = type,
            ProgramId = programId,
            EmployeeOrientationId = enrolmentId,
            Subject = Clip(headline, 300),
            Message = Clip(body, 4000),
            NavigationUrl = path is null ? null : Clip(path, 500),
            IsRead = false,
            SentAt = DateTime.UtcNow,
            EmailEventKey = eventKey,
            EmailTokens = JsonSerializer.Serialize(tokens),
            EmailStatus = OrientationNoticeEmailStatus.Queued,
            CreatedBy = Actor,
        };
        await _unitOfWork.Repository<OrientationNotification>().AddAsync(notice);
    }

    private static Dictionary<string, string?> SessionFacts(string programme, OrientationSession session, string role) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ProgrammeTitle"] = programme,
            ["SessionTitle"] = session.Title,
            ["SessionWhen"] = When(session),
            ["SessionWhere"] = Where(session),
            ["Role"] = role,
            ["Instructions"] = role == "participant" ? session.ParticipantInstructions : null,
        };

    private async Task<string> ProgrammeTitleAsync(Guid programId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<OrientationProgram>().GetQueryable().AsNoTracking()
               .Where(p => p.Id == programId)
               .Select(p => p.Title)
               .FirstOrDefaultAsync(cancellationToken)
           ?? "your orientation";

    // ── Words ─────────────────────────────────────────────────────────────────

    private static string Day(DateTime value) => value.ToString("dddd, d MMMM yyyy", En);

    private static string Day(DateOnly value) => Day(value.ToDateTime(TimeOnly.MinValue));

    private static string DayOrTbc(DateTime? value) => value is { } v ? Day(v) : "date to be confirmed";

    private static string When(OrientationSession session) => When(session.ScheduledStartAt, session.ScheduledEndAt);

    /// <summary>"Tuesday, 14 October 2026, 09:00–12:00", or across days in full; "date to be confirmed".</summary>
    private static string When(DateTime? start, DateTime? end)
    {
        if (start is not { } from) return "date to be confirmed";
        var text = from.ToString("dddd, d MMMM yyyy, HH:mm", En);
        if (end is { } to && to > from)
            text += to.Date == from.Date ? "–" + to.ToString("HH:mm", En) : " – " + to.ToString("dddd, d MMMM yyyy, HH:mm", En);
        return text;
    }

    private static string Where(OrientationSession session) => Where(session.VenueDescription, session.VirtualMeetingUrl);

    private static string Where(string? venue, string? url)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(venue)) parts.Add(venue.Trim());
        if (!string.IsNullOrWhiteSpace(url)) parts.Add($"online at {url.Trim()}");
        return parts.Count == 0 ? "place to be confirmed" : string.Join(", or ", parts);
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

/// <summary>The values <see cref="OrientationNotification.EmailStatus"/> takes.</summary>
public static class OrientationNoticeEmailStatus
{
    public const string Queued = "Queued";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string TimedOut = "TimedOut";
    public const string NoAddress = "NoAddress";
    public const string NoMailServer = "NoMailServer";

    /// <summary>Still queued when it was days old — the dispatcher was down, and news that late is not news.</summary>
    public const string Stale = "Stale";
}
