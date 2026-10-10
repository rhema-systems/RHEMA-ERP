import { ApiProblem, mobileApi } from "@/src/api/client";
import { cacheBankAccounts, loadBankAccounts } from "@/src/offline/configuration-cache";
import { sessionScope } from "@/src/offline/catalogue-runtime";
import { SqliteOutstandingInvoiceCache } from "@/src/offline/outstanding-invoice-cache";
import type {
  MobilePosBankAccountOption,
  MobilePosBootstrap,
  MobilePosCustomerSearchResult,
  OutstandingInvoice,
  UserInfo,
} from "@/src/types/api";

export interface OutstandingInvoiceLoadResult {
  invoices: OutstandingInvoice[];
  source: "Live" | "Cached";
  cachedAtUtc?: string;
}

export async function loadSessionOutstandingInvoices(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  installationId: string,
  customer: MobilePosCustomerSearchResult,
): Promise<OutstandingInvoiceLoadResult> {
  const cache = await SqliteOutstandingInvoiceCache.open(sessionScope(user, bootstrap));
  try {
    const invoices = await mobileApi.getOutstandingInvoices(installationId, customer);
    const snapshot = await cache.replaceCustomerSnapshot(
      customer.businessPartnerId,
      customer.businessPartnerRoleId,
      invoices,
    );
    return { invoices, source: "Live", cachedAtUtc: snapshot.cachedAtUtc };
  } catch (caught) {
    if (!isRetryableTransportFailure(caught)) throw caught;
    const snapshot = await cache.loadCustomerSnapshot(
      customer.businessPartnerId,
      customer.businessPartnerRoleId,
    );
    if (!snapshot) throw caught;
    return { invoices: snapshot.invoices, source: "Cached", cachedAtUtc: snapshot.cachedAtUtc };
  }
}

export async function loadSessionBankAccounts(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  installationId: string,
): Promise<MobilePosBankAccountOption[]> {
  const scope = sessionScope(user, bootstrap);
  try {
    const accounts = await mobileApi.getEligibleBankAccounts(installationId);
    await cacheBankAccounts(scope, accounts);
    return accounts;
  } catch (caught) {
    if (!isRetryableTransportFailure(caught)) throw caught;
    const cached = await loadBankAccounts(scope);
    if (!cached) throw caught;
    return cached;
  }
}

export function isRetryableTransportFailure(caught: unknown): boolean {
  if (!(caught instanceof ApiProblem)) return true;
  return caught.status === 0 || caught.status === 408 || caught.status === 429 || caught.status >= 500;
}
