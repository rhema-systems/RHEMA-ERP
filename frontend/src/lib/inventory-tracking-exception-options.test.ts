import { describe, expect, it } from 'vitest';

import {
  filterInventoryTrackingExceptions,
  inventoryTrackingExceptionLabel,
} from './inventory-tracking-exception-options';
import type { InventoryTrackingException } from '@/services/inventoryTrackingControlService';

const exception = (overrides: Partial<InventoryTrackingException> = {}): InventoryTrackingException => ({
  id: 'exception-1', inventoryItemId: 'item-1', itemCode: 'ITEM-001', itemName: 'Item one',
  warehouseId: 'warehouse-1', warehouseName: 'Main warehouse', referenceId: 'reference-1',
  referenceType: 'InventoryRequisition', referenceNumber: 'REQ-001', exceptionCodes: ['INV_TRACKING_FIFO'],
  reason: 'Approved exception', workflowInstanceId: 'workflow-1', workflowEvidenceDocumentId: 'evidence-1',
  approvedById: 'approver-1', approvedAtUtc: '2026-08-29T08:00:00Z', expiresAtUtc: '2026-08-30T08:00:00Z',
  isAvailable: true, ...overrides,
});

describe('filterInventoryTrackingExceptions', () => {
  it('returns only available exceptions matching the exact known transaction scope', () => {
    const result = filterInventoryTrackingExceptions([
      exception(),
      exception({ id: 'wrong-item', inventoryItemId: 'item-2' }),
      exception({ id: 'wrong-reference', referenceId: 'reference-2' }),
      exception({ id: 'consumed', isAvailable: false }),
    ], { inventoryItemId: 'item-1', warehouseId: 'warehouse-1', referenceId: 'reference-1' });

    expect(result.map((value) => value.id)).toEqual(['exception-1']);
  });

  it('honours optional approved location and tracking-value scope', () => {
    const result = filterInventoryTrackingExceptions([
      exception({ id: 'general' }),
      exception({ id: 'matching', locationId: 'location-1', lotNumber: 'lot-1' }),
      exception({ id: 'wrong-location', locationId: 'location-2' }),
      exception({ id: 'wrong-lot', lotNumber: 'lot-2' }),
    ], {
      inventoryItemId: 'item-1', warehouseId: 'warehouse-1', referenceId: 'reference-1',
      locationId: 'location-1', lotNumber: 'LOT-1',
    });

    expect(result.map((value) => value.id)).toEqual(['general', 'matching']);
  });

  it('retains an existing selected value so failed option refreshes do not erase form state', () => {
    const result = filterInventoryTrackingExceptions([
      exception({ id: 'existing', isAvailable: false }),
    ], { inventoryItemId: 'different-item' }, 'existing');

    expect(result.map((value) => value.id)).toEqual(['existing']);
  });

  it('builds a business-readable option label', () => {
    expect(inventoryTrackingExceptionLabel(exception())).toContain('ITEM-001 · REQ-001 · INV_TRACKING_FIFO');
  });
});
