import type { InventoryTrackingException } from '@/services/inventoryTrackingControlService';

export type InventoryTrackingExceptionContext = {
  inventoryItemId?: string;
  warehouseId?: string;
  locationId?: string;
  referenceId?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
};

const same = (left?: string, right?: string) =>
  (left || '').trim().toUpperCase() === (right || '').trim().toUpperCase();

const optionalScopeMatches = (approved?: string, actual?: string) =>
  !approved || same(approved, actual);

export const filterInventoryTrackingExceptions = (
  exceptions: InventoryTrackingException[],
  context: InventoryTrackingExceptionContext,
  selectedId?: string
) => exceptions.filter((exception) => {
  if (exception.id === selectedId) return true;
  if (!exception.isAvailable) return false;
  if (context.inventoryItemId && exception.inventoryItemId !== context.inventoryItemId) return false;
  if (context.warehouseId && exception.warehouseId !== context.warehouseId) return false;
  if (context.referenceId && exception.referenceId !== context.referenceId) return false;
  if (exception.locationId && exception.locationId !== context.locationId) return false;
  return optionalScopeMatches(exception.lotNumber, context.lotNumber) &&
    optionalScopeMatches(exception.batchNumber, context.batchNumber) &&
    optionalScopeMatches(exception.serialNumber, context.serialNumber);
});

export const inventoryTrackingExceptionLabel = (exception: InventoryTrackingException) => {
  const controls = exception.exceptionCodes.join(', ');
  const expires = new Date(exception.expiresAtUtc).toLocaleString();
  return `${exception.itemCode} · ${exception.referenceNumber} · ${controls} · expires ${expires}`;
};
