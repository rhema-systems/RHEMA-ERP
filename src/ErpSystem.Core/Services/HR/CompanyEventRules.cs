using System.Globalization;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Enums;

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
}
