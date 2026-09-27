import React from 'react';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';

import type { Invoice } from '@/types/ar';

import {
  AR_INVOICE_PRINT_BODY_CLASS,
  ArInvoicePrintDocument,
  printArInvoiceDocument,
} from './ArInvoicePrintDocument';

vi.mock('./ArInvoicePrintDocument.module.css', () => ({
  default: new Proxy({}, { get: (_target, property) => String(property) }),
}));

const openingInvoice: Invoice = {
  id: 'invoice-id',
  invoiceNumber: 'INV-2026-00001',
  businessPartnerId: 'canonical-customer-id',
  businessPartnerRoleId: 'customer-role-id',
  businessPartnerArProfileVersionId: 'ar-profile-id',
  businessPartnerCode: 'CUS-001',
  customerName: 'Akua Payables',
  customerAddress: '14 Independence Avenue\nAccra',
  invoiceDate: '2025-01-01T00:00:00Z',
  dueDate: '2025-01-31T00:00:00Z',
  subTotal: 200_000,
  taxAmount: 0,
  discountAmount: 0,
  totalAmount: 200_000,
  paidAmount: 0,
  balanceAmount: 200_000,
  status: 'Approved',
  currencyCode: 'GHS',
  exchangeRate: 1,
  paymentTermsDays: 30,
  reference: 'FINDEMO-AR-OB-001',
  isOpeningBalance: true,
  notes: 'Source schedule FINDEMO-OPEN-AR-2025',
  lineItems: [
    {
      id: 'line-id',
      invoiceId: 'invoice-id',
      lineItemType: 'GLAccount',
      glAccountId: 'migration-clearing-id',
      glAccountCode: '1990',
      glAccountName: 'Migration Clearing',
      description: 'Opening customer balance at cutover',
      quantity: 1,
      unitPrice: 200_000,
      lineTotal: 200_000,
      taxRate: 0,
      taxAmount: 0,
      discountPercentage: 0,
      discountAmount: 0,
    },
  ],
  tenantId: 'tenant-id',
  createdAt: '2026-08-20T08:00:00Z',
};

describe('ArInvoicePrintDocument', () => {
  it('renders a controlled opening-balance voucher without browser-shell actions or fake branding', () => {
    const markup = renderToStaticMarkup(
      <ArInvoicePrintDocument
        invoice={openingInvoice}
        tenantName="Finance Demonstration & Mastery"
        tenantCode="FINANCE-DEMO"
      />
    );

    expect(markup).toContain('AR OPENING BALANCE VOUCHER');
    expect(markup).toContain('APPROVED');
    expect(markup).toContain('NOT POSTED');
    expect(markup).toContain('Finance Demonstration &amp; Mastery');
    expect(markup).toContain('FINANCE-DEMO');
    expect(markup).toContain('Akua Payables');
    expect(markup).toContain('canonical-customer-id');
    expect(markup).toContain('FINDEMO-AR-OB-001');
    expect(markup).toContain('1990 · Migration Clearing');
    expect(markup).toContain('Opening customer balance at cutover');
    expect(markup).toContain('Opening tax and discount');
    expect(markup).toContain('system-controlled at zero');
    expect(markup).toContain('200,000.00');
    expect(markup).toContain('Source schedule FINDEMO-OPEN-AR-2025');
    expect(markup).toContain('Amount settled / applied');
    expect(markup).not.toContain('Early discount due');
    expect(markup).not.toContain('CUSTOMER INVOICE');
    expect(markup).not.toContain('Acme Inc.');
    expect(markup).not.toContain('<button');
    expect(markup).not.toContain('Submit for Approval');
    expect(markup).not.toContain('Issue / Post');
    expect(markup).not.toContain('Record Receipt');
    expect(markup).not.toContain('>Print<');
  });

  it('reconciles canonical ordinary-invoice totals and omits opening controls', () => {
    const markup = renderToStaticMarkup(
      <ArInvoicePrintDocument
        invoice={{
          ...openingInvoice,
          invoiceNumber: 'INV-2026-00002',
          reference: 'CUSTOMER-PO-77',
          isOpeningBalance: false,
          status: 'Sent',
          subTotal: 900,
          taxAmount: 135,
          discountAmount: 50,
          totalAmount: 985,
          paidAmount: 200,
          balanceAmount: 785,
          lineItems: [
            {
              ...openingInvoice.lineItems[0],
              lineItemType: 'Product',
              productId: 'internal-product-id',
              glAccountId: undefined,
              glAccountCode: undefined,
              glAccountName: undefined,
              description: 'Professional services',
              unitPrice: 1_000,
              lineTotal: 1_000,
              discountPercentage: 10,
              discountAmount: 100,
              taxRate: 15,
              taxAmount: 135,
            },
          ],
        }}
        tenantName="Finance Demonstration & Mastery"
        tenantCode="FINANCE-DEMO"
      />
    );

    expect(markup).toContain('CUSTOMER INVOICE');
    expect(markup).not.toContain('AR OPENING BALANCE VOUCHER');
    expect(markup).toContain('SENT');
    expect(markup).toContain('NOT POSTED');
    expect(markup).toContain('CUSTOMER-PO-77');
    expect(markup).toContain('Gross line value');
    expect(markup).toContain('1,000.00');
    expect(markup).toContain('Line discounts');
    expect(markup).toContain('100.00');
    expect(markup).toContain('Subtotal after line discounts');
    expect(markup).toContain('900.00');
    expect(markup).toContain('Document discount');
    expect(markup).toContain('50.00');
    expect(markup).toContain('135.00');
    expect(markup).toContain('985.00');
    expect(markup).toContain('200.00');
    expect(markup).toContain('785.00');
    expect(markup).toContain('Early discount due');
    expect(markup).toContain('>Product<');
    expect(markup).not.toContain('internal-product-id');
    expect(markup).not.toContain('OPENING BALANCE — CUTOVER / MIGRATION');
  });

  it('prints separate document and ledger status with posting evidence', () => {
    const markup = renderToStaticMarkup(
      <ArInvoicePrintDocument
        invoice={{
          ...openingInvoice,
          status: 'Sent',
          journalEntryId: 'journal-entry-id',
        }}
        tenantName="Finance Demonstration & Mastery"
        tenantCode="FINANCE-DEMO"
      />
    );

    expect(markup).toContain('Document status');
    expect(markup).toContain('SENT');
    expect(markup).toContain('Ledger status');
    expect(markup).toContain('POSTED');
    expect(markup).toContain('journal-entry-id');
    expect(markup).not.toContain('Not posted');
  });

  it('uses the dedicated print integration on the AR invoice detail page', () => {
    const pageSource = readFileSync(
      join(process.cwd(), 'src/app/finance/ar/invoices/[id]/page.tsx'),
      'utf8'
    );

    expect(pageSource).toContain('printArInvoiceDocument');
    expect(pageSource).toContain('<ArInvoicePrintDocument');
    expect(pageSource).not.toContain('onClick={() => window.print()}');
    expect(pageSource).not.toContain('Acme Inc.');
    expect(pageSource).not.toContain('billing@acme.inc');
  });

  it('cleans up print isolation when the browser omits afterprint', () => {
    vi.useFakeTimers();
    const print = vi.fn();
    Object.defineProperty(window, 'print', {
      configurable: true,
      value: print,
    });

    printArInvoiceDocument();

    expect(print).toHaveBeenCalledOnce();
    expect(document.body.classList.contains(AR_INVOICE_PRINT_BODY_CLASS)).toBe(
      true
    );
    vi.runAllTimers();
    expect(document.body.classList.contains(AR_INVOICE_PRINT_BODY_CLASS)).toBe(
      false
    );
    vi.useRealTimers();
  });
});
