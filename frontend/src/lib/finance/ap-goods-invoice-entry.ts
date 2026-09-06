import type { ApGoodsInvoiceEntry } from '@/types/ap';

/** No PO quantity fallback: only exact, accepted, still-uninvoiced receipt lines are billable. */
export function receiptBasedInvoiceLines<T extends { id: string }>(items: T[], entry?: ApGoodsInvoiceEntry) {
    return items.flatMap(item => {
        const receipt = entry?.lines.find(line => line.purchaseOrderItemId === item.id);
        return receipt && receipt.availableQuantity > 0
            ? [{ ...item, invoiceQuantity: receipt.availableQuantity }]
            : [];
    });
}
