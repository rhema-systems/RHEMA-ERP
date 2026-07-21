using System;
using System.Collections.Generic;
using System.Linq;
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
/// <see cref="OrganogramNodeDto"/> shape. Tenant scoping and soft-delete are handled by the
/// DbContext global query filters, so queries here do not re-filter on TenantId / IsDeleted.
/// </summary>
public class OrganogramService : IOrganogramService
{
    private const string SyntheticRootId = "__root__";

    private readonly IGenericRepository<OrganizationUnit> _units;
    private readonly IGenericRepository<EmployeePosition> _positions;
    private readonly IGenericRepository<Employee> _employees;
    private readonly IGenericRepository<Location> _locations;
    private readonly IGenericRepository<Team> _teams;
    private readonly ILogger<OrganogramService> _logger;

    public OrganogramService(
        IGenericRepository<OrganizationUnit> units,
        IGenericRepository<EmployeePosition> positions,
        IGenericRepository<Employee> employees,
        IGenericRepository<Location> locations,
        IGenericRepository<Team> teams,
        ILogger<OrganogramService> logger)
    {
        _units = units;
        _positions = positions;
        _employees = employees;
        _locations = locations;
        _teams = teams;
        _logger = logger;
    }

    public async Task<OrganogramResponseDto> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        var units = await _units.GetQueryable()
            .Include(u => u.OrganizationLevel)
            .Include(u => u.HeadEmployee)
            .OrderBy(u => u.Sequence)
            .ToListAsync(cancellationToken);

        var headcount = await _employees.GetQueryable()
            .Where(e => e.OrganizationUnitId != null)
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

        return Build("units", nodes, "Organization");
    }

    public async Task<OrganogramResponseDto> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        var positions = await _positions.GetQueryable()
            .Include(p => p.OrganizationUnit)
            .Include(p => p.StaffLevel)
            .OrderBy(p => p.Level)
            .ToListAsync(cancellationToken);

        var filled = await _employees.GetQueryable()
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

        return Build("positions", nodes, "Positions");
    }

    public async Task<OrganogramResponseDto> GetPeopleAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _employees.GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
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

        return Build("people", nodes, "Organization");
    }

    public async Task<OrganogramResponseDto> GetLocationsAsync(Guid structureId, CancellationToken cancellationToken = default)
    {
        var locations = await _locations.GetQueryable()
            .Where(l => l.StructureId == structureId)
            .Include(l => l.LocationLevel)
            .OrderBy(l => l.Sequence)
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
        var teams = await _teams.GetQueryable()
            .Include(t => t.TeamLead)
            .Include(t => t.OrganizationUnit)
            .ToListAsync(cancellationToken);

        var nodes = teams.Select(t =>
        {
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
                Badge = t.Status == TeamStatus.Active ? null : t.Status.ToString(),
            };
            if (t.OrganizationUnit != null) node.Meta["Owning unit"] = t.OrganizationUnit.Name;
            return node;
        }).ToList();

        return Build("teams", nodes, "Teams");
    }

    /// <summary>
    /// Normalises a flat node set into a single-rooted tree the client can render:
    /// dangling parent references become roots, and when more than one root exists a synthetic
    /// root is prepended so d3-org-chart always receives exactly one root.
    /// </summary>
    private static OrganogramResponseDto Build(string dimension, List<OrganogramNodeDto> nodes, string rootLabel)
    {
        var ids = new HashSet<string>(nodes.Select(n => n.Id));

        // Any node whose parent isn't present (filtered out / orphaned) is promoted to a root.
        foreach (var n in nodes)
        {
            if (!string.IsNullOrEmpty(n.ParentId) && !ids.Contains(n.ParentId))
                n.ParentId = null;
        }

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

        return new OrganogramResponseDto
        {
            Dimension = dimension,
            Nodes = nodes,
            NodeCount = nodes.Count,
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }
}
