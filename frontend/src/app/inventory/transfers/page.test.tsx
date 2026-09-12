import React from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import InventoryTransfersPage from './page';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/useWorkflowEntitySummaries', () => ({
  useWorkflowEntitySummaries: () => ({ summariesById: {} }),
  formatPendingApprovers: () => ({ short: '', full: '' }),
}));
vi.mock('@/components/workflow/WorkflowApprovalActions', () => ({ WorkflowApprovalActions: () => null }));
vi.mock('@/components/inventory/TransferDialog', () => ({ TransferDialog: () => null }));
vi.mock('@/components/inventory/ShipTransferDialog', () => ({ ShipTransferDialog: () => null }));
vi.mock('@/components/inventory/ReceiveTransferDialog', () => ({ ReceiveTransferDialog: () => null }));
vi.mock('@/services/financeCommonService', () => ({ procurementCurrencyService: { getActive: async () => [] } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouses: async () => [],
  getInventoryTransfers: async () => [{ id: 'transfer', transferNumber: 'TRF-001',
    sourceWarehouseName: 'Main warehouse', destinationWarehouseName: 'Stores warehouse',
    requestedDate: '2026-09-12', status: 'Received', approvalRequired: false, totalItems: 1 }],
} }));

afterEach(cleanup);

describe('compact inventory transfer register', () => {
  it('keeps transfer details and actions without lifecycle helper banners or manual close', async () => {
    render(<InventoryTransfersPage />);
    expect(await screen.findByText('TRF-001')).toBeInTheDocument();
    expect(screen.getByText('Main warehouse')).toBeInTheDocument();
    expect(screen.getByText('Stores warehouse')).toBeInTheDocument();
    const view = screen.getByRole('button', { name: 'View', exact: true });
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
