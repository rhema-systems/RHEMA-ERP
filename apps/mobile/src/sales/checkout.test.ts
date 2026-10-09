import { describe, expect, it } from "vitest";
import { buildCompleteSaleRequest, parseMoney, sumTenderDrafts } from "@/src/sales/checkout";
import type { MobilePosSalePreview } from "@/src/types/api";

const preview: MobilePosSalePreview = {
  businessPartnerId: "partner-1",
  businessPartnerRoleId: "role-1",
  customerCode: "WALKIN",
  customerName: "Walk-in customer",
  usedStoreDefaultCustomer: true,
  currencyCode: "GHS",
  currencyDecimalPlaces: 2,
  subTotal: 18,
  taxAmount: 2.7,
  discountAmount: 2,
  totalAmount: 20.7,
  calculatedAtUtc: "2026-10-09T12:00:00Z",
  lines: [{
    clientLineId: "line-1",
    inventoryItemId: "item-1",
    itemCode: "ITEM-1",
    description: "Item one",
    quantity: 2,
    unitPrice: 10,
    grossAmount: 20,
    discountPercentage: 10,
    discountAmount: 2,
    netAmount: 18,
    taxGroupId: "tax-1",
    taxTreatment: 0,
    taxAmount: 2.7,
    lineTotal: 20.7,
    unitOfMeasureCode: "Each",
  }],
};

describe("Mobile POS checkout helpers", () => {
  it("parses user-entered money and totals split tenders at currency precision", () => {
    expect(parseMoney("1,200.50")).toBe(1200.5);
    expect(parseMoney("not money")).toBeNull();
    expect(sumTenderDrafts([
      { paymentMethodId: "cash", amountText: "10.345", externalReference: "" },
      { paymentMethodId: "card", amountText: "10.354", externalReference: "AUTH" },
    ], 2)).toBe(20.7);
  });

  it("builds completion input only from the server preview and tender drafts", () => {
    const request = buildCompleteSaleRequest({
      installationId: "installation-1",
      clientMutationId: "mutation-1",
      localReference: "MOB-20261009-001",
      occurredAtUtc: "2026-10-09T12:01:00Z",
      preview,
      tenders: [
        { paymentMethodId: "cash", amountText: "5.70", externalReference: "" },
        { paymentMethodId: "card", amountText: "15", externalReference: " AUTH-001 " },
      ],
    });

    expect(request).toMatchObject({
      businessPartnerId: "partner-1",
      expectedSubTotal: 18,
      expectedTaxAmount: 2.7,
      expectedDiscountAmount: 2,
      expectedTotalAmount: 20.7,
      lines: [{ inventoryItemId: "item-1", unitPrice: 10, discountPercentage: 10, taxGroupId: "tax-1" }],
      tenders: [
        { paymentMethodId: "cash", amount: 5.7, externalReference: undefined },
        { paymentMethodId: "card", amount: 15, externalReference: "AUTH-001" },
      ],
    });
  });

  it("rejects a selected tender without a positive amount", () => {
    expect(() => buildCompleteSaleRequest({
      installationId: "installation-1",
      clientMutationId: "mutation-1",
      localReference: "MOB-1",
      occurredAtUtc: "2026-10-09T12:01:00Z",
      preview,
      tenders: [{ paymentMethodId: "cash", amountText: "0", externalReference: "" }],
    })).toThrow("amount greater than zero");
  });
});
