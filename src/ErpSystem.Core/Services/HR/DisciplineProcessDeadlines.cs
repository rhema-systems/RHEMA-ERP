using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The statutory clocks the disciplinary process runs to, in one place.
/// </summary>
/// <remarks>
/// <para>These are single definitions on purpose. Before this, "overdue investigation" was a
/// <c>maxDays</c> argument the caller supplied and defaulted to 30 — so the queue, any report and
/// any reminder could each answer the question differently, and the spec's actual figure appeared
/// nowhere. Two definitions of overdue over the same records is how a queue and its reminder end up
/// disagreeing; the same reasoning put the movement reminder engine on the dashboard's own 5-day
/// rule rather than a fresh number.</para>
///
/// <para><b>These clocks are computed, not stored.</b> Every input already exists on the record —
/// the reported date, the notice's sent date, the investigation's start date — so a stored due date
/// would be a second copy that drifts the moment the rule changes, and would need a migration to
/// introduce. If TDC ever wants a per-case override, that is when a column earns its place.</para>
///
/// <para><b>They report, they do not block.</b> FR-HR-177 says the system shall <i>support</i>
/// issuance within 48 hours and FR-HR-178 says it shall <i>track</i> investigations to completion
/// within four weeks — neither says refuse. A breach is a historical fact about what already
/// happened, and refusing the next step cannot un-breach it; it would only stop the case being
/// dealt with at all. Area 8 built FR-HR-173's vacancy rule as a block exactly as specified and it
/// refused nearly every movement, which is the precedent this follows.</para>
/// </remarks>
public static class DisciplineProcessDeadlines
{
    /// <summary>FR-HR-177 — a formal written query is due within 48 hours of the allegation.</summary>
    public const int WrittenQueryHours = 48;

    /// <summary>FR-HR-178 — an investigation is tracked to completion within four weeks.</summary>
    public const int InvestigationDays = 28;

    /// <summary>
    /// The notification type that IS the formal written query. FR-HR-177's "written query" and the
    /// enum's <c>ShowCause</c> are the same document: the notice asking the employee to explain
    /// themselves. The enum member documents itself as exactly that.
    /// </summary>
    public const DisciplinaryNotificationType WrittenQueryType = DisciplinaryNotificationType.ShowCause;

    /// <summary>When the written query falls due for a case reported at <paramref name="reportedDate"/>.</summary>
    public static DateTime WrittenQueryDueAt(DateTime reportedDate)
        => reportedDate.AddHours(WrittenQueryHours);

    /// <summary>
    /// When an investigation started on <paramref name="startDate"/> falls due. Null when it has not
    /// started: the clock runs from the investigation, not from the allegation, so an unopened
    /// investigation is not overdue — it is in the "needs investigation" queue instead, which is a
    /// different problem with a different owner.
    /// </summary>
    public static DateTime? InvestigationDueAt(DateTime? startDate)
        => startDate?.AddDays(InvestigationDays);

    /// <summary>The cut-off an overdue-investigation query compares a start date against.</summary>
    public static DateTime InvestigationOverdueCutoff(DateTime asAt)
        => asAt.AddDays(-InvestigationDays);
}
