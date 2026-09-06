import { describe, expect, it } from 'vitest';
import { receiptBasedInvoiceLines } from './ap-goods-invoice-entry';

describe('receipt-based invoice quantities', () => {
    const items = [{ id: 'a', orderedQuantity: 20 }, { id: 'b', orderedQuantity: 50 }];
    it('does not use ordered quantities while acceptance is unavailable', () => {
        expect(receiptBasedInvoiceLines(items)).toEqual([]);
    });
    it('uses accepted less already invoiced and excludes pending/unreceived/fully billed lines', () => {
        expect(receiptBasedInvoiceLines(items, { purchaseOrderId: 'po', lines: [
            { purchaseOrderItemId: 'a', acceptedQuantity: 8, invoicedQuantity: 3, availableQuantity: 5 },
            { purchaseOrderItemId: 'b', acceptedQuantity: 7, invoicedQuantity: 7, availableQuantity: 0 },
        ] })).toEqual([{ ...items[0], invoiceQuantity: 5 }]);
    });
    it('does not attach an unrelated receipt line by position', () => {
        expect(receiptBasedInvoiceLines(items, { purchaseOrderId: 'po', lines: [
            { purchaseOrderItemId: 'foreign', acceptedQuantity: 9, invoicedQuantity: 0, availableQuantity: 9 },
        ] })).toEqual([]);
    });
});
