import { beforeEach, describe, expect, it, vi } from 'vitest';

const { post } = vi.hoisted(() => ({ post: vi.fn() }));
vi.mock('./compatibleApiService', () => ({ compatibleApiService: { post } }));
import { estateFacilitiesService } from './estate-facilities.service';

describe('Facilities invoice Finance boundary', () => {
  beforeEach(() => post.mockReset());

  it('sends the canonical business partner and preserves the customer identity for the workspace', async () => {
    post.mockResolvedValue({ id: 'invoice-1', businessPartnerId: 'partner-1', status: 'Draft' });
    const invoice = await estateFacilitiesService.createArInvoice({
      customerId: 'partner-1', propertyUnit: 'UNIT-1', invoiceDate: '2026-09-29',
      currencyCode: 'GHS', reference: 'SERVICE', lineItems: [],
    });
    expect(post).toHaveBeenCalledWith('/estate/facilities/ar-billing/invoices', {
      invoice: { businessPartnerId: 'partner-1', invoiceDate: '2026-09-29',
        currencyCode: 'GHS', reference: 'SERVICE', lineItems: [] },
      propertyUnit: 'UNIT-1', sourceRecordReference: 'SERVICE',
    });
    expect(invoice.customerId).toBe('partner-1');
    expect(invoice.status).toBe('Draft');
  });

  it('maps the current Finance response after release', async () => {
    post.mockResolvedValue({ id: 'invoice-1', businessPartnerId: 'partner-1', status: 'Sent' });
    const invoice = await estateFacilitiesService.releaseArInvoice('invoice-1', 'UNIT-1');
    expect(post).toHaveBeenCalledWith('/estate/facilities/ar-billing/invoices/invoice-1/release', { propertyUnit: 'UNIT-1' });
    expect(invoice.customerId).toBe('partner-1');
    expect(invoice.status).toBe('Sent');
  });
});
