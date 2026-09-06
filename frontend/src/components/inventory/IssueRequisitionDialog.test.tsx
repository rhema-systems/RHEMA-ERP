import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { IssueRequisitionDialog } from './IssueRequisitionDialog';
import { inventoryRequisitionService as service } from '@/services/inventoryRequisitionService';

const state = vi.hoisted(() => ({ actor: 'receiver', toast: vi.fn() }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: state.actor } }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: state.toast }) }));
vi.mock('@/components/inventory/InventoryTrackingExceptionSelect', () => ({
  InventoryTrackingExceptionSelect: () => null,
  useAvailableInventoryTrackingExceptions: () => ({ exceptions: [], loading: false, refresh: vi.fn() }),
}));
vi.mock('@/services/inventoryRequisitionService', () => ({
  RequisitionStatusMap: { 6: 'Issued', 7: 'Completed' },
  inventoryRequisitionService: {
    getById: vi.fn(), getIssueVouchers: vi.fn(), getIssueReceivers: vi.fn(),
    getIssueAccountingOptions: vi.fn(), acknowledgeIssueVoucher: vi.fn(),
  },
}));

const voucher = (status: number | string) => ({
  id: 'voucher-1', voucherNumber: 'SIV-TEST', receiverUserId: 'receiver', receiverName: 'Jane Employee',
  issuerName: 'John Manager', status, rowVersion: 'AQID', issuedAtUtc: '2026-09-06T22:22:08Z',
  movementReasonCode: 'DEPARTMENT_CONSUMPTION', financeJournalEntryId: 'journal-1', lines: [],
});

beforeEach(() => {
  vi.clearAllMocks();
  state.actor = 'receiver';
  vi.mocked(service.getById).mockResolvedValue({
    id: 'req-1', requisitionNumber: 'REQ-TEST', status: 'Issued', requestDate: '2026-09-06',
    departmentName: 'Operations', warehouseName: 'Stores', items: [],
  } as never);
});

const open = () => render(<IssueRequisitionDialog open requisitionId="req-1" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);

describe('issued voucher handover', () => {
  it.each([1, 'Issued'])('allows only the assigned receiver to acknowledge status %s without issue setup access', async status => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher(status)] as never);
    open();
    expect(await screen.findByRole('button', { name: 'Acknowledge receipt' })).toBeEnabled();
    expect(screen.getByText('Awaiting receiver')).toBeInTheDocument();
    expect(screen.queryByText('Issue Notes')).not.toBeInTheDocument();
    expect(service.getIssueReceivers).not.toHaveBeenCalled();
    expect(service.getIssueAccountingOptions).not.toHaveBeenCalled();
  });

  it('does not offer the issuer a self-acknowledgement action', async () => {
    state.actor = 'issuer';
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher('Issued')] as never);
    open();
    await screen.findByText('SIV-TEST');
    expect(screen.queryByRole('button', { name: 'Acknowledge receipt' })).not.toBeInTheDocument();
    expect(screen.getByText('Awaiting acknowledgement by Jane Employee.')).toBeInTheDocument();
  });

  it.each([2, 'Acknowledged'])('renders acknowledged status %s without a duplicate action', async status => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([{ ...voucher(status), receiverComment: 'Received in local UAT' }] as never);
    open();
    expect(await screen.findByText('Acknowledged')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Acknowledge receipt' })).not.toBeInTheDocument();
  });

  it('sends the exact voucher, row version and receiver comment then reloads evidence', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValueOnce([voucher('Issued')] as never)
      .mockResolvedValueOnce([{ ...voucher('Acknowledged'), receiverComment: 'LOCAL UAT received' }] as never);
    open();
    const button = await screen.findByRole('button', { name: 'Acknowledge receipt' });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'LOCAL UAT received' } });
    fireEvent.click(button);
    await waitFor(() => expect(service.acknowledgeIssueVoucher).toHaveBeenCalledWith('voucher-1', 'AQID', 'LOCAL UAT received'));
    await screen.findByText('Acknowledged');
  });
});
