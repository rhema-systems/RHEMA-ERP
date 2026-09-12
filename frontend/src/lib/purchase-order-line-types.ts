export type PurchaseOrderLineType = 1 | 2 | 3 | 4 | 'StockItem' | 'Service' | 'NonStock' | 'FixedAsset';

export function purchaseOrderLineType(value?: PurchaseOrderLineType): 1 | 2 | 3 | 4 {
  if (value === 2 || value === 'Service') return 2;
  if (value === 3 || value === 'NonStock') return 3;
  if (value === 4 || value === 'FixedAsset') return 4;
  return 1;
}

export function requiresPurchaseOrderStock(value?: PurchaseOrderLineType): boolean {
  return [1, 4].includes(purchaseOrderLineType(value));
}

export function purchaseOrderLineTypeLabel(value?: PurchaseOrderLineType): string {
  return { 1: 'Stock goods', 2: 'Service', 3: 'Non-stock goods', 4: 'Fixed asset' }[purchaseOrderLineType(value)];
}
