import { describe, expect, it } from 'vitest';
import { isIndependentBankDepositReviewer } from './bank-deposit-access';

describe('bank deposit review access', () => {
    it('fails closed until both reviewer and submitter identities are known', () => {
        expect(isIndependentBankDepositReviewer(undefined, 'maker-1')).toBe(false);
        expect(isIndependentBankDepositReviewer('checker-1', undefined)).toBe(false);
    });

    it('withholds decisions from the submitter even when identifier casing differs', () => {
        expect(isIndependentBankDepositReviewer('USER-1', 'user-1')).toBe(false);
    });

    it('allows a different authenticated reviewer identity', () => {
        expect(isIndependentBankDepositReviewer('checker-1', 'maker-1')).toBe(true);
    });
});
