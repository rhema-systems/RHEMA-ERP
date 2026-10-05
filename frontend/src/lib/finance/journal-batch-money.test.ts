import { describe, expect, it } from 'vitest';
import { formatJournalBatchMoney } from './journal-batch-money';

describe('formatJournalBatchMoney', () => {
    it('formats valid ISO currency codes', () => {
        expect(formatJournalBatchMoney(1000, 'GHS')).toContain('1,000.00');
    });

    it.each([undefined, null, '', '  ', ':', 'GH'])(
        'falls back to a plain amount for invalid currency code %s',
        (currencyCode) => {
            expect(() => formatJournalBatchMoney(1000, currencyCode)).not.toThrow();
            expect(formatJournalBatchMoney(1000, currencyCode)).toBe('1,000.00');
        },
    );
});
