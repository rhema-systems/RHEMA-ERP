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
    /// Whether one employee is in the audience. Answers the portal's read without expanding the
    /// whole set for every request.
    /// </summary>
    Task<bool> IncludesAsync(
        IEnumerable<HrAudienceRule> rules, Guid employeeId, CancellationToken cancellationToken = default);
}
