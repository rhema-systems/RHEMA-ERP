import { beforeEach, describe, expect, it, vi } from 'vitest';

import { verifyBidPayment } from './tenderBidService';

describe('tender bid payment verification client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts an authorised decision to the bid-scoped payment endpoint', async () => {
    localStorage.setItem('token', 'procurement-token');
    const verifiedPayment = {
      id: 'payment-1',
      tenderFeeId: 'fee-1',
      businessPartnerId: 'supplier-1',
      businessPartnerName: 'Supplier One',
      paymentReference: 'PAY-001',
      amount: 1500,
      currency: 'GHS',
      paymentMethod: 'Bank Transfer',
      status: 'Verified',
      paymentDate: '2026-08-31T08:00:00Z',
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(verifiedPayment), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await expect(
      verifyBidPayment('bid-1', 'payment-1', {
        isApproved: true,
        notes: 'Matched to the bank statement.',
      })
    ).resolves.toEqual(verifiedPayment);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/TenderBids/bid-1/payments/payment-1/verify',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer procurement-token',
          'Content-Type': 'application/json',
        }),
        body: JSON.stringify({
          isApproved: true,
          notes: 'Matched to the bank statement.',
        }),
      })
    );
  });

  it('surfaces ProblemDetails detail and code when verification fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 409,
            detail: 'This payment has already been decided.',
            extensions: { code: 'TENDER_PAYMENT_ALREADY_DECIDED' },
          }),
          {
            status: 409,
            headers: { 'Content-Type': 'application/problem+json' },
          }
        )
      )
    );

    await expect(
      verifyBidPayment('bid-1', 'payment-1', {
        isApproved: false,
        notes: 'Reference is invalid.',
      })
    ).rejects.toThrow(
      'This payment has already been decided. (TENDER_PAYMENT_ALREADY_DECIDED)'
    );
  });
});
