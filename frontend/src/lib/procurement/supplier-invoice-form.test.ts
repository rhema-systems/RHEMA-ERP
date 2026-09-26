import { describe, expect, it } from 'vitest';
import { supplierInvoiceSchema } from './supplier-invoice-form';

const savedDraft = () => ({
  supplierId: 'supplier-1',
  invoiceDate: new Date('2026-09-14'),
  dueDate: new Date('2026-10-14'),
  acceptedSupplyKind: null,
  acceptedSupplySourceId: null,
  exchangeRateId: null,
  lineItems: [{
    sourceLineId: '7589ec67-1204-42dc-b7c0-78ae234e9020',
    description: 'Freight / Shipping',
    quantity: 1,
    unitPrice: 200,
    purchaseOrderItemId: null,
    inventoryItemId: null,
    warehouseId: null,
    budgetEntryId: null,
    unit: null,
    taxTreatment: 5,
  }],
});

describe('supplier invoice edit validation', () => {
  it('allows a saved draft with null optional API links and retains pending tax review', () => {
    const result = supplierInvoiceSchema.parse(savedDraft());
    expect(result.acceptedSupplyKind).toBeUndefined();
    expect(result.acceptedSupplySourceId).toBeUndefined();
    expect(result.exchangeRateId).toBeUndefined();
    expect(result.lineItems[0].purchaseOrderItemId).toBeUndefined();
    expect(result.lineItems[0].taxTreatment).toBe(5);
    expect(result.lineItems[0].unitPrice).toBe(200);
  });

  it('preserves existing source links and multiline descriptions', () => {
    const draft = savedDraft();
    const result = supplierInvoiceSchema.parse({ ...draft,
      acceptedSupplyKind: 'GoodsReceiptInspection',
      acceptedSupplySourceId: 'receipt-1',
      lineItems: [{ ...draft.lineItems[0], purchaseOrderItemId: 'po-item-1', description: 'Freight\nShipping' }],
    });
    expect(result.acceptedSupplyKind).toBe('GoodsReceiptInspection');
    expect(result.acceptedSupplySourceId).toBe('receipt-1');
    expect(result.lineItems[0].purchaseOrderItemId).toBe('po-item-1');
    expect(result.lineItems[0].description).toBe('Freight\nShipping');
  });

  it('still rejects unsupported source kinds and invalid invoice values', () => {
    const draft = savedDraft();
    expect(supplierInvoiceSchema.safeParse({ ...draft, acceptedSupplyKind: 'Unsupported' }).success).toBe(false);
    expect(supplierInvoiceSchema.safeParse({ ...draft, supplierId: '' }).success).toBe(false);
    expect(supplierInvoiceSchema.safeParse({ ...draft, dueDate: new Date('2026-09-01') }).success).toBe(false);
    expect(supplierInvoiceSchema.safeParse({ ...draft, lineItems: [{ ...draft.lineItems[0], quantity: 0 }] }).success).toBe(false);
    expect(supplierInvoiceSchema.safeParse({ ...draft, lineItems: [{ ...draft.lineItems[0], description: '' }] }).success).toBe(false);
  });
});
