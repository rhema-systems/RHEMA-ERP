import { describe, expect, it, vi } from "vitest";

vi.mock("expo-crypto", () => ({
  CryptoDigestAlgorithm: { SHA256: "SHA-256" },
  digestStringAsync: vi.fn(async (value: string) => value),
}));

vi.mock("@/src/api/client", async () => {
  class ApiProblem extends Error {
    constructor(
      message: string,
      readonly status: number,
      readonly code?: string,
    ) {
      super(message);
    }
  }
  return { ApiProblem, mobileApi: { pushOfflineCommand: vi.fn() } };
});

vi.mock("@/src/storage/secure-session", () => ({ loadOfflineGrantById: vi.fn() }));

import { ApiProblem } from "@/src/api/client";
import { MobilePosOutboxDispatcher, type OutboxDispatchStore } from "@/src/offline/dispatcher";
import {
  SqliteOutbox,
  calculateOutboxRetryDelayMs,
  hashOutboxPayload,
  type OutboxMessage,
} from "@/src/offline/outbox";
import type { OfflineScope } from "@/src/offline/database";
import type { MobilePosOfflineGrant, MobilePosSyncPushResult } from "@/src/types/api";

const queueSize = 1_000;
const serializationLimitMs = 5_000;
const dispatchLimitMs = 10_000;

function message(index: number, state: OutboxMessage["state"] = "Pending"): OutboxMessage {
  const suffix = index.toString().padStart(4, "0");
  return {
    scopeKey: "tenant-1:user-1:device-1:store-1:till-1",
    clientMutationId: `mutation-${suffix}`,
    localReference: `LOCAL-${suffix}`,
    commandType: "CashSale",
    schemaVersion: 1,
    tenantId: "tenant-1",
    deviceId: "device-1",
    storeId: "store-1",
    tillId: "till-1",
    tillSessionId: "session-1",
    offlineGrantId: "grant-1",
    payload: {
      clientMutationId: `mutation-${suffix}`,
      localReference: `LOCAL-${suffix}`,
      lines: [{ itemId: `item-${index % 100}`, quantity: (index % 5) + 1 }],
      tenders: [{ paymentMethodId: "cash", amount: 10 + index }],
    },
    payloadHash: `hash-${suffix}`,
    state,
    attemptCount: 0,
    createdAtUtc: "2026-10-10T00:00:00.000Z",
  };
}

function grant(): MobilePosOfflineGrant {
  return {
    id: "grant-1",
    version: 1,
    token: "signed-grant-token",
    tenantId: "tenant-1",
    userId: "user-1",
    mobilePosDeviceId: "device-1",
    mobilePosStoreId: "store-1",
    mobilePosTillId: "till-1",
    cashierTillSessionId: "session-1",
    mobilePosOfflinePolicyId: "policy-1",
    issuedAtUtc: "2026-10-10T00:00:00.000Z",
    expiresAtUtc: "2026-10-10T12:00:00.000Z",
    revocationEpoch: 1,
    policySnapshotHash: "policy-hash",
    policy: {
      policyId: "policy-1",
      policyName: "Recovery rehearsal",
      policyVersionUtc: "2026-10-10T00:00:00.000Z",
      currencyCode: "GHS",
      defaultWalkInBusinessPartnerId: "partner-1",
      defaultWalkInBusinessPartnerRoleId: "role-1",
      maximumOfflineAgeMinutes: 720,
      allowPartialPayment: false,
      allowDiscounts: false,
      allowProvisionalReceipt: true,
      allowDayEndSubmissionWithPendingSync: true,
      allowedCommandTypes: ["CashSale"],
      allowedPaymentMethods: [],
    },
  };
}

class MemoryDispatchStore implements OutboxDispatchStore {
  readonly syncedIds: string[] = [];
  readonly retries: Array<{ id: string; code: string }> = [];

  constructor(readonly messages: OutboxMessage[]) {}

  async claimNext(): Promise<OutboxMessage | null> {
    const next = this.messages.find(candidate => candidate.state === "Pending");
    if (!next) return null;
    next.state = "Syncing";
    next.attemptCount += 1;
    return { ...next };
  }

  async markSynced(clientMutationId: string, serverResult: unknown): Promise<OutboxMessage> {
    const current = this.require(clientMutationId);
    current.state = "Synced";
    current.serverResult = serverResult;
    this.syncedIds.push(clientMutationId);
    return { ...current };
  }

  async markRejected(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    return this.terminal(clientMutationId, "Rejected", errorCode, errorDetail);
  }

  async markConflict(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    return this.terminal(clientMutationId, "Conflict", errorCode, errorDetail);
  }

  async markManualReview(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    return this.terminal(clientMutationId, "ManualReview", errorCode, errorDetail);
  }

  async retry(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    const current = this.require(clientMutationId);
    current.state = "Pending";
    current.errorCode = errorCode;
    current.errorDetail = errorDetail;
    this.retries.push({ id: clientMutationId, code: errorCode });
    return { ...current };
  }

  private terminal(
    clientMutationId: string,
    state: "Rejected" | "Conflict" | "ManualReview",
    errorCode: string,
    errorDetail: string,
  ): OutboxMessage {
    const current = this.require(clientMutationId);
    current.state = state;
    current.errorCode = errorCode;
    current.errorDetail = errorDetail;
    return { ...current };
  }

  private require(clientMutationId: string): OutboxMessage {
    const current = this.messages.find(candidate => candidate.clientMutationId === clientMutationId);
    if (!current) throw new Error(`Missing rehearsal message ${clientMutationId}.`);
    return current;
  }
}

describe("Mobile POS recovery and host performance rehearsal", () => {
  it("canonicalizes and hashes 1,000 distinct queued commands within the host threshold", async () => {
    const startedAt = Date.now();
    const hashes = new Set<string>();
    for (let index = 0; index < queueSize; index += 1) {
      const input = message(index);
      const result = await hashOutboxPayload(input.payload, async canonical => canonical);
      hashes.add(result.payloadHash);
    }
    const durationMs = Date.now() - startedAt;

    console.info(`MOBILE_POS_RECOVERY_METRIC|canonicalize|count=${queueSize}|durationMs=${durationMs}|limitMs=${serializationLimitMs}`);
    expect(hashes.size).toBe(queueSize);
    expect(durationMs).toBeLessThan(serializationLimitMs);
  }, 20_000);

  it("dispatches a 1,000 command backlog once per mutation within the host threshold", async () => {
    const store = new MemoryDispatchStore(Array.from({ length: queueSize }, (_, index) => message(index)));
    const pushed = new Set<string>();
    const dispatcher = new MobilePosOutboxDispatcher(
      store,
      async () => grant(),
      async request => {
        pushed.add(request.clientMutationId);
        return {
          state: "Synced",
          clientMutationId: request.clientMutationId,
          sale: { saleId: `sale-${request.clientMutationId}` },
        } as MobilePosSyncPushResult;
      },
    );

    const startedAt = Date.now();
    for (let index = 0; index < queueSize; index += 1) {
      await expect(dispatcher.dispatchNext()).resolves.toBe("Synced");
    }
    await expect(dispatcher.dispatchNext()).resolves.toBe("Idle");
    const durationMs = Date.now() - startedAt;

    console.info(`MOBILE_POS_RECOVERY_METRIC|dispatch|count=${queueSize}|durationMs=${durationMs}|limitMs=${dispatchLimitMs}`);
    expect(pushed.size).toBe(queueSize);
    expect(store.syncedIds).toHaveLength(queueSize);
    expect(new Set(store.syncedIds).size).toBe(queueSize);
    expect(durationMs).toBeLessThan(dispatchLimitMs);
  }, 20_000);

  it("reuses the original mutation identity after an ambiguous response loss", async () => {
    const original = message(1);
    const store = new MemoryDispatchStore([original]);
    const requests: Array<{ clientMutationId: string; payloadHash: string }> = [];
    let transportAttempt = 0;
    const dispatcher = new MobilePosOutboxDispatcher(
      store,
      async () => grant(),
      async request => {
        requests.push({ clientMutationId: request.clientMutationId, payloadHash: request.payloadHash });
        transportAttempt += 1;
        if (transportAttempt === 1) {
          throw new ApiProblem("The response was lost after server completion.", 0, "NETWORK_RESPONSE_LOST");
        }
        return {
          state: "Synced",
          clientMutationId: request.clientMutationId,
          sale: { saleId: "canonical-sale-1" },
        } as MobilePosSyncPushResult;
      },
    );

    await expect(dispatcher.dispatchNext()).resolves.toBe("RetryScheduled");
    await expect(dispatcher.dispatchNext()).resolves.toBe("Synced");

    expect(requests).toEqual([
      { clientMutationId: original.clientMutationId, payloadHash: original.payloadHash },
      { clientMutationId: original.clientMutationId, payloadHash: original.payloadHash },
    ]);
    expect(store.retries).toEqual([{ id: original.clientMutationId, code: "NETWORK_RESPONSE_LOST" }]);
    expect(store.syncedIds).toEqual([original.clientMutationId]);
  });

  it("caps retry delay and returns stale in-flight claims to the pending queue", async () => {
    const calls: unknown[][] = [];
    const database = {
      runAsync: vi.fn(async (...parameters: unknown[]) => {
        calls.push(parameters);
        return { changes: 3 };
      }),
    };
    const scope: OfflineScope = {
      scopeKey: "tenant-1:user-1:device-1:store-1:till-1",
      tenantId: "tenant-1",
      userId: "user-1",
      deviceId: "device-1",
      storeId: "store-1",
      tillId: "till-1",
    };
    const staleBefore = new Date("2026-10-10T09:55:00.000Z");
    const recoveredAt = new Date("2026-10-10T10:00:00.000Z");

    const recovered = await SqliteOutbox.fromDatabase(database as never, scope)
      .recoverInterruptedClaims(staleBefore, recoveredAt);

    expect(recovered).toBe(3);
    expect(calculateOutboxRetryDelayMs(1_000)).toBe(15 * 60_000);
    expect(calls).toHaveLength(1);
    expect(String(calls[0]?.[0])).toContain("state = 'Pending'");
    expect(String(calls[0]?.[0])).toContain("state = 'Syncing'");
    expect(calls[0]?.slice(1)).toEqual([
      recoveredAt.toISOString(),
      scope.scopeKey,
      staleBefore.toISOString(),
    ]);
  });
});
