import type {
  MobilePosCompleteSaleRequest,
  MobilePosSalePreview,
  MobilePosTenderInput,
} from "@/src/types/api";

export interface TenderDraft {
  paymentMethodId: string;
  amountText: string;
  externalReference: string;
}

export function parseMoney(value: string): number | null {
  const normalized = value.replace(/,/g, "").trim();
  if (normalized.length === 0) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

export function sumTenderDrafts(tenders: TenderDraft[], decimalPlaces: number): number {
  return roundMoney(tenders.reduce((total, tender) => total + (parseMoney(tender.amountText) ?? 0), 0), decimalPlaces);
}

export function buildCompleteSaleRequest(input: {
  installationId: string;
  clientMutationId: string;
  localReference: string;
  occurredAtUtc: string;
  preview: MobilePosSalePreview;
  tenders: TenderDraft[];
}): MobilePosCompleteSaleRequest {
  const tenders: MobilePosTenderInput[] = input.tenders.map(tender => {
    const amount = parseMoney(tender.amountText);
    if (amount == null || amount <= 0) {
      throw new Error("Every selected tender needs an amount greater than zero.");
    }
    return {
      paymentMethodId: tender.paymentMethodId,
      amount: roundMoney(amount, input.preview.currencyDecimalPlaces),
      externalReference: tender.externalReference.trim() || undefined,
    };
  });

  return {
    installationId: input.installationId,
    clientMutationId: input.clientMutationId,
    localReference: input.localReference,
    businessPartnerId: input.preview.businessPartnerId,
    businessPartnerRoleId: input.preview.businessPartnerRoleId,
    occurredAtUtc: input.occurredAtUtc,
    expectedSubTotal: input.preview.subTotal,
    expectedTaxAmount: input.preview.taxAmount,
    expectedDiscountAmount: input.preview.discountAmount,
    expectedTotalAmount: input.preview.totalAmount,
    lines: input.preview.lines.map(line => ({
      clientLineId: line.clientLineId,
      inventoryItemId: line.inventoryItemId,
      quantity: line.quantity,
      unitPrice: line.unitPrice,
      discountPercentage: line.discountPercentage,
      taxGroupId: line.taxGroupId,
      taxTreatment: line.taxTreatment,
    })),
    tenders,
  };
}

export function roundMoney(value: number, decimalPlaces: number): number {
  const factor = 10 ** Math.min(Math.max(decimalPlaces, 0), 6);
  return Math.round((value + Number.EPSILON) * factor) / factor;
}
