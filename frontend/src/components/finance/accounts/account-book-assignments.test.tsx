import React, { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AccountBookAssignments } from './account-book-assignments';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountBookAssignmentInput, AccountClassification, AccountingBook } from '@/types/finance';

vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getAccountingBooks: vi.fn(),
        getAccountClassifications: vi.fn(),
    },
}));

const book = {
    id: 'book-1', code: 'IFRS', name: 'IFRS', isActive: true, isDefault: true,
    allowsPosting: true, bookType: 'PrimaryFull', lifecycleStatus: 'Active',
} as AccountingBook;

const classification = {
    id: 'class-1', accountingBookId: 'book-1', code: 'EXPENSE', name: 'Expense',
    coreAccountType: 'Expense', status: 'Active', isPostingClassification: true, isLeaf: true,
} as AccountClassification;

function Harness() {
    const [value, setValue] = useState<AccountBookAssignmentInput[]>([{
        accountingBookId: 'book-1', accountClassificationId: 'class-1',
        isEnabled: true, rowVersion: 'row-version-1',
    }]);
    return <>
        <AccountBookAssignments accountType="Expense" value={value} onChange={setValue} />
        <output data-testid="value">{JSON.stringify(value)}</output>
    </>;
}

describe('AccountBookAssignments', () => {
    beforeEach(() => {
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([book]);
        vi.mocked(financeDataService.getAccountClassifications).mockResolvedValue([classification]);
    });
    afterEach(() => { cleanup(); vi.clearAllMocks(); });

    it('retains an existing assignment as disabled so its history and row version are preserved', async () => {
        render(<Harness />);
        const checkbox = await screen.findByRole('checkbox', { name: /IFRS/ });
        fireEvent.click(checkbox);

        await waitFor(() => expect(screen.getByTestId('value').textContent).toContain('"isEnabled":false'));
        expect(screen.getByTestId('value').textContent).toContain('"rowVersion":"row-version-1"');
        expect(screen.getByTestId('value').textContent).toContain('"accountClassificationId":"class-1"');
    });
});
