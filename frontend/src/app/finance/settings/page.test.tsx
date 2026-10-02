import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import FinanceSettingsPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, FinanceSettings } from '@/types/finance';

const { toast } = vi.hoisted(() => ({ toast: vi.fn() }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast }) }));
vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getFinanceSettings: vi.fn(),
        getAccounts: vi.fn(),
        getFiscalYears: vi.fn().mockResolvedValue([]),
        updateFinanceSettings: vi.fn(),
    },
}));

const account = (id: string, accountCode: string, accountName: string, accountType: Account['accountType'],
    extra: Partial<Account> = {}): Account => ({
    id, accountCode, accountNumber: accountCode, accountName, accountType,
    tenantId: 'tenant', isSegmented: true, currencyCode: 'GHS', isMultiCurrency: false,
    isIFRSClassified: true, allowDirectPosting: true, isControlAccount: false,
    budgetTrackingEnabled: false, status: 'Active', createdAt: '', updatedAt: '', ...extra,
});
const accounts = [
    account('inventory', '1200', 'Inventory', 'Asset'),
    account('supplier-advance', '1300', 'Supplier Advances', 'Asset'),
    account('clearing', '000-1210-0000', 'Supplier Returns Clearing', 'Asset'),
    account('clearing-new', '000-1211-0000', 'Alternative Returns Clearing', 'Asset'),
    account('variance', '000-6810-0000', 'Purchase Return Cost Variance', 'Expense'),
    account('variance-new', '000-6811-0000', 'Alternative Return Variance', 'Expense'),
    account('inactive', '000-1212-0000', 'Inactive Asset', 'Asset', { status: 'Inactive' }),
    account('control', '000-1213-0000', 'Asset Control', 'Asset', { isControlAccount: true }),
    account('no-post', '000-1214-0000', 'Non Posting Asset', 'Asset', { allowDirectPosting: false }),
    account('liability', '2100', 'Accrual Liability', 'Liability'),
];
let saved: FinanceSettings;

beforeAll(() => {
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    HTMLElement.prototype.scrollIntoView = vi.fn();
});
afterAll(() => vi.unstubAllGlobals());
beforeEach(() => {
    cleanup();
    vi.clearAllMocks();
    saved = {
        id: 'settings', tenantId: 'tenant', coaType: 'Segmented', coaConfigurationLocked: true,
        baseCurrency: 'GHS', transactionsExist: true, controlAccountInventoryId: 'inventory',
        returnToVendorClearingAccountId: 'clearing', purchaseReturnVarianceAccountId: 'variance',
        apInvoicePriceTolerancePercent: 1, apInvoiceQuantityTolerancePercent: 1,
    };
    vi.mocked(financeDataService.getFinanceSettings).mockImplementation(async () => ({ ...saved }));
    vi.mocked(financeDataService.getAccounts).mockResolvedValue(accounts);
    vi.mocked(financeDataService.updateFinanceSettings).mockImplementation(async dto => {
        saved = { ...saved, ...dto };
        return { ...saved };
    });
});

async function openPage() {
    render(<FinanceSettingsPage />);
    await screen.findByRole('heading', { name: 'Finance Settings' });
}

describe('Finance Settings supplier return mappings', () => {
    it('exposes and persists an eligible supplier advance asset mapping', async () => {
        await openPage();
        fireEvent.click(screen.getByRole('combobox', { name: 'Supplier Advance Account' }));
        expect(screen.queryByRole('option', { name: /Accrual Liability/ })).not.toBeInTheDocument();
        expect(screen.queryByRole('option', { name: /Asset Control/ })).not.toBeInTheDocument();
        fireEvent.click(await screen.findByRole('option', { name: '1300 - Supplier Advances' }));
        fireEvent.click(screen.getByRole('button', { name: 'Save Settings' }));
        await waitFor(() => expect(financeDataService.updateFinanceSettings).toHaveBeenCalledWith(expect.objectContaining({ supplierAdvanceAccountId: 'supplier-advance' })));
        await waitFor(() => expect(screen.getByRole('combobox', { name: 'Supplier Advance Account' })).toHaveTextContent('1300 - Supplier Advances'));
    });

    it('exposes and persists an eligible customer advance liability mapping', async () => {
        await openPage();
        fireEvent.click(screen.getByRole('combobox', { name: 'Customer Advance Account' }));
        expect(screen.queryByRole('option', { name: /Inventory/ })).not.toBeInTheDocument();
        fireEvent.click(await screen.findByRole('option', { name: '2100 - Accrual Liability' }));
        fireEvent.click(screen.getByRole('button', { name: 'Save Settings' }));
        await waitFor(() => expect(financeDataService.updateFinanceSettings).toHaveBeenCalledWith(expect.objectContaining({ customerAdvanceAccountId: 'liability' })));
        await waitFor(() => expect(screen.getByRole('combobox', { name: 'Customer Advance Account' })).toHaveTextContent('2100 - Accrual Liability'));
    });
    it('loads both saved mappings while preserving the existing locked Inventory control', async () => {
        await openPage();
        expect(screen.getByRole('combobox', { name: 'Supplier Returns Clearing' }))
            .toHaveTextContent('000-1210-0000 - Supplier Returns Clearing');
        expect(screen.getByRole('combobox', { name: 'Purchase Return Cost Variance' }))
            .toHaveTextContent('000-6810-0000 - Purchase Return Cost Variance');
        expect(screen.getByRole('combobox', { name: 'Inventory Control' })).toBeDisabled();
        expect(screen.getByRole('combobox', { name: 'Supplier Returns Clearing' })).toBeEnabled();
        fireEvent.click(screen.getByRole('button', { name: 'Save Settings' }));
        await waitFor(() => expect(financeDataService.updateFinanceSettings).toHaveBeenCalledWith(expect.objectContaining({
            returnToVendorClearingAccountId: 'clearing', purchaseReturnVarianceAccountId: 'variance',
            controlAccountInventoryId: 'inventory',
        })));
    });

    it('searches eligible assets, excluding Inventory control, inactive and non-posting accounts', async () => {
        await openPage();
        fireEvent.click(screen.getByRole('combobox', { name: 'Supplier Returns Clearing' }));
        expect(screen.queryByRole('option', { name: /1200 - Inventory/ })).not.toBeInTheDocument();
        for (const name of ['Inactive Asset', 'Asset Control', 'Non Posting Asset', 'Accrual Liability', 'Purchase Return Cost Variance']) {
            expect(screen.queryByRole('option', { name: new RegExp(name) })).not.toBeInTheDocument();
        }
        expect(screen.queryByRole('option', { name: 'None' })).not.toBeInTheDocument();
        fireEvent.change(screen.getByPlaceholderText('Search asset accounts...'), { target: { value: 'Alternative' } });
        fireEvent.click(await screen.findByRole('option', { name: '000-1211-0000 - Alternative Returns Clearing' }));
        expect(screen.getByRole('combobox', { name: 'Supplier Returns Clearing' })).toHaveTextContent('Alternative Returns Clearing');
    });

    it('saves changed Asset and Expense mappings and shows the persisted values after reload', async () => {
        await openPage();
        fireEvent.click(screen.getByRole('combobox', { name: 'Supplier Returns Clearing' }));
        fireEvent.click(await screen.findByRole('option', { name: '000-1211-0000 - Alternative Returns Clearing' }));
        fireEvent.click(screen.getByRole('combobox', { name: 'Purchase Return Cost Variance' }));
        expect(screen.queryByRole('option', { name: /Returns Clearing/ })).not.toBeInTheDocument();
        fireEvent.click(await screen.findByRole('option', { name: '000-6811-0000 - Alternative Return Variance' }));
        fireEvent.click(screen.getByRole('button', { name: 'Save Settings' }));
        await waitFor(() => expect(financeDataService.updateFinanceSettings).toHaveBeenCalledWith(expect.objectContaining({
            returnToVendorClearingAccountId: 'clearing-new', purchaseReturnVarianceAccountId: 'variance-new',
            controlAccountInventoryId: 'inventory',
        })));
        await waitFor(() => expect(financeDataService.getFinanceSettings).toHaveBeenCalledTimes(2));
        expect(await screen.findByRole('combobox', { name: 'Supplier Returns Clearing' })).toHaveTextContent('Alternative Returns Clearing');
        expect(screen.getByRole('combobox', { name: 'Purchase Return Cost Variance' })).toHaveTextContent('Alternative Return Variance');
    });

    it('retains the selected mapping when the server rejects the save', async () => {
        const log = vi.spyOn(console, 'error').mockImplementation(() => {});
        try {
            await openPage();
            fireEvent.click(screen.getByRole('combobox', { name: 'Purchase Return Cost Variance' }));
            fireEvent.click(await screen.findByRole('option', { name: '000-6811-0000 - Alternative Return Variance' }));
            vi.mocked(financeDataService.updateFinanceSettings).mockRejectedValueOnce(new Error('Finance administration access is required.'));
            fireEvent.click(screen.getByRole('button', { name: 'Save Settings' }));
            await waitFor(() => expect(toast).toHaveBeenCalledWith(expect.objectContaining({
                description: 'Finance administration access is required.', variant: 'destructive',
            })));
            expect(screen.getByRole('combobox', { name: 'Purchase Return Cost Variance' })).toHaveTextContent('Alternative Return Variance');
            expect(financeDataService.getFinanceSettings).toHaveBeenCalledTimes(1);
        } finally { log.mockRestore(); }
    });
});
