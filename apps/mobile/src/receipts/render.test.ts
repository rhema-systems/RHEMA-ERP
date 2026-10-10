import { describe, expect, it } from "vitest";
import { buildReceiptHtml, receiptPdfFileName } from "@/src/receipts/render";
import type { MobilePosProvisionalCollectionReceipt, MobilePosProvisionalSaleReceipt, MobilePosReceipt } from "@/src/types/api";

const receipt: MobilePosReceipt = {
  receiptKind: "SALE",
  receiptId: "sale-1",
  copyType: "REPRINT",
  copyNumber: 2,
  reprintCount: 2,
  auditEventId: "audit-2",
  generatedAtUtc: "2026-10-09T12:05:00Z",
  reprintReason: "Customer copy",
  qrReference: "RHEMA|MOBILEPOS|INV/001|sale-1",
  tenantId: "tenant-1",
  tenantCode: "TDC",
  tenantName: "Rhema & <Partners>",
  storeId: "store-1",
  storeCode: "SHOP-1",
  storeName: "Accra Shop",
  locationName: "Accra",
  tillId: "till-1",
  tillNumber: "TILL-1",
  tillName: "Front till",
  tillSessionId: "session-1",
  tillSessionNumber: "SHIFT-1",
  businessDate: "2026-10-09T00:00:00Z",
  deviceId: "device-1",
  deviceName: "Z92S-1",
  cashierUserId: "user-1",
  cashierName: "Ama Cashier",
  businessPartnerId: "partner-1",
  businessPartnerRoleId: "role-1",
  customerCode: "WALK-IN",
  customerName: "Walk-in <script>alert(1)</script>",
  usedStoreDefaultCustomer: true,
  invoiceId: "invoice-1",
  invoiceNumber: "INV/001",
  invoiceStatus: "Paid",
  localReference: "MOB-001",
  occurredAtUtc: "2026-10-09T12:00:00Z",
  currencyCode: "GHS",
  subTotal: 100,
  taxAmount: 15,
  discountAmount: 5,
  totalAmount: 110,
  lines: [{
    sequence: 1,
    description: "Item & service",
    quantity: 2,
    unitPrice: 50,
    discountAmount: 5,
    taxAmount: 15,
    lineTotal: 110,
    unitOfMeasureCode: "Each",
  }],
  tenders: [{
    sequence: 1,
    paymentMethodCode: "CARD",
    paymentMethodName: "Card",
    amount: 110,
    externalReference: "AUTH&001",
    customerPaymentId: "payment-1",
    paymentNumber: "PAY-001",
    paymentStatus: "Posted",
  }],
};

const provisionalCollection: MobilePosProvisionalCollectionReceipt = {
  receiptKind: "COLLECTION_PROVISIONAL",
  receiptId: "mutation-1",
  copyType: "PROVISIONAL",
  copyNumber: 0,
  reprintCount: 0,
  generatedAtUtc: "2026-10-10T12:01:00Z",
  qrReference: "RHEMA|MOBILEPOS|PROVISIONAL|COL-001|mutation-1",
  tenantId: "tenant-1",
  tenantCode: "TDC",
  tenantName: "Rhema ERP",
  storeId: "store-1",
  storeCode: "SHOP-1",
  storeName: "Accra Shop",
  locationName: "Accra",
  tillId: "till-1",
  tillNumber: "TILL-1",
  tillName: "Front till",
  tillSessionId: "session-1",
  tillSessionNumber: "PENDING SYNC",
  businessDate: "2026-10-10",
  deviceId: "device-1",
  deviceName: "Z92S-1",
  cashierUserId: "user-1",
  cashierName: "Ama Cashier",
  businessPartnerId: "partner-1",
  businessPartnerRoleId: "role-1",
  customerCode: "CUST-1",
  customerName: "Customer One",
  localReference: "COL-001",
  occurredAtUtc: "2026-10-10T12:00:00Z",
  currencyCode: "GHS",
  totalAmount: 75,
  wasRecordedOffline: true,
  allocations: [{ sequence: 1, invoiceId: "invoice-1", invoiceNumber: "INV-001", amount: 75 }],
  tenders: [{ sequence: 1, paymentMethodCode: "CASH", paymentMethodName: "Cash", amount: 75 }],
};

const provisionalSale: MobilePosProvisionalSaleReceipt = {
  ...receipt,
  receiptKind: "SALE_PROVISIONAL",
  receiptId: "sale-mutation-1",
  copyType: "PROVISIONAL",
  copyNumber: 0,
  reprintCount: 0,
  generatedAtUtc: "2026-10-10T12:02:00Z",
  qrReference: "RHEMA|MOBILEPOS|PROVISIONAL|MOB-001|sale-mutation-1",
  tillSessionNumber: "PENDING SYNC",
  businessDate: "2026-10-10",
  occurredAtUtc: "2026-10-10T12:00:00Z",
  tenders: [{ sequence: 1, paymentMethodCode: "CASH", paymentMethodName: "Cash", amount: 110 }],
};

describe("receipt rendering", () => {
  it("renders a printable canonical receipt and escapes business data", () => {
    const html = buildReceiptHtml(receipt);

    expect(html).toContain("REPRINT &middot; COPY 2");
    expect(html).toContain("Rhema &amp; &lt;Partners&gt;");
    expect(html).toContain("Walk-in &lt;script&gt;alert(1)&lt;/script&gt;");
    expect(html).toContain("Item &amp; service");
    expect(html).toContain("PAY-001 &middot; AUTH&amp;001");
    expect(html).toContain("GHS 110.00");
    expect(html).not.toContain("<script>alert(1)</script>");
  });

  it("creates a filesystem-safe name that distinguishes the audited copy", () => {
    expect(receiptPdfFileName(receipt)).toBe("INV-001-sale-reprint-2.pdf");
    expect(receiptPdfFileName({ ...receipt, copyType: "ORIGINAL", copyNumber: 0 })).toBe("INV-001-sale-original.pdf");
  });

  it("renders a clearly non-final provisional collection slip", () => {
    const html = buildReceiptHtml(provisionalCollection);

    expect(html).toContain("PROVISIONAL &middot; PENDING SYNCHRONIZATION");
    expect(html).toContain("INV-001");
    expect(html).toContain("Pending server payment number");
    expect(html).toContain("not a final Finance receipt");
    expect(receiptPdfFileName(provisionalCollection)).toBe("COL-001-collection-provisional.pdf");
  });

  it("renders a provisional sale slip without canonical invoice or payment claims", () => {
    const html = buildReceiptHtml(provisionalSale);

    expect(html).toContain("PROVISIONAL &middot; PENDING SYNCHRONIZATION");
    expect(html).toContain("Provisional cash sale");
    expect(html).toContain("Pending server payment number");
    expect(html).toContain("not a final Finance receipt");
    expect(html).not.toContain("INV/001");
    expect(html).not.toContain("PAY-001");
    expect(receiptPdfFileName(provisionalSale)).toBe("MOB-001-sale-provisional.pdf");
  });
});
