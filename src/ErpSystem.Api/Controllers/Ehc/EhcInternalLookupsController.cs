using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/lookups")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcInternalLookupsController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;

    public EhcInternalLookupsController(
        ErpSystem.Data.ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    [HttpGet("departments")]
    public async Task<ActionResult> GetDepartments(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcLookupItemDto>() });
        }

        var items = await _db.Set<Department>()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new EhcLookupItemDto { Id = d.Id, Name = d.Name })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("agents")]
    public async Task<ActionResult> GetAgents(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcLookupItemDto>() });
        }

        var agents = await _userManager.GetUsersInRoleAsync(Constants.Roles.HelpdeskAgent);
        var items = agents
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new EhcLookupItemDto
            {
                Id = u.Id,
                Name = $"{u.FirstName} {u.LastName}".Trim()
            })
            .ToList();

        return Ok(new { success = true, data = items });
    }

    [HttpGet("my-department")]
    public async Task<ActionResult> GetMyDepartment(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        var employeeId = _currentUserService.EmployeeId;
        if (!employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        var employee = await _db.Set<Employee>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId.Value && e.TenantId == tenantId && !e.IsDeleted && e.IsActive, cancellationToken);

        if (employee == null || employee.DepartmentId == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        var department = await _db.Set<Department>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId && d.TenantId == tenantId && !d.IsDeleted && d.IsActive, cancellationToken);

        if (department == null)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        return Ok(new { success = true, data = new EhcLookupItemDto { Id = department.Id, Name = department.Name } });
    }
}
