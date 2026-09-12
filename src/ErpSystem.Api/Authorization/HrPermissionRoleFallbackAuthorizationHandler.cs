using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;

namespace ErpSystem.Api.Authorization;

/// <summary>
/// Grants HR permissions to established HR roles, alongside the database-backed
/// <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
/// <remarks>
/// <para>ASP.NET runs every handler registered for a requirement and one <c>Succeed</c> is
/// enough, so registering this second handler yields "role OR database permission" without
/// modifying the existing handler.</para>
///
/// <para>It exists because permissions resolve from <c>UserRoles → RolePermissions</c> in the
/// database. A tenant provisioned before the HR permission seed has no rows for these
/// permissions, so introducing the medical policies would lock out every HR user except
/// SuperAdmin on deploy. This keeps those users working while the seed propagates.</para>
///
/// <para>Deliberately scoped to permissions beginning <c>HR.</c> so Finance authorization —
/// which has a fully seeded permission set — is untouched.</para>
///
/// <para><b>It grants exactly what <see cref="HrPermissions.RoleGrants"/> says the role would
/// have been seeded, and nothing more.</b> An earlier version succeeded on the <c>HR.</c> prefix
/// alone without inspecting the verb, so an HR-role user holding only Read and Write also
/// satisfied <c>MedicalAdminPolicy</c> and could delete medical records — including paid expense
/// claims. Standing in for the seed means matching the seed; both sides read the same map.</para>
/// </remarks>
public sealed class HrPermissionRoleFallbackAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (requirement.Permissions.Count == 0)
            return Task.CompletedTask;

        // Only stand in for HR permissions; never widen another module's policy.
        var isHrRequirement = requirement.Permissions.All(permission =>
            permission.StartsWith(HrPermissions.Prefix, StringComparison.Ordinal));
        if (!isHrRequirement)
            return Task.CompletedTask;

        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        // Everything the caller's roles would have been granted by the seed.
        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (roleName, permissions) in HrPermissions.RoleGrants)
        {
            if (principal.IsInRole(roleName))
                granted.UnionWith(permissions);
        }

        // Holding any one of a requirement's permissions satisfies it, matching the
        // database-backed PermissionAuthorizationHandler's own OR semantics.
        if (requirement.Permissions.Any(granted.Contains))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
