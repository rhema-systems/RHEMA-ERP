import { describe, expect, it } from "vitest";
import { buildCompleteCollectionRequest, sumCollectionAllocations } from "@/src/collections/checkout";
import type { OutstandingInvoice } from "@/src/types/api";

const invoices: OutstandingInvoice[] = [
  {
    id: "invoice-1", invoiceNumber: "INV-001", invoiceDate: "2026-10-01", totalAmount: 100,
    paidAmount: 0, balanceAmount: 100, daysOverdue: 0, currencyCode: "GHS",
    isDiscountAvailable: false, requiresTaxAdjustmentForDiscount: false,
  },
  {
    id: "invoice-2", invoiceNumber: "INV-002", invoiceDate: "2026-10-02", totalAmount: 80,
    paidAmount: 20, balanceAmount: 60, daysOverdue: 2, currencyCode: "GHS",
    isDiscountAvailable: false, requiresTaxAdjustmentForDiscount: false,
  },
];

describe("Mobile POS collection checkout", () => {
  it("builds partial multi-invoice allocations and split tenders at currency precision", () => {
    const request = buildCompleteCollectionRequest({
      installationId: "install-1",
      clientMutationId: "mutation-1",
      localReference: "COL-1",
      occurredAtUtc: "2026-10-10T10:00:00.000Z",
      businessPartnerId: "partner-1",
      businessPartnerRoleId: "role-1",
      invoices,
      allocations: [
        { invoiceId: "invoice-1", amountText: "70" },
        { invoiceId: "invoice-2", amountText: "30.004" },
      ],
      tenders: [
        { paymentMethodId: "cash", amountText: "60", externalReference: "" },
        { paymentMethodId: "card", amountText: "40", externalReference: " AUTH-1 ", bankAccountId: "bank-1" },
      ],
    });

    expect(request.allocations).toEqual([
      { invoiceId: "invoice-1", amount: 70 },
      { invoiceId: "invoice-2", amount: 30 },
    ]);
    expect(request.tenders).toEqual([
      { paymentMethodId: "cash", amount: 60, externalReference: undefined, bankAccountId: undefined },
      { paymentMethodId: "card", amount: 40, externalReference: "AUTH-1", bankAccountId: "bank-1" },
    ]);
    expect(sumCollectionAllocations([
      { invoiceId: "invoice-1", amountText: "70" },
      { invoiceId: "invoice-2", amountText: "30" },
    ])).toBe(100);
  });

  it("rejects an allocation above the live outstanding balance", () => {
    expect(() => buildCompleteCollectionRequest({
      installationId: "install-1",
      clientMutationId: "mutation-1",
      localReference: "COL-1",
      occurredAtUtc: "2026-10-10T10:00:00.000Z",
      businessPartnerId: "partner-1",
      businessPartnerRoleId: "role-1",
      invoices,
      allocations: [{ invoiceId: "invoice-2", amountText: "60.01" }],
      tenders: [{ paymentMethodId: "cash", amountText: "60.01", externalReference: "" }],
    })).toThrow("exceeds its outstanding balance");
  });

  it("requires tender total to equal the invoice allocation total", () => {
    expect(() => buildCompleteCollectionRequest({
      installationId: "install-1",
      clientMutationId: "mutation-1",
      localReference: "COL-1",
      occurredAtUtc: "2026-10-10T10:00:00.000Z",
      businessPartnerId: "partner-1",
      businessPartnerRoleId: "role-1",
      invoices,
      allocations: [{ invoiceId: "invoice-1", amountText: "50" }],
      tenders: [{ paymentMethodId: "cash", amountText: "49", externalReference: "" }],
    })).toThrow("Tender total must equal");
  });
});
