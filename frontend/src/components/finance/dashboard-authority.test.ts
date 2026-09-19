import { describe, expect, it } from 'vitest';
import { filterDashboardEvidence, resolveDashboardBook } from './dashboard-authority';
import type { Account, AccountingBook, JournalEntry } from '@/types/finance';

const book = (code: string, isDefault: boolean): AccountingBook => ({
    id: code, tenantId: 'tenant', code, name: code, purpose: 'Reporting', isActive: true,
    isDefault, allowsPosting: true, isSystemDefined: true, sortOrder: 1,
});

describe('finance dashboard book authority', () => {
    it('fails closed when default-book authority is missing or ambiguous', () => {
        expect(resolveDashboardBook([book('IFRS', false)])).toHaveProperty('error');
        expect(resolveDashboardBook([book('IFRS', true), book('LOCAL', true)])).toHaveProperty('error');
    });

    it('filters journals and account mappings to the exact same selected book', () => {
        const entries = [
            { id: 'i', bookClassification: 'IFRS' },
            { id: 'l', bookClassification: 'LOCAL' },
        ] as JournalEntry[];
        const accounts = [{ id: 'a', accountingBooks: [
            { id: 'ai', accountingBookCode: 'IFRS', isEnabled: true, accountClassificationSystemRole: 'Cash' },
            { id: 'al', accountingBookCode: 'LOCAL', isEnabled: true, accountClassificationSystemRole: 'Bank' },
        ] }] as Account[];

        const result = filterDashboardEvidence(entries, accounts, 'IFRS');
        expect(result.entries.map(item => item.id)).toEqual(['i']);
        expect(result.accounts[0].accountingBooks?.map(item => item.id)).toEqual(['ai']);
    });
});
