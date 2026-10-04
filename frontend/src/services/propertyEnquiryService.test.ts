import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import {
  canSearchOrLinkExistingCustomer,
  canSubmitPropertyEstateHandoff,
  propertyEnquiryService,
  type PropertyEnquiryProspect,
} from './propertyEnquiryService';

vi.mock('./api.service', () => ({
  apiService: { request: vi.fn() },
}));

describe('propertyEnquiryService', () => {
  beforeEach(() => vi.mocked(apiService.request).mockReset());

  it('loads every scoped enquiry page for CRM selectors', async () => {
    vi.mocked(apiService.request)
      .mockResolvedValueOnce({
        success: true,
        data: [{ id: 'enquiry-1', ticketNumber: 'PE-001' }],
        totalCount: 26,
      })
      .mockResolvedValueOnce({
        success: true,
        data: [{ id: 'enquiry-26', ticketNumber: 'PE-026' }],
        totalCount: 26,
      });

    const enquiries = await propertyEnquiryService.listAll();

    expect(enquiries.map((item) => item.id)).toEqual([
      'enquiry-1',
      'enquiry-26',
    ]);
    expect(apiService.request).toHaveBeenNthCalledWith(
      1,
      '/ehc/internal/property-enquiries?page=1',
      { method: 'GET' }
    );
    expect(apiService.request).toHaveBeenNthCalledWith(
      2,
      '/ehc/internal/property-enquiries?page=2',
      { method: 'GET' }
    );
  });

  it('creates an opportunity through an explicit prospect action', async () => {
    vi.mocked(apiService.request).mockResolvedValue({
      success: true,
      data: {
        id: 'opp-1',
        referenceNumber: 'OPP-001',
        stage: 'Qualification',
        amount: 0,
        currency: 'GHS',
      },
    });

    const request = {
      amount: 250000,
      currency: 'GHS',
      expectedCloseDate: '2026-12-01',
      reserveProperty: true,
      reservationDays: 14,
      notes: 'Qualified buyer',
    };
    await propertyEnquiryService.createOpportunity('enquiry-1', request);

    expect(apiService.request).toHaveBeenCalledWith(
      '/ehc/internal/property-enquiries/enquiry-1/prospect/opportunity',
      { method: 'POST', body: JSON.stringify(request) }
    );
  });

  it('uses separate audited routes for real email and internal activity', async () => {
    vi.mocked(apiService.request).mockResolvedValue({
      success: true,
      data: {},
    });

    await propertyEnquiryService.sendEmail(
      'enquiry-1',
      'Please confirm Friday.'
    );
    await propertyEnquiryService.addActivity('enquiry-1', {
      activityType: 'Note',
      notes: 'Internal only',
    });

    expect(apiService.request).toHaveBeenNthCalledWith(
      1,
      '/ehc/internal/property-enquiries/enquiry-1/reply',
      {
        method: 'POST',
        body: JSON.stringify({ body: 'Please confirm Friday.' }),
      }
    );
    expect(apiService.request).toHaveBeenNthCalledWith(
      2,
      '/ehc/internal/property-enquiries/enquiry-1/internal-note',
      {
        method: 'POST',
        body: JSON.stringify({ body: '[Note] Internal only' }),
      }
    );
  });

  it('requires customer linking to be an explicit operation', async () => {
    vi.mocked(apiService.request).mockResolvedValue({
      success: true,
      data: { id: 'enquiry-1' },
    });

    await propertyEnquiryService.linkBusinessPartner('enquiry-1', 'partner-1');

    expect(apiService.request).toHaveBeenCalledWith(
      '/ehc/internal/property-enquiries/enquiry-1/prospect/link-business-partner',
      {
        method: 'POST',
        body: JSON.stringify({ businessPartnerId: 'partner-1' }),
      }
    );
  });

  it('keeps deposit recording and clearance as separate controlled operations', async () => {
    vi.mocked(apiService.request).mockResolvedValue({
      success: true,
      data: { id: 'receipt-1' },
    });

    const receivedAt = '2026-10-01T08:30:00.000Z';
    const clearedAt = '2026-10-02T09:45:00.000Z';
    await propertyEnquiryService.recordDeposit('enquiry-1', {
      amount: 50000,
      currency: 'GHS',
      paymentMethod: 'BankTransfer',
      transactionReference: 'BANK-001',
      receivedAt,
    });
    await propertyEnquiryService.clearDeposit(
      'enquiry-1',
      'receipt-1',
      clearedAt
    );

    expect(apiService.request).toHaveBeenNthCalledWith(
      1,
      '/ehc/internal/property-enquiries/enquiry-1/prospect/deposits',
      {
        method: 'POST',
        body: JSON.stringify({
          amount: 50000,
          currency: 'GHS',
          paymentMethod: 'BankTransfer',
          transactionReference: 'BANK-001',
          receivedAt,
        }),
      }
    );
    expect(apiService.request).toHaveBeenNthCalledWith(
      2,
      '/ehc/internal/property-enquiries/enquiry-1/prospect/deposits/receipt-1/clear',
      {
        method: 'POST',
        body: JSON.stringify({ clearedAt }),
      }
    );
  });

  it('passes the operator-selected reversal date to the controlled reversal route', async () => {
    vi.mocked(apiService.request).mockResolvedValue({
      success: true,
      data: { id: 'receipt-1' },
    });
    const reversalDate = '2026-10-03T11:15:00.000Z';

    await propertyEnquiryService.reverseDeposit(
      'enquiry-1',
      'receipt-1',
      'Bank returned the transfer',
      reversalDate
    );

    expect(apiService.request).toHaveBeenCalledWith(
      '/ehc/internal/property-enquiries/enquiry-1/prospect/deposits/receipt-1/reverse',
      {
        method: 'POST',
        body: JSON.stringify({
          reason: 'Bank returned the transfer',
          reversalDate,
        }),
      }
    );
  });

  it('loads and saves the source-specific prospect deposit policy', async () => {
    const policy = {
      salesSaleableSourceId: 'source-1',
      requirementType: 'Percentage' as const,
      fixedAmount: null,
      percentage: 20,
      depositLiabilityAccountId: 'liability-1',
      defaultBankAccountId: 'bank-1',
      defaultLiquidityAccountId: null,
      isActive: true,
    };
    vi.mocked(apiService.request).mockResolvedValue({
      success: true,
      data: policy,
    });

    await propertyEnquiryService.getDepositPolicy('source-1');
    await propertyEnquiryService.upsertDepositPolicy(policy);

    expect(apiService.request).toHaveBeenNthCalledWith(
      1,
      '/ehc/internal/property-enquiries/prospect-deposit-policy?salesSaleableSourceId=source-1',
      { method: 'GET' }
    );
    expect(apiService.request).toHaveBeenNthCalledWith(
      2,
      '/ehc/internal/property-enquiries/prospect-deposit-policy',
      { method: 'POST', body: JSON.stringify(policy) }
    );
  });

  it('only permits existing-customer matching after qualification and before a customer is linked', () => {
    const prospect = {
      ticketId: 'ticket-1',
      leadId: 'lead-1',
      status: 'New',
      agreedAmount: 0,
      currency: 'GHS',
      depositRequirementType: 'Full',
      requiredDeposit: 0,
      clearedDeposit: 0,
      depositThresholdMet: false,
    } satisfies PropertyEnquiryProspect;

    expect(canSearchOrLinkExistingCustomer(prospect)).toBe(false);
    expect(
      canSearchOrLinkExistingCustomer({ ...prospect, status: 'Contacted' })
    ).toBe(false);
    expect(
      canSearchOrLinkExistingCustomer({ ...prospect, status: 'Disqualified' })
    ).toBe(false);
    expect(
      canSearchOrLinkExistingCustomer({ ...prospect, status: 'Qualified' })
    ).toBe(true);
    expect(
      canSearchOrLinkExistingCustomer({ ...prospect, status: 'Opportunity' })
    ).toBe(true);
    expect(
      canSearchOrLinkExistingCustomer({
        ...prospect,
        status: 'Qualified',
        businessPartnerId: 'customer-1',
      })
    ).toBe(false);
  });

  it('requires both backend handoff readiness and a linked customer', () => {
    const prospect = {
      ticketId: 'ticket-1',
      leadId: 'lead-1',
      status: 'Opportunity',
      agreedAmount: 100,
      currency: 'GHS',
      depositRequirementType: 'Full',
      requiredDeposit: 100,
      clearedDeposit: 100,
      depositThresholdMet: true,
    } satisfies PropertyEnquiryProspect;

    expect(canSubmitPropertyEstateHandoff(prospect, true)).toBe(false);
    expect(
      canSubmitPropertyEstateHandoff(
        { ...prospect, businessPartnerId: 'customer-1' },
        false
      )
    ).toBe(false);
    expect(
      canSubmitPropertyEstateHandoff(
        { ...prospect, businessPartnerId: 'customer-1' },
        true
      )
    ).toBe(true);
  });
});
