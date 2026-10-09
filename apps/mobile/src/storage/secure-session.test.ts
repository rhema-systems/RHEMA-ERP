import { beforeEach, describe, expect, it, vi } from "vitest";
import type { MobilePosOfflineGrant } from "@/src/types/api";

const secureValues = vi.hoisted(() => new Map<string, string>());

vi.mock("expo-crypto", () => ({ randomUUID: vi.fn(() => "00000000-0000-0000-0000-000000000001") }));
vi.mock("react-native", () => ({ Platform: { OS: "android" } }));
vi.mock("expo-secure-store", () => ({
  WHEN_UNLOCKED_THIS_DEVICE_ONLY: "WHEN_UNLOCKED_THIS_DEVICE_ONLY",
  getItemAsync: vi.fn(async (key: string) => secureValues.get(key) ?? null),
  setItemAsync: vi.fn(async (key: string, value: string) => { secureValues.set(key, value); }),
  deleteItemAsync: vi.fn(async (key: string) => { secureValues.delete(key); }),
}));

import {
  clearOfflineGrant,
  deleteOfflineGrant,
  loadOfflineGrant,
  loadOfflineGrantById,
  saveOfflineGrant,
} from "./secure-session";

const grantId = "00000000-0000-0000-0000-000000000111";

function grant(): MobilePosOfflineGrant {
  return {
    id: grantId,
    version: 1,
    token: "signed-token",
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
    policySnapshotHash: "hash",
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

describe("secure offline grant vault", () => {
  beforeEach(() => secureValues.clear());

  it("keeps the grant referenced by durable work when the active session pointer is cleared", async () => {
    const value = grant();
    await saveOfflineGrant(value);

    await expect(loadOfflineGrant()).resolves.toEqual(value);
    await clearOfflineGrant();

    await expect(loadOfflineGrant()).resolves.toBeNull();
    await expect(loadOfflineGrantById(grantId)).resolves.toEqual(value);
  });

  it("deletes a retained grant only when durable work no longer needs it", async () => {
    await saveOfflineGrant(grant());
    await deleteOfflineGrant(grantId);

    await expect(loadOfflineGrant()).resolves.toBeNull();
    await expect(loadOfflineGrantById(grantId)).resolves.toBeNull();
  });

  it("rejects malformed grant identifiers before reading secure storage", async () => {
    await expect(loadOfflineGrantById("../token")).rejects.toThrow("offline grant ID is invalid");
  });
});
