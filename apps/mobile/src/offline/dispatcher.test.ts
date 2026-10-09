import { describe, expect, it, vi } from "vitest";

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
vi.mock("@/src/storage/secure-session", () => ({ loadOfflineGrant: vi.fn() }));

import { ApiProblem } from "@/src/api/client";
import { MobilePosOutboxDispatcher, type OutboxDispatchStore } from "./dispatcher";
import type { OutboxMessage } from "./outbox";
import type { MobilePosOfflineGrant, MobilePosSyncPushResult } from "@/src/types/api";

function message(): OutboxMessage {
  return {
    scopeKey: "tenant-1:user-1:device-1:store-1:till-1",
    clientMutationId: "mutation-1",
    localReference: "LOCAL-1",
    commandType: "CashSale",
    schemaVersion: 1,
    tenantId: "tenant-1",
    deviceId: "device-1",
    storeId: "store-1",
    tillId: "till-1",
    tillSessionId: "session-1",
    offlineGrantId: "grant-1",
    payload: { clientMutationId: "mutation-1", localReference: "LOCAL-1" },
    payloadHash: "abc123",
    state: "Syncing",
    attemptCount: 1,
    createdAtUtc: "2026-10-09T00:00:00.000Z",
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
    issuedAtUtc: "2026-10-09T00:00:00.000Z",
    expiresAtUtc: "2026-10-09T01:00:00.000Z",
    revocationEpoch: 1,
    policySnapshotHash: "policy-hash",
    policy: {
      policyId: "policy-1",
      policyName: "Offline",
      policyVersionUtc: "2026-10-09T00:00:00.000Z",
      currencyCode: "GHS",
      defaultWalkInBusinessPartnerId: "partner-1",
      defaultWalkInBusinessPartnerRoleId: "role-1",
      maximumOfflineAgeMinutes: 60,
      allowPartialPayment: false,
      allowDiscounts: false,
      allowProvisionalReceipt: true,
      allowDayEndSubmissionWithPendingSync: true,
      allowedCommandTypes: ["CashSale"],
      allowedPaymentMethods: [],
    },
  };
}

function store(next: OutboxMessage | null = message()) {
  const calls: Array<{ action: string; code?: string }> = [];
  const complete = async (action: string, _id: string, code?: string): Promise<OutboxMessage> => {
    calls.push({ action, code });
    return { ...message(), state: action === "synced" ? "Synced" : action === "retry" ? "Pending" : "ManualReview" } as OutboxMessage;
  };
  const value: OutboxDispatchStore = {
    claimNext: vi.fn(async () => next),
    markSynced: vi.fn(async id => complete("synced", id)),
    markRejected: vi.fn(async (id, code) => complete("rejected", id, code)),
    markConflict: vi.fn(async (id, code) => complete("conflict", id, code)),
    markManualReview: vi.fn(async (id, code) => complete("manual", id, code)),
    retry: vi.fn(async (id, code) => complete("retry", id, code)),
  };
  return { value, calls };
}

describe("Mobile POS outbox dispatcher", () => {
  it("loads the signed grant only at dispatch and marks a canonical result synced", async () => {
    const outbox = store();
    const push = vi.fn(async request => ({
      state: "Synced",
      clientMutationId: request.clientMutationId,
      sale: { saleId: "sale-1" },
    }) as MobilePosSyncPushResult);
    const dispatcher = new MobilePosOutboxDispatcher(outbox.value, async () => grant(), push);

    await expect(dispatcher.dispatchNext()).resolves.toBe("Synced");
    expect(push).toHaveBeenCalledWith(expect.objectContaining({
      offlineGrantToken: "signed-grant-token",
      payloadHash: "abc123",
      clientMutationId: "mutation-1",
    }));
    expect(outbox.calls).toEqual([{ action: "synced", code: undefined }]);
  });

  it.each([
    ["Rejected", "MOBILE_POS_LIMIT", "rejected", "Rejected"],
    ["Conflict", "MOBILE_POS_CONFLICT", "conflict", "Conflict"],
  ] as const)("maps %s server decisions to terminal outbox states", async (state, code, action, expected) => {
    const outbox = store();
    const dispatcher = new MobilePosOutboxDispatcher(
      outbox.value,
      async () => grant(),
      async () => ({ state, clientMutationId: "mutation-1", errorCode: code, errorDetail: "decision" }),
    );

    await expect(dispatcher.dispatchNext()).resolves.toBe(expected);
    expect(outbox.calls).toEqual([{ action, code }]);
  });

  it("schedules safe idempotent retry after an ambiguous transport failure", async () => {
    const outbox = store();
    const dispatcher = new MobilePosOutboxDispatcher(
      outbox.value,
      async () => grant(),
      async () => { throw new ApiProblem("Gateway timeout", 504, "UPSTREAM_TIMEOUT"); },
    );

    await expect(dispatcher.dispatchNext()).resolves.toBe("RetryScheduled");
    expect(outbox.calls).toEqual([{ action: "retry", code: "UPSTREAM_TIMEOUT" }]);
  });

  it("requires manual review when the secure grant no longer matches the queued scope", async () => {
    const outbox = store();
    const changed = { ...grant(), mobilePosTillId: "another-till" };
    const push = vi.fn();
    const dispatcher = new MobilePosOutboxDispatcher(outbox.value, async () => changed, push);

    await expect(dispatcher.dispatchNext()).resolves.toBe("ManualReview");
    expect(push).not.toHaveBeenCalled();
    expect(outbox.calls).toEqual([{ action: "manual", code: "MOBILE_POS_OFFLINE_GRANT_SCOPE_MISMATCH" }]);
  });
});
