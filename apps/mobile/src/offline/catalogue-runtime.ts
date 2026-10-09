import { mobileApi } from "@/src/api/client";
import { SqliteCatalogueCache, synchronizeCatalogue } from "@/src/offline/catalogue-cache";
import { createOfflineScope } from "@/src/offline/database";
import type { MobilePosBootstrap, MobilePosCatalogueItem, UserInfo } from "@/src/types/api";

export async function synchronizeSessionCatalogue(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  installationId: string,
): Promise<number> {
  const cache = await openSessionCatalogue(user, bootstrap);
  return synchronizeCatalogue(cache, (sinceUtc, cursor) =>
    mobileApi.getCatalogueChanges(installationId, sinceUtc, cursor));
}

export async function searchSessionCatalogue(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  term: string,
  limit = 30,
): Promise<MobilePosCatalogueItem[]> {
  const cache = await openSessionCatalogue(user, bootstrap);
  return cache.search(term, limit);
}

function openSessionCatalogue(user: UserInfo, bootstrap: MobilePosBootstrap): Promise<SqliteCatalogueCache> {
  const tenantId = user.currentTenantId?.trim();
  if (!tenantId) throw new Error("Select a tenant before using the offline catalogue.");
  return SqliteCatalogueCache.open(createOfflineScope({
    tenantId,
    userId: bootstrap.userId,
    deviceId: bootstrap.device.id,
    storeId: bootstrap.store.id,
    tillId: bootstrap.till.id,
  }));
}
