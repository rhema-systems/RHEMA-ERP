import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';

import type { VendorInvoice } from '@/types/ap';

import {
  AP_INVOICE_PRINT_BODY_CLASS,
  ApInvoicePrintDocument,
  printApInvoiceDocument,
} from './ApInvoicePrintDocument';

vi.mock('./ApInvoicePrintDocument.module.css', () => ({
  default: new Proxy({}, { get: (_target, property) => String(property) }),
}));

const openingInvoice: VendorInvoice = {
  id: 'invoice-id',
  invoiceNumber: 'VI-2026-00001',
  supplierInvoiceNumber: 'FINDEMO-AP-OB-001',
  supplierId: 'canonical-supplier-id',
  supplierName: 'Tema Engineering Services Ltd',
  invoiceDate: '2025-01-01T00:00:00Z',
  receivedDate: '2025-01-01T00:00:00Z',
  dueDate: '2025-01-31T00:00:00Z',
  subTotal: 150_000,
  taxAmount: 0,
  discountAmount: 0,
  totalAmount: 150_000,
  paidAmount: 0,
  balanceAmount: 150_000,
  currencyCode: 'GHS',
  exchangeRate: 1,
  baseCurrencyAmount: 150_000,
  paymentTermsDays: 30,
  earlyPaymentDiscountPercentage: 0,
  earlyPaymentDiscountAmount: 0,
  withholdingTaxRate: 0,
  withholdingTaxAmount: 0,
  matchingType: 'None',
  matchingStatus: 'Unmatched',
  matchingPriceTolerancePercent: 0,
  matchingQuantityTolerancePercent: 0,
  status: 'Approved',
  approvalStatus: 'Approved',
  notes: 'Source schedule FINDEMO-OPEN-AP-2025',
  matchingNotes: 'Opening balances do not use invoice matching.',
  isOpeningBalance: true,
  lineItems: [
    {
      id: 'line-id',
      vendorInvoiceId: 'invoice-id',
      lineItemType: 'Expense',
      glAccountId: 'migration-clearing-id',
      glAccountName: '1990 Migration Clearing',
      description: 'Opening supplier balance at cutover',
      quantity: 1,
      unitPrice: 150_000,
      lineTotal: 150_000,
      taxRate: 0,
      taxAmount: 0,
      discountPercentage: 0,
      discountAmount: 0,
    },
  ],
  paymentAllocations: [],
  createdAt: '2026-08-20T08:00:00Z',
};

describe('ApInvoicePrintDocument', () => {
  it('renders a controlled opening-balance register copy without screen actions', () => {
    const markup = renderToStaticMarkup(
      <ApInvoicePrintDocument
        invoice={openingInvoice}
        tenantName="Finance Demonstration & Mastery"
        tenantCode="FINANCE-DEMO"
      />
    );

    expect(markup).toContain('AP OPENING BALANCE VOUCHER');
    expect(markup).toContain('APPROVED — NOT POSTED');
    expect(markup).toContain('Finance Demonstration &amp; Mastery');
    expect(markup).toContain('FINANCE-DEMO');
    expect(markup).toContain('Tema Engineering Services Ltd');
    expect(markup).toContain('FINDEMO-AP-OB-001');
    expect(markup).toContain('1990 Migration Clearing');
    expect(markup).toContain('Opening supplier balance at cutover');
    expect(markup).toContain('Opening tax, WHT and discount are');
    expect(markup).toContain('system-controlled at zero');
    expect(markup).toContain('150,000.00');
    expect(markup).toContain('Source schedule FINDEMO-OPEN-AP-2025');
    expect(markup).toContain('Not posted');
    expect(markup).not.toContain(
      'Opening balances do not use invoice matching.'
    );
    expect(markup).not.toContain('Post Opening Balance');
    expect(markup).not.toContain('Schedule Payment');
    expect(markup).not.toContain('Void');
  });

  it('reconciles gross, discount, tax, total, paid and balance values for an ordinary invoice', () => {
    const markup = renderToStaticMarkup(
      <ApInvoicePrintDocument
        invoice={{
          ...openingInvoice,
          invoiceNumber: 'VI-2026-00003',
          supplierInvoiceNumber: 'SUP-INV-77',
          isOpeningBalance: false,
          subTotal: 900,
          discountAmount: 100,
          taxAmount: 135,
          totalAmount: 1_035,
          paidAmount: 200,
          balanceAmount: 835,
          lineItems: [
            {
              ...openingInvoice.lineItems[0],
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

    expect(markup).toContain('ACCOUNTS PAYABLE INVOICE RECORD');
    expect(markup).not.toContain('AP OPENING BALANCE VOUCHER');
    expect(markup).toContain('Gross line value');
    expect(markup).toContain('Line discounts');
    expect(markup).toContain('1,000.00');
    expect(markup).toContain('100.00');
    expect(markup).toContain('900.00');
    expect(markup).toContain('135.00');
    expect(markup).toContain('1,035.00');
    expect(markup).toContain('200.00');
    expect(markup).toContain('835.00');
    expect(markup).toContain('Amount settled / applied');
    expect(markup).not.toContain('OPENING BALANCE — CUTOVER / MIGRATION');
  });

  it('prints posting evidence when the invoice has a journal entry', () => {
    const markup = renderToStaticMarkup(
      <ApInvoicePrintDocument
        invoice={{ ...openingInvoice, journalEntryId: 'journal-entry-id' }}
        tenantName="Finance Demonstration & Mastery"
        tenantCode="FINANCE-DEMO"
      />
    );

    expect(markup).toContain('POSTED');
    expect(markup).toContain('journal-entry-id');
    expect(markup).not.toContain('APPROVED — NOT POSTED');
  });

  it('cleans up print isolation when the browser omits afterprint', () => {
    vi.useFakeTimers();
    const print = vi.fn();
    Object.defineProperty(window, 'print', {
      configurable: true,
      value: print,
    });

    printApInvoiceDocument();

    expect(print).toHaveBeenCalledOnce();
    expect(document.body.classList.contains(AP_INVOICE_PRINT_BODY_CLASS)).toBe(
      true
    );
    vi.runAllTimers();
    expect(document.body.classList.contains(AP_INVOICE_PRINT_BODY_CLASS)).toBe(
      false
    );
    vi.useRealTimers();
  });
});
