using System.Text;
using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR;

/// <summary>What a calendar file asks of the mail client (RFC 5546): add or update the entry, or take it away.</summary>
public enum HrCalendarMethod
{
    Request,
    Cancel,
}

/// <summary>One person on a calendar file — its organiser, or the one attendee it is addressed to.</summary>
public sealed record HrCalendarPerson(string Email, string? Name = null);

/// <summary>
/// One entry for <see cref="HrCalendarFile.Build"/>: a company event or an interview, as one recipient's mail client
/// should hold it.
/// </summary>
public sealed class HrCalendarEntry
{
    /// <summary>
    /// The same for every file about the same thing, for ever — the event's or the interview's id — so a client
    /// replaces the entry it holds instead of adding a second one.
    /// </summary>
    public required string Uid { get; init; }

    /// <summary>Raised on each change; with an equal one, the newer stamp (DTSTAMP) wins (RFC 5546).</summary>
    public int Sequence { get; init; }

    public HrCalendarMethod Method { get; init; } = HrCalendarMethod.Request;

    public required string Summary { get; init; }
    public string? Description { get; init; }
    public string? Location { get; init; }
    public string? Url { get; init; }

    /// <summary>A timed entry: its start and end in UTC (TDC works on GMT, so the stored day-clock is UTC).</summary>
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }

    /// <summary>An all-day entry: its first and last day, both included. Times are ignored.</summary>
    public bool AllDay { get; init; }
    public DateOnly FirstDay { get; init; }
    public DateOnly LastDay { get; init; }

    /// <summary>Whom replies go to. Without one a client can show the entry but cannot answer it.</summary>
    public HrCalendarPerson? Organizer { get; init; }

    /// <summary>The one person this file is addressed to — never the whole guest list, which is nobody else's.</summary>
    public HrCalendarPerson? Attendee { get; init; }
    public bool AttendeeRequired { get; init; } = true;

    /// <summary>Whether the client should offer Accept / Decline.</summary>
    public bool RsvpRequested { get; init; }
}

/// <summary>
/// Builds the calendar file (RFC 5545 / 5546) that HR's emails carry — company events (lane 2e-3, D-14) and interviews,
/// one builder for both.
/// </summary>
/// <remarks>
/// <para><b>Why one builder.</b> The interview invite was built by hand inside <c>JobInterviewService</c> with a
/// random UID on every email: a rescheduled interview arrived as a second calendar entry beside the first, and there
/// was no way to take one away. Here the UID is stable, the SEQUENCE is the caller's, and CANCEL exists.</para>
///
/// <para><b>Sent as an attachment</b>, typed <c>text/calendar; method=…</c>: the platform's mailer sends attachments
/// only, with no inline calendar part. Gmail and Outlook both offer the entry from it.</para>
///
/// <para>Lines end CRLF and are folded at 75 octets, and text is escaped, as RFC 5545 requires — a long description
/// or an organiser's name with a comma otherwise breaks some clients.</para>
/// </remarks>
public static class HrCalendarFile
{
    public static EmailAttachmentDto Build(HrCalendarEntry e, string fileName)
    {
        var method = e.Method == HrCalendarMethod.Cancel ? "CANCEL" : "REQUEST";
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Rhema ERP//HR//EN",
            "CALSCALE:GREGORIAN",
            $"METHOD:{method}",
            "BEGIN:VEVENT",
            $"UID:{e.Uid}",
            $"DTSTAMP:{Utc(DateTime.UtcNow)}",
            $"SEQUENCE:{Math.Max(0, e.Sequence)}",
        };

        if (e.AllDay)
        {
            var last = e.LastDay < e.FirstDay ? e.FirstDay : e.LastDay;
            lines.Add($"DTSTART;VALUE=DATE:{e.FirstDay:yyyyMMdd}");
            // DTEND is exclusive: the day after the last.
            lines.Add($"DTEND;VALUE=DATE:{last.AddDays(1):yyyyMMdd}");
        }
        else
        {
            var end = e.EndUtc <= e.StartUtc ? e.StartUtc.AddHours(1) : e.EndUtc;
            lines.Add($"DTSTART:{Utc(e.StartUtc)}");
            lines.Add($"DTEND:{Utc(end)}");
        }

        lines.Add($"SUMMARY:{Text(e.Summary)}");
        if (!string.IsNullOrWhiteSpace(e.Description)) lines.Add($"DESCRIPTION:{Text(e.Description)}");
        if (!string.IsNullOrWhiteSpace(e.Location)) lines.Add($"LOCATION:{Text(e.Location)}");
        if (!string.IsNullOrWhiteSpace(e.Url) && Uri.TryCreate(e.Url.Trim(), UriKind.Absolute, out var url))
            lines.Add($"URL:{url.AbsoluteUri}");
        if (e.Organizer is { } organizer && !string.IsNullOrWhiteSpace(organizer.Email))
            lines.Add($"ORGANIZER{Cn(organizer.Name)}:mailto:{organizer.Email.Trim()}");
        if (e.Attendee is { } attendee && !string.IsNullOrWhiteSpace(attendee.Email))
        {
            var role = e.AttendeeRequired ? "REQ-PARTICIPANT" : "OPT-PARTICIPANT";
            var answer = e.Method == HrCalendarMethod.Request
                ? $";PARTSTAT=NEEDS-ACTION;RSVP={(e.RsvpRequested ? "TRUE" : "FALSE")}"
                : string.Empty;
            lines.Add($"ATTENDEE{Cn(attendee.Name)};ROLE={role}{answer}:mailto:{attendee.Email.Trim()}");
        }
        lines.Add(e.Method == HrCalendarMethod.Cancel ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
        lines.Add("TRANSP:OPAQUE");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        var body = new StringBuilder();
        foreach (var line in lines) Fold(body, line);

        return new EmailAttachmentDto
        {
            FileName = fileName,
            Content = Encoding.UTF8.GetBytes(body.ToString()),
            ContentType = $"text/calendar; method={method}; charset=UTF-8",
        };
    }

    private static string Utc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>TEXT, escaped (RFC 5545 § 3.3.11).</summary>
    private static string Text(string s) => s.Trim()
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

    /// <summary>A CN parameter, quoted, with nothing in it that would end the quote or the line.</summary>
    private static string Cn(string? name)
    {
        var clean = string.IsNullOrWhiteSpace(name)
            ? null
            : name.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ").Trim();
        return clean is null ? string.Empty : $";CN=\"{clean}\"";
    }

    /// <summary>Folds a content line at 75 octets, never inside a UTF-8 character (RFC 5545 § 3.1).</summary>
    private static void Fold(StringBuilder body, string line)
    {
        var octets = 0;
        var first = true;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > (first ? 75 : 74))
            {
                body.Append("\r\n ");
                octets = 0;
                first = false;
            }
            body.Append(rune.ToString());
            octets += size;
        }
        body.Append("\r\n");
    }
}
