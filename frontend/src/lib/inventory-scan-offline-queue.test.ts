import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { InventoryScanOperation, SynchronizeInventoryScanBatch } from '@/services/inventoryScanningService';
import {
  flushInventoryScanQueue,
  listQueuedInventoryScanBatches,
  queueInventoryScanBatch,
} from './inventory-scan-offline-queue';

class FakeRequest<T> {
  result!: T;
  error: DOMException | null = null;
  onsuccess: (() => void) | null = null;
  onerror: (() => void) | null = null;
}

class FakeTransaction {
  oncomplete: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;
  error: DOMException | null = null;

  constructor(private readonly values: Map<string, unknown>) {}

  objectStore() {
    return {
      put: (value: unknown) => {
        const key = (value as { idempotencyKey: string }).idempotencyKey;
        this.values.set(key, structuredClone(value));
        queueMicrotask(() => this.oncomplete?.());
      },
      delete: (key: string) => {
        this.values.delete(key);
        queueMicrotask(() => this.oncomplete?.());
      },
      getAll: () => {
        const request = new FakeRequest<unknown[]>();
        queueMicrotask(() => {
          request.result = [...this.values.values()].map(value => structuredClone(value));
          request.onsuccess?.();
        });
        return request;
      },
    };
  }
}

class FakeDatabase {
  private created = false;
  private readonly values = new Map<string, unknown>();

  objectStoreNames = { contains: () => this.created };
  createObjectStore() { this.created = true; }
  transaction() { return new FakeTransaction(this.values); }
  close() {}
}

class FakeIndexedDb {
  private readonly database = new FakeDatabase();

  open() {
    const request = new FakeRequest<FakeDatabase>() as FakeRequest<FakeDatabase> & {
      onupgradeneeded: (() => void) | null;
    };
    request.onupgradeneeded = null;
    request.result = this.database;
    queueMicrotask(() => {
      if (!this.database.objectStoreNames.contains()) request.onupgradeneeded?.();
      request.onsuccess?.();
    });
    return request;
  }
}

const requiredOperations = [
  InventoryScanOperation.GoodsReceipt,
  InventoryScanOperation.RequisitionIssue,
  InventoryScanOperation.RequisitionReturn,
  InventoryScanOperation.TransferShipment,
  InventoryScanOperation.TransferReceipt,
];

const request = (operation: InventoryScanOperation, sequence: number): SynchronizeInventoryScanBatch => ({
  deviceId: 'scanner-e2e024',
  idempotencyKey: `e2e024-${sequence}`,
  operation,
  documentId: `document-${sequence}`,
  warehouseId: `warehouse-${sequence}`,
  applyTransaction: true,
  lines: [{
    clientLineId: `line-${sequence}`,
    rawIdentifier: `ITEM-${sequence}`,
    quantity: 1,
    scannedAtUtc: `2026-08-02T12:00:0${sequence}.000Z`,
  }],
});

describe('inventory scan offline queue', () => {
  beforeEach(() => {
    vi.stubGlobal('indexedDB', new FakeIndexedDb() as unknown as IDBFactory);
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-02T12:00:00.000Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('flushes all five required operations in durable capture order', async () => {
    for (const [index, operation] of requiredOperations.entries()) {
      vi.setSystemTime(new Date(`2026-08-02T12:00:0${index}.000Z`));
      await queueInventoryScanBatch(request(operation, index));
    }
    const synchronize = vi.fn().mockResolvedValue({ status: 'Applied' });

    const result = await flushInventoryScanQueue(synchronize);

    expect(synchronize.mock.calls.map(([value]) => value.operation)).toEqual(requiredOperations);
    expect(result).toEqual({ completed: requiredOperations.map((_, index) => `e2e024-${index}`), failed: [] });
    expect(await listQueuedInventoryScanBatches()).toEqual([]);
  });

  it('stops at the first failure and preserves dependent work for an ordered retry', async () => {
    for (const [index, operation] of requiredOperations.slice(0, 3).entries()) {
      vi.setSystemTime(new Date(`2026-08-02T12:00:0${index}.000Z`));
      await queueInventoryScanBatch(request(operation, index));
    }
    const before = await listQueuedInventoryScanBatches();
    const synchronize = vi.fn()
      .mockResolvedValueOnce({ status: 'Applied' })
      .mockRejectedValueOnce(new Error('temporary network failure'));

    const result = await flushInventoryScanQueue(synchronize);
    const remaining = await listQueuedInventoryScanBatches();

    expect(synchronize).toHaveBeenCalledTimes(2);
    expect(result).toEqual({
      completed: ['e2e024-0'],
      failed: [{ idempotencyKey: 'e2e024-1', lastError: 'temporary network failure' }],
    });
    expect(remaining.map(value => value.idempotencyKey)).toEqual(['e2e024-1', 'e2e024-2']);
    expect(remaining[0]).toMatchObject({ attempts: 2, lastError: 'temporary network failure' });
    expect(remaining[0].queuedAtUtc).toBe(before[1].queuedAtUtc);
    expect(remaining[1]).toMatchObject({ attempts: 1 });
  });
});
