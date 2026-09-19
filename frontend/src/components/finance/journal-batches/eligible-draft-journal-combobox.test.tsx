import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { EligibleDraftJournalCombobox } from './eligible-draft-journal-combobox';
import type { EligibleJournalBatchDraft } from '@/types/journal-batches';

Object.assign(globalThis, { React });
Object.assign(globalThis, {
    ResizeObserver: class ResizeObserver {
        observe() {}
        unobserve() {}
        disconnect() {}
    },
});
Object.defineProperty(Element.prototype, 'scrollIntoView', {
    configurable: true,
    value: vi.fn(),
});

const journal: EligibleJournalBatchDraft = {
    id: 'journal-1',
    journalEntryNumber: 'JE-2026-000123',
    entryDate: '2026-09-02T00:00:00',
    description: 'Month-end accrual',
    referenceNumber: 'ACCRUAL-SEP',
    totalDebit: 1250,
    totalCredit: 1250,
    lineCount: 4,
};

describe('EligibleDraftJournalCombobox', () => {
    it('shows searchable journal evidence and returns the selected draft', () => {
        const onSelect = vi.fn();
        const onSearchChange = vi.fn();
        const onOpenChange = vi.fn();
        const { rerender } = render(
            <EligibleDraftJournalCombobox
                options={[journal]}
                search=""
                onSearchChange={onSearchChange}
                onSelect={onSelect}
                open={false}
                onOpenChange={onOpenChange}
                loading={false}
                currencyCode="GHS"
            />,
        );

        fireEvent.click(screen.getByRole('combobox', { name: 'Eligible draft journal' }));
        expect(onOpenChange).toHaveBeenCalledWith(true);

        rerender(
            <EligibleDraftJournalCombobox
                options={[journal]}
                search=""
                onSearchChange={onSearchChange}
                onSelect={onSelect}
                open
                onOpenChange={onOpenChange}
                loading={false}
                currencyCode="GHS"
            />,
        );

        fireEvent.change(screen.getByPlaceholderText('Search journal number, reference, or description...'), {
            target: { value: 'ACCRUAL' },
        });
        expect(onSearchChange).toHaveBeenCalledWith('ACCRUAL');
        expect(screen.getByText('JE-2026-000123')).toBeInTheDocument();
        expect(screen.getByText('Month-end accrual')).toBeInTheDocument();
        expect(screen.getByText(/ACCRUAL-SEP · 4 lines/)).toBeInTheDocument();

        fireEvent.click(screen.getByText('Month-end accrual'));
        expect(onSelect).toHaveBeenCalledWith(journal);
        expect(onOpenChange).toHaveBeenCalledWith(false);
    });

    it('explains when no attachable drafts exist', () => {
        render(
            <EligibleDraftJournalCombobox
                options={[]}
                search=""
                onSearchChange={vi.fn()}
                onSelect={vi.fn()}
                open
                onOpenChange={vi.fn()}
                loading={false}
                currencyCode="GHS"
            />,
        );

        expect(screen.getByText('No eligible draft journals were found for this period and accounting book.')).toBeInTheDocument();
    });
});
