export const FINANCE_READ_PERMISSION = 'Finance.Read';
export const MANAGE_CODING_DIMENSIONS_PERMISSION = 'Finance.Dimensions.Manage';

export function getDimensionSettingsAccess(hasPermission: (permission: string) => boolean) {
    const canRead = hasPermission(FINANCE_READ_PERMISSION);
    return {
        canRead,
        canManage: canRead && hasPermission(MANAGE_CODING_DIMENSIONS_PERMISSION),
    };
}
