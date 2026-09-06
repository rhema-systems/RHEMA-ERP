import { describe, expect, it } from 'vitest';
import {
    APPROVE_ACCOUNTING_BOOKS_PERMISSION,
    FINANCE_READ_PERMISSION,
    getAccountingBookAccess,
    MANAGE_ACCOUNTING_BOOKS_PERMISSION,
    TRANSITION_ACCOUNTING_BOOKS_PERMISSION,
} from './accounting-book-access';

describe('accounting book access', () => {
    it('requires Finance.Read before any settings action is exposed', () => {
        const actionOnly = new Set([
            MANAGE_ACCOUNTING_BOOKS_PERMISSION,
            TRANSITION_ACCOUNTING_BOOKS_PERMISSION,
            APPROVE_ACCOUNTING_BOOKS_PERMISSION,
        ]);
        expect(getAccountingBookAccess(permission => actionOnly.has(permission))).toEqual({
            canRead: false,
            canManage: false,
            canRequestTransition: false,
            canApproveTransition: false,
            canManagePeriods: false,
            canApprovePeriods: false,
            canManageInitialization: false,
            canApproveInitialization: false,
        });
    });

    it('treats management, transition and approval as separate action grants', () => {
        const granted = new Set([FINANCE_READ_PERMISSION, MANAGE_ACCOUNTING_BOOKS_PERMISSION]);
        expect(getAccountingBookAccess(permission => granted.has(permission))).toEqual({
            canRead: true,
            canManage: true,
            canRequestTransition: false,
            canApproveTransition: false,
            canManagePeriods: false,
            canApprovePeriods: false,
            canManageInitialization: false,
            canApproveInitialization: false,
        });
    });
});
