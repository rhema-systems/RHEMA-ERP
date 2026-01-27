using ErpSystem.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

public interface IRoleService
{
    Task<IEnumerable<ApplicationRole>> GetAllRolesAsync();
    Task<ApplicationRole?> GetRoleByIdAsync(Guid roleId);
    Task<ApplicationRole?> GetRoleByNameAsync(string roleName);
    Task<ApplicationRole> CreateRoleAsync(ApplicationRole role);
    Task<ApplicationRole> UpdateRoleAsync(ApplicationRole role);
    Task<bool> DeleteRoleAsync(Guid roleId);
    Task<bool> RoleExistsAsync(string roleName);
    Task<IEnumerable<ApplicationUser>> GetUsersInRoleAsync(string roleName);
}

public class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<RoleService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IEnumerable<ApplicationRole>> GetAllRolesAsync()
    {
        return await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<ApplicationRole?> GetRoleByIdAsync(Guid roleId)
    {
        return await _roleManager.FindByIdAsync(roleId.ToString());
    }

    public async Task<ApplicationRole?> GetRoleByNameAsync(string roleName)
    {
        return await _roleManager.FindByNameAsync(roleName);
    }

    public async Task<ApplicationRole> CreateRoleAsync(ApplicationRole role)
    {
        try
        {
            role.Id = Guid.NewGuid();
            role.CreatedAt = DateTime.UtcNow;

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create role: {errors}");
            }

            _logger.LogInformation("Created role {RoleName} with ID {RoleId}", role.Name, role.Id);
            return role;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", role.Name);
            throw;
        }
    }

    public async Task<ApplicationRole> UpdateRoleAsync(ApplicationRole role)
    {
        try
        {
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to update role: {errors}");
            }

            _logger.LogInformation("Updated role {RoleName} with ID {RoleId}", role.Name, role.Id);
            return role;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", role.Id);
            throw;
        }
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role == null)
            {
                return false;
            }

            // Don't allow deletion of system roles
            if (role.IsSystemRole)
            {
                _logger.LogWarning("Attempted to delete system role {RoleName}", role.Name);
                return false;
            }

            // Check if role is still in use
            var usersInRole = await GetUsersInRoleAsync(role.Name!);
            if (usersInRole.Any())
            {
                _logger.LogWarning("Cannot delete role {RoleName} as it still has {UserCount} users assigned",
                    role.Name, usersInRole.Count());
                throw new InvalidOperationException($"Cannot delete role '{role.Name}' as it still has users assigned to it.");
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                _logger.LogInformation("Deleted role {RoleName} with ID {RoleId}", role.Name, roleId);
                return true;
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Failed to delete role {RoleId}: {Errors}", roleId, errors);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", roleId);
            throw;
        }
    }

    public async Task<bool> RoleExistsAsync(string roleName)
    {
        return await _roleManager.RoleExistsAsync(roleName);
    }

    public async Task<IEnumerable<ApplicationUser>> GetUsersInRoleAsync(string roleName)
    {
        return await _userManager.GetUsersInRoleAsync(roleName);
    }
}
