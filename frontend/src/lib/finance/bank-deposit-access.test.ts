import { describe, expect, it } from 'vitest';
import {
    canRecordBankDepositAcknowledgement,
    isIndependentBankDepositReviewer,
} from './bank-deposit-access';

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

    it('withholds bank acknowledgement from the submitter even when permission is present', () => {
        expect(canRecordBankDepositAcknowledgement('maker-1', 'maker-1', true)).toBe(false);
    });

    it('requires both confirmation permission and an independent identity', () => {
        expect(canRecordBankDepositAcknowledgement('checker-1', 'maker-1', false)).toBe(false);
        expect(canRecordBankDepositAcknowledgement('checker-1', 'maker-1', true)).toBe(true);
        expect(canRecordBankDepositAcknowledgement('checker-1', undefined, true)).toBe(false);
    });
});
