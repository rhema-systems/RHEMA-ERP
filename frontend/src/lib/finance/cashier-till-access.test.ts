import { describe, expect, it } from 'vitest';
import { canManageUnusedTill, isIndependentTillReviewer } from './cashier-till-access';

describe('cashier till review access', () => {
    it('fails closed until both reviewer and cashier identities are known', () => {
        expect(isIndependentTillReviewer(undefined, 'cashier-1')).toBe(false);
        expect(isIndependentTillReviewer('reviewer-1', undefined)).toBe(false);
    });

    it('withholds review authority from the cashier even when identifier casing differs', () => {
        expect(isIndependentTillReviewer('USER-1', 'user-1')).toBe(false);
    });

    it('allows a different authenticated reviewer identity', () => {
        expect(isIndependentTillReviewer('reviewer-1', 'cashier-1')).toBe(true);
    });
});

describe('cashier till unused-session management access', () => {
    it('requires the operating permission, owner identity, and server mutability decision', () => {
        expect(canManageUnusedTill('cashier-1', 'cashier-1', true, true)).toBe(true);
        expect(canManageUnusedTill('cashier-1', 'cashier-1', false, true)).toBe(false);
        expect(canManageUnusedTill('cashier-1', 'cashier-1', true, false)).toBe(false);
        expect(canManageUnusedTill('other-user', 'cashier-1', true, true)).toBe(false);
    });

    it('fails closed when either identity is unavailable', () => {
        expect(canManageUnusedTill(undefined, 'cashier-1', true, true)).toBe(false);
        expect(canManageUnusedTill('cashier-1', undefined, true, true)).toBe(false);
    });
});
