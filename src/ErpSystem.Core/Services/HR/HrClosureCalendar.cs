using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// HR's one reader of business closures: which closures fall in a range, whom each covers, and
/// whether a date is a closure day for a given site or unit (company-schedule final closure, lane 1).
/// </summary>
/// <remarks>
/// <para><b>Tenant-explicit throughout.</b> The leave usage reader, the leave reminder sweep and the
/// attendance reconciler run at night with nobody signed in, and they will read closures through
/// this, so every method takes the tenant from its caller — the tenant of the record being acted
/// on, never a value from a request body.</para>
///
/// <para><b>Scope comes from the closure's type</b> (<see cref="BusinessClosureRules.ScopeOf"/>), and
/// coverage follows the audience resolver's own rules: a site matches employees assigned to that
/// exact location; a unit matches its own staff and the staff of every unit beneath it.</para>
/// </remarks>
public interface IHrClosureCalendar
{
    /// <summary>
    /// The tenant's closures with at least one occurrence in <paramref name="from"/>..<paramref name="to"/>,
    /// recurring ones included.
    /// </summary>
    Task<IReadOnlyList<BusinessClosure>> GetClosuresAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// For each closure, which of <paramref name="employeeIds"/> it covers. Every closure is a key,
    /// with an empty set when it covers none of them.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> CoverageAsync(
        Guid tenantId, IReadOnlyCollection<BusinessClosure> closures, IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a closure that is a day off covers <paramref name="date"/> for someone at this site
    /// and in this unit. With neither given, only a company-wide closure answers yes (C-37).
    /// </summary>
    /// <remarks>A partial closure keeps the day a working day, so it never answers yes here.</remarks>
    Task<bool> IsNonWorkingClosureAsync(
        Guid tenantId, DateOnly date, Guid? locationId, Guid? organizationUnitId,
        CancellationToken cancellationToken = default);
}

public sealed class HrClosureCalendar : IHrClosureCalendar
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audience;

    public HrClosureCalendar(IUnitOfWork unitOfWork, IHrAudienceResolver audience)
    {
        _unitOfWork = unitOfWork;
        _audience = audience;
    }

    public async Task<IReadOnlyList<BusinessClosure>> GetClosuresAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) return [];

        var candidates = await BusinessClosureRules
            .Candidates(_unitOfWork.Repository<BusinessClosure>().GetQueryable(), tenantId, from, to)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(c => BusinessClosureRules.OccurrencesIn(c, from, to).Any())
            .OrderBy(c => c.StartDate)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> CoverageAsync(
        Guid tenantId, IReadOnlyCollection<BusinessClosure> closures, IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        var result = closures.ToDictionary(c => c.Id, _ => (IReadOnlySet<Guid>)new HashSet<Guid>());
        var ids = employeeIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (result.Count == 0 || ids.Count == 0) return result;

        var people = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && ids.Contains(e.Id))
            .Select(e => new { e.Id, e.LocationId, e.OrganizationUnitId })
            .ToListAsync(cancellationToken);

        var scopes = closures.ToDictionary(c => c.Id, BusinessClosureRules.ScopeOf);

        // The unit tree is read once, and only when some closure is unit-wide.
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> chains = new Dictionary<Guid, IReadOnlySet<Guid>>();
        if (scopes.Values.Any(s => s.Kind == ClosureScopeKind.Unit))
            chains = await _audience.UnitAncestriesAsync(
                tenantId,
                people.Where(p => p.OrganizationUnitId is not null).Select(p => p.OrganizationUnitId!.Value),
                cancellationToken);

        foreach (var closure in closures)
        {
            var scope = scopes[closure.Id];
            result[closure.Id] = people
                .Where(p => scope.Kind switch
                {
                    ClosureScopeKind.Company => true,
                    ClosureScopeKind.Site => scope.TargetId is { } site && p.LocationId == site,
                    ClosureScopeKind.Unit => scope.TargetId is { } unit
                                             && p.OrganizationUnitId is { } own
                                             && chains.TryGetValue(own, out var chain)
                                             && chain.Contains(unit),
                    _ => false,
                })
                .Select(p => p.Id)
                .ToHashSet();
        }

        return result;
    }

    public async Task<bool> IsNonWorkingClosureAsync(
        Guid tenantId, DateOnly date, Guid? locationId, Guid? organizationUnitId,
        CancellationToken cancellationToken = default)
    {
        var closures = (await GetClosuresAsync(tenantId, date, date, cancellationToken))
            .Where(BusinessClosureRules.IsNonWorking)
            .Select(c => BusinessClosureRules.ScopeOf(c))
            .ToList();
        if (closures.Count == 0) return false;
        if (closures.Any(s => s.Kind == ClosureScopeKind.Company)) return true;

        if (locationId is { } site && closures.Any(s => s.Kind == ClosureScopeKind.Site && s.TargetId == site))
            return true;

        if (organizationUnitId is { } unit && closures.Any(s => s.Kind == ClosureScopeKind.Unit))
        {
            var chain = (await _audience.UnitAncestriesAsync(tenantId, [unit], cancellationToken))
                .GetValueOrDefault(unit) ?? new HashSet<Guid>();
            return closures.Any(s => s.Kind == ClosureScopeKind.Unit && s.TargetId is { } target && chain.Contains(target));
        }

        return false;
    }
}
