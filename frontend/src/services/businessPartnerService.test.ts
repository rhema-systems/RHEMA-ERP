import { beforeEach, describe, expect, it, vi } from 'vitest';

import { businessPartnerService } from './businessPartnerService';

const PARTNER_ID = '2d73e382-0530-4da2-95c5-a722cfc32e90';
const PAYMENT_TERM_ID = 'f7677490-e6f7-4da8-80d2-760a94c393bc';

const successfulPartnerResponse = () =>
  Promise.resolve(
    new Response(
      JSON.stringify({
        id: PARTNER_ID,
        partnerCode: 'BP-001',
        partnerType: 'Supplier',
        partnerName: 'Supplier One',
        status: 'Active',
        isPreferred: false,
        isBlacklisted: false,
        createdAt: '2026-08-05T00:00:00Z',
      }),
      { status: 200, headers: { 'Content-Type': 'application/json' } }
    )
  );

describe('businessPartnerService nullable Guid payloads', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'internal-token');
  });

  it('omits blank nullable Guid fields when updating a partner', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(successfulPartnerResponse);

    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier One',
      parentId: '',
      paymentTermId: '',
    });

    const request = fetch.mock.calls[0][1];
    const payload = JSON.parse(String(request?.body)) as Record<string, unknown>;

    expect(payload).not.toHaveProperty('parentId');
    expect(payload).not.toHaveProperty('paymentTermId');
  });

  it('preserves a selected payment-term Guid when updating a partner', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(successfulPartnerResponse);

    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier One',
      paymentTermId: PAYMENT_TERM_ID,
    });

    const request = fetch.mock.calls[0][1];
    const payload = JSON.parse(String(request?.body)) as Record<string, unknown>;

    expect(payload.paymentTermId).toBe(PAYMENT_TERM_ID);
  });

  it('rejects a legacy payment-term code before sending an invalid API request', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(successfulPartnerResponse);

    await expect(
      businessPartnerService.updatePartner(PARTNER_ID, {
        partnerName: 'Supplier One',
        paymentTermId: 'Net30',
      })
    ).rejects.toThrow('Payment term must be selected from the available options.');

    expect(fetch).not.toHaveBeenCalled();
  });
});
