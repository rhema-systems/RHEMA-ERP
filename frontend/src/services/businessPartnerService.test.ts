import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  businessPartnerService,
  type BusinessPartnerPostingDefaults,
} from './businessPartnerService';

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
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);

    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier One',
      parentId: '',
      paymentTermId: '',
    });

    const request = fetch.mock.calls[0][1];
    const payload = JSON.parse(String(request?.body)) as Record<
      string,
      unknown
    >;

    expect(payload).not.toHaveProperty('parentId');
    expect(payload).not.toHaveProperty('paymentTermId');
  });

  it('preserves a selected payment-term Guid when updating a partner', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);

    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier One',
      paymentTermId: PAYMENT_TERM_ID,
    });

    const request = fetch.mock.calls[0][1];
    const payload = JSON.parse(String(request?.body)) as Record<
      string,
      unknown
    >;

    expect(payload.paymentTermId).toBe(PAYMENT_TERM_ID);
  });

  it('rejects a legacy payment-term code before sending an invalid API request', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);

    await expect(
      businessPartnerService.updatePartner(PARTNER_ID, {
        partnerName: 'Supplier One',
        paymentTermId: 'Net30',
      })
    ).rejects.toThrow(
      'Payment term must be selected from the available options.'
    );

    expect(fetch).not.toHaveBeenCalled();
  });
});

describe('business partner posting defaults', () => {
  const defaults: BusinessPartnerPostingDefaults = {
    subjectToWithholdingDeduction: true,
    withholdingTaxRate: 7.5,
    cashAccountSource: 'Chequebook',
    defaultTaxGroupId: PAYMENT_TERM_ID,
    defaultBankAccountId: PARTNER_ID,
    defaultApAccountId: PAYMENT_TERM_ID,
    defaultExpenseAccountId: null,
  };

  beforeEach(() => vi.restoreAllMocks());

  it('sends supplier credit limit and nested defaults on create without requiring every mapping', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);
    await businessPartnerService.createPartner({
      partnerName: 'Supplier',
      partnerType: 'Supplier',
      creditLimit: 4500,
      taxNumber: 'TIN-001',
      postingDefaults: defaults,
    });
    const payload = JSON.parse(String(fetch.mock.calls[0][1]?.body));
    expect(payload.creditLimit).toBe(4500);
    expect(payload.taxNumber).toBe('TIN-001');
    expect(payload.postingDefaults).toEqual(defaults);
  });

  it('preserves saved IDs and sends explicit null only for cleared mappings on update', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);
    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier',
      creditLimit: null,
      postingDefaults: {
        ...defaults,
        defaultApAccountId: '',
        defaultFreightAccountId: PARTNER_ID,
      },
    });
    const payload = JSON.parse(String(fetch.mock.calls[0][1]?.body));
    expect(payload.postingDefaults.defaultApAccountId).toBeNull();
    expect(payload.postingDefaults.defaultFreightAccountId).toBe(PARTNER_ID);
    expect(payload.postingDefaults.defaultTaxGroupId).toBe(PAYMENT_TERM_ID);
    expect(payload.creditLimit).toBeNull();
  });

  it('keeps legacy updates from overwriting absent posting defaults', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);
    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier',
    });
    expect(JSON.parse(String(fetch.mock.calls[0][1]?.body))).not.toHaveProperty(
      'postingDefaults'
    );
  });

  it.each([0, -1, 101, Number.NaN])(
    'rejects invalid enabled WHT rate %s without a request',
    async (withholdingTaxRate) => {
      const fetch = vi
        .spyOn(globalThis, 'fetch')
        .mockImplementation(successfulPartnerResponse);
      await expect(
        businessPartnerService.updatePartner(PARTNER_ID, {
          partnerName: 'Supplier',
          postingDefaults: { ...defaults, withholdingTaxRate },
        })
      ).rejects.toThrow(/WHT Rate/);
      expect(fetch).not.toHaveBeenCalled();
    }
  );

  it('normalizes disabled withholding to zero', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);
    await businessPartnerService.updatePartner(PARTNER_ID, {
      partnerName: 'Supplier',
      postingDefaults: { ...defaults, subjectToWithholdingDeduction: false },
    });
    expect(
      JSON.parse(String(fetch.mock.calls[0][1]?.body)).postingDefaults
        .withholdingTaxRate
    ).toBe(0);
  });

  it('rejects a negative supplier credit limit', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(successfulPartnerResponse);
    await expect(
      businessPartnerService.createPartner({
        partnerName: 'Supplier',
        partnerType: 'Supplier',
        creditLimit: -1,
      })
    ).rejects.toThrow('Credit Limit');
    expect(fetch).not.toHaveBeenCalled();
  });

  it('shows server validation detail and code while leaving the caller form intact', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          detail: 'Select an active payable account.',
          code: 'PARTNER_ACCOUNT_INVALID',
        }),
        { status: 400 }
      )
    );
    await expect(
      businessPartnerService.updatePartner(PARTNER_ID, {
        partnerName: 'Supplier',
        postingDefaults: defaults,
      })
    ).rejects.toThrow(
      'Select an active payable account. (PARTNER_ACCOUNT_INVALID)'
    );
    expect(defaults.defaultApAccountId).toBe(PAYMENT_TERM_ID);
  });
});
