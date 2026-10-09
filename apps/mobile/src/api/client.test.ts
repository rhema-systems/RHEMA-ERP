import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const storage = vi.hoisted(() => ({
  tokens: { accessToken: "old-access", refreshToken: "old-refresh" } as { accessToken: string; refreshToken: string } | null,
  saveTokens: vi.fn(),
  clearTokens: vi.fn(),
}));

vi.mock("@/src/storage/secure-session", () => ({
  loadServerProfile: vi.fn(async () => ({ environment: "TEST", apiBaseUrl: "https://erp.example.com" })),
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
    storage.saveTokens.mockImplementation(async (accessToken: string, refreshToken: string) => {
      storage.tokens = { accessToken, refreshToken };
    });
    storage.clearTokens.mockImplementation(async () => {
      storage.tokens = null;
    });
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
});
