import { describe, expect, it } from 'vitest';
import type { OutstandingVendorInvoice, SupplierDebitNote } from '@/types/ap';
import { filterSupplierCreditApplicationCandidates } from './SupplierDebitNoteApplicationsCard';

function note(
  id: string,
  currencyCode: string,
  remainingAmount = 100
): SupplierDebitNote {
  return {
    id,
    debitNoteNumber: `DN-${id}`,
    vendorId: 'bp-100',
    supplierId: 'supplier-900',
    vendorName: 'Tema Engineering Limited',
    debitNoteDate: '2025-04-01',
    currencyCode,
    exchangeRate: 1,
    subTotal: remainingAmount,
    taxAmount: 0,
    discountAmount: 0,
    totalAmount: remainingAmount,
    baseCurrencyAmount: remainingAmount,
    appliedAmount: 0,
    remainingAmount,
    applicationStatus: 'Available',
    approvalSource: 'SupplierDebitNoteWorkflow',
    status: 2,
    statusName: 'Posted',
    lineItems: [],
    applications: [],
    createdAt: '2025-04-01T00:00:00Z',
    rowVersion: 'row-version',
  };
}

function invoice(
  invoiceId: string,
  currencyCode: string,
  balanceAmount = 100
): OutstandingVendorInvoice {
  return {
    invoiceId,
    invoiceNumber: `INV-${invoiceId}`,
    invoiceDate: '2025-03-01',
    totalAmount: balanceAmount,
    paidAmount: 0,
    balanceAmount,
    currencyCode,
    daysOverdue: 0,
    isDiscountAvailable: false,
  };
}

describe('supplier debit-note application currency candidates', () => {
  it('keeps all eligible currencies visible before a note or invoice is selected', () => {
    const result = filterSupplierCreditApplicationCandidates(
      [note('usd-note', 'USD'), note('eur-note', 'EUR')],
      [invoice('usd-invoice', 'USD'), invoice('ghs-invoice', 'GHS')]
    );

    expect(result.notes.map((item) => item.id)).toEqual([
      'usd-note',
      'eur-note',
    ]);
    expect(result.invoices.map((item) => item.invoiceId)).toEqual([
      'usd-invoice',
      'ghs-invoice',
    ]);
  });

  it('allows a foreign-currency note/invoice pair and filters only by their shared currency', () => {
    const result = filterSupplierCreditApplicationCandidates(
      [note('usd-note', 'USD'), note('eur-note', 'EUR')],
      [invoice('usd-invoice', 'USD'), invoice('ghs-invoice', 'GHS')],
      'usd-note'
    );

    expect(result.invoices.map((item) => item.invoiceId)).toEqual([
      'usd-invoice',
    ]);
    expect(result.currenciesCompatible).toBe(true);
  });

  it('fails closed when stale selections identify different note and invoice currencies', () => {
    const result = filterSupplierCreditApplicationCandidates(
      [note('usd-note', 'USD')],
      [invoice('ghs-invoice', 'GHS')],
      'usd-note',
      'ghs-invoice'
    );

    expect(result.currenciesCompatible).toBe(false);
  });
});
