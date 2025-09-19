using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Services;

public interface IUserService
{
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task<ApplicationUser?> GetUserByIdAsync(Guid userId);
    Task<ApplicationUser?> GetUserByUsernameAsync(string username);
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
    Task<ApplicationUser> CreateUserAsync(ApplicationUser user, string password);
    Task<ApplicationUser> UpdateUserAsync(ApplicationUser user);
    Task<bool> DeleteUserAsync(Guid userId);
    Task<IEnumerable<string>> GetUserRolesAsync(ApplicationUser user);
    Task<IdentityResult> AddToRolesAsync(ApplicationUser user, IEnumerable<string> roles);
    Task<IdentityResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles);
    Task<IdentityResult> UpdateUserRolesAsync(ApplicationUser user, IEnumerable<string> roles);
    Task<bool> IsUserInRoleAsync(ApplicationUser user, string role);
}

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _userManager.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId)
    {
        return await _userManager.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<ApplicationUser?> GetUserByUsernameAsync(string username)
    {
        return await _userManager.FindByNameAsync(username);
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<ApplicationUser> CreateUserAsync(ApplicationUser user, string password)
    {
        try
        {
            user.Id = Guid.NewGuid();
            user.CreatedAt = DateTime.UtcNow;
            user.SecurityStamp = Guid.NewGuid().ToString();

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create user: {errors}");
            }

            _logger.LogInformation("Created user {Username} with ID {UserId}", user.UserName, user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", user.UserName);
            throw;
        }
    }

    public async Task<ApplicationUser> UpdateUserAsync(ApplicationUser user)
    {
        try
        {
            user.UpdatedAt = DateTime.UtcNow;
            var result = await _userManager.UpdateAsync(user);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to update user: {errors}");
            }

            _logger.LogInformation("Updated user {Username} with ID {UserId}", user.UserName, user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", user.Id);
            throw;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("Deleted user {Username} with ID {UserId}", user.UserName, userId);
                return true;
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Failed to delete user {UserId}: {Errors}", userId, errors);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(ApplicationUser user)
    {
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IdentityResult> AddToRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var validRoles = new List<string>();
        
        foreach (var role in roles)
        {
            if (await _roleManager.RoleExistsAsync(role))
            {
                validRoles.Add(role);
            }
            else
            {
                _logger.LogWarning("Role {RoleName} does not exist", role);
            }
        }

        if (validRoles.Any())
        {
            return await _userManager.AddToRolesAsync(user, validRoles);
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var userRoles = await GetUserRolesAsync(user);
        var rolesToRemove = roles.Intersect(userRoles);

        if (rolesToRemove.Any())
        {
            return await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateUserRolesAsync(ApplicationUser user, IEnumerable<string> newRoles)
    {
        var currentRoles = await GetUserRolesAsync(user);
        
        // Remove roles that are no longer needed
        var rolesToRemove = currentRoles.Except(newRoles);
        var removeResult = await RemoveFromRolesAsync(user, rolesToRemove);
        if (!removeResult.Succeeded)
        {
            return removeResult;
        }

        // Add new roles
        var rolesToAdd = newRoles.Except(currentRoles);
        var addResult = await AddToRolesAsync(user, rolesToAdd);
        
        return addResult;
    }

    public async Task<bool> IsUserInRoleAsync(ApplicationUser user, string role)
    {
        return await _userManager.IsInRoleAsync(user, role);
    }
}