import { mobileApi } from "@/src/api/client";
import { SqliteCatalogueCache, synchronizeCatalogue } from "@/src/offline/catalogue-cache";
import { SqliteCustomerCache } from "@/src/offline/customer-cache";
import { cacheSessionConfiguration } from "@/src/offline/configuration-cache";
import { createOfflineScope } from "@/src/offline/database";
import type { MobilePosBootstrap, MobilePosCatalogueItem, MobilePosCustomerSearchResult, UserInfo } from "@/src/types/api";

export async function synchronizeSessionReferenceData(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  installationId: string,
): Promise<void> {
  const scope = sessionScope(user, bootstrap);
  await cacheSessionConfiguration(scope, bootstrap);
  if (user.permissions.includes("MobilePOS.Till.Operate")
    && user.permissions.includes("MobilePOS.Invoice.Create")) {
    await synchronizeCatalogue(await SqliteCatalogueCache.open(scope), (sinceUtc, cursor) =>
      mobileApi.getCatalogueChanges(installationId, sinceUtc, cursor));
  }
  if (user.permissions.includes("MobilePOS.Customer.View")) {
    await (await SqliteCustomerCache.open(scope)).synchronize((sinceUtc, cursor) =>
      mobileApi.getCustomerChanges(installationId, sinceUtc, cursor));
  }
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

export async function searchSessionCustomers(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  term: string,
  limit = 20,
): Promise<MobilePosCustomerSearchResult[]> {
  return (await SqliteCustomerCache.open(sessionScope(user, bootstrap))).search(term, limit);
}

function openSessionCatalogue(user: UserInfo, bootstrap: MobilePosBootstrap): Promise<SqliteCatalogueCache> {
  return SqliteCatalogueCache.open(sessionScope(user, bootstrap));
}

export function sessionScope(user: UserInfo, bootstrap: MobilePosBootstrap) {
  const tenantId = user.currentTenantId?.trim();
  if (!tenantId) throw new Error("Select a tenant before using the offline catalogue.");
  return createOfflineScope({
    tenantId,
    userId: bootstrap.userId,
    deviceId: bootstrap.device.id,
    storeId: bootstrap.store.id,
    tillId: bootstrap.till.id,
  });
}
