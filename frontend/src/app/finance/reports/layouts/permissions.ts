export const FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS = {
    read: 'Finance.Read',
    manage: 'Finance.Reports.Layouts.Manage',
    publish: 'Finance.Reports.Layouts.Publish',
    run: 'Finance.Reports.Run',
} as const;

export function resolveFinancialStatementLayoutPermissions(
    hasPermission: (permission: string) => boolean,
) {
    const canRead = hasPermission(FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS.read);
    return {
        canRead,
        canManage: canRead && hasPermission(FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS.manage),
        canPublish: canRead && hasPermission(FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS.publish),
        canRun: canRead && hasPermission(FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS.run),
    };
}
