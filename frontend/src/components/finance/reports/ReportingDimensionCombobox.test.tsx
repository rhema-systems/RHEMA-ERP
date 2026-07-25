import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReportingDimensionCombobox } from './ReportingDimensionCombobox';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { SegmentStructure } from '@/types/finance';

vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getReportingSegmentOptions: vi.fn(),
    },
}));

beforeAll(() => {
    class ResizeObserverStub {
        observe() {}
        unobserve() {}
        disconnect() {}
    }
    vi.stubGlobal('ResizeObserver', ResizeObserverStub);
    Element.prototype.scrollIntoView = vi.fn();
});

const lookupDimension: SegmentStructure = {
    id: 'department-id',
    segmentName: 'Department',
    segmentCode: 'DEPT',
    segmentPosition: 1,
    segmentLength: 3,
    dataType: 'Alphanumeric',
    lookupTableRequired: true,
    isMandatory: true,
    isReportingDimension: true,
    isNaturalAccount: false,
    isActive: true,
    lookupValues: [
        {
            id: 'finance',
            segmentStructureId: 'department-id',
            segmentValue: 'FIN',
            description: 'Finance',
            effectiveDate: '2026-01-01',
            isActive: true,
            displayOrder: 1,
            createdAt: '2026-01-01',
            updatedAt: '2026-01-01',
        },
        {
            id: 'operations',
            segmentStructureId: 'department-id',
            segmentValue: 'OPS',
            description: 'Operations',
            effectiveDate: '2026-01-01',
            isActive: true,
            displayOrder: 2,
            createdAt: '2026-01-01',
            updatedAt: '2026-01-01',
        },
    ],
    createdAt: '2026-01-01',
    updatedAt: '2026-01-01',
};

const naturalAccountDimension: SegmentStructure = {
    ...lookupDimension,
    id: 'natural-account-id',
    segmentName: 'Natural Account',
    segmentCode: 'ACCT',
    segmentPosition: 2,
    segmentLength: 4,
    dataType: 'Numeric',
    lookupTableRequired: false,
    isNaturalAccount: true,
    lookupValues: [],
};

describe('ReportingDimensionCombobox', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('searches lookup values by description and commits the selected value', async () => {
        const onValueChange = vi.fn();
        render(
            <ReportingDimensionCombobox
                dimension={lookupDimension}
                value=""
                onValueChange={onValueChange}
            />
        );

        fireEvent.click(screen.getByRole('combobox', { name: 'Filter by Department' }));
        fireEvent.change(await screen.findByPlaceholderText('Search department...'), {
            target: { value: 'Finance' },
        });
        fireEvent.click(await screen.findByText('Finance'));

        expect(onValueChange).toHaveBeenCalledWith('FIN');
    });

    it('debounces account-backed searches instead of accepting arbitrary text', async () => {
        vi.mocked(financeDataService.getReportingSegmentOptions).mockResolvedValue({
            items: [{ segmentValue: '6200', description: 'Utilities Expense', accountCombinationCount: 4 }],
            hasMore: false,
        });
        const onValueChange = vi.fn();
        render(
            <ReportingDimensionCombobox
                dimension={naturalAccountDimension}
                value=""
                onValueChange={onValueChange}
            />
        );

        fireEvent.click(screen.getByRole('combobox', { name: 'Filter by Natural Account' }));
        fireEvent.change(await screen.findByPlaceholderText('Search natural account...'), {
            target: { value: 'util' },
        });

        await waitFor(() => expect(financeDataService.getReportingSegmentOptions).toHaveBeenCalledWith(
            'natural-account-id',
            'util',
            50,
            expect.any(AbortSignal)
        ));
        expect(onValueChange).not.toHaveBeenCalled();
    });
});
