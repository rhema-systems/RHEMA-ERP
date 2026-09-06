import { describe, expect, it } from 'vitest';
import {
    APPROVE_BOOK_APPLICABILITY_PERMISSION,
    getAccountingBookApplicabilityAccess,
    MANAGE_BOOK_APPLICABILITY_PERMISSION,
    RESOLVE_BOOK_APPLICABILITY_PERMISSION,
    VIEW_BOOK_APPLICABILITY_PERMISSION,
} from './accounting-book-applicability-access';

describe('accounting book applicability access', () => {
    it('does not treat action or Finance.Read grants as policy read authority', () => {
        const grants = new Set(['Finance.Read', MANAGE_BOOK_APPLICABILITY_PERMISSION, APPROVE_BOOK_APPLICABILITY_PERMISSION, RESOLVE_BOOK_APPLICABILITY_PERMISSION]);
        expect(getAccountingBookApplicabilityAccess(permission => grants.has(permission))).toEqual({
            canRead: false, canManage: false, canApprove: false, canResolve: false,
        });
    });

    it('keeps policy management, approval and resolution independent', () => {
        const grants = new Set([VIEW_BOOK_APPLICABILITY_PERMISSION, MANAGE_BOOK_APPLICABILITY_PERMISSION]);
        expect(getAccountingBookApplicabilityAccess(permission => grants.has(permission))).toEqual({
            canRead: true, canManage: true, canApprove: false, canResolve: false,
        });
    });
});
