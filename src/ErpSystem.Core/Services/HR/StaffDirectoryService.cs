using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The staff directory. See <see cref="IStaffDirectoryService"/> for why it does not reuse
/// <c>EmployeeService</c>'s paged search or <c>EmployeeDto</c>.
/// </summary>
public class StaffDirectoryService : IStaffDirectoryService
{
    private const int MaxPageSize = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audience;
    private readonly ICurrentUserProvider _currentUserProvider;

    public StaffDirectoryService(
        IUnitOfWork unitOfWork,
        IHrAudienceResolver audience,
        ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _audience = audience;
        _currentUserProvider = currentUserProvider;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// Who the directory contains. The same predicate the organogram counts on — active and not
    /// terminated — so a headcount on the org chart and a browse of the same unit agree.
    /// </summary>
    private static readonly Expression<Func<Employee, bool>> OnStrength =
        e => !e.IsDeleted && e.IsActive && e.StaffStatus != StaffStatus.Terminated;

    private static Guid? Self(Guid callerEmployeeId) =>
        callerEmployeeId == Guid.Empty ? null : callerEmployeeId;

    private IQueryable<Employee> Directory(Guid tenantId) =>
        _unitOfWork.Repository<Employee>()
            .GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .Where(OnStrength);

    // ── Search and browse ─────────────────────────────────────────────────────

    public async Task<PagedResult<StaffDirectoryEntryDto>> SearchAsync(
        Guid callerEmployeeId,
        string? search,
        Guid? organizationUnitId,
        Guid? locationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var tenantId = GetTenantId();
        var self = Self(callerEmployeeId);

        var q = Directory(tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            q = q.Where(e =>
                EF.Functions.Like(e.FirstName, like) ||
                EF.Functions.Like(e.LastName, like) ||
                EF.Functions.Like(e.FirstName + " " + e.LastName, like) ||
                EF.Functions.Like(e.EmployeeNumber, like) ||
                EF.Functions.Like(e.EmailAddress, like) ||
                EF.Functions.Like(e.Position.Title, like) ||
                (e.OrganizationUnit != null && EF.Functions.Like(e.OrganizationUnit.Name, like)));
        }

        if (organizationUnitId is { } unitId && unitId != Guid.Empty)
        {
            // The subtree, not the unit — see IStaffDirectoryService for the measurement that
            // makes this the difference between a browse that works and one that finds nobody.
            var subtree = (await _audience.UnitSubtreeAsync(unitId, cancellationToken)).ToList();
            q = q.Where(e => e.OrganizationUnitId != null && subtree.Contains(e.OrganizationUnitId.Value));
        }

        if (locationId is { } locId && locId != Guid.Empty)
            q = q.Where(e => e.LocationId == locId);

        var totalCount = await q.CountAsync(cancellationToken);

        var rows = await q
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection(self))
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffDirectoryEntryDto>
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    // ── One card ──────────────────────────────────────────────────────────────

    public async Task<StaffDirectoryProfileDto?> GetProfileAsync(
        Guid callerEmployeeId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (employeeId == Guid.Empty) return null;

        var tenantId = GetTenantId();
        var self = Self(callerEmployeeId);

        var person = await Directory(tenantId)
            .Where(e => e.Id == employeeId)
            .Select(e => new
            {
                Entry = new StaffDirectoryEntryDto
                {
                    Id = e.Id,
                    EmployeeNumber = e.EmployeeNumber,
                    FullName = e.FirstName + " " + e.LastName,
                    DisplayName = e.FirstName + " " + e.LastName,
                    Title = e.Title,
                    PositionTitle = e.Position.Title,
                    OrganizationUnitId = e.OrganizationUnitId,
                    OrganizationUnitName = e.OrganizationUnit != null ? e.OrganizationUnit.Name : null,
                    OrganizationLevelName = e.OrganizationLevel != null ? e.OrganizationLevel.Name : null,
                    LocationId = e.LocationId,
                    LocationName = e.Location != null ? e.Location.Name : null,
                    EmailAddress = e.EmailAddress,
                    BusinessNumber = e.BusinessNumber,
                    Extension = e.Extension,
                    PicturePath = e.PicturePath,
                    IsSelf = self != null && e.Id == self,
                },
                e.ManagerId,
                ManagerName = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                ManagerPositionTitle = e.Manager != null ? e.Manager.Position.Title : null,
                ManagerOnStrength = e.Manager != null
                    && !e.Manager.IsDeleted
                    && e.Manager.IsActive
                    && e.Manager.StaffStatus != StaffStatus.Terminated,
                UnitId = e.OrganizationUnitId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (person == null) return null;

        var profile = new StaffDirectoryProfileDto
        {
            Id = person.Entry.Id,
            EmployeeNumber = person.Entry.EmployeeNumber,
            FullName = person.Entry.FullName,
            DisplayName = person.Entry.DisplayName,
            Title = person.Entry.Title,
            PositionTitle = person.Entry.PositionTitle,
            OrganizationUnitId = person.Entry.OrganizationUnitId,
            OrganizationUnitName = person.Entry.OrganizationUnitName,
            OrganizationLevelName = person.Entry.OrganizationLevelName,
            LocationId = person.Entry.LocationId,
            LocationName = person.Entry.LocationName,
            EmailAddress = person.Entry.EmailAddress,
            BusinessNumber = person.Entry.BusinessNumber,
            Extension = person.Entry.Extension,
            PicturePath = person.Entry.PicturePath,
            IsSelf = person.Entry.IsSelf,

            // A manager who has left is not shown as the reporting line. Nothing clears the FK
            // on a termination, so without this the card would name a leaver as the person to
            // go to — the same trap the organogram's on-strength predicate exists to avoid.
            ManagerId = person.ManagerOnStrength ? person.ManagerId : null,
            ManagerName = person.ManagerOnStrength ? person.ManagerName : null,
            ManagerPositionTitle = person.ManagerOnStrength ? person.ManagerPositionTitle : null,

            DirectReports = await DirectReportsOfAsync(tenantId, employeeId, self, cancellationToken),
        };

        profile.UnitPath = await UnitPathAsync(tenantId, person.UnitId, cancellationToken);
        return profile;
    }

    // ── My team ───────────────────────────────────────────────────────────────

    public async Task<MyTeamDto> GetMyTeamAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var me = await Directory(tenantId)
            .Where(e => e.Id == employeeId)
            .Select(e => new
            {
                e.ManagerId,
                ManagerName = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                ManagerPositionTitle = e.Manager != null ? e.Manager.Position.Title : null,
                ManagerEmail = e.Manager != null ? e.Manager.EmailAddress : null,
                ManagerOnStrength = e.Manager != null
                    && !e.Manager.IsDeleted
                    && e.Manager.IsActive
                    && e.Manager.StaffStatus != StaffStatus.Terminated,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var hasManager = me is { ManagerOnStrength: true };

        return new MyTeamDto
        {
            ManagerId = hasManager ? me!.ManagerId : null,
            ManagerName = hasManager ? me!.ManagerName : null,
            ManagerPositionTitle = hasManager ? me!.ManagerPositionTitle : null,
            ManagerEmailAddress = hasManager ? me!.ManagerEmail : null,
            DirectReports = await DirectReportsOfAsync(tenantId, employeeId, employeeId, cancellationToken),
        };
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private async Task<List<StaffDirectoryEntryDto>> DirectReportsOfAsync(
        Guid tenantId, Guid managerId, Guid? self, CancellationToken ct)
        => await Directory(tenantId)
            .Where(e => e.ManagerId == managerId)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Select(Projection(self))
            .ToListAsync(ct);

    /// <summary>
    /// Root-first names of the unit and its ancestors. Loads the tenant's unit edges once and
    /// walks them in memory — the same shape <c>HrAudienceResolver</c> uses — rather than one
    /// query per level.
    /// </summary>
    private async Task<List<string>> UnitPathAsync(Guid tenantId, Guid? unitId, CancellationToken ct)
    {
        if (unitId is not { } start || start == Guid.Empty) return [];

        var units = await _unitOfWork.Repository<OrganizationUnit>()
            .GetQueryable()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Name, u.ParentUnitId })
            .ToListAsync(ct);

        var byId = units.ToDictionary(u => u.Id);
        var path = new List<string>();
        var seen = new HashSet<Guid>();
        var current = start;

        // `seen` guards a cycle in the data; without it a unit that is its own ancestor loops.
        while (byId.TryGetValue(current, out var unit) && seen.Add(current))
        {
            path.Insert(0, unit.Name);
            if (unit.ParentUnitId is not { } parent) break;
            current = parent;
        }

        return path;
    }

    private static Expression<Func<Employee, StaffDirectoryEntryDto>> Projection(Guid? self) =>
        e => new StaffDirectoryEntryDto
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FullName = e.FirstName + " " + e.LastName,
            DisplayName = e.FirstName + " " + e.LastName,
            Title = e.Title,
            PositionTitle = e.Position.Title,
            OrganizationUnitId = e.OrganizationUnitId,
            OrganizationUnitName = e.OrganizationUnit != null ? e.OrganizationUnit.Name : null,
            OrganizationLevelName = e.OrganizationLevel != null ? e.OrganizationLevel.Name : null,
            LocationId = e.LocationId,
            LocationName = e.Location != null ? e.Location.Name : null,
            EmailAddress = e.EmailAddress,
            BusinessNumber = e.BusinessNumber,
            Extension = e.Extension,
            PicturePath = e.PicturePath,
            IsSelf = self != null && e.Id == self,
        };
}
