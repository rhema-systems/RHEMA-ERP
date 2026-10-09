import { describe, expect, it, vi } from "vitest";

vi.mock("expo-crypto", () => ({
  CryptoDigestAlgorithm: { SHA256: "SHA-256" },
  digestStringAsync: vi.fn(async () => "mock-digest"),
}));

import {
  assertNoOutboxSecrets,
  assertOutboxTransition,
  calculateOutboxRetryDelayMs,
  canonicalOutboxJson,
  hashOutboxPayload,
} from "@/src/offline/outbox";

describe("Mobile POS transactional outbox", () => {
  it("canonicalizes equivalent command payloads to one stable representation and hash input", async () => {
    const first = { total: 12.5, customer: { name: "Walk In", id: "customer-1" }, lines: [{ quantity: 1, itemId: "item-1" }] };
    const second = { lines: [{ itemId: "item-1", quantity: 1 }], customer: { id: "customer-1", name: "Walk In" }, total: 12.5 };
    expect(canonicalOutboxJson(first)).toBe(canonicalOutboxJson(second));

    const digest = vi.fn(async (value: string) => `HASH:${value.length}`);
    const result = await hashOutboxPayload(first, digest);
    expect(digest).toHaveBeenCalledWith(result.payloadJson);
    expect(result.payloadHash).toBe(`hash:${result.payloadJson.length}`);
  });

  it("rejects payloads that cannot be represented safely", () => {
    expect(() => canonicalOutboxJson({ amount: Number.NaN })).toThrow("non-finite");
    const circular: Record<string, unknown> = {};
    circular.self = circular;
    expect(() => canonicalOutboxJson(circular)).toThrow("circular");
    expect(() => canonicalOutboxJson({ when: new Date() })).toThrow("unsupported object type");
  });

  it("prevents credentials and signed grant tokens from entering SQLite payloads", () => {
    expect(() => assertNoOutboxSecrets({ offlineGrantId: "grant-1", externalAuthorizationReference: "MOMO-1" })).not.toThrow();
    expect(() => assertNoOutboxSecrets({ offlineGrantToken: "signed-secret" })).toThrow("must not persist");
    expect(() => assertNoOutboxSecrets({ nested: { access_token: "jwt" } })).toThrow("must not persist");
  });

  it("allows only the governed lifecycle transitions", () => {
    expect(() => assertOutboxTransition("DraftLocal", "Pending")).not.toThrow();
    expect(() => assertOutboxTransition("Pending", "Syncing")).not.toThrow();
    expect(() => assertOutboxTransition("Syncing", "Synced")).not.toThrow();
    expect(() => assertOutboxTransition("Syncing", "Pending")).not.toThrow();
    expect(() => assertOutboxTransition("Conflict", "ManualReview")).not.toThrow();
    expect(() => assertOutboxTransition("ManualReview", "Pending")).not.toThrow();
    expect(() => assertOutboxTransition("Pending", "Synced")).toThrow("not allowed");
    expect(() => assertOutboxTransition("Synced", "Pending")).toThrow("not allowed");
    expect(() => assertOutboxTransition("Rejected", "Pending")).toThrow("not allowed");
  });

  it("uses bounded exponential retry delays", () => {
    expect(calculateOutboxRetryDelayMs(1)).toBe(5_000);
    expect(calculateOutboxRetryDelayMs(2)).toBe(10_000);
    expect(calculateOutboxRetryDelayMs(8)).toBe(640_000);
    expect(calculateOutboxRetryDelayMs(10)).toBe(900_000);
    expect(calculateOutboxRetryDelayMs(100)).toBe(900_000);
  });
});
