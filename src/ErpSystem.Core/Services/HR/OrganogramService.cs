using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Read-only projection of the existing org hierarchies into the uniform
/// <see cref="OrganogramNodeDto"/> shape. Tenant scoping is applied explicitly because the
/// ApplicationDbContext is registered without a tenant (global filters are inert).
/// </summary>
public class OrganogramService : IOrganogramService
{
    private const string SyntheticRootId = "__root__";

    private readonly IGenericRepository<OrganizationUnit> _units;
    private readonly IGenericRepository<EmployeePosition> _positions;
    private readonly IGenericRepository<Employee> _employees;
    private readonly IGenericRepository<Location> _locations;
    private readonly IGenericRepository<Team> _teams;
    private readonly IGenericRepository<TeamMember> _teamMembers;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OrganogramService> _logger;

    public OrganogramService(
        IGenericRepository<OrganizationUnit> units,
        IGenericRepository<EmployeePosition> positions,
        IGenericRepository<Employee> employees,
        IGenericRepository<Location> locations,
        IGenericRepository<Team> teams,
        IGenericRepository<TeamMember> teamMembers,
        ICurrentUserProvider currentUserProvider,
        ILogger<OrganogramService> logger)
    {
        _units = units;
        _positions = positions;
        _employees = employees;
        _locations = locations;
        _teams = teams;
        _teamMembers = teamMembers;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// Who counts as being in the organisation. This is the predicate
    /// <c>EmployeeService.CanBeAssignedWork</c> uses (<c>EmployeeService.cs:2385</c>) and it is
    /// reused verbatim rather than restated, so the organogram and the rest of HR cannot drift on
    /// what "a member of staff" means.
    /// </summary>
    /// <remarks>
    /// An <see cref="Expression{TDelegate}"/> rather than a method, and applied as its own
    /// <c>.Where()</c> clause, because EF cannot translate a call to a C# method inside a query — a
    /// predicate written as one compiles and then throws at runtime. Measured on DEFAULT
    /// 2026-08-22: 79 of 6,286 employees are terminated, and until this predicate existed here every
    /// one of them was counted as staff on the unit and position charts.
    /// </remarks>
    private static readonly Expression<Func<Employee, bool>> OnStrength =
        e => e.IsActive && e.StaffStatus != StaffStatus.Terminated;

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public async Task<OrganogramResponseDto> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var units = await _units.GetQueryable()
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.OrganizationLevel)
            .Include(u => u.HeadEmployee)
            .OrderBy(u => u.Sequence)
            .ThenBy(u => u.Name)
            .ToListAsync(cancellationToken);

        var headcount = await _employees.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.OrganizationUnitId != null)
            .Where(OnStrength)
            .GroupBy(e => e.OrganizationUnitId)
            .Select(g => new { UnitId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var headcountByUnit = headcount
            .Where(x => x.UnitId.HasValue)
            .ToDictionary(x => x.UnitId!.Value, x => x.Count);

        var nodes = units.Select(u =>
        {
            var node = new OrganogramNodeDto
            {
                Id = u.Id.ToString(),
                ParentId = u.ParentUnitId?.ToString(),
                Name = u.Name,
                Title = u.OrganizationLevel?.Name,
                Code = string.IsNullOrWhiteSpace(u.Code) ? null : u.Code,
                HeadName = u.HeadEmployee?.FullName,
                IsActive = u.IsActive,
                IsVacant = u.HeadEmployeeId == null,
                EmployeeCount = headcountByUnit.TryGetValue(u.Id, out var c) ? c : 0,
                Badge = !u.IsActive ? "Inactive" : (u.HeadEmployeeId == null ? "Vacant lead" : null),
            };
            if (!string.IsNullOrWhiteSpace(u.AccountCode)) node.Meta["Account code"] = u.AccountCode!;
            if (!string.IsNullOrWhiteSpace(u.Description)) node.Meta["Description"] = u.Description!;
            return node;
        }).ToList();

        return Build("units", nodes, "Organization", rollUpHeadcount: true);
    }

    public async Task<OrganogramResponseDto> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var positions = await _positions.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.StaffLevel)
            .OrderBy(p => p.Level)
            .ThenBy(p => p.Title)
            .ToListAsync(cancellationToken);

        var filled = await _employees.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Where(OnStrength)
            .GroupBy(e => e.PositionId)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var filledByPosition = filled.ToDictionary(x => x.PositionId, x => x.Count);

        var nodes = positions.Select(p =>
        {
            var current = filledByPosition.TryGetValue(p.Id, out var c) ? c : 0;
            var node = new OrganogramNodeDto
            {
                Id = p.Id.ToString(),
                ParentId = p.ReportsToPositionId?.ToString(),
                Name = p.Title,
                Title = p.OrganizationUnit?.Name,
                Code = string.IsNullOrWhiteSpace(p.Code) ? null : p.Code,
                IsActive = p.IsActive,
                EmployeeCount = current,
                ExpectedHeadcount = p.ExpectedHeadcount,
                IsVacant = current == 0,
                Badge = current == 0
                    ? "Vacant"
                    : (current < p.ExpectedHeadcount ? $"{current}/{p.ExpectedHeadcount}" : null),
            };
            if (p.StaffLevel != null) node.Meta["Staff level"] = p.StaffLevel.Name;
            node.Meta["Headcount"] = $"{current} of {p.ExpectedHeadcount}";
            return node;
        }).ToList();

        return Build("positions", nodes, "Positions", rollUpHeadcount: true);
    }

    public async Task<OrganogramResponseDto> GetPeopleAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // On strength only. A leaver is not in the organisation, and drawing one still holding
        // reporting lines is the opposite of what a reporting chart is read for. Their reports fall
        // to the root through the dangling-parent promotion in Build(), which is the honest picture:
        // the post is vacant and nobody has been reassigned yet.
        var employees = await _employees.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Where(OnStrength)
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync(cancellationToken);

        var reports = employees
            .Where(e => e.ManagerId != null)
            .GroupBy(e => e.ManagerId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var nodes = employees.Select(e =>
        {
            var node = new OrganogramNodeDto
            {
                Id = e.Id.ToString(),
                ParentId = e.ManagerId?.ToString(),
                Name = e.FullName,
                Title = e.Position?.Title,
                Code = string.IsNullOrWhiteSpace(e.EmployeeNumber) ? null : e.EmployeeNumber,
                ImageUrl = string.IsNullOrWhiteSpace(e.PicturePath) ? null : e.PicturePath,
                IsActive = e.IsActive,
                EmployeeCount = reports.TryGetValue(e.Id, out var c) ? c : 0,
                Badge = e.StaffStatus == StaffStatus.Active ? null : e.StaffStatus.ToString(),
            };
            if (e.OrganizationUnit != null) node.Meta["Unit"] = e.OrganizationUnit.Name;
            if (!string.IsNullOrWhiteSpace(e.EmailAddress)) node.Meta["Email"] = e.EmailAddress;
            return node;
        }).ToList();

        return Build("people", nodes, "Organization", rollUpHeadcount: true);
    }

    public async Task<OrganogramResponseDto> GetLocationsAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var locations = await _locations.GetQueryable()
            .Where(l => l.TenantId == tenantId && l.StructureId == structureId)
            .Include(l => l.LocationLevel)
            .OrderBy(l => l.Sequence)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);

        var nodes = locations.Select(l =>
        {
            var node = new OrganogramNodeDto
            {
                Id = l.Id.ToString(),
                ParentId = l.ParentLocationId?.ToString(),
                Name = l.Name,
                Title = l.LocationLevel?.Name,
                Code = string.IsNullOrWhiteSpace(l.Code) ? null : l.Code,
                IsActive = l.IsActive,
                Badge = l.IsActive ? null : "Inactive",
            };
            if (!string.IsNullOrWhiteSpace(l.City)) node.Meta["City"] = l.City!;
            var address = string.Join(", ", new[] { l.AddressLine1, l.AddressLine2 }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(address)) node.Meta["Address"] = address;
            return node;
        }).ToList();

        return Build("locations", nodes, "Locations");
    }

    public async Task<OrganogramResponseDto> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var teams = await _teams.GetQueryable()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.TeamLead)
            .Include(t => t.OrganizationUnit)
            .OrderBy(t => t.Sequence)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // Membership headcount, on the same two terms as everywhere else in this service: the
        // membership must be live, and the person must be on strength. Counting a leaver's old
        // membership here would put them back on the organogram through the teams dimension, out
        // the side of the D-27 fix.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var memberships = await _teamMembers.GetQueryable()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Where(m => m.LeaveDate == null || m.LeaveDate >= today)
            .Join(_employees.GetQueryable().Where(e => e.TenantId == tenantId).Where(OnStrength),
                  m => m.EmployeeId, e => e.Id, (m, e) => m.TeamId)
            .ToListAsync(cancellationToken);
        var memberCountByTeam = memberships
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var nodes = teams.Select(t =>
        {
            var members = memberCountByTeam.TryGetValue(t.Id, out var c) ? c : 0;
            var node = new OrganogramNodeDto
            {
                Id = t.Id.ToString(),
                ParentId = t.ParentTeamId?.ToString(),
                Name = t.Name,
                Title = t.TeamType.ToString(),
                Code = string.IsNullOrWhiteSpace(t.Code) ? null : t.Code,
                HeadName = t.TeamLead?.FullName,
                IsActive = t.Status == TeamStatus.Active,
                IsVacant = t.TeamLeadId == null,
                EmployeeCount = members,
                ExpectedHeadcount = t.MaxMembers,
                Badge = t.Status != TeamStatus.Active
                    ? t.Status.ToString()
                    : (t.TeamLeadId == null ? "No lead" : null),
            };
            if (t.OrganizationUnit != null) node.Meta["Owning unit"] = t.OrganizationUnit.Name;
            if (!string.IsNullOrWhiteSpace(t.ProjectCode)) node.Meta["Project"] = t.ProjectCode!;
            if (!string.IsNullOrWhiteSpace(t.CostCenterCode)) node.Meta["Cost centre"] = t.CostCenterCode!;
            node.Meta["Members"] = t.MaxMembers.HasValue ? $"{members} of {t.MaxMembers}" : members.ToString();
            node.Meta["Effective from"] = t.EffectiveFrom.ToString("yyyy-MM-dd");
            if (t.EffectiveTo.HasValue) node.Meta["Effective to"] = t.EffectiveTo.Value.ToString("yyyy-MM-dd");
            return node;
        }).ToList();

        // Rolls up now that teams carry a headcount at all — slice 4b. Before the register existed
        // the dimension projected a table nothing could write, so every node was null and a
        // rolled-up "0" would have been a number invented for rows that had none.
        return Build("teams", nodes, "Teams", rollUpHeadcount: true);
    }

    /// <summary>
    /// Normalises a flat node set into a single-rooted tree the client can render: self-parents and
    /// cycles are broken, dangling parent references become roots, and when more than one root
    /// exists a synthetic root is prepended so the renderer always receives exactly one.
    /// </summary>
    /// <param name="rollUpHeadcount">
    /// True on the dimensions that carry a headcount (units, positions, people). False on locations
    /// and teams, where <c>EmployeeCount</c> is null throughout and a rolled-up "0" would be a
    /// number invented for nodes that have none.
    /// </param>
    private static OrganogramResponseDto Build(
        string dimension, List<OrganogramNodeDto> nodes, string rootLabel, bool rollUpHeadcount = false)
    {
        var byId = new Dictionary<string, OrganogramNodeDto>(nodes.Count);
        foreach (var n in nodes) byId[n.Id] = n;

        // Any node whose parent isn't present (filtered out / orphaned) is promoted to a root.
        // This is also what re-roots the reports of someone who has left: their manager is no longer
        // on strength, so the id no longer resolves and the reports surface rather than vanish.
        foreach (var n in nodes)
        {
            if (!string.IsNullOrEmpty(n.ParentId) && !byId.ContainsKey(n.ParentId))
                n.ParentId = null;
        }

        DetachCycles(nodes, byId);

        var roots = nodes.Where(n => string.IsNullOrEmpty(n.ParentId)).ToList();
        if (roots.Count > 1)
        {
            foreach (var r in roots)
                r.ParentId = SyntheticRootId;

            nodes.Insert(0, new OrganogramNodeDto
            {
                Id = SyntheticRootId,
                ParentId = null,
                Name = rootLabel,
                Title = $"{roots.Count} top-level nodes",
                IsActive = true,
            });
        }

        if (rollUpHeadcount) RollUpHeadcount(nodes);

        return new OrganogramResponseDto
        {
            Dimension = dimension,
            Nodes = nodes,
            NodeCount = nodes.Count,
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Breaks any parent cycle by detaching the node that closes it.
    /// </summary>
    /// <remarks>
    /// The contract of these endpoints is "a flat list the client turns into a tree". Nothing in the
    /// product stops <c>Employee.ManagerId</c> from forming a loop — the org-unit service refuses
    /// cycles, the employee service does not — and a looped group is reachable from no root at all,
    /// so the client either drops those people silently or spins building the tree. Detaching one
    /// node per cycle makes the group render, visibly, as its own top-level branch.
    /// </remarks>
    private static void DetachCycles(List<OrganogramNodeDto> nodes, Dictionary<string, OrganogramNodeDto> byId)
    {
        foreach (var n in nodes)
        {
            if (n.ParentId == n.Id) n.ParentId = null;
        }

        var settled = new HashSet<string>();
        var onPath = new HashSet<string>();
        var path = new List<OrganogramNodeDto>();

        foreach (var start in nodes)
        {
            if (settled.Contains(start.Id)) continue;
            onPath.Clear();
            path.Clear();

            var cursor = start;
            while (cursor is not null && !settled.Contains(cursor.Id))
            {
                if (!onPath.Add(cursor.Id))
                {
                    cursor.ParentId = null; // this node closes the loop; it becomes a root
                    break;
                }
                path.Add(cursor);
                cursor = string.IsNullOrEmpty(cursor.ParentId) ? null : byId[cursor.ParentId];
            }

            foreach (var seen in path) settled.Add(seen.Id);
        }
    }

    /// <summary>
    /// Fills <see cref="OrganogramNodeDto.TotalEmployeeCount"/> with the subtree total at every node.
    /// </summary>
    /// <remarks>
    /// Iterative rather than recursive: the people dimension is six thousand nodes and a stack
    /// overflow on a deep chain is not a failure mode worth owning for the sake of four fewer lines.
    /// Safe to walk because <see cref="DetachCycles"/> has already run.
    /// </remarks>
    private static void RollUpHeadcount(List<OrganogramNodeDto> nodes)
    {
        var children = new Dictionary<string, List<OrganogramNodeDto>>();
        foreach (var n in nodes)
        {
            n.TotalEmployeeCount = n.EmployeeCount;
            if (string.IsNullOrEmpty(n.ParentId)) continue;
            if (!children.TryGetValue(n.ParentId, out var siblings))
                children[n.ParentId] = siblings = new List<OrganogramNodeDto>();
            siblings.Add(n);
        }

        var order = new List<OrganogramNodeDto>(nodes.Count);
        var stack = new Stack<OrganogramNodeDto>();
        foreach (var root in nodes.Where(n => string.IsNullOrEmpty(n.ParentId)))
            stack.Push(root);

        while (stack.Count > 0)
        {
            var n = stack.Pop();
            order.Add(n);
            if (children.TryGetValue(n.Id, out var kids))
                foreach (var k in kids) stack.Push(k);
        }

        // Reverse pre-order is a valid post-order: every child is settled before its parent is read.
        for (var i = order.Count - 1; i >= 0; i--)
        {
            var n = order[i];
            if (!children.TryGetValue(n.Id, out var kids)) continue;
            var total = n.EmployeeCount ?? 0;
            foreach (var k in kids) total += k.TotalEmployeeCount ?? 0;
            n.TotalEmployeeCount = total;
        }
    }
}
