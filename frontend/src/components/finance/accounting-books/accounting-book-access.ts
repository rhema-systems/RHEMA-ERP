export const FINANCE_READ_PERMISSION = 'Finance.Read';
export const MANAGE_ACCOUNTING_BOOKS_PERMISSION = 'Finance.AccountingBooks.Manage';
export const TRANSITION_ACCOUNTING_BOOKS_PERMISSION = 'Finance.AccountingBooks.Transitions.Request';
export const APPROVE_ACCOUNTING_BOOKS_PERMISSION = 'Finance.AccountingBooks.Transitions.Approve';
export const MANAGE_BOOK_PERIODS_PERMISSION = 'Finance.AccountingBooks.Periods.Manage';
export const APPROVE_BOOK_PERIODS_PERMISSION = 'Finance.AccountingBooks.Periods.Approve';
export const MANAGE_BOOK_INITIALIZATION_PERMISSION = 'Finance.AccountingBooks.Initialization.Manage';
export const APPROVE_BOOK_INITIALIZATION_PERMISSION = 'Finance.AccountingBooks.Initialization.Approve';

// A visible permission is not maker-checker authority. Withhold decision controls
// until both actor and maker identities are known and differ; the API checks again.
export function isIndependentChecker(actorId?: string | null, makerId?: string | null) {
    return Boolean(actorId && makerId && actorId.toLowerCase() !== makerId.toLowerCase());
}

export function getAccountingBookAccess(hasPermission: (permission: string) => boolean) {
    const canRead = hasPermission(FINANCE_READ_PERMISSION);
    return {
        canRead,
        canManage: canRead && hasPermission(MANAGE_ACCOUNTING_BOOKS_PERMISSION),
        canRequestTransition: canRead && hasPermission(TRANSITION_ACCOUNTING_BOOKS_PERMISSION),
        canApproveTransition: canRead && hasPermission(APPROVE_ACCOUNTING_BOOKS_PERMISSION),
        canManagePeriods: canRead && hasPermission(MANAGE_BOOK_PERIODS_PERMISSION),
        canApprovePeriods: canRead && hasPermission(APPROVE_BOOK_PERIODS_PERMISSION),
        canManageInitialization: canRead && hasPermission(MANAGE_BOOK_INITIALIZATION_PERMISSION),
        canApproveInitialization: canRead && hasPermission(APPROVE_BOOK_INITIALIZATION_PERMISSION),
    };
}
