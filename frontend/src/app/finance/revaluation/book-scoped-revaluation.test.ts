import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const source = readFileSync(resolve(process.cwd(), 'src/app/finance/revaluation/page.tsx'), 'utf8');

describe('book-scoped revaluation workspace', () => {
    it('requires an API-driven posting book for preview and posting', () => {
        expect(source).toContain('financeDataService.getAccountingBooks(false)');
        expect(source).toContain('book.isActive && book.allowsPosting');
        expect(source).toContain('accountingBookCode: parameters.accountingBookCode');
        expect(source).toContain('!parameters.accountingBookCode');
        expect(source).not.toContain("accountingBookCode: 'IFRS'");
    });

    it('shows frozen book and governance warning evidence', () => {
        expect(source).toContain('preview.accountingBookCode');
        expect(source).toContain('preview.accountingBookName');
        expect(source).toContain('line.hasGovernanceWarning');
        expect(source).toContain('line.governanceWarning');
        expect(source).toContain('batch.nonstandardPolicyCount');
    });

    it('distinguishes history loading, failure with retry, and a successful empty result', () => {
        expect(source).toContain('historyLoading ?');
        expect(source).toContain('historyError ?');
        expect(source).toContain('Revaluation history could not be loaded');
        expect(source).toContain('Retry history');
        expect(source).toContain('No revaluation batches found this year.');
    });

    it('requires Finance.Read for the workspace and the run permission for mutations', () => {
        expect(source).toContain('getFxRevaluationAccess(hasPermission)');
        expect(source).toContain('Finance.Read is required');
        expect(source).toContain('canRun ? <Button onClick={handlePost}');
        expect(source).toContain("canRun && batch.status === 'Posted'");
    });
});
