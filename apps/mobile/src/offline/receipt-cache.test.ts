import { describe, expect, it } from "vitest";
import { canonicalReceiptNumber, parseCanonicalReceipt } from "@/src/offline/receipt-cache";
import type { MobilePosCollectionReceipt, MobilePosReceipt } from "@/src/types/api";

const saleReceipt: MobilePosReceipt = {
  receiptKind: "SALE",
  receiptId: "sale-1",
  copyType: "ORIGINAL",
  copyNumber: 0,
  reprintCount: 0,
  generatedAtUtc: "2026-10-10T10:01:00.000Z",
  qrReference: "RHEMA|SALE|sale-1",
  tenantId: "tenant-1",
  tenantCode: "T1",
  tenantName: "Tenant One",
  storeId: "store-1",
  storeCode: "S1",
  storeName: "Store One",
  locationName: "Accra",
  tillId: "till-1",
  tillNumber: "TILL-1",
  tillName: "Main Till",
  tillSessionId: "session-1",
  tillSessionNumber: "TS-1",
  businessDate: "2026-10-10",
  deviceId: "device-1",
  deviceName: "Z92S-1",
  cashierUserId: "user-1",
  cashierName: "Cashier",
  businessPartnerId: "partner-1",
  businessPartnerRoleId: "role-1",
  customerCode: "C001",
  customerName: "Walk-in Customer",
  usedStoreDefaultCustomer: true,
  invoiceId: "invoice-1",
  invoiceNumber: "INV-001",
  invoiceStatus: "Posted",
  localReference: "MOB-001",
  occurredAtUtc: "2026-10-10T10:00:00.000Z",
  currencyCode: "GHS",
  subTotal: 10,
  taxAmount: 0,
  discountAmount: 0,
  totalAmount: 10,
  lines: [],
  tenders: [],
};

const collectionReceipt: MobilePosCollectionReceipt = {
  ...saleReceipt,
  receiptKind: "COLLECTION",
  receiptId: "collection-1",
  localReference: "COL-001",
  totalAmount: 10,
  wasRecordedOffline: true,
  allocations: [],
  tenders: [],
} as unknown as MobilePosCollectionReceipt;

describe("canonical receipt cache contracts", () => {
  it("uses the Finance invoice number for sales and the governed local reference for collections", () => {
    expect(canonicalReceiptNumber(saleReceipt)).toBe("INV-001");
    expect(canonicalReceiptNumber(collectionReceipt)).toBe("COL-001");
  });

  it("round-trips canonical sale and collection projections", () => {
    expect(parseCanonicalReceipt(JSON.stringify(saleReceipt))).toEqual(saleReceipt);
    expect(parseCanonicalReceipt(JSON.stringify(collectionReceipt))).toEqual(collectionReceipt);
  });

  it("rejects provisional, malformed, and structurally incomplete receipt data", () => {
    expect(() => parseCanonicalReceipt("not-json")).toThrow(/unreadable/);
    expect(() => parseCanonicalReceipt(JSON.stringify({ ...saleReceipt, receiptKind: "SALE_PROVISIONAL" }))).toThrow(/canonical/);
    expect(() => parseCanonicalReceipt(JSON.stringify({ ...saleReceipt, lines: undefined }))).toThrow(/incomplete/);
    expect(() => parseCanonicalReceipt(JSON.stringify({ ...collectionReceipt, copyType: "PROVISIONAL" }))).toThrow(/copy type/);
  });
});
