import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import NewAccountPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings, SegmentStructure } from '@/types/finance';

const { toast, push } = vi.hoisted(() => ({ toast: vi.fn(), push: vi.fn() }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast }) }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push, back: vi.fn() }) }));
vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getFinanceSettings: vi.fn(), getSegmentStructures: vi.fn(), createAccount: vi.fn(),
    },
}));

const segment = (id: string, segmentName: string, segmentPosition: number, segmentLength: number,
    isActive = true): SegmentStructure => ({
    id, segmentName, segmentCode: id, segmentPosition, segmentLength,
    isActive, isMandatory: true, dataType: 'Numeric', separatorCharacter: '-',
    lookupTableRequired: false, isReportingDimension: false, isNaturalAccount: id === 'natural',
    lookupValues: [], lookupValueCount: 0, createdAt: '', updatedAt: '',
});

beforeAll(() => {
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
});
afterAll(() => vi.unstubAllGlobals());
beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
        coaType: 'Segmented', accountSeparator: '-', baseCurrency: 'GHS',
    } as FinanceSettings);
    vi.mocked(financeDataService.getSegmentStructures).mockResolvedValue([
        segment('fund', 'Fund', 1, 3, false),
        segment('project', 'Project', 3, 4),
        segment('department', 'Department', 1, 3),
        segment('natural', 'Natural Account', 2, 4),
    ]);
    vi.mocked(financeDataService.createAccount).mockResolvedValue({} as never);
});
afterEach(cleanup);

async function fillAccount() {
    render(<NewAccountPage />);
    await screen.findByRole('heading', { name: 'New Segmented Account' });
    fireEvent.change(screen.getByPlaceholderText('Enter Department value...'), { target: { value: '000' } });
    fireEvent.change(screen.getByPlaceholderText('Enter Natural Account value...'), { target: { value: '1210' } });
    fireEvent.change(screen.getByPlaceholderText('Enter Project value...'), { target: { value: '0000' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Account Name *' }), { target: { value: 'Supplier Returns Clearing' } });
}

describe('New account active segment structure', () => {
    it('excludes inactive mandatory segments from the form and generated account number', async () => {
        await fillAccount();
        expect(screen.queryByText('Fund')).not.toBeInTheDocument();
        expect(screen.queryByPlaceholderText('Enter Fund value...')).not.toBeInTheDocument();
        expect(screen.getByRole('textbox', { name: 'Account Number * Auto-generated' })).toHaveValue('000-1210-0000');
    });

    it('validates and submits only active segments in configured order', async () => {
        await fillAccount();
        fireEvent.click(screen.getByRole('button', { name: 'Create Account' }));
        await waitFor(() => expect(financeDataService.createAccount).toHaveBeenCalledWith(expect.objectContaining({
            accountCode: '000-1210-0000', accountNumber: '000-1210-0000',
            segmentValues: [
                { segmentStructureId: 'department', segmentPosition: 1, segmentValue: '000' },
                { segmentStructureId: 'natural', segmentPosition: 2, segmentValue: '1210' },
                { segmentStructureId: 'project', segmentPosition: 3, segmentValue: '0000' },
            ],
        })));
        expect(push).toHaveBeenCalledWith('/finance/accounts');
    });

    it('continues to require active mandatory segment values', async () => {
        await fillAccount();
        fireEvent.change(screen.getByPlaceholderText('Enter Department value...'), { target: { value: '' } });
        const log = vi.spyOn(console, 'error').mockImplementation(() => {});
        try {
            fireEvent.click(screen.getByRole('button', { name: 'Create Account' }));
            await waitFor(() => expect(toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Department is required.' })));
            expect(financeDataService.createAccount).not.toHaveBeenCalled();
        } finally { log.mockRestore(); }
    });
});
