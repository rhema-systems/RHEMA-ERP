export const FINANCE_READ_PERMISSION = 'Finance.Read';
export const MANAGE_ACCOUNTING_BOOKS_PERMISSION = 'Finance.AccountingBooks.Manage';
export const TRANSITION_ACCOUNTING_BOOKS_PERMISSION = 'Finance.AccountingBooks.Transitions.Request';
export const APPROVE_ACCOUNTING_BOOKS_PERMISSION = 'Finance.AccountingBooks.Transitions.Approve';

export function getAccountingBookAccess(hasPermission: (permission: string) => boolean) {
    const canRead = hasPermission(FINANCE_READ_PERMISSION);
    return {
        canRead,
        canManage: canRead && hasPermission(MANAGE_ACCOUNTING_BOOKS_PERMISSION),
        canRequestTransition: canRead && hasPermission(TRANSITION_ACCOUNTING_BOOKS_PERMISSION),
        canApproveTransition: canRead && hasPermission(APPROVE_ACCOUNTING_BOOKS_PERMISSION),
    };
}
