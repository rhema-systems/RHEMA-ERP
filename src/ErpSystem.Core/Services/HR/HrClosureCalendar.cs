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

    /// <summary>
    /// For each employee, every date in <paramref name="from"/>..<paramref name="to"/> that a closure
    /// covering them makes a day off — company-wide, their site's, their unit's. Every employee asked
    /// about is a key, with an empty set when nothing covers them.
    /// </summary>
    /// <remarks>
    /// <para>Leave's per-employee overlay (lane 1b, F-28). The company-wide days are in it too, though
    /// <see cref="IHrWorkingDayCalculator.GetHolidayDatesAsync"/> already holds them: a caller unions
    /// the two, and a date in both is one date.</para>
    ///
    /// <para>A partial closure is not in it: its day is still worked.</para>
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<DateOnly>>> GetClosureDatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// For each employee, every closure day in <paramref name="from"/>..<paramref name="to"/> with the
    /// closure behind it and whether staff are paid for it — the read payroll is pointed at
    /// (company-schedule final closure, lane 1d: D-15c, F-51).
    /// </summary>
    /// <remarks>
    /// <para>HR records <c>IsPaidClosure</c> ("Staff are paid") and applies none of it: what an unpaid
    /// closure day takes off a payslip is payroll's, as an unpaid leave day is
    /// (<c>docs/HR/integration/handoffs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md</c> § 2.1, § 3).
    /// No payroll code calls this yet — it is the one place to read from, not a push.</para>
    ///
    /// <para>A partial closure is a working day; it is included only when
    /// <paramref name="includePartial"/> asks for it, flagged as worked. Every employee asked about is a
    /// key; a day covered by two closures appears once per closure.</para>
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<HrClosureDay>>> GetClosureDaysAsync(
        Guid tenantId, IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        bool includePartial = false, CancellationToken cancellationToken = default);
}

/// <summary>One closure day for one employee: the date, the closure, and whether it is paid and worked.</summary>
public sealed record HrClosureDay(DateOnly Date, Guid ClosureId, string Title, bool IsPaid, bool IsWorkingDay);

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

    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<DateOnly>>> GetClosureDatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var sets = employeeIds.Distinct().ToDictionary(id => id, _ => new HashSet<DateOnly>());
        if (sets.Count > 0 && to >= from)
        {
            var closures = (await GetClosuresAsync(tenantId, from, to, cancellationToken))
                .Where(BusinessClosureRules.IsNonWorking)
                .ToList();
            if (closures.Count > 0)
            {
                var coverage = await CoverageAsync(tenantId, closures, sets.Keys.ToList(), cancellationToken);
                foreach (var closure in closures)
                {
                    var covered = coverage[closure.Id];
                    if (covered.Count == 0) continue;
                    var dates = BusinessClosureRules.DatesIn(closure, from, to).ToList();
                    foreach (var employeeId in covered)
                        sets[employeeId].UnionWith(dates);
                }
            }
        }
        return sets.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<DateOnly>)kv.Value);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<HrClosureDay>>> GetClosureDaysAsync(
        Guid tenantId, IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        bool includePartial = false, CancellationToken cancellationToken = default)
    {
        var days = employeeIds.Distinct().ToDictionary(id => id, _ => new List<HrClosureDay>());
        if (days.Count > 0 && to >= from)
        {
            var closures = (await GetClosuresAsync(tenantId, from, to, cancellationToken))
                .Where(c => includePartial || BusinessClosureRules.IsNonWorking(c))
                .ToList();
            if (closures.Count > 0)
            {
                var coverage = await CoverageAsync(tenantId, closures, days.Keys.ToList(), cancellationToken);
                foreach (var closure in closures)
                {
                    var covered = coverage[closure.Id];
                    if (covered.Count == 0) continue;
                    var worked = !BusinessClosureRules.IsNonWorking(closure);
                    var dates = BusinessClosureRules.DatesIn(closure, from, to).ToList();
                    foreach (var employeeId in covered)
                        days[employeeId].AddRange(dates.Select(d =>
                            new HrClosureDay(d, closure.Id, closure.Title, closure.IsPaidClosure, worked)));
                }
            }
        }
        return days.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<HrClosureDay>)kv.Value.OrderBy(d => d.Date).ToList());
    }
}
