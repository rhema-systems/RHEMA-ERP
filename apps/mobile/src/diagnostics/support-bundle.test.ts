import { describe, expect, it } from "vitest";
import { buildMobilePosSupportBundle } from "./support-bundle";

describe("Mobile POS support bundle", () => {
  it("contains operational identifiers and queue counts without transaction or credential data", () => {
    const bundle = buildMobilePosSupportBundle({
      profile: { environment: "UAT", apiBaseUrl: "https://uat.example.com" },
      appVersion: "1.2.3",
      generatedAt: new Date("2026-10-10T12:00:00.000Z"),
      reference: "MOBILE-TEST-0001",
      bootstrap: {
        environmentName: "UAT",
        userId: "user-secret-not-exported",
        userName: "cashier-secret-not-exported",
        serverTimeUtc: "2026-10-10T12:00:00.000Z",
        currentTillSessionId: "session-1",
        device: {
          id: "device-1",
          deviceName: "Till handset",
          status: "Active",
          revocationEpoch: 2,
          printerAdapterKey: "zcs-smartpos",
          scannerAdapterKey: "zcs-smartpos",
        },
        store: {
          id: "store-1",
          code: "ACC-01",
          name: "Accra Store",
          status: "Active",
          currencyCode: "GHS",
          timeZoneId: "Africa/Accra",
          defaultWalkInBusinessPartnerId: "customer-secret-not-exported",
          defaultWalkInBusinessPartnerRoleId: "role-secret-not-exported",
          defaultWalkInCustomerCode: "WALK-IN",
          defaultWalkInCustomerName: "Walk In",
        },
        till: {
          id: "till-1",
          mobilePosStoreId: "store-1",
          storeCode: "ACC-01",
          storeName: "Accra Store",
          tillNumber: "TILL-01",
          name: "Front till",
          status: "Active",
          liquidityAccountId: "liquidity-secret-not-exported",
          liquidityAccountCode: "CASH-01",
          currencyCode: "GHS",
          paymentMethods: [],
        },
      },
      offlineGrant: {
        id: "grant-1",
        version: 1,
        token: "signed-token-must-not-export",
        tenantId: "tenant-secret-not-exported",
        userId: "user-secret-not-exported",
        mobilePosDeviceId: "device-1",
        mobilePosStoreId: "store-1",
        mobilePosTillId: "till-1",
        cashierTillSessionId: "session-1",
        mobilePosOfflinePolicyId: "policy-1",
        issuedAtUtc: "2026-10-10T11:00:00.000Z",
        expiresAtUtc: "2026-10-10T13:00:00.000Z",
        revocationEpoch: 2,
        policySnapshotHash: "policy-secret-not-exported",
        policy: {
          policyId: "policy-1",
          policyName: "Offline",
          policyVersionUtc: "2026-10-10T00:00:00.000Z",
          currencyCode: "GHS",
          defaultWalkInBusinessPartnerId: "customer-secret-not-exported",
          defaultWalkInBusinessPartnerRoleId: "role-secret-not-exported",
          maximumOfflineAgeMinutes: 60,
          allowPartialPayment: false,
          allowDiscounts: false,
          allowProvisionalReceipt: true,
          allowDayEndSubmissionWithPendingSync: true,
          allowedCommandTypes: ["CashSale"],
          allowedPaymentMethods: [],
        },
      },
      outbox: { total: 5, pending: 2, rejected: 1, conflict: 1, manualReview: 1, receiptCacheFailures: 0, messages: [{ payload: { cardNumber: "must-not-export" } }] as never },
    });

    expect(bundle).toMatchObject({
      supportReference: "MOBILE-TEST-0001",
      environment: "UAT",
      apiOrigin: "https://uat.example.com",
      device: { id: "device-1", printerAdapterKey: "zcs-smartpos" },
      assignment: { storeCode: "ACC-01", tillNumber: "TILL-01", currentTillSessionId: "session-1" },
      offlineAuthorization: { grantId: "grant-1", policyId: "policy-1" },
      queue: { visibleUnresolvedTotal: 5, queryLimit: 500, pending: 2, rejected: 1, conflict: 1, manualReview: 1 },
    });
    const json = JSON.stringify(bundle);
    for (const forbidden of ["signed-token", "cardNumber", "customer-secret", "user-secret", "liquidity-secret", "policy-secret"]) {
      expect(json).not.toContain(forbidden);
    }
  });

  it("rejects an invalid evidence timestamp", () => {
    expect(() => buildMobilePosSupportBundle({
      profile: { environment: "TEST", apiBaseUrl: "http://10.0.2.2:5000" },
      generatedAt: new Date(Number.NaN),
    })).toThrow("timestamp is invalid");
  });
});
