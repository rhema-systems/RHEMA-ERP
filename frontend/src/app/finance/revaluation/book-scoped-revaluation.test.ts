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
});
