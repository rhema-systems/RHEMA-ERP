using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Procurement;

internal static class ProcurementRolePermissionEvaluator
{
    public static bool HasRegisteredProcurementPermission(
        this ICurrentUserProvider currentUser,
        string permissionCode) =>
        currentUser.Roles
            .Select(ProcurementAccessControlRegistry.FindRole)
            .Where(role => role is not null)
            .Any(role => role!.PermissionCodes.Contains(
                permissionCode,
                StringComparer.OrdinalIgnoreCase));
}
