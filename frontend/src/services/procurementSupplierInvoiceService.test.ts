import { describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService as finance } from './accountsPayableService';
import { accountsPayableService as procurement } from './procurementSupplierInvoiceService';

vi.mock('./api.service', () => ({ apiService: { get: vi.fn().mockResolvedValue({}), post: vi.fn().mockResolvedValue({}), put: vi.fn().mockResolvedValue({}) } }));

describe('supplier invoice workspace contracts', () => {
  it('keeps Finance and Procurement invoice reads on their respective routes', async () => {
    await finance.getInvoice('finance-id');
    await procurement.getInvoice('procurement-id');
    expect(apiService.get).toHaveBeenCalledWith('/ap/invoices/finance-id');
    expect(apiService.get).toHaveBeenCalledWith('/procurement/supplier-invoices/procurement-id');
  });
  it('uses the Procurement boundary for account distributions and workflow submission', async () => {
    await procurement.getInvoiceDistribution('invoice-id');
    await procurement.submitInvoiceForApproval('invoice-id');
    expect(apiService.get).toHaveBeenCalledWith('/procurement/supplier-invoices/invoice-id/distribution');
    expect(apiService.post).toHaveBeenCalledWith('/procurement/supplier-invoices/invoice-id/submit', {});
  });
  it('uses canonical partner, role and accounting date for Procurement defaults', async () => {
    await procurement.getInvoiceSupplierDefaults('partner-id', 'po-id', '2026-09-26', 'supplier-role');
    expect(apiService.get).toHaveBeenCalledWith('/procurement/supplier-invoices/supplier-defaults?businessPartnerId=partner-id&purchaseOrderId=po-id&invoiceDate=2026-09-26&businessPartnerRoleId=supplier-role');
  });
});
