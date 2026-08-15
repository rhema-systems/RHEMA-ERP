import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('journal-batch control currency contract', () => {
    it('loads and locks the tenant base currency instead of accepting arbitrary text', () => {
        // This source-level contract protects an important product boundary: the backend
        // supports base-currency batch control totals, while member journals may still
        // carry foreign-currency evidence. A generic editable currency field would imply
        // functionality that JournalBatchService intentionally rejects.
        const source = readFileSync(
            resolve(process.cwd(), 'src/app/finance/journal-batches/new/page.tsx'),
            'utf8',
        );

        expect(source).toContain('getCurrencies({ isActive: true })');
        expect(source).toContain('currency.isBaseCurrency');
        expect(source).toContain('readOnly');
        expect(source).toContain('Batch control totals are measured in the tenant base currency.');
        expect(source).not.toContain('onChange={(e) => setForm({ ...form, controlCurrencyCode:');
    });
});
