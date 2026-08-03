import { SynchronizeInventoryScanBatch } from '@/services/inventoryScanningService';

const DATABASE = 'tdc-inventory-mobile-scanning-v1';
const STORE = 'pendingScanBatches';

export type QueuedInventoryScanBatch = SynchronizeInventoryScanBatch & { queuedAtUtc: string; attempts: number; lastError?: string };
export type InventoryScanQueueFlushResult = {
  completed: string[];
  failed: Array<{ idempotencyKey: string; lastError: string }>;
};

const openDatabase = () => new Promise<IDBDatabase>((resolve, reject) => {
  const request = indexedDB.open(DATABASE, 1);
  request.onupgradeneeded = () => {
    const database = request.result;
    if (!database.objectStoreNames.contains(STORE)) database.createObjectStore(STORE, { keyPath: 'idempotencyKey' });
  };
  request.onsuccess = () => resolve(request.result);
  request.onerror = () => reject(request.error);
});

const complete = (transaction: IDBTransaction) => new Promise<void>((resolve, reject) => {
  transaction.oncomplete = () => resolve();
  transaction.onerror = () => reject(transaction.error);
  transaction.onabort = () => reject(transaction.error);
});

export async function queueInventoryScanBatch(request: SynchronizeInventoryScanBatch, lastError?: string) {
  const current = (await listQueuedInventoryScanBatches()).find(value => value.idempotencyKey === request.idempotencyKey);
  const database = await openDatabase();
  const transaction = database.transaction(STORE, 'readwrite');
  transaction.objectStore(STORE).put({ ...request, queuedAtUtc: current?.queuedAtUtc || new Date().toISOString(), attempts: (current?.attempts || 0) + 1, lastError });
  await complete(transaction);
  database.close();
}

export async function listQueuedInventoryScanBatches(): Promise<QueuedInventoryScanBatch[]> {
  const database = await openDatabase();
  const transaction = database.transaction(STORE, 'readonly');
  const result = await new Promise<QueuedInventoryScanBatch[]>((resolve, reject) => {
    const request = transaction.objectStore(STORE).getAll();
    request.onsuccess = () => resolve((request.result as QueuedInventoryScanBatch[]).sort((a, b) => a.queuedAtUtc.localeCompare(b.queuedAtUtc)));
    request.onerror = () => reject(request.error);
  });
  database.close();
  return result;
}

export async function removeQueuedInventoryScanBatch(idempotencyKey: string) {
  const database = await openDatabase();
  const transaction = database.transaction(STORE, 'readwrite');
  transaction.objectStore(STORE).delete(idempotencyKey);
  await complete(transaction);
  database.close();
}

export async function flushInventoryScanQueue(
  synchronize: (request: SynchronizeInventoryScanBatch) => Promise<unknown>,
): Promise<InventoryScanQueueFlushResult> {
  const queued = await listQueuedInventoryScanBatches();
  const completed: string[] = [];
  const failed: InventoryScanQueueFlushResult['failed'] = [];
  for (const request of queued) {
    try {
      await synchronize(request);
      await removeQueuedInventoryScanBatch(request.idempotencyKey);
      completed.push(request.idempotencyKey);
    } catch (error) {
      const lastError = error instanceof Error ? error.message : 'Synchronization failed';
      await queueInventoryScanBatch(request, lastError);
      failed.push({ idempotencyKey: request.idempotencyKey, lastError });
      // Preserve capture order. The operator can correct a permanent conflict and
      // explicitly retry without later dependent stock activity overtaking it.
      break;
    }
  }
  return { completed, failed };
}
