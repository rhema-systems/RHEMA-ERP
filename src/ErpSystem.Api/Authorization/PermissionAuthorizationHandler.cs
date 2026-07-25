using System.Security.Claims;
using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        ApplicationDbContext db,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (requirement.Permissions.Count == 0)
        {
            return;
        }

        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!TryGetUserId(principal, out var userId) || !TryGetTenantId(principal, out var tenantId))
        {
            _logger.LogWarning("Permission authorization failed because user or tenant claim was missing or invalid.");
            return;
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId);

        if (user == null || !user.IsActive)
        {
            return;
        }

        if (!await HasTenantAccessAsync(user, tenantId))
        {
            _logger.LogWarning(
                "Permission authorization denied for user {UserId}: tenant {TenantId} is not in the user's active tenant scope.",
                userId,
                tenantId);
            return;
        }

        if (principal.IsInRole(Constants.Roles.SuperAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var requiredPermissions = requirement.Permissions
            .Select(permission => permission.ToUpperInvariant())
            .ToArray();

        var hasPermission = await _db.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .AnyAsync(userRole => userRole.Role.RolePermissions
                .Any(rolePermission => requiredPermissions.Contains(rolePermission.Permission.Name.ToUpper())));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> HasTenantAccessAsync(ApplicationUser user, Guid tenantId)
    {
        if (user.TenantId == tenantId)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        return await _db.UserTenants
            .AsNoTracking()
            .AnyAsync(userTenant =>
                userTenant.UserId == user.Id &&
                userTenant.TenantId == tenantId &&
                !userTenant.IsDeleted &&
                userTenant.Status == UserTenantStatus.Active &&
                (userTenant.ExpiresAt == null || userTenant.ExpiresAt > now));
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static bool TryGetTenantId(ClaimsPrincipal principal, out Guid tenantId)
        => Guid.TryParse(
            principal.FindFirstValue(Constants.Claims.TenantId) ??
            principal.FindFirstValue("tenant_id"),
            out tenantId);
}
