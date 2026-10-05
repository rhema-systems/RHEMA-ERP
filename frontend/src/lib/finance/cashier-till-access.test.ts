import { describe, expect, it } from 'vitest';
import { isIndependentTillReviewer } from './cashier-till-access';

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
