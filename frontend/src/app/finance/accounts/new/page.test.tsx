import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import NewAccountPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceSettings, SegmentStructure } from '@/types/finance';

const { toast, push } = vi.hoisted(() => ({ toast: vi.fn(), push: vi.fn() }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true, isLoading: false }) }));
vi.mock('@/components/finance/accounts/account-book-assignments', () => ({
    AccountBookAssignments: ({ onChange }: { onChange: (value: unknown[]) => void }) => {
        React.useEffect(() => onChange([{ accountingBookId: 'ifrs', accountClassificationId: 'asset', isEnabled: true }]), [onChange]);
        return null;
    },
}));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push, back: vi.fn() }) }));
vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getFinanceSettings: vi.fn(), getSegmentStructures: vi.fn(), createAccount: vi.fn(),
    },
}));

const segment = (id: string, segmentName: string, segmentPosition: number, segmentLength: number,
    isActive = true): SegmentStructure => ({
    id, segmentName, segmentCode: id, segmentPosition, segmentLength,
    isActive, lifecycleStatus: 'Active', isRequired: true, rowVersion: '',
    accountUsageCount: 0, totalAccountCount: 0, canActivate: false, canFreeze: false, isSystemDefined: false,
    dataType: 'Numeric', separatorCharacter: '-',
    lookupTableRequired: false, isReportingDimension: false, isNaturalAccount: id === 'natural',
    lookupValues: [], lookupValueCount: 0, createdAt: '', updatedAt: '',
});

beforeAll(() => {
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    window.HTMLElement.prototype.scrollIntoView = vi.fn();
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
    it('renders seeded active Legal Entity values as a governed dropdown', async () => {
        const legalEntity = segment('legal-entity', 'Legal Entity / Company', 1, 7);
        legalEntity.lookupTableRequired = true;
        legalEntity.lookupValues = [{
            id: 'tenant-value',
            segmentStructureId: legalEntity.id,
            segmentValue: 'DEFAULT',
            description: 'Default Tenant',
            effectiveDate: '',
            isActive: true,
            displayOrder: 1,
            createdAt: '',
            updatedAt: '',
        }];
        vi.mocked(financeDataService.getSegmentStructures).mockResolvedValue([
            legalEntity,
            segment('natural', 'Natural Account', 2, 4),
        ]);

        render(<NewAccountPage />);
        await screen.findByRole('heading', { name: 'New Segmented Account' });

        const dropdown = screen.getAllByRole('combobox').find(
            element => element.textContent?.includes('Select Legal Entity / Company')
        );
        if (!dropdown) throw new Error('Legal Entity dropdown was not rendered.');
        expect(dropdown).toBeEnabled();
        fireEvent.click(dropdown);
        expect(await screen.findByText('DEFAULT')).toBeInTheDocument();
        expect(screen.getByText('- Default Tenant')).toBeInTheDocument();
    });

    it('does not degrade a governed lookup segment into free text when no active values exist', async () => {
        const legalEntity = segment('legal-entity', 'Legal Entity / Company', 1, 7);
        legalEntity.lookupTableRequired = true;
        vi.mocked(financeDataService.getSegmentStructures).mockResolvedValue([
            legalEntity,
            segment('natural', 'Natural Account', 2, 4),
        ]);

        render(<NewAccountPage />);
        await screen.findByRole('heading', { name: 'New Segmented Account' });

        expect(screen.queryByPlaceholderText('Enter Legal Entity / Company value...')).not.toBeInTheDocument();
        expect(screen.getByText('This governed segment has no active values. Add or activate values in Account Segments before creating the account.')).toBeInTheDocument();

        fireEvent.change(screen.getByPlaceholderText('Enter Natural Account value...'), { target: { value: '1210' } });
        fireEvent.change(screen.getByRole('textbox', { name: 'Account Name *' }), { target: { value: 'Cash' } });
        fireEvent.click(screen.getByRole('button', { name: 'Create Account' }));

        await waitFor(() => expect(toast).toHaveBeenCalledWith(expect.objectContaining({
            description: 'Legal Entity / Company has no active lookup values. Configure the segment before creating an account.',
        })));
        expect(financeDataService.createAccount).not.toHaveBeenCalled();
    });

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
            accountCode: '1210', accountNumber: '000-1210-0000',
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
