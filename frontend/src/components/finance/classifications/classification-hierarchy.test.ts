import { describe, expect, it } from 'vitest';
import type { AccountClassification } from '@/types/finance';
import { filterClassificationHierarchy, flattenClassificationHierarchy } from './classification-hierarchy';

const item = (id: string, code: string, parentClassificationId: string | null, displayOrder: number): AccountClassification => ({
    id, code, parentClassificationId, displayOrder, accountingBookId: 'book', accountingBookCode: 'IFRS',
    name: code, coreAccountType: 'Asset', defaultRevaluationTreatment: 'Exclude', isPostingClassification: id !== 'root',
    status: 'Active', rowVersion: '', childCount: id === 'root' ? 2 : 0, nonRetiredChildCount: id === 'root' ? 2 : 0, totalAccountCount: 0,
    enabledAccountCount: 0, isLeaf: id !== 'root', canRetire: id !== 'root', hasDraftLayoutReference: false,
});

describe('classification hierarchy', () => {
    it('orders parents before their children and respects display order', () => {
        const rows = flattenClassificationHierarchy([
            item('b', 'BANK', 'root', 20), item('root', 'ASSETS', null, 10), item('a', 'CASH', 'root', 10),
        ]);
        expect(rows.map(row => [row.classification.code, row.depth])).toEqual([
            ['ASSETS', 0], ['CASH', 1], ['BANK', 1],
        ]);
    });

    it('filters by stable code, status and core account type', () => {
        const rows = flattenClassificationHierarchy([item('root', 'ASSETS', null, 10), item('a', 'CASH', 'root', 10)]);
        expect(filterClassificationHierarchy(rows, 'cash', 'Active', 'Asset').map(row => row.classification.code)).toEqual(['CASH']);
        expect(filterClassificationHierarchy(rows, '', 'Retired', 'all')).toEqual([]);
    });
});
