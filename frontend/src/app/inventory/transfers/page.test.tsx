import React from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import InventoryTransfersPage from './page';

const navigation = vi.hoisted(() => ({ params: new URLSearchParams(), replace: vi.fn(), detail: vi.fn() }));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: navigation.replace }),
  usePathname: () => '/inventory/transfers', useSearchParams: () => navigation.params,
}));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/useWorkflowEntitySummaries', () => ({
  useWorkflowEntitySummaries: () => ({ summariesById: {} }),
  formatPendingApprovers: () => ({ short: '', full: '' }),
}));
vi.mock('@/components/workflow/WorkflowApprovalActions', () => ({ WorkflowApprovalActions: () => null }));
vi.mock('@/components/inventory/TransferDialog', () => ({
  TransferDialog: ({ open, transfer, mode }: { open: boolean; transfer?: { id: string }; mode: string }) =>
    open ? <section aria-label="Selected transfer" data-mode={mode}>{transfer?.id}</section> : null,
}));
vi.mock('@/components/inventory/ShipTransferDialog', () => ({ ShipTransferDialog: () => null }));
vi.mock('@/components/inventory/ReceiveTransferDialog', () => ({ ReceiveTransferDialog: () => null }));
vi.mock('@/services/financeCommonService', () => ({ procurementCurrencyService: { getActive: async () => [] } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouses: async () => [],
  getInventoryTransferById: navigation.detail,
  getInventoryTransfers: async () => [{ id: 'transfer', transferNumber: 'TRF-001',
    sourceWarehouseName: 'Main warehouse', destinationWarehouseName: 'Stores warehouse',
    requestedDate: '2026-09-12', status: 'Received', approvalRequired: false, totalItems: 1 }],
} }));

beforeEach(() => { navigation.params = new URLSearchParams(); vi.clearAllMocks(); });
afterEach(cleanup);

describe('compact inventory transfer register', () => {
  it('opens the requested record in view mode even when absent from the current register', async () => {
    navigation.params = new URLSearchParams('recordId=old-transfer');
    navigation.detail.mockResolvedValue({ id: 'old-transfer', transferNumber: 'TRF-OLD', status: 'Received' });
    render(<InventoryTransfersPage />);
    const selected = await screen.findByRole('region', { name: 'Selected transfer' });
    expect(selected).toHaveTextContent('old-transfer');
    expect(selected).toHaveAttribute('data-mode', 'view');
    expect(navigation.detail).toHaveBeenCalledExactlyOnceWith('old-transfer');
    expect(navigation.replace).toHaveBeenCalledWith('/inventory/transfers', { scroll: false });
  });
  it('keeps transfer details and actions without lifecycle helper banners or manual close', async () => {
    render(<InventoryTransfersPage />);
    expect(await screen.findByText('TRF-001')).toBeInTheDocument();
    expect(screen.getByText('Main warehouse')).toBeInTheDocument();
    expect(screen.getByText('Stores warehouse')).toBeInTheDocument();
    const view = screen.getByRole('button', { name: 'View' });
    expect(view).toHaveAttribute('title', 'View');
    expect(view.textContent).toBe('');
    expect(view.querySelector('svg')).not.toBeNull();
    expect(screen.getByRole('button', { name: 'New Transfer' })).toBeInTheDocument();
    expect(screen.queryByText('Controlled transfer lifecycle')).not.toBeInTheDocument();
    expect(screen.queryByText('Independent closure')).not.toBeInTheDocument();
    expect(screen.queryByText('Protected Central DMS evidence')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Resolve \/ Close|Close transfer/i })).not.toBeInTheDocument();
  });
});
