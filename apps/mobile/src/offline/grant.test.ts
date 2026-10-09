import { describe, expect, it } from "vitest";
import { isOfflineGrantUsable, offlineGrantMinutesRemaining } from "@/src/offline/grant";
import type { MobilePosBootstrap, MobilePosOfflineGrant } from "@/src/types/api";

const bootstrap: MobilePosBootstrap = {
  environmentName: "TEST",
  userId: "user-1",
  userName: "cashier",
  device: { id: "device-1", deviceName: "Till device", status: "Active", revocationEpoch: 4 },
  store: {
    id: "store-1",
    code: "ACC-01",
    name: "Accra Store",
    status: "Active",
    currencyCode: "GHS",
    timeZoneId: "Africa/Accra",
    defaultWalkInBusinessPartnerId: "partner-1",
    defaultWalkInBusinessPartnerRoleId: "role-1",
    defaultWalkInCustomerCode: "WALK-IN",
    defaultWalkInCustomerName: "Accra Walk-in Customer",
    offlinePolicyId: "policy-1",
    offlinePolicyName: "Standard Offline",
  },
  till: {
    id: "till-1",
    mobilePosStoreId: "store-1",
    storeCode: "ACC-01",
    storeName: "Accra Store",
    tillNumber: "TILL-01",
    name: "Till 01",
    status: "Active",
    liquidityAccountId: "account-1",
    liquidityAccountCode: "CASH-01",
    currencyCode: "GHS",
    paymentMethods: [],
  },
  offlinePolicy: {
    id: "policy-1",
    name: "Standard Offline",
    authorizationWindowMinutes: 120,
    maximumOfflineAgeMinutes: 240,
    allowCashSale: true,
    allowCashReceipt: true,
    allowPartialPayment: false,
    allowReturns: false,
    allowReversals: false,
    allowProvisionalReceipt: true,
    allowDayEndSubmissionWithPendingSync: false,
  },
  currentTillSessionId: "session-1",
  serverTimeUtc: "2026-10-09T09:00:00Z",
};

const grant: MobilePosOfflineGrant = {
  id: "grant-1",
  version: 1,
  token: "opaque-signed-token",
  tenantId: "tenant-1",
  userId: "user-1",
  mobilePosDeviceId: "device-1",
  mobilePosStoreId: "store-1",
  mobilePosTillId: "till-1",
  cashierTillSessionId: "session-1",
  mobilePosOfflinePolicyId: "policy-1",
  issuedAtUtc: "2026-10-09T09:00:00Z",
  expiresAtUtc: "2026-10-09T11:00:00Z",
  revocationEpoch: 4,
  policySnapshotHash: "ABC123",
  policy: {
    policyId: "policy-1",
    policyName: "Standard Offline",
    policyVersionUtc: "2026-10-09T08:00:00Z",
    currencyCode: "GHS",
    defaultWalkInBusinessPartnerId: "partner-1",
    defaultWalkInBusinessPartnerRoleId: "role-1",
    maximumOfflineAgeMinutes: 240,
    allowPartialPayment: false,
    allowDiscounts: false,
    allowProvisionalReceipt: true,
    allowDayEndSubmissionWithPendingSync: false,
    allowedCommandTypes: ["CashSale", "CashReceipt"],
    allowedPaymentMethods: [],
  },
};

describe("offline grant binding", () => {
  const now = new Date("2026-10-09T10:00:00Z");

  it("accepts an unexpired grant bound to the active context", () => {
    expect(isOfflineGrantUsable(grant, bootstrap, "tenant-1", now)).toBe(true);
    expect(offlineGrantMinutesRemaining(grant, now)).toBe(60);
  });

  it.each([
    ["expired", { expiresAtUtc: "2026-10-09T09:59:59Z" }],
    ["wrong tenant", { tenantId: "tenant-2" }],
    ["wrong user", { userId: "user-2" }],
    ["wrong device", { mobilePosDeviceId: "device-2" }],
    ["wrong store", { mobilePosStoreId: "store-2" }],
    ["wrong till", { mobilePosTillId: "till-2" }],
    ["wrong session", { cashierTillSessionId: "session-2" }],
    ["wrong policy", { mobilePosOfflinePolicyId: "policy-2" }],
    ["revoked device epoch", { revocationEpoch: 3 }],
  ])("rejects a %s grant", (_label, change) => {
    expect(isOfflineGrantUsable({ ...grant, ...change }, bootstrap, "tenant-1", now)).toBe(false);
  });

  it("rejects a grant when the walk-in customer snapshot no longer matches", () => {
    const changed = { ...grant, policy: { ...grant.policy, defaultWalkInBusinessPartnerId: "partner-2" } };
    expect(isOfflineGrantUsable(changed, bootstrap, "tenant-1", now)).toBe(false);
  });
});
