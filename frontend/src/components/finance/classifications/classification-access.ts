export const MANAGE_CHART_OF_ACCOUNTS_PERMISSION = 'Finance.ChartOfAccounts.Manage';

export function canConfigureClassifications(hasPermission: (permission: string) => boolean): boolean {
    return hasPermission(MANAGE_CHART_OF_ACCOUNTS_PERMISSION);
}
