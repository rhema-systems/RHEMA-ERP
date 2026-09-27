import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { inventoryManagementService } from './inventoryManagementService';

vi.mock('axios');
const mockedAxios = vi.mocked(axios, true);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  localStorage.setItem('authToken', 'tenant-token');
  vi.stubGlobal('crypto', { randomUUID: vi.fn(() => '60800000-0000-4000-8000-000000000001') });
  mockedAxios.post.mockResolvedValue({ data: {} });
});

describe('inventoryManagementService controlled transfers', () => {
  const control = {
    rowVersion: 'AQID',
    idempotencyKey: 'dispatch-key',
    correlationId: 'transfer-dispatch:1',
    comment: 'Independent dispatch',
  };

  it('sends exact dispatch and receipt quantities with concurrency and replay controls', async () => {
    await inventoryManagementService.shipTransfer('transfer-1', control, 'TRACK-1', [{ itemId: 'line-1', shippedQuantity: 4 }]);
    await inventoryManagementService.receiveTransfer('transfer-1', { ...control, idempotencyKey: 'receipt-key' }, [{
      id: 'line-1', receivedQuantity: 2, damagedQuantity: 1, shortageQuantity: 1,
      discrepancyReasonCode: 'DAMAGED', discrepancyReason: 'Carrier damage and one missing unit',
      evidence: [{ centralDocumentVersionId: 'version-1', evidenceReference: 'DMS-608 / 1' }],
    }]);

    expect(mockedAxios.post.mock.calls[0][1]).toEqual(expect.objectContaining({
      rowVersion: 'AQID', idempotencyKey: 'dispatch-key', trackingNumber: 'TRACK-1',
      items: [{ itemId: 'line-1', shippedQuantity: 4 }],
    }));
    expect(mockedAxios.post.mock.calls[1][1]).toEqual(expect.objectContaining({
      rowVersion: 'AQID', idempotencyKey: 'receipt-key',
      receivedItems: [expect.objectContaining({ damagedQuantity: 1, shortageQuantity: 1, discrepancyReasonCode: 'DAMAGED' })],
    }));
  });

  it('carries central-DMS resolution lineage and independent closure controls', async () => {
    await inventoryManagementService.resolveTransferDiscrepancies('transfer-1', {
      ...control,
      discrepancyIds: ['discrepancy-1'],
      resolutionCode: 'REPLACEMENT_RECEIVED',
      resolutionNotes: 'Replacement inspected at destination',
      evidence: [{ centralDocumentVersionId: 'version-2', evidenceReference: 'DMS-RES / 2' }],
    });
    await inventoryManagementService.closeTransfer('transfer-1', { ...control, idempotencyKey: 'close-key' });

    expect(mockedAxios.post.mock.calls[0][0]).toMatch(/\/inventory\/transfers\/transfer-1\/resolve-discrepancies$/);
    expect(mockedAxios.post.mock.calls[0][1]).toEqual(expect.objectContaining({
      resolutionCode: 'REPLACEMENT_RECEIVED',
      evidence: [{ centralDocumentVersionId: 'version-2', evidenceReference: 'DMS-RES / 2' }],
    }));
    expect(mockedAxios.post.mock.calls[1][0]).toMatch(/\/inventory\/transfers\/transfer-1\/close$/);
    expect(mockedAxios.post.mock.calls[1][1]).toEqual(expect.objectContaining({ rowVersion: 'AQID', idempotencyKey: 'close-key' }));
    expect(mockedAxios.post.mock.calls[1][2]).toEqual(expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer tenant-token' }) }));
  });

  it('uses the owner picking endpoint and carries bin allocation and carrier identities unchanged', async () => {
    mockedAxios.get.mockResolvedValue({ data: [{ itemId: 'line-1', sourceLocationId: 'bin-a', quantityAvailable: 4 }] });
    expect(await inventoryManagementService.getTransferPickingOptions('transfer-1')).toEqual([{ itemId: 'line-1', sourceLocationId: 'bin-a', quantityAvailable: 4 }]);
    expect(mockedAxios.get).toHaveBeenCalledWith(expect.stringMatching(/\/inventory\/transfers\/transfer-1\/picking-options$/), expect.any(Object));
    await inventoryManagementService.shipTransfer('transfer-1', control, undefined, [{ itemId: 'line-1', shippedQuantity: 4, picks: [{ sourceLocationId: 'bin-a', quantity: 4 }] }], { carrierBusinessPartnerId: 'supplier-1', vehicleNumber: 'GT-42' });
    expect(mockedAxios.post.mock.calls[0][1]).toEqual(expect.objectContaining({ carrierBusinessPartnerId: 'supplier-1', vehicleNumber: 'GT-42', items: [{ itemId: 'line-1', shippedQuantity: 4, picks: [{ sourceLocationId: 'bin-a', quantity: 4 }] }] }));
    await inventoryManagementService.receiveTransfer('transfer-1', control, [{ id: 'line-1', receivedQuantity: 2, allocations: [{ dispatchAllocationId: 'dispatch-1', receivedQuantity: 2, destinationLocationId: 'dest-a' }] }]);
    expect(mockedAxios.post.mock.calls[1][1]).toEqual(expect.objectContaining({ receivedItems: [{ id: 'line-1', receivedQuantity: 2, allocations: [{ dispatchAllocationId: 'dispatch-1', receivedQuantity: 2, destinationLocationId: 'dest-a' }] }] }));
  });
});
