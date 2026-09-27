import React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Page from './page';

const mocks = vi.hoisted(() => ({ id: 'voucher-1', get: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: mocks.id }) }));
vi.mock('next/link', () => ({ default: ({ children, ...props }: React.ComponentProps<'a'>) => <a {...props}>{children}</a> }));
vi.mock('@/services/inventoryRequisitionService', () => ({ inventoryRequisitionService: { getIssueVoucher: mocks.get } }));

describe('store issue voucher search destination', () => {
  beforeEach(() => { vi.stubGlobal('React', React); mocks.id = 'voucher-1'; mocks.get.mockReset(); });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('loads the selected voucher through its owner and shows actual receipt quantities without an action form', async () => {
    mocks.get.mockResolvedValue({ id: 'voucher-1', voucherNumber: 'SIV-123', status: 'Acknowledged', requisitionNumber: 'REQ-1',
      issuedAtUtc: '2026-09-27T00:00:00Z', lines: [{ id: 'line', itemCode: 'CEM', itemName: 'Cement', quantity: 100, receivedQuantity: 100, outstandingQuantity: 0 }] });
    render(<Page />);
    expect(await screen.findByRole('heading', { name: 'SIV-123' })).toBeInTheDocument();
    expect(mocks.get).toHaveBeenCalledWith('voucher-1');
    expect(screen.getByText('Acknowledged')).toBeInTheDocument();
    expect(screen.getAllByRole('cell', { name: /^100$/ })).toHaveLength(2);
    expect(screen.queryByRole('button', { name: /acknowledge|issue|save/i })).not.toBeInTheDocument();
  });

  it('ignores a late response after another record was selected and shows a failed read', async () => {
    let resolve!: (value: unknown) => void;
    mocks.get.mockImplementationOnce(() => new Promise(done => { resolve = done; })).mockRejectedValueOnce(new Error('Access denied'));
    const view = render(<Page />);
    mocks.id = 'voucher-2'; view.rerender(<Page />);
    await screen.findByRole('alert');
    resolve({ voucherNumber: 'STALE', lines: [] });
    await waitFor(() => expect(mocks.get).toHaveBeenCalledWith('voucher-2'));
    expect(screen.queryByText('STALE')).not.toBeInTheDocument();
  });

  it('labels historical acknowledgements without presenting inferred quantities as measured receipts', async () => {
    mocks.get.mockResolvedValue({ id: 'voucher-1', voucherNumber: 'SIV-LEGACY', status: 'Acknowledged',
      isLegacyAcknowledgement: true, issuedAtUtc: '2026-08-01T00:00:00Z',
      lines: [{ id: 'line', itemCode: 'CEM', itemName: 'Cement', quantity: 100, receivedQuantity: 100, outstandingQuantity: 0 }] });
    render(<Page />);
    expect(await screen.findByRole('heading', { name: 'SIV-LEGACY' })).toBeInTheDocument();
    expect(screen.getByText('Historical acknowledgement: actual quantities were not captured per line.')).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Not recorded' })).toBeInTheDocument();
    expect(screen.getAllByRole('cell', { name: /^100$/ })).toHaveLength(1);
    expect(screen.queryByRole('cell', { name: /^0$/ })).not.toBeInTheDocument();
  });
});
