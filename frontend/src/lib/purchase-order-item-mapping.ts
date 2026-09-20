export function getPurchaseOrderItemMappingError(
  items: ReadonlyArray<{ inventoryItemId?: string | null; itemDescription?: string; unitOfMeasure?: string }>
): string | null {
  const index = items.findIndex(item => !item.itemDescription?.trim() || !item.unitOfMeasure?.trim());
  return index < 0 ? null : `Line ${index + 1}: enter a description and unit of measure. Catalogue selection is optional; stock items are mapped before receiving.`;
}
