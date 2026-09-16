import { describe, expect, it } from 'vitest';
import {
    appendJournalLine,
    createFreshManualJournalLine,
    createFreshUnitJournalLine,
} from './journal-line-addition';

describe('journal line addition', () => {
    it('keeps the first manual and unit lines blank and at fresh defaults', () => {
        expect(createFreshManualJournalLine('manual-1', 'GHS')).toEqual({
            id: 'manual-1',
            accountId: '',
            description: '',
            currencyCode: 'GHS',
            exchangeRate: 1,
            debit: 0,
            credit: 0,
            rateStatus: 'ready',
            dimensions: {},
        });
        expect(createFreshUnitJournalLine('unit-1')).toEqual({
            id: 'unit-1',
            unitAccountId: '',
            quantity: '',
            description: '',
        });
    });

    it('copies only the immediately preceding manual line description', () => {
        const earlier = {
            ...createFreshManualJournalLine('manual-1', 'GHS', 'Earlier description'),
            accountId: 'account-1',
            debit: 125,
            currencyCode: 'USD',
            exchangeRate: 15.5,
            dimensions: { COST_CENTER: 'OPS' },
            exchangeRateId: 'rate-1',
            foreignDebit: 8.06,
        };
        const preceding = {
            ...earlier,
            id: 'manual-2',
            description: 'Immediately preceding description',
            accountId: 'account-2',
        };

        const result = appendJournalLine(
            [earlier, preceding],
            description => createFreshManualJournalLine('manual-3', 'GHS', description),
        );

        expect(result[2]).toEqual({
            id: 'manual-3',
            accountId: '',
            description: 'Immediately preceding description',
            currencyCode: 'GHS',
            exchangeRate: 1,
            debit: 0,
            credit: 0,
            rateStatus: 'ready',
            dimensions: {},
        });
        expect(result[2]).not.toHaveProperty('exchangeRateId');
        expect(result[2]).not.toHaveProperty('foreignDebit');
    });

    it('does not search backward when the immediately preceding description is blank', () => {
        const result = appendJournalLine(
            [
                createFreshManualJournalLine('manual-1', 'GHS', 'Do not copy'),
                createFreshManualJournalLine('manual-2', 'GHS', ''),
            ],
            description => createFreshManualJournalLine('manual-3', 'GHS', description),
        );

        expect(result[2].description).toBe('');
    });

    it('uses the current preceding state for rapid consecutive manual additions', () => {
        let state = [createFreshManualJournalLine('manual-1', 'GHS', 'Current description')];
        const add = (id: string) => {
            state = appendJournalLine(
                state,
                description => createFreshManualJournalLine(id, 'GHS', description),
            );
        };

        add('manual-2');
        state[1].description = 'Latest description';
        add('manual-3');

        expect(state.map(line => line.description)).toEqual([
            'Current description',
            'Latest description',
            'Latest description',
        ]);
    });

    it('gives Unit Journal the same description-only behavior', () => {
        const preceding = {
            ...createFreshUnitJournalLine('unit-1', 'Units received'),
            unitAccountId: 'unit-account-1',
            quantity: '40',
        };

        const result = appendJournalLine(
            [preceding],
            description => createFreshUnitJournalLine('unit-2', description),
        );

        expect(result[1]).toEqual({
            id: 'unit-2',
            unitAccountId: '',
            quantity: '',
            description: 'Units received',
        });
    });
});
