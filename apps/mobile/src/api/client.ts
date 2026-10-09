import {
  clearTokens,
  loadServerProfile,
  loadTokens,
  saveTokens,
} from "@/src/storage/secure-session";
import type {
  DeviceEnrollmentRequest,
  LoginRequest,
  LoginResponse,
  MobilePosBootstrap,
  MobilePosOpenTillSessionRequest,
  MobilePosTillReconciliation,
  MobilePosTillSession,
  MobilePosDevice,
  MobilePosOfflineGrant,
  MobilePosCustomerSearchResult,
  MobilePosCustomerChangePage,
  MobilePosCatalogueItem,
  MobilePosCatalogueChangePage,
  MobilePosBankAccountOption,
  MobilePosCompleteSaleRequest,
  MobilePosSalePreview,
  MobilePosSalePreviewRequest,
  MobilePosSaleResult,
  MobilePosSyncPushRequest,
  MobilePosSyncPushResult,
  MobilePosReceipt,
  MobilePosReceiptReprintRequest,
  OutstandingInvoice,
  SelectTenantResponse,
  UserInfo,
} from "@/src/types/api";

type ProblemPayload = {
  title?: string;
  detail?: string;
  message?: string;
  code?: string;
  errors?: Record<string, string[]> | string[];
  extensions?: { code?: string; correlationId?: string };
};

export class ApiProblem extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
    readonly correlationId?: string,
    readonly validationErrors?: ProblemPayload["errors"],
  ) {
    super(message);
    this.name = "ApiProblem";
  }
}

let refreshFlight: Promise<void> | null = null;

async function parseResponse<T>(response: Response): Promise<T> {
  const text = await response.text();
  const headerCorrelationId = response.headers.get("x-correlation-id") ?? undefined;
  let payload: (T & ProblemPayload) | undefined;
  if (text) {
    try {
      payload = JSON.parse(text) as T & ProblemPayload;
    } catch {
      if (response.ok) {
        throw new ApiProblem(
          "The server returned an unreadable response. Try again and contact support if it continues.",
          502,
          "INVALID_SERVER_RESPONSE",
          headerCorrelationId,
        );
      }
      throw new ApiProblem(
        `Request failed with HTTP ${response.status}.`,
        response.status,
        "NON_JSON_ERROR_RESPONSE",
        headerCorrelationId,
      );
    }
  }
  if (response.ok) return payload as T;

  const correlationId = payload?.extensions?.correlationId
    ?? headerCorrelationId;
  throw new ApiProblem(
    payload?.detail ?? payload?.message ?? payload?.title ?? `Request failed with HTTP ${response.status}.`,
    response.status,
    payload?.extensions?.code ?? payload?.code,
    correlationId,
    payload?.errors,
  );
}

async function rawRequest<T>(path: string, init: RequestInit, accessToken?: string): Promise<T> {
  const profile = await loadServerProfile();
  if (!profile) throw new ApiProblem("Configure the RHEMA ERP server before signing in.", 0, "SERVER_NOT_CONFIGURED");

  const response = await fetch(`${profile.apiBaseUrl}${path}`, {
    ...init,
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  return parseResponse<T>(response);
}

async function refreshSession(): Promise<void> {
  if (refreshFlight) return refreshFlight;
  refreshFlight = (async () => {
    const tokens = await loadTokens();
    if (!tokens) throw new ApiProblem("Your session has expired. Sign in again.", 401, "SESSION_EXPIRED");
    const response = await rawRequest<LoginResponse>("/api/auth/refresh", {
      method: "POST",
      body: JSON.stringify({ token: tokens.accessToken, refreshToken: tokens.refreshToken }),
    });
    if (!response.token || !response.refreshToken) {
      throw new ApiProblem("The server did not return refreshed credentials.", 401, "INVALID_REFRESH_RESPONSE");
    }
    await saveTokens(response.token, response.refreshToken, response.expiresAt);
  })();

  try {
    await refreshFlight;
  } catch (error) {
    await clearTokens();
    throw error;
  } finally {
    refreshFlight = null;
  }
}

async function authorizedRequest<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  const tokens = await loadTokens();
  if (!tokens) throw new ApiProblem("Sign in to continue.", 401, "SESSION_REQUIRED");
  try {
    return await rawRequest<T>(path, init, tokens.accessToken);
  } catch (error) {
    if (retry && error instanceof ApiProblem && error.status === 401) {
      await refreshSession();
      return authorizedRequest<T>(path, init, false);
    }
    throw error;
  }
}

export const mobileApi = {
  login: (request: LoginRequest) => rawRequest<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(request),
  }),
  me: () => authorizedRequest<UserInfo>("/api/auth/me"),
  selectTenant: (tenantCode: string) => authorizedRequest<SelectTenantResponse>("/api/auth/select-tenant", {
    method: "POST",
    body: JSON.stringify({ tenantCode, setAsDefault: false }),
  }),
  logout: async () => {
    const tokens = await loadTokens();
    if (!tokens) return;
    await authorizedRequest<void>("/api/auth/logout", {
      method: "POST",
      body: JSON.stringify({ refreshToken: tokens.refreshToken }),
    }, false);
  },
  bootstrap: (installationId: string) => authorizedRequest<MobilePosBootstrap>(
    `/api/mobile-pos/v1/bootstrap?installationId=${encodeURIComponent(installationId)}`,
  ),
  getCurrentTillSession: (installationId: string) => authorizedRequest<MobilePosTillSession | undefined>(
    `/api/mobile-pos/v1/till-sessions/current?installationId=${encodeURIComponent(installationId)}`,
  ),
  openTillSession: (request: MobilePosOpenTillSessionRequest) => authorizedRequest<MobilePosTillSession>(
    "/api/mobile-pos/v1/till-sessions/open",
    { method: "POST", body: JSON.stringify(request) },
  ),
  getTillReconciliation: (sessionId: string, installationId: string) => authorizedRequest<MobilePosTillReconciliation>(
    `/api/mobile-pos/v1/till-sessions/${encodeURIComponent(sessionId)}/reconciliation?installationId=${encodeURIComponent(installationId)}`,
  ),
  issueOfflineGrant: (installationId: string) => authorizedRequest<MobilePosOfflineGrant>(
    "/api/mobile-pos/v1/offline-grants",
    { method: "POST", body: JSON.stringify({ installationId }) },
  ),
  searchCustomers: (installationId: string, search: string, limit = 20) => authorizedRequest<MobilePosCustomerSearchResult[]>(
    `/api/mobile-pos/v1/customers/search?installationId=${encodeURIComponent(installationId)}&q=${encodeURIComponent(search)}&limit=${limit}`,
  ),
  getCustomerChanges: (installationId: string, sinceUtc?: string, cursor?: string, limit = 250) => {
    const parameters = [
      `installationId=${encodeURIComponent(installationId)}`,
      ...(sinceUtc ? [`sinceUtc=${encodeURIComponent(sinceUtc)}`] : []),
      ...(cursor ? [`cursor=${encodeURIComponent(cursor)}`] : []),
      `limit=${limit}`,
    ];
    return authorizedRequest<MobilePosCustomerChangePage>(`/api/mobile-pos/v1/customers/changes?${parameters.join("&")}`);
  },
  getOutstandingInvoices: (installationId: string, customer: MobilePosCustomerSearchResult) => authorizedRequest<OutstandingInvoice[]>(
    `/api/mobile-pos/v1/customers/${encodeURIComponent(customer.businessPartnerId)}/outstanding-invoices?businessPartnerRoleId=${encodeURIComponent(customer.businessPartnerRoleId)}&installationId=${encodeURIComponent(installationId)}`,
  ),
  searchCatalogue: (installationId: string, search: string, limit = 30) => authorizedRequest<MobilePosCatalogueItem[]>(
    `/api/mobile-pos/v1/catalogue/search?installationId=${encodeURIComponent(installationId)}&q=${encodeURIComponent(search)}&limit=${limit}`,
  ),
  getCatalogueChanges: (installationId: string, sinceUtc?: string, cursor?: string, limit = 250) => {
    const parameters = [
      `installationId=${encodeURIComponent(installationId)}`,
      ...(sinceUtc ? [`sinceUtc=${encodeURIComponent(sinceUtc)}`] : []),
      ...(cursor ? [`cursor=${encodeURIComponent(cursor)}`] : []),
      `limit=${limit}`,
    ];
    return authorizedRequest<MobilePosCatalogueChangePage>(`/api/mobile-pos/v1/catalogue/changes?${parameters.join("&")}`);
  },
  getEligibleBankAccounts: (installationId: string) => authorizedRequest<MobilePosBankAccountOption[]>(
    `/api/mobile-pos/v1/bank-accounts?installationId=${encodeURIComponent(installationId)}`,
  ),
  previewSale: (request: MobilePosSalePreviewRequest) => authorizedRequest<MobilePosSalePreview>(
    "/api/mobile-pos/v1/sales/preview",
    { method: "POST", body: JSON.stringify(request) },
  ),
  completeSale: (request: MobilePosCompleteSaleRequest) => authorizedRequest<MobilePosSaleResult>(
    "/api/mobile-pos/v1/sales",
    { method: "POST", body: JSON.stringify(request) },
  ),
  pushOfflineCommand: (request: MobilePosSyncPushRequest) => authorizedRequest<MobilePosSyncPushResult>(
    "/api/mobile-pos/v1/sync/push",
    { method: "POST", body: JSON.stringify(request) },
  ),
  getReceipt: (saleId: string, installationId: string) => authorizedRequest<MobilePosReceipt>(
    `/api/mobile-pos/v1/receipts/${encodeURIComponent(saleId)}?installationId=${encodeURIComponent(installationId)}`,
  ),
  recordReceiptReprint: (saleId: string, request: MobilePosReceiptReprintRequest) => authorizedRequest<MobilePosReceipt>(
    `/api/mobile-pos/v1/receipts/${encodeURIComponent(saleId)}/reprint-events`,
    { method: "POST", body: JSON.stringify(request) },
  ),
  requestEnrollment: (request: DeviceEnrollmentRequest) => authorizedRequest<MobilePosDevice>(
    "/api/mobile-pos/v1/devices/enrollment-requests",
    { method: "POST", body: JSON.stringify(request) },
  ),
};
