import { describe, expect, it } from "vitest";
import { buildReceiptHtml, receiptPdfFileName } from "@/src/receipts/render";
import type { MobilePosReceipt } from "@/src/types/api";

const receipt: MobilePosReceipt = {
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
    expect(receiptPdfFileName(receipt)).toBe("INV-001-reprint-2.pdf");
    expect(receiptPdfFileName({ ...receipt, copyType: "ORIGINAL", copyNumber: 0 })).toBe("INV-001-original.pdf");
  });
});
