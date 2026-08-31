import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  TENDER_PAYMENT_VERIFY_PERMISSION,
  TenderPaymentVerificationPanel,
} from './page';
import type { TenderPaymentDto } from '@/services/tenderBidService';

const mocks = vi.hoisted(() => ({
  verifyBidPayment: vi.fn(),
  toastError: vi.fn(),
  toastSuccess: vi.fn(),
}));

vi.mock('@/services/tenderBidService', () => ({
  verifyBidPayment: mocks.verifyBidPayment,
}));

vi.mock('sonner', () => ({
  toast: {
    error: mocks.toastError,
    success: mocks.toastSuccess,
  },
}));

const pendingPayment: TenderPaymentDto = {
  id: 'payment-1',
  tenderFeeId: 'fee-1',
  businessPartnerId: 'supplier-1',
  businessPartnerName: 'Supplier One',
  paymentReference: 'PAY-001',
  amount: 1500,
  currency: 'GHS',
  paymentMethod: 'Bank Transfer',
  status: 'Pending',
  paymentDate: '2026-08-31T08:00:00Z',
  transactionId: 'BANK-TXN-91',
};

describe('tender fee payment verification panel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows complete payment evidence without decision controls for an unauthorised user', () => {
    expect(TENDER_PAYMENT_VERIFY_PERMISSION).toBe(
      'procurement.tender.payment.verify'
    );

    render(
      <TenderPaymentVerificationPanel
        bidId="bid-1"
        payments={[pendingPayment]}
        canVerifyPayment={false}
        onRefresh={vi.fn()}
      />
    );

    expect(screen.getByText('PAY-001')).toBeInTheDocument();
    expect(screen.getByText('Transaction: BANK-TXN-91')).toBeInTheDocument();
    expect(screen.getByText(/GHS.*1,500/)).toBeInTheDocument();
    expect(screen.getByText('Bank Transfer')).toBeInTheDocument();
    expect(screen.getByText('Pending verification')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Approve payment PAY-001' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Reject payment PAY-001' })
    ).not.toBeInTheDocument();
  });

  it('requires a rejection reason, posts the decision, and refreshes the payments', async () => {
    const rejectedPayment = { ...pendingPayment, status: 'Rejected' };
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    mocks.verifyBidPayment.mockResolvedValue(rejectedPayment);

    render(
      <TenderPaymentVerificationPanel
        bidId="bid-1"
        payments={[pendingPayment]}
        canVerifyPayment
        onRefresh={onRefresh}
      />
    );

    fireEvent.click(
      screen.getByRole('button', { name: 'Reject payment PAY-001' })
    );
    fireEvent.click(screen.getByRole('button', { name: 'Reject payment' }));

    expect(
      await screen.findByText('A rejection reason is required.')
    ).toBeInTheDocument();
    expect(mocks.verifyBidPayment).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('Decision notes'), {
      target: { value: 'The bank reference cannot be reconciled.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Reject payment' }));

    await waitFor(() => {
      expect(mocks.verifyBidPayment).toHaveBeenCalledWith(
        'bid-1',
        'payment-1',
        {
          isApproved: false,
          notes: 'The bank reference cannot be reconciled.',
        }
      );
    });
    expect(onRefresh).toHaveBeenCalledWith(rejectedPayment);
    expect(mocks.toastSuccess).toHaveBeenCalledWith(
      'Tender fee payment rejected.'
    );
    await waitFor(() => {
      expect(
        screen.queryByRole('dialog', {
          name: 'Reject tender fee payment',
        })
      ).not.toBeInTheDocument();
    });
  });

  it('keeps the approval dialog open with the server ProblemDetails message on failure', async () => {
    mocks.verifyBidPayment.mockRejectedValue(
      new Error(
        'This payment belongs to a different bid. (TENDER_PAYMENT_BID_MISMATCH)'
      )
    );

    render(
      <TenderPaymentVerificationPanel
        bidId="bid-1"
        payments={[pendingPayment]}
        canVerifyPayment
        onRefresh={vi.fn()}
      />
    );

    fireEvent.click(
      screen.getByRole('button', { name: 'Approve payment PAY-001' })
    );
    fireEvent.change(screen.getByLabelText('Decision notes (optional)'), {
      target: { value: 'Confirmed against the receipt.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Approve payment' }));

    expect(
      await screen.findByText(
        'This payment belongs to a different bid. (TENDER_PAYMENT_BID_MISMATCH)'
      )
    ).toBeInTheDocument();
    expect(
      screen.getByRole('dialog', { name: 'Approve tender fee payment' })
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Decision notes (optional)')).toHaveValue(
      'Confirmed against the receipt.'
    );
    expect(mocks.toastError).toHaveBeenCalledWith(
      'This payment belongs to a different bid. (TENDER_PAYMENT_BID_MISMATCH)'
    );
  });
});
