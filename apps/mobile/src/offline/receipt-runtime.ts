import { mobileApi } from "@/src/api/client";
import { sessionScope } from "@/src/offline/catalogue-runtime";
import { SqliteReceiptCache, type CachedReceiptDocument, type CanonicalMobilePosReceipt } from "@/src/offline/receipt-cache";
import { SqliteOutbox } from "@/src/offline/outbox";
import { getInstallationId } from "@/src/storage/secure-session";
import type { MobilePosBootstrap, MobilePosSyncPushResult, UserInfo } from "@/src/types/api";

export async function cacheSessionReceipt(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  receipt: CanonicalMobilePosReceipt,
): Promise<CachedReceiptDocument> {
  return (await SqliteReceiptCache.open(sessionScope(user, bootstrap))).save(receipt);
}

export async function loadSessionReceipts(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  limit = 100,
): Promise<CachedReceiptDocument[]> {
  return (await SqliteReceiptCache.open(sessionScope(user, bootstrap))).list(limit);
}

export interface ReceiptHydrationResult {
  cached: number;
  failed: number;
}

export async function hydrateSynchronizedSessionReceipts(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  limit = 100,
): Promise<ReceiptHydrationResult> {
  const cache = await SqliteReceiptCache.open(sessionScope(user, bootstrap));
  const installationId = await getInstallationId();
  const messages = await (await SqliteOutbox.open(sessionScope(user, bootstrap))).listRecent(["Synced"], limit);
  let cached = 0;
  let failed = 0;
  for (const message of messages) {
    const result = asSyncResult(message.serverResult);
    const kind = result?.sale?.saleId ? "SALE" : result?.collection?.collectionId ? "COLLECTION" : null;
    const receiptId = result?.sale?.saleId ?? result?.collection?.collectionId;
    if (!kind || !receiptId) continue;
    try {
      if (await cache.get(kind, receiptId)) continue;
      const receipt = kind === "SALE"
        ? await mobileApi.getReceipt(receiptId, installationId)
        : await mobileApi.getCollectionReceipt(receiptId, installationId);
      await cache.save(receipt);
      cached += 1;
    } catch {
      failed += 1;
    }
  }
  return { cached, failed };
}

function asSyncResult(value: unknown): MobilePosSyncPushResult | null {
  if (typeof value !== "object" || value === null) return null;
  const candidate = value as Partial<MobilePosSyncPushResult>;
  return candidate.state === "Synced" && typeof candidate.clientMutationId === "string"
    ? candidate as MobilePosSyncPushResult
    : null;
}
