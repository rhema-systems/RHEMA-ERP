import { SynchronizeInventoryScanBatch } from '@/services/inventoryScanningService';

const DATABASE = 'tdc-inventory-mobile-scanning-v1';
const DATABASE_VERSION = 2;
const STORE = 'pendingScanBatches';

export type InventoryScanQueueScope = { tenantId: string; actorUserId: string };
export type QueuedInventoryScanBatch = SynchronizeInventoryScanBatch & {
  queueKey: string;
  scopeKey: string;
  tenantId: string;
  actorUserId: string;
  queuedAtUtc: string;
  attempts: number;
  lastError?: string;
};
export type InventoryScanQueueFlushResult = {
  completed: string[];
  failed: Array<{ idempotencyKey: string; lastError: string }>;
};

const normalize = (value: string) => value.trim().toLowerCase();
const scopeKey = (scope: InventoryScanQueueScope) => `${normalize(scope.tenantId)}:${normalize(scope.actorUserId)}`;
const queueKey = (scope: InventoryScanQueueScope, idempotencyKey: string) => `${scopeKey(scope)}:${normalize(idempotencyKey)}`;

const openDatabase = () => new Promise<IDBDatabase>((resolve, reject) => {
  const request = indexedDB.open(DATABASE, DATABASE_VERSION);
  request.onupgradeneeded = () => {
    const database = request.result;
    // Version 1 had no tenant/actor ownership. It cannot be migrated safely, so
    // discard it rather than exposing or replaying one operator's stock work as another.
    if (database.objectStoreNames.contains(STORE)) database.deleteObjectStore(STORE);
    const store = database.createObjectStore(STORE, { keyPath: 'queueKey' });
    store.createIndex('scopeKey', 'scopeKey', { unique: false });
  };
  request.onsuccess = () => resolve(request.result);
  request.onerror = () => reject(request.error);
});

const complete = (transaction: IDBTransaction) => new Promise<void>((resolve, reject) => {
  transaction.oncomplete = () => resolve();
  transaction.onerror = () => reject(transaction.error);
  transaction.onabort = () => reject(transaction.error);
});

export async function queueInventoryScanBatch(
  scope: InventoryScanQueueScope,
  request: SynchronizeInventoryScanBatch,
  lastError?: string,
) {
  const current = (await listQueuedInventoryScanBatches(scope))
    .find(value => value.idempotencyKey === request.idempotencyKey);
  const database = await openDatabase();
  const transaction = database.transaction(STORE, 'readwrite');
  transaction.objectStore(STORE).put({
    ...request,
    queueKey: queueKey(scope, request.idempotencyKey),
    scopeKey: scopeKey(scope),
    tenantId: scope.tenantId,
    actorUserId: scope.actorUserId,
    queuedAtUtc: current?.queuedAtUtc || new Date().toISOString(),
    attempts: (current?.attempts || 0) + 1,
    lastError,
  });
  await complete(transaction);
  database.close();
}

export async function listQueuedInventoryScanBatches(
  scope: InventoryScanQueueScope,
): Promise<QueuedInventoryScanBatch[]> {
  const database = await openDatabase();
  const transaction = database.transaction(STORE, 'readonly');
  const result = await new Promise<QueuedInventoryScanBatch[]>((resolve, reject) => {
    const request = transaction.objectStore(STORE).index('scopeKey').getAll(scopeKey(scope));
    request.onsuccess = () => resolve((request.result as QueuedInventoryScanBatch[])
      .sort((a, b) => a.queuedAtUtc.localeCompare(b.queuedAtUtc)));
    request.onerror = () => reject(request.error);
  });
  database.close();
  return result;
}

export async function removeQueuedInventoryScanBatch(scope: InventoryScanQueueScope, idempotencyKey: string) {
  const database = await openDatabase();
  const transaction = database.transaction(STORE, 'readwrite');
  transaction.objectStore(STORE).delete(queueKey(scope, idempotencyKey));
  await complete(transaction);
  database.close();
}

export async function flushInventoryScanQueue(
  scope: InventoryScanQueueScope,
  synchronize: (request: SynchronizeInventoryScanBatch) => Promise<unknown>,
): Promise<InventoryScanQueueFlushResult> {
  const queued = await listQueuedInventoryScanBatches(scope);
  const completed: string[] = [];
  const failed: InventoryScanQueueFlushResult['failed'] = [];
  for (const request of queued) {
    try {
      await synchronize(request);
      await removeQueuedInventoryScanBatch(scope, request.idempotencyKey);
      completed.push(request.idempotencyKey);
    } catch (error) {
      const lastError = error instanceof Error ? error.message : 'Synchronization failed';
      await queueInventoryScanBatch(scope, request, lastError);
      failed.push({ idempotencyKey: request.idempotencyKey, lastError });
      // Preserve capture order. The operator can correct a permanent conflict and
      // explicitly retry without later dependent stock activity overtaking it.
      break;
    }
  }
  return { completed, failed };
}
