using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;

namespace ErpSystem.Data.Services;

public interface IRolePermissionService
{
    Task<IEnumerable<ApplicationRole>> GetAllRolesWithPermissionsAsync();
    Task<ApplicationRole?> GetRoleWithPermissionsByIdAsync(Guid roleId);
}

public class RolePermissionService : IRolePermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RolePermissionService> _logger;

    public RolePermissionService(
        ApplicationDbContext context,
        ILogger<RolePermissionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<ApplicationRole>> GetAllRolesWithPermissionsAsync()
    {
        return await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<ApplicationRole?> GetRoleWithPermissionsByIdAsync(Guid roleId)
    {
        return await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId);
    }
}