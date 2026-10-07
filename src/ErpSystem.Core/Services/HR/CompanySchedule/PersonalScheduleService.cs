using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// One person's diary — everything the organisation has them down for, in one place.
/// </summary>
/// <remarks>
/// <para><b>Round 4, D5.</b> Before this an employee had to visit five screens to find out what
/// their week held: their events, their room bookings, the interviews they sit on, their training,
/// their leave and travel. Each module knew its own part and none of them assembled it.</para>
///
/// <para>⚠ <b>This asks the SAME question the clash check asks</b> — "what is this person committed
/// to?" — over a fortnight instead of an hour, so it fans out over the same registered
/// <c>IPanelistCommitmentSource</c> implementations rather than re-reading six modules. That is the
/// whole reason D1 introduced an interface rather than a method: a diary written separately would
/// have started identical and drifted, and the day somebody adds an eighth kind of commitment only
/// one of them would learn about it.</para>
///
/// <para>⚠ The hard/soft distinction is carried through but means something milder here. On the
/// clash check hard REFUSES a booking; in a diary nothing is refused and the flag is only a hint
/// about how firm the entry is.</para>
/// </remarks>
public interface IPersonalScheduleService
{
    /// <summary>Everything one employee is committed to between two dates.</summary>
    Task<PersonalScheduleDto> GetForEmployeeAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same, for every employee in an organisation unit and everything beneath it — what a head
    /// needs before scheduling something for their team.
    /// </summary>
    Task<TeamScheduleDto> GetForUnitAsync(
        Guid organizationUnitId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The units whose team schedule the caller may read (company-schedule final closure lane 5b, R4-10B.3, the user's
    /// ruling): every active unit for the HR desk; else the units <paramref name="actorEmployeeId"/> heads and every unit
    /// beneath them.
    /// </summary>
    Task<TeamScheduleUnitsDto> GetReadableUnitsAsync(
        Guid? actorEmployeeId, bool hrDesk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="actorEmployeeId"/> heads <paramref name="organizationUnitId"/> or a unit above it — the
    /// unit head's own read of a team schedule, without the HR desk's permission (lane 5b, R4-10B.3).
    /// </summary>
    Task<bool> HeadsUnitOrAncestorAsync(
        Guid? actorEmployeeId, Guid organizationUnitId, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IPersonalScheduleService"/>
public class PersonalScheduleService : IPersonalScheduleService
{
    private readonly IReadOnlyList<IPanelistCommitmentSource> _sources;
    private readonly IHrAudienceResolver _audience;
    private readonly IGenericRepository<Entities.HR.Employee> _employees;
    private readonly IGenericRepository<Entities.HR.OrganizationUnit> _units;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PersonalScheduleService> _logger;

    public PersonalScheduleService(
        IEnumerable<IPanelistCommitmentSource> sources,
        IHrAudienceResolver audience,
        IGenericRepository<Entities.HR.Employee> employees,
        IGenericRepository<Entities.HR.OrganizationUnit> units,
        ICurrentUserProvider currentUserProvider,
        ILogger<PersonalScheduleService> logger)
    {
        _sources = (sources ?? Array.Empty<IPanelistCommitmentSource>()).ToList();
        _audience = audience;
        _employees = employees;
        _units = units;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>One row of the tenant's unit tree — read once per request; 451 units on UAT.</summary>
    private sealed record UnitRow(Guid Id, string Name, Guid? ParentUnitId, Guid? HeadEmployeeId, bool IsActive);

    private Task<List<UnitRow>> UnitTreeAsync(Guid tenantId, CancellationToken ct) =>
        _units.GetQueryable().AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .Select(u => new UnitRow(u.Id, u.Name, u.ParentUnitId, u.HeadEmployeeId, u.IsActive))
            .ToListAsync(ct);

    /// <summary>A unit and every unit above it, nearest first; a cycle in the data stops the walk.</summary>
    private static IEnumerable<UnitRow> Ancestry(IReadOnlyDictionary<Guid, UnitRow> byId, Guid unitId)
    {
        var seen = new HashSet<Guid>();
        Guid? current = unitId;
        while (current is { } id && seen.Add(id) && byId.TryGetValue(id, out var row))
        {
            yield return row;
            current = row.ParentUnitId;
        }
    }

    public async Task<bool> HeadsUnitOrAncestorAsync(
        Guid? actorEmployeeId, Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        if (actorEmployeeId is not { } actor || actor == Guid.Empty) return false;
        var byId = (await UnitTreeAsync(GetTenantId(), cancellationToken)).ToDictionary(u => u.Id);
        return Ancestry(byId, organizationUnitId).Any(u => u.IsActive && u.HeadEmployeeId == actor);
    }

    public async Task<TeamScheduleUnitsDto> GetReadableUnitsAsync(
        Guid? actorEmployeeId, bool hrDesk, CancellationToken cancellationToken = default)
    {
        var tree = await UnitTreeAsync(GetTenantId(), cancellationToken);
        var byId = tree.ToDictionary(u => u.Id);
        string PathOf(Guid id) => string.Join(" › ", Ancestry(byId, id).Reverse().Select(u => u.Name));

        IEnumerable<UnitRow> readable;
        if (hrDesk)
        {
            readable = tree.Where(u => u.IsActive);
        }
        else if (actorEmployeeId is { } actor && actor != Guid.Empty)
        {
            // A unit is readable when its own head, or the head of any unit above it, is the caller.
            readable = tree.Where(u => u.IsActive && Ancestry(byId, u.Id).Any(a => a.IsActive && a.HeadEmployeeId == actor));
        }
        else
        {
            readable = Enumerable.Empty<UnitRow>();
        }

        return new TeamScheduleUnitsDto
        {
            CanReadEveryUnit = hrDesk,
            Units = readable
                .Select(u => new TeamScheduleUnitDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    ParentUnitId = u.ParentUnitId,
                    Path = PathOf(u.Id),
                    HeadedByCaller = actorEmployeeId is { } a && u.HeadEmployeeId == a,
                })
                .OrderBy(u => u.Path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// ⚠ Sixty days, matching <c>SuggestPanelSlotsAsync</c>. A diary query is seven reads per
    /// employee; an unbounded range over a unit is how a report turns into an outage.
    /// </summary>
    private static void RequireSaneRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new InvalidOperationException("The end of the range falls before its start.");
        if (to.DayNumber - from.DayNumber > 60)
            throw new InvalidOperationException("Sixty days is the most that can be read at once — narrow the range.");
    }

    public async Task<PersonalScheduleDto> GetForEmployeeAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        RequireSaneRange(from, to);
        var tenantId = GetTenantId();

        var employee = (await _employees.FindAsync(e => e.Id == employeeId))
            .FirstOrDefault(e => e.TenantId == tenantId && !e.IsDeleted);
        if (employee is null)
            throw new ArgumentException($"Employee '{employeeId}' not found.");

        var (entries, failed) = await GatherAsync(new[] { employeeId }, from, to, tenantId, cancellationToken);

        return new PersonalScheduleDto
        {
            EmployeeId = employeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
            OrganizationUnitId = employee.OrganizationUnitId,
            From = from,
            To = to,
            Entries = entries.Where(e => e.SubjectId == employeeId)
                .OrderBy(e => e.Start)
                .ThenBy(e => e.Label)
                .ToList(),
            IncompleteSources = failed,
        };
    }

    public async Task<TeamScheduleDto> GetForUnitAsync(
        Guid organizationUnitId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        RequireSaneRange(from, to);
        var tenantId = GetTenantId();

        // ⚠ The SUBTREE, not the unit. A head scheduling for their directorate means everybody under
        // them, and a unit-only read would quietly leave out the sections that report into it.
        //
        // ⚠ UnitSubtreeAsync returns UNIT ids, NOT employee ids — its own remarks say so: the staff
        // directory wanted the walk as a SQL predicate rather than a resolved employee set. Reading
        // them as employee ids returns nobody, silently, because no employee id matches a unit id.
        // That is what the first cut did, and the team read answered "0 members" for a populated
        // directorate.
        var unitIds = (await _audience.UnitSubtreeAsync(organizationUnitId, cancellationToken)).ToList();
        if (unitIds.Count == 0)
            return new TeamScheduleDto { OrganizationUnitId = organizationUnitId, From = from, To = to };

        // ⚠ Active only, matching the audience resolver's own population: a leaver should not appear
        // in next week's team diary.
        var employees = (await _employees.FindAsync(e =>
                e.OrganizationUnitId != null && unitIds.Contains(e.OrganizationUnitId.Value)))
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive)
            .ToList();

        var (entries, failed) = await GatherAsync(employees.Select(e => e.Id).ToList(), from, to, tenantId, cancellationToken);
        // Lane 5b (R4-10B.4): each member's own unit by name, so the page can narrow to a sub-unit or the direct members.
        var unitNames = (await UnitTreeAsync(tenantId, cancellationToken)).ToDictionary(u => u.Id, u => u.Name);

        return new TeamScheduleDto
        {
            OrganizationUnitId = organizationUnitId,
            OrganizationUnitName = unitNames.GetValueOrDefault(organizationUnitId),
            From = from,
            To = to,
            IncompleteSources = failed,
            Members = employees
                .Select(e => new PersonalScheduleDto
                {
                    EmployeeId = e.Id,
                    EmployeeName = $"{e.FirstName} {e.LastName}".Trim(),
                    OrganizationUnitId = e.OrganizationUnitId,
                    OrganizationUnitName = e.OrganizationUnitId is { } u ? unitNames.GetValueOrDefault(u) : null,
                    From = from,
                    To = to,
                    Entries = entries.Where(x => x.SubjectId == e.Id)
                        .OrderBy(x => x.Start)
                        .ThenBy(x => x.Label)
                        .ToList(),
                    IncompleteSources = failed,
                })
                .OrderBy(m => m.EmployeeName)
                .ToList(),
        };
    }

    /// <summary>
    /// The fan-out. One pass over the registered sources for the whole set of people.
    /// </summary>
    /// <remarks>
    /// ⚠ A source that throws is logged and dropped rather than taking the diary down — but the
    /// entry simply will not appear, which for a DIARY means somebody sees a free afternoon they do
    /// not have. That is why the failure is logged at Error rather than Warning, and why — since
    /// company-schedule lane 5b (R4-10A.3) — the source's name is answered too, so the page can say
    /// the diary is incomplete. A cancelled request is not a failed source: it is thrown, not swallowed.
    /// </remarks>
    private async Task<(List<PersonalScheduleEntryDto> Entries, List<string> Failed)> GatherAsync(
        IReadOnlyList<Guid> employeeIds, DateOnly from, DateOnly to, Guid tenantId,
        CancellationToken cancellationToken)
    {
        var failed = new List<string>();
        var query = new PanelistCommitmentQuery(
            employeeIds,
            Array.Empty<Guid>(),
            from.ToDateTime(TimeOnly.MinValue),
            to.ToDateTime(TimeOnly.MaxValue),
            ExcludeInterviewId: null,
            tenantId);

        var entries = new List<PersonalScheduleEntryDto>();
        foreach (var source in _sources)
        {
            try
            {
                foreach (var c in await source.GetCommitmentsAsync(query, cancellationToken))
                    entries.Add(new PersonalScheduleEntryDto
                    {
                        SubjectId     = c.SubjectId,
                        Kind          = c.Kind,
                        Hardness      = c.Hardness,
                        Label         = c.Label,
                        Start         = c.Start,
                        End           = c.End,
                        IsDayGranular = c.IsDayGranular,
                        Reference     = c.Reference,
                    });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Diary source '{Source}' failed for {From}–{To}. The schedule shown is INCOMPLETE.",
                    source.SourceName, from, to);
                failed.Add(source.SourceName);
            }
        }

        return (entries, failed);
    }
}
