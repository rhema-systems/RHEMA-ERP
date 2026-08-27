using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Expands audience rules into employees. See <see cref="IHrAudienceResolver"/> for why this is
/// shared rather than a third private copy.
/// </summary>
public class HrAudienceResolver : IHrAudienceResolver
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public HrAudienceResolver(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(
        IEnumerable<HrAudienceRule> rules, CancellationToken cancellationToken = default)
    {
        var all = rules?.ToList() ?? [];

        // No rules reaches NOBODY, on purpose. Defaulting an empty audience to "everyone" would
        // turn a forgotten step into a tenant-wide broadcast.
        if (all.Count == 0) return [];

        var tenantId = GetTenantId();

        var included = new HashSet<Guid>();
        foreach (var rule in all.Where(r => !r.IsExclusion))
            included.UnionWith(await MatchAsync(tenantId, rule, cancellationToken));

        foreach (var rule in all.Where(r => r.IsExclusion))
            included.ExceptWith(await MatchAsync(tenantId, rule, cancellationToken));

        return included;
    }

    public async Task<int> CountAsync(
        IEnumerable<HrAudienceRule> rules, CancellationToken cancellationToken = default)
        => (await ResolveAsync(rules, cancellationToken)).Count;

    public async Task<bool> IncludesAsync(
        IEnumerable<HrAudienceRule> rules, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var all = rules?.ToList() ?? [];
        if (all.Count == 0) return false;

        var tenantId = GetTenantId();

        var employee = await Employees(tenantId)
            .Where(e => e.Id == employeeId)
            .Select(e => new
            {
                e.Id,
                e.OrganizationUnitId,
                e.PositionId,
                e.OrganizationLevelId,
                e.LocationId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Not an active employee of this tenant — no rule can reach them.
        if (employee is null) return false;

        // Unit membership is the one axis that needs the tree walked, so it is resolved once
        // and only if some rule actually asks about a unit.
        HashSet<Guid>? unitAndAncestors = null;
        async Task<HashSet<Guid>> UnitChainAsync()
        {
            if (unitAndAncestors is not null) return unitAndAncestors;
            unitAndAncestors = employee.OrganizationUnitId is { } unitId
                ? await AncestorsOfAsync(tenantId, unitId, cancellationToken)
                : [];
            return unitAndAncestors;
        }

        async Task<bool> MatchesAsync(HrAudienceRule rule) => rule.TargetType switch
        {
            HrAudienceTargetType.AllEmployees => true,
            HrAudienceTargetType.Employee => rule.TargetId == employee.Id,
            HrAudienceTargetType.Position => rule.TargetId is { } p && employee.PositionId == p,
            HrAudienceTargetType.OrganizationLevel =>
                rule.TargetId is { } l && employee.OrganizationLevelId == l,
            HrAudienceTargetType.Location => rule.TargetId is { } loc && employee.LocationId == loc,
            // Targeting a unit reaches everyone beneath it, so the employee matches if the
            // target is their unit or any ancestor of it.
            HrAudienceTargetType.OrganizationUnit =>
                rule.TargetId is { } u && (await UnitChainAsync()).Contains(u),
            _ => false,
        };

        var included = false;
        foreach (var rule in all.Where(r => !r.IsExclusion))
        {
            if (await MatchesAsync(rule)) { included = true; break; }
        }
        if (!included) return false;

        foreach (var rule in all.Where(r => r.IsExclusion))
        {
            if (await MatchesAsync(rule)) return false;
        }
        return true;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The population every rule draws from: active, non-deleted employees of this tenant. A
    /// leaver should not receive next week's staff notice.
    /// </summary>
    private IQueryable<Employee> Employees(Guid tenantId) =>
        _unitOfWork.Repository<Employee>()
            .GetQueryable()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive);

    private async Task<IEnumerable<Guid>> MatchAsync(
        Guid tenantId, HrAudienceRule rule, CancellationToken ct)
    {
        var employees = Employees(tenantId);

        switch (rule.TargetType)
        {
            case HrAudienceTargetType.AllEmployees:
                return await employees.Select(e => e.Id).ToListAsync(ct);

            case HrAudienceTargetType.Employee:
                return rule.TargetId is { } empId
                    ? await employees.Where(e => e.Id == empId).Select(e => e.Id).ToListAsync(ct)
                    : [];

            case HrAudienceTargetType.Position:
                return rule.TargetId is { } posId
                    ? await employees.Where(e => e.PositionId == posId).Select(e => e.Id).ToListAsync(ct)
                    : [];

            case HrAudienceTargetType.OrganizationLevel:
                return rule.TargetId is { } levelId
                    ? await employees.Where(e => e.OrganizationLevelId == levelId).Select(e => e.Id).ToListAsync(ct)
                    : [];

            case HrAudienceTargetType.Location:
                return rule.TargetId is { } locId
                    ? await employees.Where(e => e.LocationId == locId).Select(e => e.Id).ToListAsync(ct)
                    : [];

            case HrAudienceTargetType.OrganizationUnit:
                if (rule.TargetId is not { } unitId) return [];
                var unitIds = await DescendantsOfAsync(tenantId, unitId, ct);
                return await employees
                    .Where(e => e.OrganizationUnitId != null && unitIds.Contains(e.OrganizationUnitId.Value))
                    .Select(e => e.Id)
                    .ToListAsync(ct);

            default:
                return [];
        }
    }

    /// <summary>
    /// A unit and every unit beneath it. Loads the tenant's unit edges ONCE and walks them in
    /// memory rather than issuing a query per node the way the appraisal cycle's private copy
    /// does — an org tree is small, and the recursive version is one round trip per unit.
    /// </summary>
    private async Task<HashSet<Guid>> DescendantsOfAsync(Guid tenantId, Guid rootId, CancellationToken ct)
    {
        var edges = await UnitEdgesAsync(tenantId, ct);

        var result = new HashSet<Guid> { rootId };
        var frontier = new Queue<Guid>();
        frontier.Enqueue(rootId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var (id, parentId) in edges)
            {
                // The Add guards against a cycle in the data taking the walk round forever.
                if (parentId == current && result.Add(id)) frontier.Enqueue(id);
            }
        }
        return result;
    }

    /// <summary>A unit and every unit above it — the mirror walk, for "is this employee in?".</summary>
    private async Task<HashSet<Guid>> AncestorsOfAsync(Guid tenantId, Guid unitId, CancellationToken ct)
    {
        var edges = await UnitEdgesAsync(tenantId, ct);
        var parents = edges.ToDictionary(e => e.Id, e => e.ParentUnitId);

        var chain = new HashSet<Guid> { unitId };
        var current = unitId;
        while (parents.TryGetValue(current, out var parent) && parent is { } parentId)
        {
            if (!chain.Add(parentId)) break; // a cycle in the data
            current = parentId;
        }
        return chain;
    }

    private async Task<List<(Guid Id, Guid? ParentUnitId)>> UnitEdgesAsync(Guid tenantId, CancellationToken ct)
        => (await _unitOfWork.Repository<OrganizationUnit>()
                .GetQueryable()
                .Where(u => u.TenantId == tenantId && !u.IsDeleted)
                .Select(u => new { u.Id, u.ParentUnitId })
                .ToListAsync(ct))
            .Select(u => (u.Id, u.ParentUnitId))
            .ToList();
}
