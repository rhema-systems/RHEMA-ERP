import type { InventoryItemDto, WarehouseItemDto } from '@/services/inventoryManagementService';

// A selected item can be assigned to every target; existing pairs are skipped by the API.
export function eligibleAssignmentItems(
  items: InventoryItemDto[], assignments: WarehouseItemDto[], warehouseIds: string[],
): InventoryItemDto[] {
  if (!warehouseIds.length) return [];
  const existing = new Set(assignments.map(item => `${item.warehouseId}:${item.inventoryItemId}`));
  return items.filter(item => item.isActive && warehouseIds.some(id => !existing.has(`${id}:${item.id}`)));
}

export function retainEligibleSelection(selected: string[], eligibleIds: string[]): string[] {
  const allowed = new Set(eligibleIds);
  return selected.filter(id => allowed.has(id));
}

export function selectFilteredItems(selected: string[], filteredIds: string[], eligibleIds: string[]): string[] {
  return [...new Set([...retainEligibleSelection(selected, eligibleIds), ...retainEligibleSelection(filteredIds, eligibleIds)])];
}
