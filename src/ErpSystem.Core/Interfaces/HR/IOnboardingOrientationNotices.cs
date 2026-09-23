using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Orientation &amp; onboarding lifecycle notices (round 4, lane K-b): an enrolment, a session placed,
/// moved, put off or called off, a completion, a certificate.
/// </summary>
/// <remarks>
/// <para><b>Every method STAGES, none saves.</b> The in-app notice — and its queued email — is added
/// to the caller's unit of work, and the caller's own save commits it with the event. So a notice
/// exists exactly when the thing it reports does: an enrolment that fails to save leaves no notice
/// about it, and one that saves cannot lose its notice.</para>
///
/// <para><b>Never throws.</b> A notice that cannot be composed is logged and skipped; the event it
/// reports goes ahead. Telling somebody is never a reason to refuse the thing itself.</para>
/// </remarks>
public interface IOnboardingOrientationNotices
{
    /// <summary>
    /// An enrolment was made — by HR, by an audience rule, or as a recurring programme's next cycle.
    /// Silent when the programme's "Send reminders" is off.
    /// </summary>
    /// <param name="reason">Why, in words ("Enrolled by the rule …"); null to derive it from the
    /// enrolment's source.</param>
    Task EnrolledAsync(
        EmployeeOrientation enrolment, OrientationNoticeProgramme programme, OrientationSession? session, string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>An existing enrolment was put on a session, or moved to another.</summary>
    Task PlacedOnSessionAsync(
        EmployeeOrientation enrolment, OrientationSession session, CancellationToken cancellationToken = default);

    /// <summary>Employees facilitating a live session are told of it.</summary>
    Task FacilitatorsScheduledAsync(
        OrientationSession session, IReadOnlyCollection<Guid> facilitatorEmployeeIds, CancellationToken cancellationToken = default);

    /// <summary>A live session's date, time, place or link changed; <paramref name="before"/> is how it stood.</summary>
    Task SessionRescheduledAsync(
        OrientationSession session, OrientationSessionSnapshot before, CancellationToken cancellationToken = default);

    /// <summary>A live session was postponed or cancelled.</summary>
    Task SessionCalledOffAsync(
        OrientationSession session, OrientationSessionStatus newStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// An enrolment completed for the first time; <paramref name="certificate"/> is the one issued with
    /// it, if the programme issues one.
    /// </summary>
    Task CompletedAsync(
        EmployeeOrientation enrolment, OrientationProgram programme, OrientationCertificate? certificate,
        CancellationToken cancellationToken = default);

    /// <summary>HR issued (or reissued) a certificate by hand.</summary>
    Task CertificateIssuedAsync(
        EmployeeOrientation enrolment, OrientationProgram programme, OrientationCertificate certificate, bool reissue,
        CancellationToken cancellationToken = default);

    // ── Onboarding (lane K-b2) ────────────────────────────────────────────────

    /// <summary>
    /// A plan was made: the new hire is welcomed, and its coordinator and buddy are told. Pass the
    /// tasks it was made with, which are not saved yet.
    /// </summary>
    Task PlanAssignedAsync(
        OnboardingPlan plan, IReadOnlyCollection<OnboardingTask> tasks, CancellationToken cancellationToken = default);

    /// <summary>A plan's coordinator or buddy changed hands: the new one is told.</summary>
    Task PlanRolesChangedAsync(
        OnboardingPlan plan, Guid? previousCoordinatorId, Guid? previousBuddyId, CancellationToken cancellationToken = default);

    /// <summary>A task was given to somebody — added for them, or passed to them.</summary>
    Task TaskAssignedAsync(OnboardingTask task, CancellationToken cancellationToken = default);

    /// <summary>Somebody marked their task done and it needs signing off: the plan's coordinator is told.</summary>
    Task TaskAwaitingSignOffAsync(
        OnboardingTask task, Guid doneByEmployeeId, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a notice needs of a programme. A record rather than the entity because the audience-rule
/// engine reads programmes as a projection, not as tracked entities.
/// </summary>
public sealed record OrientationNoticeProgramme(Guid Id, string Title, bool EnableReminders)
{
    public static OrientationNoticeProgramme From(OrientationProgram programme)
        => new(programme.Id, programme.Title, programme.EnableReminders);
}

/// <summary>A session's when and where, taken before an edit so a notice can say what it moved from.</summary>
public sealed record OrientationSessionSnapshot(
    DateTime? StartAt, DateTime? EndAt, string? Venue, string? MeetingUrl, OrientationDeliveryMode Mode,
    OrientationSessionStatus Status)
{
    public static OrientationSessionSnapshot Of(OrientationSession session) => new(
        session.ScheduledStartAt, session.ScheduledEndAt, session.VenueDescription, session.VirtualMeetingUrl,
        session.DeliveryMode, session.Status);

    /// <summary>
    /// Whether the when and where a participant acts on are unchanged. Blank and absent text are the
    /// same place: an edit that turns an empty venue into null has moved nobody.
    /// </summary>
    public bool SameWhenAndWhereAs(OrientationSessionSnapshot other) =>
        StartAt == other.StartAt
        && EndAt == other.EndAt
        && Mode == other.Mode
        && string.Equals(Norm(Venue), Norm(other.Venue), StringComparison.Ordinal)
        && string.Equals(Norm(MeetingUrl), Norm(other.MeetingUrl), StringComparison.Ordinal);

    private static string Norm(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
