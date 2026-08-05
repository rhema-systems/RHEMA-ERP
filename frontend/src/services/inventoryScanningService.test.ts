import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  getInventoryScanDeviceId,
  InventoryScanOperation,
  inventoryScanningService,
  SynchronizeInventoryScanBatch,
} from './inventoryScanningService';

vi.mock('axios', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('inventoryScanningService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem('authToken', 'tenant-token');
  });

  it('persists a stable device identity without storing transaction context', () => {
    const first = getInventoryScanDeviceId();
    const second = getInventoryScanDeviceId();

    expect(first).toBe(second);
    expect(first).toMatch(/^web-/);
    expect(localStorage.length).toBe(2);
    expect(localStorage.getItem('tdcInventoryScanDeviceId')).toBe(first);
  });

  it('sends the durable idempotency key and full scan payload to synchronization', async () => {
    const request: SynchronizeInventoryScanBatch = {
      deviceId: 'scanner-01',
      idempotencyKey: 'batch-01',
      operation: InventoryScanOperation.TransferReceipt,
      documentId: 'document-01',
      warehouseId: 'warehouse-01',
      applyTransaction: true,
      lines: [{
        clientLineId: 'line-01',
        rawIdentifier: 'ITEM-QR-01',
        quantity: 2,
        locationIdentifier: 'BIN-A-01',
        lotNumber: 'LOT-01',
        serialNumber: 'SERIAL-01',
        scannedAtUtc: '2026-08-01T20:00:00.000Z',
      }],
    };
    vi.mocked(axios.post).mockResolvedValue({ data: { id: 'batch-result' } });

    await inventoryScanningService.synchronize(request);

    expect(axios.post).toHaveBeenCalledWith(
      '/api/inventory/mobile-scanning/synchronize',
      request,
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer tenant-token',
          'Idempotency-Key': 'batch-01',
          'X-Correlation-ID': 'batch-01',
        }),
      }),
    );
  });
});
