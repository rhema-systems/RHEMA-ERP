export function getPurchaseOrderItemMappingError(
  items: ReadonlyArray<{ inventoryItemId?: string | null }>
): string | null {
  const index = items.findIndex(item => !item.inventoryItemId?.trim() || item.inventoryItemId === '00000000-0000-0000-0000-000000000000');
  return index < 0 ? null : `Line ${index + 1}: select the matching saved inventory item. An approved item code alone is not an inventory selection.`;
}
