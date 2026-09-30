using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Who one cycle's targets reach: the staff each active target covers, and the covered staff its
/// exclusions leave out, each with the exclusion's reason. Built by <see cref="AppraisalCycleScope"/>.
/// </summary>
/// <remarks>
/// The sets are concrete <see cref="HashSet{T}"/>s because they feed <c>Contains</c> in EF queries, which
/// translates for a set type it knows. Treat them as read-only.
/// </remarks>
public sealed class AppraisalCycleScopeResolution
{
    public AppraisalCycleScopeResolution(
        Dictionary<Guid, HashSet<Guid>> byTarget, HashSet<Guid> covered, Dictionary<Guid, string> excluded)
    {
        ByTarget = byTarget;
        Covered = covered;
        Excluded = excluded;
        InScope = covered.Where(id => !excluded.ContainsKey(id)).ToHashSet();
    }

    /// <summary>The active staff each resolved target covers, by target id, before any exclusion.</summary>
    public Dictionary<Guid, HashSet<Guid>> ByTarget { get; }

    /// <summary>Everyone the targets cover, before any exclusion.</summary>
    public HashSet<Guid> Covered { get; }

    /// <summary>The covered staff an active exclusion leaves out, each with that exclusion's reason.</summary>
    public Dictionary<Guid, string> Excluded { get; }

    /// <summary>Who the cycle appraises: covered and not excluded.</summary>
    public HashSet<Guid> InScope { get; }

    /// <summary>
    /// How many of one target's staff are in scope: the target's live count. A target that was not
    /// resolved (an inactive one, which generation ignores) reaches no one.
    /// </summary>
    public int InScopeCountOf(Guid targetId) =>
        ByTarget.TryGetValue(targetId, out var staff) ? staff.Count(id => !Excluded.ContainsKey(id)) : 0;
}

/// <summary>
/// Another Open cycle of the same type and year that already covers some of the people asked about: in its scope, or
/// holding an unwithdrawn appraisal for them (<see cref="AppraisalCycleScope.FindOpenOverlapsAsync"/>).
/// </summary>
public sealed record AppraisalCycleOverlap(Guid CycleId, string CycleCode, string CycleName, HashSet<Guid> Shared);

/// <summary>
/// The one reading of who a cycle covers (performance closure E-c). Generation, the open's overlap
/// check, the in-scope list (the open notice, the deadline reminders, <c>GET {id}/employees</c>), the
/// progress page's excluded count, each target's live count and the coverage preview all resolve
/// through it. There were three copies: the in-scope list's also took in everyone holding a post that
/// any active template was scoped to, tenant-wide, read inactive targets, counted leavers, and read a
/// level target through the units at that level rather than the employee's own level — so the open
/// notice and the reminders could reach people generation would never appraise.
/// </summary>
public static class AppraisalCycleScope
{
    /// <summary>
    /// A cycle's active targets with their exclusions — the only targets generation reads. Oldest first,
    /// so when two exclusions leave out the same person, the older one's reason is the one reported.
    /// </summary>
    public static Task<List<AppraisalCycleTarget>> LoadActiveTargetsAsync(
        IQueryable<AppraisalCycleTarget> targets, Guid tenantId, Guid cycleId, CancellationToken cancellationToken)
        => targets
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId && t.IsActive && !t.IsDeleted)
            .Include(t => t.Exclusions)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

    /// <summary>A cycle's scope, from its active targets.</summary>
    public static async Task<AppraisalCycleScopeResolution> ResolveCycleAsync(
        IQueryable<AppraisalCycleTarget> targets,
        IQueryable<Employee> employees,
        IQueryable<OrganizationUnit> units,
        Guid tenantId,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var active = await LoadActiveTargetsAsync(targets, tenantId, cycleId, cancellationToken);
        return await ResolveAsync(active, employees, units, tenantId, cancellationToken);
    }

    /// <summary>
    /// The scope of the given targets (the caller passes the active ones, exclusions loaded): each target's
    /// active staff — everyone in the position; in the unit and every unit beneath it; at the level — and
    /// then the exclusions, applied across all of them.
    /// </summary>
    public static async Task<AppraisalCycleScopeResolution> ResolveAsync(
        IReadOnlyCollection<AppraisalCycleTarget> targets,
        IQueryable<Employee> employees,
        IQueryable<OrganizationUnit> units,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var staff = employees.Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive);
        var byTarget = new Dictionary<Guid, HashSet<Guid>>();
        var covered = new HashSet<Guid>();

        foreach (var target in targets)
        {
            var ids = new List<Guid>();
            switch (target.TargetType)
            {
                case AppraisalTargetType.Position when target.PositionId is { } positionId:
                    ids = await staff.Where(e => e.PositionId == positionId)
                        .Select(e => e.Id).ToListAsync(cancellationToken);
                    break;

                case AppraisalTargetType.OrganizationUnit when target.OrganizationUnitId is { } unitId:
                    var unitIds = (await UnitAndBelowAsync(units, tenantId, unitId, cancellationToken)).ToList();
                    ids = await staff.Where(e => e.OrganizationUnitId.HasValue && unitIds.Contains(e.OrganizationUnitId.Value))
                        .Select(e => e.Id).ToListAsync(cancellationToken);
                    break;

                case AppraisalTargetType.OrganizationLevel when target.OrganizationLevelId is { } levelId:
                    ids = await staff.Where(e => e.OrganizationLevelId == levelId)
                        .Select(e => e.Id).ToListAsync(cancellationToken);
                    break;
            }

            var set = ids.ToHashSet();
            byTarget[target.Id] = set;
            covered.UnionWith(set);
        }

        var exclusions = targets.SelectMany(t => t.Exclusions)
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToList();
        var excluded = await ExcludedAsync(exclusions, covered, employees, units, tenantId, cancellationToken);

        return new AppraisalCycleScopeResolution(byTarget, covered, excluded);
    }

    /// <summary>
    /// The other Open cycles of <paramref name="cycle"/>'s type and year that already cover any of
    /// <paramref name="employeeIds"/> — in their scope, or holding an unwithdrawn appraisal for them (performance closure
    /// D-60). A person is appraised by one running cycle of a type and year: the open refuses a cycle that overlaps one,
    /// and generation refuses to create a second appraisal. The appraisal half catches what the scope cannot see — a
    /// person who moved to another post after the other cycle generated theirs.
    /// </summary>
    public static async Task<List<AppraisalCycleOverlap>> FindOpenOverlapsAsync(
        AppraisalCycle cycle,
        IReadOnlyCollection<Guid> employeeIds,
        IQueryable<AppraisalCycle> cycles,
        IQueryable<AppraisalCycleTarget> targets,
        IQueryable<Employee> employees,
        IQueryable<OrganizationUnit> units,
        IQueryable<PerformanceAppraisal> appraisals,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var overlaps = new List<AppraisalCycleOverlap>();
        if (employeeIds.Count == 0) return overlaps;
        var asked = employeeIds.ToHashSet();

        var siblings = await cycles
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                     && c.Id != cycle.Id
                     && c.AppraisalType == cycle.AppraisalType
                     && c.Year == cycle.Year
                     && c.Status == AppraisalCycleStatus.Open)
            .OrderBy(c => c.CycleName).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.CycleCode, c.CycleName })
            .ToListAsync(cancellationToken);
        if (siblings.Count == 0) return overlaps;

        var siblingIds = siblings.Select(s => s.Id).ToList();
        var held = (await appraisals
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId
                         && siblingIds.Contains(a.AppraisalCycleId)
                         && asked.Contains(a.EmployeeId)
                         && a.Status != AppraisalStatus.Withdrawn)
                .Select(a => new { a.AppraisalCycleId, a.EmployeeId })
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.AppraisalCycleId, a => a.EmployeeId);

        foreach (var sibling in siblings)
        {
            var scope = await ResolveCycleAsync(targets, employees, units, tenantId, sibling.Id, cancellationToken);
            var shared = asked.Where(scope.InScope.Contains).ToHashSet();
            shared.UnionWith(held[sibling.Id]);
            if (shared.Count > 0)
                overlaps.Add(new AppraisalCycleOverlap(sibling.Id, sibling.CycleCode, sibling.CycleName, shared));
        }

        return overlaps;
    }

    /// <summary>
    /// The covered staff the exclusions leave out. Only the most specific scope an exclusion names counts:
    /// the exclusion dialog cascades level → unit → position → employee, so a one-person exclusion carries
    /// the position (and perhaps the unit and level) it was picked under, as context — an employee, else a
    /// position, else a unit and the units beneath it, else a level.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> ExcludedAsync(
        List<AppraisalCycleTargetExclusion> exclusions,
        HashSet<Guid> covered,
        IQueryable<Employee> employees,
        IQueryable<OrganizationUnit> units,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var excluded = new Dictionary<Guid, string>();
        if (exclusions.Count == 0 || covered.Count == 0) return excluded;

        void Take(Guid employeeId, string reason)
        {
            if (covered.Contains(employeeId)) excluded.TryAdd(employeeId, reason);
        }

        var people = employees.Where(e => e.TenantId == tenantId && !e.IsDeleted);

        foreach (var x in exclusions.Where(x => x.EmployeeId.HasValue))
            Take(x.EmployeeId!.Value, x.Reason);

        var byPosition = exclusions.Where(x => x.PositionId.HasValue && !x.EmployeeId.HasValue).ToList();
        if (byPosition.Count > 0)
        {
            var positionIds = byPosition.Select(x => x.PositionId!.Value).Distinct().ToList();
            var holders = await people.Where(e => positionIds.Contains(e.PositionId))
                .Select(e => new { e.Id, e.PositionId }).ToListAsync(cancellationToken);
            foreach (var holder in holders)
                Take(holder.Id, byPosition.First(x => x.PositionId == holder.PositionId).Reason);
        }

        var byUnit = exclusions
            .Where(x => x.OrganizationUnitId.HasValue && !x.EmployeeId.HasValue && !x.PositionId.HasValue)
            .ToList();
        if (byUnit.Count > 0)
        {
            var reasonByUnit = new Dictionary<Guid, string>();
            foreach (var x in byUnit)
                foreach (var unitId in await UnitAndBelowAsync(units, tenantId, x.OrganizationUnitId!.Value, cancellationToken))
                    reasonByUnit.TryAdd(unitId, x.Reason);

            var unitIds = reasonByUnit.Keys.ToList();
            var members = await people.Where(e => e.OrganizationUnitId.HasValue && unitIds.Contains(e.OrganizationUnitId.Value))
                .Select(e => new { e.Id, UnitId = e.OrganizationUnitId!.Value }).ToListAsync(cancellationToken);
            foreach (var member in members)
                Take(member.Id, reasonByUnit[member.UnitId]);
        }

        var byLevel = exclusions
            .Where(x => x.OrganizationLevelId.HasValue && !x.EmployeeId.HasValue && !x.PositionId.HasValue && !x.OrganizationUnitId.HasValue)
            .ToList();
        if (byLevel.Count > 0)
        {
            var levelIds = byLevel.Select(x => x.OrganizationLevelId!.Value).Distinct().ToList();
            var atLevel = await people.Where(e => e.OrganizationLevelId.HasValue && levelIds.Contains(e.OrganizationLevelId.Value))
                .Select(e => new { e.Id, LevelId = e.OrganizationLevelId!.Value }).ToListAsync(cancellationToken);
            foreach (var person in atLevel)
                Take(person.Id, byLevel.First(x => x.OrganizationLevelId == person.LevelId).Reason);
        }

        return excluded;
    }

    /// <summary>A unit and every unit beneath it, a tier of the tree per query.</summary>
    private static async Task<HashSet<Guid>> UnitAndBelowAsync(
        IQueryable<OrganizationUnit> units, Guid tenantId, Guid unitId, CancellationToken cancellationToken)
    {
        var found = new HashSet<Guid> { unitId };
        var tier = new List<Guid> { unitId };
        while (tier.Count > 0)
        {
            var parents = tier;
            var children = await units
                .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.ParentUnitId.HasValue && parents.Contains(u.ParentUnitId.Value))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            tier = children.Where(found.Add).ToList();
        }
        return found;
    }
}
