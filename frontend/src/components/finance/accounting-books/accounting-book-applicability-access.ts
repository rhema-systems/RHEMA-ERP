export const VIEW_BOOK_APPLICABILITY_PERMISSION = 'Finance.AccountingBooks.ApplicabilityPolicy.Read';
export const MANAGE_BOOK_APPLICABILITY_PERMISSION = 'Finance.AccountingBooks.ApplicabilityPolicy.Manage';
export const APPROVE_BOOK_APPLICABILITY_PERMISSION = 'Finance.AccountingBooks.ApplicabilityPolicy.Approve';
export const RESOLVE_BOOK_APPLICABILITY_PERMISSION = 'Finance.AccountingBooks.Applicability.Resolve';

export function getAccountingBookApplicabilityAccess(hasPermission: (permission: string) => boolean) {
    const canRead = hasPermission(VIEW_BOOK_APPLICABILITY_PERMISSION);
    return {
        canRead,
        canManage: canRead && hasPermission(MANAGE_BOOK_APPLICABILITY_PERMISSION),
        canApprove: canRead && hasPermission(APPROVE_BOOK_APPLICABILITY_PERMISSION),
        canResolve: canRead && hasPermission(RESOLVE_BOOK_APPLICABILITY_PERMISSION),
    };
}
