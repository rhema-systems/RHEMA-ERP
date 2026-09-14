export const VIEW_FINANCE_PERMISSION = 'Finance.Read';
export const MANAGE_ACCOUNT_STRUCTURE_PERMISSION = 'Finance.Policy.ConfigureChartOfAccounts';

export const getSegmentAccess = (hasPermission: (permission: string) => boolean) => ({
  canRead: hasPermission(VIEW_FINANCE_PERMISSION),
  canManage: hasPermission(VIEW_FINANCE_PERMISSION) && hasPermission(MANAGE_ACCOUNT_STRUCTURE_PERMISSION),
});
