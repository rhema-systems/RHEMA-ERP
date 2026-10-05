using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>One audience rule, independent of whatever entity stores it.</summary>
/// <remarks>
/// A plain record rather than an interface over the entities so that announcements (slice 12c),
/// the policy library (slice 12d) and anything later can share one resolver without any of them
/// having to know about the others' tables.
/// </remarks>
public sealed record HrAudienceRule(HrAudienceTargetType TargetType, Guid? TargetId, bool IsExclusion);

/// <summary>
/// Expands audience rules into the employees they actually reach (area 25 slice 12c).
/// </summary>
/// <remarks>
/// <para><b>Why this exists as a service.</b> The only working rule-to-employee expansion in the
/// codebase is <c>AppraisalCycleService.ResolveTargetEmployeesAsync</c>, which is private, covers
/// three axes, and walks the unit tree with one query per node. <c>OrientationAudienceRule</c>
/// models the same idea and has no resolver at all — its rules are stored and never expanded.
/// Rather than write a third, this is the shared one.</para>
///
/// <para><b>Includes are unioned, then exclusions are subtracted.</b> That order is what makes
/// "everyone except the depot" expressible. An empty rule set reaches NOBODY, deliberately: a
/// broadcast with no audience is a mistake, and defaulting it to everyone would turn that
/// mistake into a tenant-wide message.</para>
/// </remarks>
public interface IHrAudienceResolver
{
    /// <summary>The employees these rules reach. Active, non-deleted, this tenant.</summary>
    Task<IReadOnlyCollection<Guid>> ResolveAsync(
        IEnumerable<HrAudienceRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many people the rules reach — for the "this will go to 412 people" line a sender
    /// should see BEFORE they publish, not after.
    /// </summary>
    Task<int> CountAsync(
        IEnumerable<HrAudienceRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// One organisation unit and every unit beneath it, in the caller's tenant.
    /// </summary>
    /// <remarks>
    /// The descendant walk is this service's, and the staff directory (slice 13a) needs it as a
    /// SQL predicate rather than as a resolved employee set: browsing a unit whose subtree holds
    /// 7,716 people should page in the database, not materialise every id first. Exposing the
    /// walk is cheaper than the alternative, which is the fourth private copy of it.
    /// </remarks>
    Task<IReadOnlyCollection<Guid>> UnitSubtreeAsync(
        Guid unitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether one employee is in the audience. Answers the portal's read without expanding the
    /// whole set for every request.
    /// </summary>
    Task<bool> IncludesAsync(
        IEnumerable<HrAudienceRule> rules, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="ResolveAsync"/> for a tenant named by the caller rather than read off the
    /// signed-in user (round 4, lane I).
    /// </summary>
    /// <remarks>
    /// A nightly sweep has no signed-in user, and neither does an employee import committing in the
    /// background — every other method here throws in both. The caller takes responsibility for
    /// the tenant being the right one: pass the tenant of the RECORD being acted on (the employee,
    /// the programme), never a value from a request body.
    /// </remarks>
    Task<IReadOnlyCollection<Guid>> ResolveForTenantAsync(
        Guid tenantId, IEnumerable<HrAudienceRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of <paramref name="employeeIds"/> the rules reach, in a named tenant (company-schedule
    /// final closure, lane 2c) — the diaries' question, "is this event for these people?", answered in
    /// the database rather than by expanding a whole-company rule.
    /// </summary>
    Task<IReadOnlySet<Guid>> IncludedAmongForTenantAsync(
        Guid tenantId, IEnumerable<HrAudienceRule> rules, IEnumerable<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A unit followed by every unit above it, nearest first — the order a "most specific match
    /// wins" rule needs (round 4, lane I4: onboarding template precedence).
    /// </summary>
    Task<IReadOnlyList<Guid>> UnitAncestryAsync(
        Guid tenantId, Guid unitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="UnitAncestryAsync"/> for many units at once, loading the unit tree once: each
    /// unit mapped to itself and every unit above it (company-schedule final closure, lane 1).
    /// </summary>
    /// <remarks>
    /// For "which of these people does a unit-wide closure cover": a team diary can span dozens of
    /// units, and the one-unit method reads the whole tree on every call.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> UnitAncestriesAsync(
        Guid tenantId, IEnumerable<Guid> unitIds, CancellationToken cancellationToken = default);
}
