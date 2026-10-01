using ErpSystem.Shared;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who decides a salary review or employment action proposal (performance closure F3, D-12, D-94, D-104): the Managing
/// Director, never the person who submitted it, and never the employee it is about. Run in the service on both paths —
/// the engine and the fallback — before the engine is asked anything.
/// </summary>
/// <remarks>
/// <para>The seeded routes named HR beside the Managing Director, and on the fallback the approve permission is HR's —
/// so an HR officer approved the increase they had just put a figure on and submitted. HR prepares a proposal; it does
/// not decide one.</para>
/// <para>⚠ <b>Neither SuperAdmin nor TenantAdmin is a decider</b> (D-104). A pay or employment decision is the
/// appointing authority's; a system administrator approving one is the segregation-of-duties weakness the rule exists to
/// close — administrators run the system, they do not make its business decisions. SeparationService holds the same for
/// FR-HR-092. When the Managing Director is away, the answer is an acting appointment holding the role, not an
/// administrator's override.</para>
/// </remarks>
public static class ProposalDecisionRules
{
    /// <summary>The Managing Director, under either role spelling.</summary>
    public static bool IsDecider(IEnumerable<string>? roles)
        => roles?.Any(r =>
               string.Equals(r, Constants.Roles.ManagingDirector, StringComparison.OrdinalIgnoreCase)
               || string.Equals(r, Constants.Roles.TdcManagingDirector, StringComparison.OrdinalIgnoreCase)) ?? false;

    /// <summary>
    /// Refuses anyone but a disinterested decider. <paramref name="what"/> names the record, e.g.
    /// <c>"salary review proposal"</c>.
    /// </summary>
    public static void EnsureMayDecide(
        IEnumerable<string>? roles, Guid? actorEmployeeId, Guid? submittedById, Guid subjectEmployeeId, string what)
    {
        if (!IsDecider(roles))
            throw new UnauthorizedAccessException($"A {what} is decided by the Managing Director.");

        if (actorEmployeeId is not { } actor || actor == Guid.Empty) return;

        if (actor == subjectEmployeeId)
            throw new UnauthorizedAccessException($"This {what} is about you; someone else decides it.");

        if (actor == submittedById)
            throw new UnauthorizedAccessException($"You submitted this {what}; someone else decides it.");
    }

    /// <summary>
    /// With no definition published there is no engine to keep recall to the requester (F9): only the person who
    /// submitted it pulls a proposal back. A proposal submitted before submitters were recorded names nobody.
    /// </summary>
    public static void EnsureMayRecall(Guid? actorEmployeeId, Guid? submittedById, string what)
    {
        if (submittedById is not { } submitter) return;
        if (actorEmployeeId != submitter)
            throw new UnauthorizedAccessException($"Only the person who submitted this {what} can recall it.");
    }
}
