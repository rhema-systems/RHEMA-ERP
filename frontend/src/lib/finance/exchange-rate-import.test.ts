import { describe, expect, it } from 'vitest';

import {
    buildExchangeRateTemplateCsv,
    parseExchangeRateImportFile,
} from './exchange-rate-import';

const makeCsvFile = (content: string, name = 'exchange-rates.csv') => {
    const bytes = new TextEncoder().encode(content);
    return {
        name,
        type: 'text/csv',
        size: bytes.byteLength,
        lastModified: Date.now(),
        arrayBuffer: async () => bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    } as File;
};

const headers = [
    'baseCurrencyCode,targetCurrencyCode,rate,effectiveDate,rateType,rateSource,sourceName,sourceReference,expiryDate,isActive,approvalStatus',
];

describe('exchange-rate import Ghana statutory controls', () => {
    it('ships directionally correct target-per-base examples', () => {
        const csv = buildExchangeRateTemplateCsv('2026-09-29');

        expect(csv).toContain('"GHS","USD","0.0800"');
        expect(csv).toContain('"GHS","GBP","0.063291","2026-09-29","GhanaStatutory"');
        expect(csv).not.toContain('"GHS","USD","12.5000"');
    });

    it('accepts an exact-date Bank of Ghana statutory rate with retained source evidence', async () => {
        const csv = [
            ...headers,
            'GHS,USD,0.08,2026-09-29,GhanaStatutory,Bank of Ghana,Bank of Ghana,BOG-INTERBANK-2026-09-29,,TRUE,Approved',
        ].join('\n');

        const result = await parseExchangeRateImportFile(makeCsvFile(csv));

        expect(result.errors).toEqual([]);
        expect(result.rows).toEqual([
            expect.objectContaining({
                baseCurrencyCode: 'GHS',
                targetCurrencyCode: 'USD',
                rate: 0.08,
                effectiveDate: '2026-09-29',
                rateType: 'GhanaStatutory',
                rateSource: 'Bank of Ghana',
                sourceReference: 'BOG-INTERBANK-2026-09-29',
            }),
        ]);
    });

    it('rejects Ghana statutory rows without Bank of Ghana provenance and a source reference', async () => {
        const csv = [
            ...headers,
            'GHS,USD,0.08,2026-09-29,GhanaStatutory,Manual,,,,TRUE,Approved',
        ].join('\n');

        const result = await parseExchangeRateImportFile(makeCsvFile(csv));

        expect(result.rows).toEqual([]);
        expect(result.errors).toContain('Row 2: GhanaStatutory rateSource must identify Bank of Ghana.');
        expect(result.errors).toContain('Row 2: GhanaStatutory sourceReference is required.');
    });
});
