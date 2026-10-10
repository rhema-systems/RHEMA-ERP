import { describe, expect, it } from "vitest";
import { buildReceiptText } from "@/src/printing/receipt-text";
import type { MobilePosReceipt } from "@/src/types/api";

const receipt = {
  copyType: "REPRINT",
  copyNumber: 2,
  tenantName: "RHEMA Enterprise Resource Planning",
  storeName: "Accra Central Shop",
  tillNumber: "TILL-1",
  tillSessionNumber: "SHIFT-1",
  invoiceNumber: "INV/2026/001",
  customerName: "Walk-in Customer",
  customerCode: "WALK-IN",
  cashierName: "Ama Cashier",
  occurredAtUtc: "2026-10-10T10:15:00Z",
  localReference: "MOB-001",
  currencyCode: "GHS",
  subTotal: 100,
  discountAmount: 5,
  taxAmount: 15,
  totalAmount: 110,
  qrReference: "RHEMA|MOBILEPOS|INV/2026/001|sale-1",
  lines: [{
    description: "A product with a long receipt description",
    quantity: 2,
    unitOfMeasureCode: "Each",
    unitPrice: 50,
    discountAmount: 5,
    lineTotal: 110,
  }],
  tenders: [{
    paymentMethodName: "Cash",
    amount: 110,
    paymentNumber: "PAY-001",
    externalReference: undefined,
  }],
} as MobilePosReceipt;

describe("Z92S receipt text", () => {
  it("renders deterministic compact text within the 32 character paper width", () => {
    const text = buildReceiptText(receipt);
    const lines = text.split("\n");

    expect(text).toContain("REPRINT COPY 2");
    expect(text).toContain("INV/2026/001");
    expect(text).toContain("GHS 110.00");
    expect(text).toContain("PAY-001");
    expect(lines.every(line => line.length <= 32)).toBe(true);
  });

  it("rejects unsupported paper widths", () => {
    expect(() => buildReceiptText(receipt, 12)).toThrow(/between 24 and 48/);
  });
});
