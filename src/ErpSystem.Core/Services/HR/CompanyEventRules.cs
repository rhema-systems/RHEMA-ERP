using System.Globalization;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>An event's dates and times, as a create, an edit or a reschedule asks for them.</summary>
public readonly record struct EventWindow(
    DateTime StartDate, TimeSpan? StartTime, DateTime EndDate, TimeSpan? EndTime, bool AllDay)
{
    public static EventWindow Of(CompanyEvent e) => new(e.StartDate, e.StartTime, e.EndDate, e.EndTime, e.IsAllDayEvent);

    /// <summary>The moment it starts: the start date at the start time, or at midnight for an all-day or untimed event.</summary>
    public DateTime Start => StartDate.Date + (AllDay ? TimeSpan.Zero : StartTime ?? TimeSpan.Zero);

    /// <summary>The same dates, times and all-day switch — what decides whether an edit is a move.</summary>
    public bool SameAs(EventWindow other) =>
        StartDate.Date == other.StartDate.Date && EndDate.Date == other.EndDate.Date
        && AllDay == other.AllDay
        && (AllDay || (StartTime == other.StartTime && EndTime == other.EndTime));
}

/// <summary>How two events clash (lane 2g-2, C-15): not at all, a warning shown before saving, or refused.</summary>
public enum EventClash
{
    None = 0,
    Warning = 1,
    Refused = 2,
}

/// <summary>
/// The rules an event's dates, status and approval follow (company-schedule final closure, lane 2a) —
/// pure, so the service, the diary source and the harness read the same answers.
/// </summary>
/// <remarks>
/// <para>⚠ <b>Refusals are sentences.</b> Each rule answers what to do about it, and the service throws
/// it as an <see cref="InvalidOperationException"/>, which this module's filter answers as a 422.</para>
///
/// <para>⚠ <b>Times are the event's own day-clock</b>, with no time zone: TDC works on GMT, so "now" is
/// compared in UTC.</para>
/// </remarks>
public static class CompanyEventRules
{
    /// <summary>The statuses an edit may set. Confirmed only where no approval is needed; the rest have their own action.</summary>
    public static readonly EventStatus[] EditableStatuses =
        [EventStatus.Scheduled, EventStatus.InProgress, EventStatus.Postponed];

    /// <summary>Cancelled or completed: nothing about it can change any more.</summary>
    public static bool IsClosed(CompanyEvent e) =>
        e.IsCancelled || e.Status is EventStatus.Cancelled or EventStatus.Completed;

    /// <summary>Needs approval and has none — including an approved event that has since moved.</summary>
    public static bool IsAwaitingApproval(CompanyEvent e) => e.RequiresApproval && e.ApprovalDate == null;

    /// <summary>
    /// Settled enough that an accepted invitation to it is a firm commitment (F-41): confirmed or under
    /// way, or — for an event that needs no approval — scheduled or rescheduled.
    /// </summary>
    /// <remarks>
    /// ⚠ Before lane 2a only Confirmed and InProgress counted, and only Approve ever set Confirmed, so an
    /// event that needed no approval was never firm however many people had accepted it.
    /// </remarks>
    public static bool IsFirm(CompanyEvent e) =>
        !IsClosed(e)
        && (e.Status is EventStatus.Confirmed or EventStatus.InProgress
            || (!IsAwaitingApproval(e) && e.Status is EventStatus.Scheduled or EventStatus.Rescheduled));

    // ── Event against event (lane 2g-2, C-15: the user's rulings, as D-9) ────────────────────────────

    /// <summary>Going ahead: not cancelled, completed or postponed. One awaiting approval counts — it may go ahead.</summary>
    public static bool IsLive(CompanyEvent e) => !IsClosed(e) && e.Status != EventStatus.Postponed;

    /// <summary>
    /// In the same place: the same site, or either with no site — online, or for the whole company (the user's ruling).
    /// Two events at different sites at once are in different places.
    /// </summary>
    public static bool SamePlace(CompanyEvent a, CompanyEvent b) =>
        a.LocationId is null || b.LocationId is null || a.LocationId == b.LocationId;

    /// <summary>
    /// At the same time: their days overlap and, when both are timed, so do their hours — a multi-day event holds its
    /// hours on each of its days, as the diaries read it (<see cref="DailyWindows"/>). An all-day or untimed event holds
    /// the whole day.
    /// </summary>
    public static bool SameTime(CompanyEvent a, CompanyEvent b)
    {
        if (a.StartDate.Date > b.EndDate.Date || b.StartDate.Date > a.EndDate.Date) return false;
        var aTimed = !a.IsAllDayEvent && a.StartTime.HasValue && a.EndTime.HasValue;
        var bTimed = !b.IsAllDayEvent && b.StartTime.HasValue && b.EndTime.HasValue;
        return !aTimed || !bTimed || (a.StartTime < b.EndTime && b.StartTime < a.EndTime);
    }

    /// <summary>
    /// Whether two events clash (C-15), and how hard. Both live, in the same place, at the same time, and at least one
    /// for more than its guest list — two guest-list meetings are the diaries' business, not this rule's. <b>Refused</b>
    /// when both are for the whole company, or both for the same unit (their audience as <see cref="AudienceRuleOf"/>
    /// reads it, so a private event is for its guests whatever its scope). <b>A warning</b> otherwise — a whole-company
    /// event against a unit's, say: HR decides.
    /// </summary>
    public static EventClash ClashOf(CompanyEvent a, CompanyEvent b)
    {
        if (!IsLive(a) || !IsLive(b) || !SamePlace(a, b) || !SameTime(a, b)) return EventClash.None;
        var ra = AudienceRuleOf(a);
        var rb = AudienceRuleOf(b);
        if (ra is null && rb is null) return EventClash.None;
        if (ra is { TargetType: HrAudienceTargetType.AllEmployees } && rb is { TargetType: HrAudienceTargetType.AllEmployees })
            return EventClash.Refused;
        if (ra is { TargetType: HrAudienceTargetType.OrganizationUnit } && rb is { TargetType: HrAudienceTargetType.OrganizationUnit }
            && ra.TargetId == rb.TargetId)
            return EventClash.Refused;
        return EventClash.Warning;
    }

    /// <summary>
    /// Who an event is for, beyond its guest list and organiser, as an audience rule (lane 2c, D-16) — or
    /// null when it is for the guest list alone.
    /// </summary>
    /// <remarks>
    /// <para><b>Visibility narrows, Scope widens.</b> A Private or Confidential event is for its guests and
    /// organiser only. A Department or Management visibility is the unit (with everything beneath it) or
    /// management, whatever the scope. A Public one is for its scope: everyone, the unit, or management.
    /// "Selected" and "External only" are their guest lists.</para>
    ///
    /// <para>Management is the resolver's rule: unit heads and line managers (D-16).</para>
    /// </remarks>
    public static HrAudienceRule? AudienceRuleOf(CompanyEvent e)
    {
        HrAudienceRule? Unit() => e.OrganizationUnitId is { } u
            ? new HrAudienceRule(HrAudienceTargetType.OrganizationUnit, u, false)
            : null;
        var management = new HrAudienceRule(HrAudienceTargetType.Management, null, false);

        return e.Visibility switch
        {
            EventVisibility.Private or EventVisibility.Confidential => null,
            EventVisibility.Management => management,
            EventVisibility.Department => Unit(),
            _ => e.Scope switch
            {
                ParticipantScope.AllStaff => new HrAudienceRule(HrAudienceTargetType.AllEmployees, null, false),
                ParticipantScope.Department => Unit(),
                ParticipantScope.ManagementOnly => management,
                _ => null,
            },
        };
    }

    /// <summary>
    /// The event's audience on the company calendar and in the diaries: its audience rule when it is
    /// shown on the company calendar, otherwise nobody beyond its guests and organiser.
    /// </summary>
    public static HrAudienceRule? CalendarAudienceOf(CompanyEvent e) => e.ShowOnCompanyCalendar ? AudienceRuleOf(e) : null;

    /// <summary>The audience as a sentence: "Everyone", "Finance and the units beneath it", "Management", "Its guests and organiser".</summary>
    public static string DescribeAudience(CompanyEvent e, string? unitName)
    {
        var rule = AudienceRuleOf(e);
        return rule?.TargetType switch
        {
            HrAudienceTargetType.AllEmployees => "Everyone",
            HrAudienceTargetType.OrganizationUnit => $"{unitName ?? "The unit"} and the units beneath it",
            HrAudienceTargetType.Management => "Management — unit heads and line managers",
            _ => "Its guests and organiser",
        };
    }

    /// <summary>A category that belongs elsewhere: public holidays and company milestones have their own registers (F-44).</summary>
    public static string? RefuseCategory(EventCategory category) => category switch
    {
        EventCategory.Holiday =>
            "Public holidays are kept in the holiday calendar, not as events. Add it there, or choose another category.",
        EventCategory.Milestone =>
            "Company milestones are kept in the milestones register, not as events. Add it there, or choose another category.",
        _ => null,
    };

    /// <summary>
    /// Checks a window and the settings that hang off its start, and drops what does not apply: the
    /// times of an all-day event, a deadline nobody is asked to meet, a lead with no reminder.
    /// Answers the sentence to refuse with, or null.
    /// </summary>
    public static string? ValidateAndNormalise(
        ref EventWindow window, bool requiresRsvp, ref DateTime? rsvpDeadline, bool sendReminders, ref int? reminderDaysBefore)
    {
        // ⚠ [Required] on a non-nullable DateTime never fails: an omitted date binds as 0001-01-01.
        if (window.StartDate.Year < 2000) return "Give the date the event starts.";
        if (window.EndDate.Year < 2000) return "Give the date the event ends.";
        if (window.EndDate.Date < window.StartDate.Date)
            return "The event ends before it starts. Set the last day on or after the first.";

        if (window.AllDay)
        {
            window = window with { StartTime = null, EndTime = null };
        }
        else
        {
            if (window.StartTime.HasValue != window.EndTime.HasValue)
                return "Give both a start and an end time, or mark the event as all-day.";
            if (window.StartTime is { } st && window.EndTime is { } et)
            {
                if (st < TimeSpan.Zero || st >= TimeSpan.FromDays(1) || et <= TimeSpan.Zero || et > TimeSpan.FromDays(1))
                    return "Times must fall within the day.";
                // On a multi-day event the times are each day's hours, so the end must follow the start there too.
                if (et <= st)
                    return window.StartDate.Date == window.EndDate.Date
                        ? "The event ends before it starts. Set the end time after the start time."
                        : "On an event over several days the times are each day's hours: set the end time after the start time.";
            }
        }

        if (requiresRsvp)
        {
            if (rsvpDeadline is not { } deadline)
                return "An event that asks for replies needs a reply-by date. Set the RSVP deadline, or stop asking for replies.";
            if (deadline > window.Start)
                return $"The RSVP deadline ({Describe(deadline)}) falls after the event starts ({Describe(window.Start)}). Set it on or before the start.";
        }
        else
        {
            rsvpDeadline = null;
        }

        if (sendReminders)
        {
            if (reminderDaysBefore is not { } days)
                return "Say how many days before the event the reminder goes, or switch reminders off.";
            if (days < 0)
                return "The reminder cannot go after the event. Give 0 days or more.";
        }
        else
        {
            reminderDaysBefore = null;
        }

        return null;
    }

    /// <summary>
    /// The event's hours on each of its days within <paramref name="from"/>..<paramref name="to"/>: a timed
    /// event over several days keeps the same hours each day (R4-10A.2); an all-day or untimed one covers
    /// each whole day.
    /// </summary>
    /// <remarks>
    /// ⚠ The diary source used to build one window from the first day only, so days two onward never
    /// reached the clash check or the diaries.
    /// </remarks>
    public static IEnumerable<(DateOnly Day, DateTime Start, DateTime End)> DailyWindows(
        CompanyEvent e, DateOnly from, DateOnly to)
    {
        var first = DateOnly.FromDateTime(e.StartDate);
        var last = DateOnly.FromDateTime(e.EndDate);
        if (last < first) last = first;
        var day = first > from ? first : from;
        var end = last < to ? last : to;
        var timed = !e.IsAllDayEvent && e.StartTime.HasValue && e.EndTime.HasValue;

        for (; day <= end; day = day.AddDays(1))
        {
            var midnight = day.ToDateTime(TimeOnly.MinValue);
            yield return timed
                ? (day, midnight + e.StartTime!.Value, midnight + e.EndTime!.Value)
                : (day, midnight, day.ToDateTime(TimeOnly.MaxValue));
        }
    }

    /// <summary>"Tuesday 14 October 2026, 09:30", or without the time at midnight.</summary>
    public static string Describe(DateTime when) =>
        when.TimeOfDay == TimeSpan.Zero
            ? when.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture)
            : when.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The window as a sentence: "14 October 2026, 09:30–16:45", "14–16 October 2026", "all day 14 October 2026".</summary>
    public static string Describe(EventWindow w)
    {
        var start = w.StartDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var end = w.EndDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var days = w.StartDate.Date == w.EndDate.Date ? start : $"{start} – {end}";
        return !w.AllDay && w.StartTime is { } st && w.EndTime is { } et
            ? $@"{days}, {st:hh\:mm}–{et:hh\:mm}"
            : $"all day {days}";
    }

    // ── Reminders and chases (lane 2e-1, F-33) ───────────────────────────────────────────────────

    /// <summary>
    /// Whether the event's reminder can go now — one rule for the "Send reminder now" button and the hourly sweep.
    /// It must be live (scheduled, confirmed or rescheduled), approved where approval is needed, and not yet begun.
    /// Answers the sentence to refuse with, or null.
    /// </summary>
    /// <remarks>
    /// ⚠ F-33: the buttons refused only a cancelled event, so they reminded people of completed, postponed and past
    /// events — and invited them to one still awaiting approval. The sweep already refused those.
    /// </remarks>
    public static string? RefuseReminding(CompanyEvent e, DateTime nowUtc) => RefuseSending(e, nowUtc, "remind");

    /// <summary>
    /// Whether the RSVP chase can go now — the reminder's rule, and the event asks for replies by a date still ahead.
    /// </summary>
    public static string? RefuseChasing(CompanyEvent e, DateTime nowUtc)
    {
        if (RefuseSending(e, nowUtc, "chase") is { } refusal) return refusal;
        if (!e.RequiresRsvp || e.RsvpDeadline is not { } deadline)
            return $"{e.EventName} does not ask for replies, so there is nobody to chase.";
        return deadline <= nowUtc
            ? $"The reply-by date for {e.EventName} ({Describe(deadline)}) has passed, so it is too late to chase."
            : null;
    }

    /// <summary>
    /// Whether the invitations not yet delivered can be sent again now (lane 2e-2): not to a closed event, not while it
    /// awaits approval (its approval sends them), and not once it has begun. A postponed event may still invite, as its
    /// guest list may still grow.
    /// </summary>
    public static string? RefuseInviting(CompanyEvent e, DateTime nowUtc)
    {
        if (IsClosed(e))
            return $"{e.EventName} is {(e.IsCancelled || e.Status == EventStatus.Cancelled ? "cancelled" : "completed")}, so nobody more can be invited.";
        if (IsAwaitingApproval(e))
            return $"{e.EventName} is still awaiting approval. Its invitations go when it is approved.";
        return e.Status == EventStatus.InProgress || e.StartDate.Date < nowUtc.Date
            ? $"{e.EventName} has already begun, so it is too late to invite anybody."
            : null;
    }

    private static string? RefuseSending(CompanyEvent e, DateTime nowUtc, string verb)
    {
        if (IsClosed(e))
            return $"{e.EventName} is {(e.IsCancelled || e.Status == EventStatus.Cancelled ? "cancelled" : "completed")}, so there is nobody to {verb}.";
        if (e.Status == EventStatus.Postponed)
            return $"{e.EventName} is postponed and has no date yet. Reschedule it before you {verb} anybody.";
        if (IsAwaitingApproval(e))
            return $"{e.EventName} is still awaiting approval, so nobody has been invited yet.";
        return e.Status == EventStatus.InProgress || e.StartDate.Date < nowUtc.Date
            ? $"{e.EventName} has already begun, so it is too late to {verb} anybody."
            : null;
    }

    // ── Guests, attendance and tasks (lane 2d) ───────────────────────────────────────────────────

    /// <summary>An answer an invitation can be given: accepted, declined or tentative (F-11).</summary>
    /// <remarks>⚠ The reply door took any status as an answer — "Not sent", "Sent", "No response".</remarks>
    public static bool IsAnswer(InvitationStatus status) =>
        status is InvitationStatus.Accepted or InvitationStatus.Declined or InvitationStatus.Tentative;

    /// <summary>
    /// Checks a guest and trims their details: an employee, or someone from outside with a name and an
    /// email address — the invitation goes to the address. Answers the sentence to refuse with, or null.
    /// </summary>
    /// <remarks>⚠ The server took an outside guest with no name and no address, whom no invitation could reach.</remarks>
    public static string? NormaliseGuest(Guid? employeeId, ref string? name, ref string? email, ref string? organisation)
    {
        name = Clean(name);
        email = Clean(email);
        organisation = Clean(organisation);

        if (employeeId is not null)
            return name is null && email is null && organisation is null
                ? null
                : "A guest is either an employee or someone from outside, not both. Clear the outside guest's details, or the employee.";

        return name is null || email is null
            ? "A guest from outside needs a name and an email address: the invitation goes to the address."
            : null;
    }

    /// <summary>
    /// Whether attendance can be marked yet: once the event has started, and never on a cancelled one.
    /// Answers the sentence to refuse with, or null.
    /// </summary>
    /// <remarks>
    /// ⚠ A register was taken a fortnight before a meeting, with a check-in on a day still to come — the
    /// round-4 suite did exactly that twelve times on UAT, each row then "checked out" before it checked in.
    /// </remarks>
    public static string? RefuseMarking(CompanyEvent e, DateTime nowUtc)
    {
        if (e.IsCancelled || e.Status == EventStatus.Cancelled)
            return $"{e.EventName} was cancelled, so there is no attendance to mark.";

        var start = EventWindow.Of(e).Start;
        return start > nowUtc
            ? $"{e.EventName} has not started yet — it starts {Describe(start)}. Mark attendance once it is under way."
            : null;
    }

    /// <summary>A check-in time as given: not still to come, and on one of the event's days.</summary>
    public static string? RefuseCheckIn(CompanyEvent e, DateTime checkIn, DateTime nowUtc)
    {
        // A few minutes' grace for a clock that runs ahead of the server's.
        if (checkIn > nowUtc.AddMinutes(5))
            return $"The check-in time ({Describe(checkIn)}) has not come yet. Give the time they arrived.";
        if (checkIn.Date < e.StartDate.Date || checkIn.Date > e.EndDate.Date)
            return $"The check-in time ({Describe(checkIn)}) is not on one of {e.EventName}'s days. Give the time they arrived.";
        return null;
    }

    /// <summary>
    /// Whether a person can be checked out: checked in, not checked out already, and not with a check-in
    /// still to come. Answers the sentence to refuse with, or null.
    /// </summary>
    public static string? RefuseCheckOut(string name, EventAttendance a, DateTime nowUtc)
    {
        if (!a.Attended || a.CheckInTime is not { } checkIn)
            return $"{name} has no check-in to check out from. Mark them as attended first.";
        if (a.CheckOutTime is { } done)
            return $"{name} was already checked out, {Describe(done)}.";
        return checkIn > nowUtc
            ? $"{name}'s check-in is recorded for {Describe(checkIn)}, which has not come yet. Correct the check-in first."
            : null;
    }

    /// <summary>The statuses a task may be given. Overdue is not one: it is worked out from the due date.</summary>
    public static bool IsSettableTaskStatus(EventTaskStatus status) =>
        status is EventTaskStatus.NotStarted or EventTaskStatus.InProgress
            or EventTaskStatus.Completed or EventTaskStatus.Cancelled;

    /// <summary>Late: due before today and still open. Worked out on every read, never stored (F-12).</summary>
    /// <remarks>
    /// ⚠ Nothing set the stored Overdue status, and the repository's overdue query had no caller and read the
    /// server's local date.
    /// </remarks>
    public static bool IsOverdue(EventTaskStatus status, DateTime? dueDate, DateTime nowUtc) =>
        dueDate is { } due && due.Date < nowUtc.Date
        && status is not (EventTaskStatus.Completed or EventTaskStatus.Cancelled);

    /// <summary>Trimmed, or null when blank.</summary>
    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
