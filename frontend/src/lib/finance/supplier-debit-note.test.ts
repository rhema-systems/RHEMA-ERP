import { describe, expect, it } from 'vitest';
import { CompoundBasis, TaxApplicability, TaxCategory } from '@/types/tax';

import {
  calculateSupplierDebitNoteLine,
  effectiveSupplierDebitNoteApplications,
  supplierDebitNoteApplicationLimit,
} from './supplier-debit-note';

describe('supplier debit-note controls', () => {
  it('calculates ordered compound purchase taxes and excludes withholding', () => {
    const line = calculateSupplierDebitNoteLine(
      {
        description: 'Credit',
        quantity: 2,
        unitPrice: 100,
        discountPercentage: 10,
      },
      {
        id: 'tax-group',
        tenantId: 'tenant',
        code: 'GH',
        name: 'Ghana purchase tax',
        applicability: TaxApplicability.Purchases,
        isDefault: false,
        isActive: true,
        createdAt: '2025-01-01',
        components: [
          {
            id: 'levy',
            taxId: 'levy',
            taxCode: 'LEVY',
            taxName: 'Levy',
            taxRate: 2.5,
            taxCategory: TaxCategory.Levy,
            calculationOrder: 1,
            compoundBasis: CompoundBasis.BaseOnly,
          },
          {
            id: 'vat',
            taxId: 'vat',
            taxCode: 'VAT',
            taxName: 'VAT',
            taxRate: 15,
            taxCategory: TaxCategory.Standard,
            calculationOrder: 2,
            compoundBasis: CompoundBasis.Cumulative,
          },
          {
            id: 'wht',
            taxId: 'wht',
            taxCode: 'WHT',
            taxName: 'WHT',
            taxRate: 3,
            taxCategory: TaxCategory.Withholding,
            calculationOrder: 3,
            compoundBasis: CompoundBasis.BaseOnly,
          },
        ],
      }
    );

    expect(line.discountAmount).toBe(20);
    expect(line.taxAmount).toBe(32.18);
    expect(line.lineTotal).toBe(212.18);
  });

  it('removes an application after its linked reversal', () => {
    const base = {
      supplierDebitNoteId: 'note',
      debitNoteNumber: 'DN-1',
      vendorPaymentId: 'payment',
      paymentNumber: 'PAY-1',
      vendorInvoiceId: 'invoice',
      invoiceNumber: 'INV-1',
      applicationAmount: 10,
      functionalAmount: 10,
      currencyCode: 'GHS',
      exchangeRate: 1,
      applicationDate: '2025-01-01',
      paymentPostingEventId: undefined,
      paymentJournalEntryId: undefined,
      appliedAt: undefined,
      createdAt: '2025-01-01',
      createdBy: 'user',
      supplierCreditNoteReference: undefined,
      notes: undefined,
    };
    const applications = [
      {
        ...base,
        id: 'original',
        isReversal: false,
        originalApplicationId: undefined,
      },
      {
        ...base,
        id: 'reversal',
        isReversal: true,
        originalApplicationId: 'original',
      },
    ];

    expect(effectiveSupplierDebitNoteApplications(applications)).toEqual([]);
  });

  it('caps an application at the lowest remaining balance', () => {
    expect(supplierDebitNoteApplicationLimit(350, 200)).toBe(200);
  });
});
