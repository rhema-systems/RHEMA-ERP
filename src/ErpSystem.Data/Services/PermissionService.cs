using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Services;

public interface IPermissionService
{
    Task<IEnumerable<Permission>> GetAllPermissionsAsync();
    Task<IEnumerable<Permission>> GetPermissionsByCategoryAsync(string category);
    Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId);
    Task<Permission?> GetPermissionByIdAsync(Guid permissionId);
    Task<Permission?> GetPermissionByNameAsync(string name);
    Task<Permission> CreatePermissionAsync(Permission permission);
    Task<Permission> UpdatePermissionAsync(Permission permission);
    Task<bool> DeletePermissionAsync(Guid permissionId);
    Task<bool> AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, string? grantedBy = null);
    Task<bool> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId);
    Task<bool> UpdateRolePermissionsAsync(Guid roleId, IEnumerable<Guid> permissionIds, string? grantedBy = null);
    Task<IEnumerable<string>> GetPermissionCategoriesAsync();
    Task<bool> PermissionExistsAsync(string name);
    Task<bool> HasPermissionAsync(Guid roleId, string permissionName);
}

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        ApplicationDbContext context,
        ILogger<PermissionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
    {
        return await _context.Permissions
            .OrderBy(p => p.Category)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Permission>> GetPermissionsByCategoryAsync(string category)
    {
        return await _context.Permissions
            .Where(p => p.Category == category)
            .OrderBy(p => p.DisplayName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId)
    {
        return await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();
    }

    public async Task<Permission?> GetPermissionByIdAsync(Guid permissionId)
    {
        return await _context.Permissions
            .FirstOrDefaultAsync(p => p.Id == permissionId);
    }

    public async Task<Permission?> GetPermissionByNameAsync(string name)
    {
        return await _context.Permissions
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<Permission> CreatePermissionAsync(Permission permission)
    {
        try
        {
            permission.Id = Guid.NewGuid();
            permission.CreatedAt = DateTime.UtcNow;

            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created permission {PermissionName} with ID {PermissionId}",
                permission.Name, permission.Id);
            return permission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission {PermissionName}", permission.Name);
            throw;
        }
    }

    public async Task<Permission> UpdatePermissionAsync(Permission permission)
    {
        try
        {
            permission.UpdatedAt = DateTime.UtcNow;
            _context.Permissions.Update(permission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated permission {PermissionName} with ID {PermissionId}",
                permission.Name, permission.Id);
            return permission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permission {PermissionId}", permission.Id);
            throw;
        }
    }

    public async Task<bool> DeletePermissionAsync(Guid permissionId)
    {
        try
        {
            var permission = await GetPermissionByIdAsync(permissionId);
            if (permission == null)
            {
                return false;
            }

            // Don't allow deletion of system permissions
            if (permission.IsSystemPermission)
            {
                _logger.LogWarning("Attempted to delete system permission {PermissionName}", permission.Name);
                return false;
            }

            // Remove all role-permission relationships first
            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.PermissionId == permissionId)
                .ToListAsync();

            if (rolePermissions.Any())
            {
                _context.RolePermissions.RemoveRange(rolePermissions);
            }

            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted permission {PermissionName} with ID {PermissionId}",
                permission.Name, permissionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permission {PermissionId}", permissionId);
            throw;
        }
    }

    public async Task<bool> AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, string? grantedBy = null)
    {
        try
        {
            // Check if the relationship already exists
            var existingRelationship = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

            if (existingRelationship != null)
            {
                return true; // Already exists
            }

            var rolePermission = new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = grantedBy
            };

            _context.RolePermissions.Add(rolePermission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned permission {PermissionId} to role {RoleId}", permissionId, roleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permission {PermissionId} to role {RoleId}", permissionId, roleId);
            throw;
        }
    }

    public async Task<bool> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId)
    {
        try
        {
            var rolePermission = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

            if (rolePermission == null)
            {
                return true; // Already removed
            }

            _context.RolePermissions.Remove(rolePermission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Removed permission {PermissionId} from role {RoleId}", permissionId, roleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {PermissionId} from role {RoleId}", permissionId, roleId);
            throw;
        }
    }

    public async Task<bool> UpdateRolePermissionsAsync(Guid roleId, IEnumerable<Guid> permissionIds, string? grantedBy = null)
    {
        try
        {
            // Get current role permissions
            var currentPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            var currentPermissionIds = currentPermissions.Select(rp => rp.PermissionId).ToHashSet();
            var newPermissionIds = permissionIds.ToHashSet();

            // Remove permissions that are no longer assigned
            var permissionsToRemove = currentPermissions
                .Where(rp => !newPermissionIds.Contains(rp.PermissionId))
                .ToList();

            if (permissionsToRemove.Any())
            {
                _context.RolePermissions.RemoveRange(permissionsToRemove);
            }

            // Add new permissions
            var permissionsToAdd = newPermissionIds
                .Where(id => !currentPermissionIds.Contains(id))
                .Select(id => new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = id,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = grantedBy
                })
                .ToList();

            if (permissionsToAdd.Any())
            {
                _context.RolePermissions.AddRange(permissionsToAdd);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated permissions for role {RoleId}: removed {RemovedCount}, added {AddedCount}",
                roleId, permissionsToRemove.Count, permissionsToAdd.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for role {RoleId}", roleId);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetPermissionCategoriesAsync()
    {
        return await _context.Permissions
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<bool> PermissionExistsAsync(string name)
    {
        return await _context.Permissions
            .AnyAsync(p => p.Name == name);
    }

    public async Task<bool> HasPermissionAsync(Guid roleId, string permissionName)
    {
        return await _context.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.Permission.Name == permissionName);
    }
}
