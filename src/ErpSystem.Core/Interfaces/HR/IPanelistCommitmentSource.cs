using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// One place a panelist may already be committed when an interview is being scheduled.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane D1.</b> The clash check knew about exactly three things — other
/// interviews, leave and travel — and the organisation runs on more than three. A panelist could be
/// chairing a board meeting, sitting in a room somebody else had booked, away on a training course
/// or at a site that was closed that day, and the check would say they were free.</para>
///
/// <para>One interface, N registered implementations, so adding a source is adding a class and a
/// registration rather than editing a method that already knew too much. Same shape as
/// <c>IGeoAreaConsumer</c>, and for the same reason: the thing that must not happen is a new
/// commitment type being invented somewhere else in HR and nobody remembering this exists.</para>
///
/// <para>⚠ A source that cannot speak about external associates returns nothing for them rather
/// than throwing. Externals are not employees: there is no leave, no travel and no training for
/// them, and only the sources keyed on an interview or an event participation can answer.</para>
/// </remarks>
public interface IPanelistCommitmentSource
{
    /// <summary>What this source looks at, for the log line and the diagnostic endpoint.</summary>
    string SourceName { get; }

    /// <summary>
    /// Everything this source knows that overlaps the proposed window, for the people named.
    /// </summary>
    Task<IReadOnlyList<PanelistCommitment>> GetCommitmentsAsync(
        PanelistCommitmentQuery query, CancellationToken cancellationToken = default);
}

/// <summary>What a source is being asked about.</summary>
/// <param name="EmployeeIds">Internal panelists. May be empty.</param>
/// <param name="ExternalAssociateIds">External panelists. May be empty; most sources ignore them.</param>
/// <param name="WindowStart">Inclusive start of the proposed window, as a whole date and time.</param>
/// <param name="WindowEnd">Exclusive end of the proposed window.</param>
/// <param name="ExcludeInterviewId">
/// The interview being edited, which must not be found clashing with itself.
/// </param>
/// <param name="TenantId">
/// ⚠ Passed explicitly. The DbContext is registered without a tenant, so its global filter is
/// inert and every source must scope its own read — the convention the whole HR module runs on.
/// </param>
public sealed record PanelistCommitmentQuery(
    IReadOnlyList<Guid> EmployeeIds,
    IReadOnlyList<Guid> ExternalAssociateIds,
    DateTime WindowStart,
    DateTime WindowEnd,
    Guid? ExcludeInterviewId,
    Guid TenantId)
{
    /// <summary>The date the window falls on, for the day-granular sources.</summary>
    public DateOnly Date => DateOnly.FromDateTime(WindowStart);

    /// <summary>
    /// Whether a day-granular commitment spanning these dates touches the window's day.
    /// </summary>
    /// <remarks>
    /// Leave and travel are recorded as whole days, so "does it overlap the 09:00–11:00 window?" is
    /// not a question they can answer. They answer "is this person away that day?", which is why
    /// they are <see cref="CommitmentHardness.Soft"/>: somebody on annual leave may well agree to
    /// come in for an hour, and refusing outright would be the system overruling them.
    /// </remarks>
    public bool CoversDay(DateOnly from, DateOnly to) => from <= Date && to >= Date;
}

/// <summary>One thing standing between a panelist and the proposed window.</summary>
/// <param name="SubjectId">The employee, or the external associate.</param>
/// <param name="IsExternal">True when <paramref name="SubjectId"/> is an associate, not an employee.</param>
/// <param name="Label">What a recruiter reads — "Panel interview INT-000082", "Annual leave".</param>
/// <param name="Start">
/// When it starts. For a day-granular source this is the start of that day, so the timeline still
/// renders; <paramref name="IsDayGranular"/> says not to read the time as precise.
/// </param>
/// <param name="Reference">The record's own number, where it has one, so a recruiter can go and look.</param>
public sealed record PanelistCommitment(
    Guid SubjectId,
    bool IsExternal,
    CommitmentKind Kind,
    CommitmentHardness Hardness,
    string Label,
    DateTime Start,
    DateTime End,
    bool IsDayGranular = false,
    string? Reference = null);
