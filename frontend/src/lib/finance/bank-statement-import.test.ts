import * as XLSX from 'xlsx';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

import { parseBankStatementImportFile } from './bank-statement-import';

const makeFile = (content: string | ArrayBuffer | Uint8Array, name: string, type: string) => {
    const bytes = typeof content === 'string'
        ? new TextEncoder().encode(content)
        : content instanceof ArrayBuffer
            ? new Uint8Array(content)
            : content;
    return {
        name,
        type,
        size: bytes.byteLength,
        lastModified: Date.now(),
        arrayBuffer: async () => bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    } as File;
};

describe('bank statement import', () => {
    it('accepts the downloadable workbook shipped with the application', async () => {
        const template = await readFile(path.join(process.cwd(), 'public', 'templates', 'bank-statement-import-template.xlsx'));
        const file = makeFile(Uint8Array.from(template).buffer, 'bank-statement-import-template.xlsx', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');

        const result = await parseBankStatementImportFile(file);

        expect(result.errors).toEqual([]);
        expect(result.rowCount).toBe(3);
        expect(result.closingBalance).toBe(1250);
    });

    it('normalizes the Statement Lines worksheet into the backend CSV contract', async () => {
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([
            ['Instructions only'],
        ]), 'Instructions');
        XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([
            ['Date', 'Description', 'Reference', 'Debit', 'Credit', 'Balance'],
            ['2026-06-01', 'Opening deposit', 'DEP-1', '', 1000, 1000],
            ['2026-06-05', 'Supplier payment', 'CHQ-88', 250, '', 750],
        ]), 'Statement Lines');
        const buffer = XLSX.write(workbook, { type: 'array', bookType: 'xlsx' });
        const file = makeFile(buffer, 'june-statement.xlsx', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');

        const result = await parseBankStatementImportFile(file);

        expect(result.errors).toEqual([]);
        expect(result.rowCount).toBe(2);
        expect(result.statementDate).toBe('2026-06-05');
        expect(result.closingBalance).toBe(750);
        expect(result.file?.name).toBe('june-statement-normalized.csv');
    });

    it('reports invalid debit/credit combinations and running balances', async () => {
        const csv = [
            'Date,Description,Reference,Debit,Credit,Balance',
            '2026-06-01,Opening deposit,DEP-1,100,100,1000',
            '2026-06-02,Fee,FEE-1,25,,990',
        ].join('\n');
        const file = makeFile(csv, 'invalid.csv', 'text/csv');

        const result = await parseBankStatementImportFile(file);

        expect(result.file).toBeUndefined();
        expect(result.errors).toContain('Row 2: enter a positive amount in either Debit or Credit, but not both.');
        expect(result.errors).toContain('Row 3: Balance should be 975.00 based on the previous row, debit, and credit.');
    });

    it('accepts common bank header aliases and day-first dates', async () => {
        const csv = [
            'Transaction Date,Narrative,Reference Number,Withdrawal,Deposit,Running Balance',
            '01/06/2026,Opening deposit,DEP-1,,1000,1000',
            '05/06/2026,Supplier payment,CHQ-88,250,,750',
        ].join('\n');
        const file = makeFile(csv, 'bank-export.csv', 'text/csv');

        const result = await parseBankStatementImportFile(file);

        expect(result.errors).toEqual([]);
        expect(result.rowCount).toBe(2);
        expect(result.statementDate).toBe('2026-06-05');
    });
});
