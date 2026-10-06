import { describe, expect, it } from 'vitest';

import {
    CashTransactionType,
    type UnmatchedStatementLine,
    type UnmatchedTransaction,
} from '@/types/cash-management';

import { isReconciliationDirectionCompatible } from './reconciliation-matching';

const transaction = (
    transactionType: CashTransactionType,
    transactionNumber = 'TXN-001',
): UnmatchedTransaction => ({
    id: 'transaction-1',
    transactionNumber,
    transactionDate: '2026-10-05',
    description: 'Test transaction',
    amount: 2880,
    transactionType,
});

const statementLine = (debitAmount: number, creditAmount: number): UnmatchedStatementLine => ({
    id: 'statement-line-1',
    transactionDate: '2026-10-05',
    description: 'Test statement line',
    amount: debitAmount || creditAmount,
    debitAmount,
    creditAmount,
});

describe('isReconciliationDirectionCompatible', () => {
    it.each([
        CashTransactionType.Receipt,
        CashTransactionType.Deposit,
    ])('matches %s to a statement credit', (transactionType) => {
        expect(isReconciliationDirectionCompatible(
            transaction(transactionType),
            statementLine(0, 2880),
        )).toBe(true);
    });

    it.each([
        CashTransactionType.Payment,
        CashTransactionType.ReturnedCheque,
    ])('matches %s to a statement debit', (transactionType) => {
        expect(isReconciliationDirectionCompatible(
            transaction(transactionType),
            statementLine(2880, 0),
        )).toBe(true);
    });

    it('preserves transfer-leg direction', () => {
        expect(isReconciliationDirectionCompatible(
            transaction(CashTransactionType.Transfer, 'TRF-001-IN'),
            statementLine(0, 2880),
        )).toBe(true);
        expect(isReconciliationDirectionCompatible(
            transaction(CashTransactionType.Transfer, 'TRF-001-OUT'),
            statementLine(2880, 0),
        )).toBe(true);
    });

    it('rejects the opposite statement direction', () => {
        expect(isReconciliationDirectionCompatible(
            transaction(CashTransactionType.Deposit),
            statementLine(2880, 0),
        )).toBe(false);
    });
});
