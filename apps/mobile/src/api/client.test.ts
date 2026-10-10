import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const storage = vi.hoisted(() => ({
  tokens: { accessToken: "old-access", refreshToken: "old-refresh" } as { accessToken: string; refreshToken: string } | null,
  profile: { environment: "TEST", apiBaseUrl: "https://erp.example.com" },
  saveTokens: vi.fn(),
  clearTokens: vi.fn(),
}));

vi.mock("@/src/storage/secure-session", () => ({
  loadServerProfile: vi.fn(async () => storage.profile),
  loadTokens: vi.fn(async () => storage.tokens),
  saveTokens: storage.saveTokens,
  clearTokens: storage.clearTokens,
}));

import { ApiProblem, mobileApi } from "@/src/api/client";

const jsonResponse = (body: unknown, status = 200, headers?: Record<string, string>) => new Response(
  JSON.stringify(body),
  { status, headers: { "content-type": "application/json", ...headers } },
);

describe("Mobile POS API client", () => {
  beforeEach(() => {
    storage.tokens = { accessToken: "old-access", refreshToken: "old-refresh" };
    storage.profile = { environment: "TEST", apiBaseUrl: "https://erp.example.com" };
    storage.saveTokens.mockImplementation(async (accessToken: string, refreshToken: string) => {
      storage.tokens = { accessToken, refreshToken };
    });
    storage.clearTokens.mockImplementation(async () => {
      storage.tokens = null;
    });
  });

  it("revalidates the stored server origin before exposing credentials", async () => {
    storage.profile = { environment: "PRODUCTION", apiBaseUrl: "http://attacker.example.com" };
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const error = await mobileApi.me().catch(caught => caught);

    expect(error).toBeInstanceOf(Error);
    expect((error as Error).message).toContain("Use HTTPS for remote servers");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("maps ProblemDetails detail, code, correlation and validation errors", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({
      title: "Validation failed",
      detail: "Select an approved customer.",
      errors: { customerId: ["Customer is required."] },
      extensions: { code: "CUSTOMER_REQUIRED", correlationId: "trace-123" },
    }, 400)));

    const error = await mobileApi.login({ username: "cashier", password: "secret", rememberMe: false })
      .catch(caught => caught);

    expect(error).toBeInstanceOf(ApiProblem);
    expect(error).toMatchObject({
      message: "Select an approved customer.",
      status: 400,
      code: "CUSTOMER_REQUIRED",
      correlationId: "trace-123",
      validationErrors: { customerId: ["Customer is required."] },
    });
  });

  it("rejects malformed successful JSON without exposing response content", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("<html>proxy failure</html>", {
      status: 200,
      headers: { "x-correlation-id": "proxy-456" },
    })));

    const error = await mobileApi.login({ username: "cashier", password: "secret", rememberMe: false })
      .catch(caught => caught);

    expect(error).toMatchObject({
      status: 502,
      code: "INVALID_SERVER_RESPONSE",
      correlationId: "proxy-456",
    });
    expect((error as Error).message).not.toContain("proxy failure");
  });

  it("sanitizes a non-JSON error response while preserving its correlation reference", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("upstream gateway unavailable", {
      status: 503,
      headers: { "x-correlation-id": "gateway-789" },
    })));

    const error = await mobileApi.login({ username: "cashier", password: "secret", rememberMe: false })
      .catch(caught => caught);

    expect(error).toMatchObject({
      message: "Request failed with HTTP 503.",
      status: 503,
      code: "NON_JSON_ERROR_RESPONSE",
      correlationId: "gateway-789",
    });
    expect((error as Error).message).not.toContain("gateway unavailable");
  });

  it("uses one refresh request for concurrent 401 responses and retries once", async () => {
    const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
      const url = input.toString();
      const authorization = (init?.headers as Record<string, string> | undefined)?.Authorization;
      if (url.endsWith("/api/auth/refresh")) {
        return jsonResponse({
          token: "new-access",
          refreshToken: "new-refresh",
          expiresAt: "2026-10-10T00:00:00Z",
        });
      }
      if (url.endsWith("/api/auth/me") && authorization === "Bearer old-access") {
        return jsonResponse({ title: "Unauthorized" }, 401);
      }
      if (url.endsWith("/api/auth/me") && authorization === "Bearer new-access") {
        return jsonResponse({
          id: "user-1",
          username: "cashier",
          email: "cashier@example.com",
          firstName: "Mobile",
          lastName: "Cashier",
          tenantId: "tenant-1",
          tenantName: "Tenant One",
          tenantCode: "T1",
          roles: [],
          permissions: ["MobilePOS.Access"],
          availableTenants: [],
          sessionId: "session-1",
        });
      }
      return jsonResponse({ title: "Unexpected request" }, 500);
    });
    vi.stubGlobal("fetch", fetchMock);

    const [first, second] = await Promise.all([mobileApi.me(), mobileApi.me()]);

    expect(first.username).toBe("cashier");
    expect(second.username).toBe("cashier");
    expect(fetchMock.mock.calls.filter(([input]) => input.toString().endsWith("/api/auth/refresh"))).toHaveLength(1);
    expect(storage.saveTokens).toHaveBeenCalledTimes(1);
    expect(storage.tokens).toEqual({ accessToken: "new-access", refreshToken: "new-refresh" });
  });

  it("requests an offline grant for the current installation with bearer authorization", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request) => jsonResponse({
      id: "grant-1",
      token: "signed-token",
      policy: { allowedCommandTypes: ["CashSale"] },
    }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await mobileApi.issueOfflineGrant("installation-123456");

    expect(result.id).toBe("grant-1");
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("https://erp.example.com/api/mobile-pos/v1/offline-grants");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer old-access");
    expect(JSON.parse(String(init.body))).toEqual({ installationId: "installation-123456" });
  });

  it("loads and opens the assigned Finance till session", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request, _init?: RequestInit) => jsonResponse({ sessionNumber: "TILL-0001" }));
    vi.stubGlobal("fetch", fetchMock);

    await mobileApi.getCurrentTillSession("installation/till 1");
    await mobileApi.openTillSession({
      installationId: "installation/till 1",
      openingFloatAmount: 125,
      openingNotes: "Opening count",
    });

    expect(fetchMock.mock.calls[0]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/till-sessions/current?installationId=installation%2Ftill%201",
    );
    const [url, init] = fetchMock.mock.calls[1] as unknown as [string, RequestInit];
    expect(url).toBe("https://erp.example.com/api/mobile-pos/v1/till-sessions/open");
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toEqual({
      installationId: "installation/till 1",
      openingFloatAmount: 125,
      openingNotes: "Opening count",
    });
  });

  it("loads the server-derived till reconciliation with encoded context", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request) => jsonResponse({ completedSaleCount: 2, salesAndTendersBalance: true }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await mobileApi.getTillReconciliation("session/1", "installation till/1");

    expect(result.completedSaleCount).toBe(2);
    expect(fetchMock.mock.calls[0]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/till-sessions/session%2F1/reconciliation?installationId=installation%20till%2F1",
    );
  });

  it("loads and submits governed till-close evidence", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request, _init?: RequestInit) => jsonResponse({ id: "close-1" }));
    vi.stubGlobal("fetch", fetchMock);

    await mobileApi.getTillCloseSubmission("session-1", "installation-1");
    await mobileApi.submitTillClose("session-1", {
      installationId: "installation-1",
      countLines: [{ denomination: 20, quantity: 3 }],
      varianceReason: "Counted twice",
      sessionRowVersion: "AQID",
      pendingClientMutationIds: ["mutation-123456"],
    });

    expect(fetchMock.mock.calls[0]?.[0].toString()).toContain("/till-sessions/session-1/close-submission?installationId=installation-1");
    const [url, init] = fetchMock.mock.calls[1] as unknown as [string, RequestInit];
    expect(url).toContain("/till-sessions/session-1/close-submission");
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toMatchObject({
      sessionRowVersion: "AQID",
      pendingClientMutationIds: ["mutation-123456"],
    });
  });

  it("encodes approved-customer search and outstanding-invoice request context", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request) => jsonResponse([]));
    vi.stubGlobal("fetch", fetchMock);

    await mobileApi.searchCustomers("installation/customer 1", "A&B Trading", 15);
    await mobileApi.getOutstandingInvoices("installation/customer 1", {
      businessPartnerId: "partner-1",
      businessPartnerRoleId: "role-1",
      code: "CUST-1",
      name: "A&B Trading",
      currencyCode: "GHS",
      isDefaultWalkInCustomer: false,
    });

    const calls = fetchMock.mock.calls as unknown as Array<[string | URL | Request]>;
    expect(calls[0]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/customers/search?installationId=installation%2Fcustomer%201&q=A%26B%20Trading&limit=15",
    );
    expect(fetchMock.mock.calls[1]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/customers/partner-1/outstanding-invoices?businessPartnerRoleId=role-1&installationId=installation%2Fcustomer%201",
    );
  });

  it("uses the governed catalogue, preview, and completion endpoints", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request) => jsonResponse({ lines: [], tenders: [] }));
    vi.stubGlobal("fetch", fetchMock);
    const previewRequest = {
      installationId: "install/01",
      lines: [{ clientLineId: "line-1", inventoryItemId: "item-1", quantity: 2, discountPercentage: 0 }],
    };

    await mobileApi.searchCatalogue("install/01", "A&B", 12);
    await mobileApi.getEligibleBankAccounts("install/01");
    await mobileApi.previewSale(previewRequest);
    await mobileApi.completeSale({
      installationId: "install/01",
      clientMutationId: "mutation-1",
      localReference: "POS-1",
      occurredAtUtc: "2026-10-09T10:00:00.000Z",
      expectedSubTotal: 20,
      expectedTaxAmount: 3,
      expectedDiscountAmount: 0,
      expectedTotalAmount: 23,
      lines: [{
        clientLineId: "line-1",
        inventoryItemId: "item-1",
        quantity: 2,
        unitPrice: 10,
        discountPercentage: 0,
        taxTreatment: 1,
      }],
      tenders: [{ paymentMethodId: "cash-1", amount: 23 }],
    });
    await mobileApi.getReceipt("sale/1", "install/01");
    await mobileApi.recordReceiptReprint("sale/1", {
      installationId: "install/01",
      clientEventId: "reprint-event-1",
      reason: "Customer copy",
    });

    expect(fetchMock.mock.calls[0]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/catalogue/search?installationId=install%2F01&q=A%26B&limit=12",
    );
    expect(fetchMock.mock.calls[1]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/bank-accounts?installationId=install%2F01",
    );
    const [, previewInit] = fetchMock.mock.calls[2] as unknown as [string, RequestInit];
    expect(fetchMock.mock.calls[2]?.[0].toString()).toBe("https://erp.example.com/api/mobile-pos/v1/sales/preview");
    expect(previewInit.method).toBe("POST");
    expect(JSON.parse(String(previewInit.body))).toEqual(previewRequest);
    const [, completeInit] = fetchMock.mock.calls[3] as unknown as [string, RequestInit];
    expect(fetchMock.mock.calls[3]?.[0].toString()).toBe("https://erp.example.com/api/mobile-pos/v1/sales");
    expect(completeInit.method).toBe("POST");
    expect(JSON.parse(String(completeInit.body))).toMatchObject({
      clientMutationId: "mutation-1",
      expectedTotalAmount: 23,
      tenders: [{ paymentMethodId: "cash-1", amount: 23 }],
    });
    expect(fetchMock.mock.calls[4]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/receipts/sale%2F1?installationId=install%2F01",
    );
    const [, reprintInit] = fetchMock.mock.calls[5] as unknown as [string, RequestInit];
    expect(fetchMock.mock.calls[5]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/receipts/sale%2F1/reprint-events",
    );
    expect(reprintInit.method).toBe("POST");
    expect(JSON.parse(String(reprintInit.body))).toEqual({
      installationId: "install/01",
      clientEventId: "reprint-event-1",
      reason: "Customer copy",
    });
  });

  it("encodes catalogue watermark and continuation cursor requests", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request) => jsonResponse({
      snapshotAtUtc: "2026-10-09T10:00:00.000Z",
      hasMore: false,
      upserts: [],
      tombstoneInventoryItemIds: [],
    }));
    vi.stubGlobal("fetch", fetchMock);

    await mobileApi.getCatalogueChanges(
      "install/01",
      "2026-10-08T10:00:00.000Z",
      "page+/=",
      125,
    );

    expect(fetchMock.mock.calls[0]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/catalogue/changes?installationId=install%2F01&sinceUtc=2026-10-08T10%3A00%3A00.000Z&cursor=page%2B%2F%3D&limit=125",
    );
  });

  it("encodes approved-customer watermark and continuation cursor requests", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request) => jsonResponse({
      snapshotAtUtc: "2026-10-09T11:00:00.000Z",
      hasMore: false,
      upserts: [],
      tombstoneBusinessPartnerRoleIds: [],
    }));
    vi.stubGlobal("fetch", fetchMock);

    await mobileApi.getCustomerChanges(
      "install/01",
      "2026-10-08T11:00:00.000Z",
      "customer-page+/=",
      75,
    );

    expect(fetchMock.mock.calls[0]?.[0].toString()).toBe(
      "https://erp.example.com/api/mobile-pos/v1/customers/changes?installationId=install%2F01&sinceUtc=2026-10-08T11%3A00%3A00.000Z&cursor=customer-page%2B%2F%3D&limit=75",
    );
  });

  it("reports the detected printer and scanner adapters in the device heartbeat", async () => {
    const fetchMock = vi.fn(async (_input: string | URL | Request, _init?: RequestInit) => jsonResponse({ id: "device-1", status: "Active", revocationEpoch: 1 }));
    vi.stubGlobal("fetch", fetchMock);

    await mobileApi.heartbeat({
      installationId: "install-01",
      appVersion: "0.1.0",
      operatingSystemVersion: "14",
      printerAdapterKey: "zcs-smartpos",
      scannerAdapterKey: "zcs-smartpos",
    });

    const call = fetchMock.mock.calls[0];
    expect(call?.[0].toString()).toBe("https://erp.example.com/api/mobile-pos/v1/heartbeat");
    const init = call?.[1];
    expect(init).toBeDefined();
    if (!init) throw new Error("Expected a heartbeat request body.");
    expect(JSON.parse(String(init.body))).toMatchObject({
      printerAdapterKey: "zcs-smartpos",
      scannerAdapterKey: "zcs-smartpos",
    });
  });
});
