import { parseMoney, roundMoney, type TenderDraft } from "@/src/sales/checkout";
import type {
  MobilePosCompleteCollectionRequest,
  MobilePosTenderInput,
  OutstandingInvoice,
} from "@/src/types/api";

export interface CollectionAllocationDraft {
  invoiceId: string;
  amountText: string;
}

export function sumCollectionAllocations(
  drafts: CollectionAllocationDraft[],
  decimalPlaces = 2,
): number {
  return roundMoney(
    drafts.reduce((total, draft) => total + (parseMoney(draft.amountText) ?? 0), 0),
    decimalPlaces,
  );
}

export function buildCompleteCollectionRequest(input: {
  installationId: string;
  clientMutationId: string;
  localReference: string;
  occurredAtUtc: string;
  businessPartnerId: string;
  businessPartnerRoleId: string;
  invoices: OutstandingInvoice[];
  allocations: CollectionAllocationDraft[];
  tenders: TenderDraft[];
  decimalPlaces?: number;
}): MobilePosCompleteCollectionRequest {
  const decimalPlaces = input.decimalPlaces ?? 2;
  const invoiceById = new Map(input.invoices.map(invoice => [invoice.id, invoice]));
  const allocations = input.allocations.flatMap(draft => {
    const amount = parseMoney(draft.amountText);
    if (amount == null || amount === 0) return [];
    const invoice = invoiceById.get(draft.invoiceId);
    if (!invoice) throw new Error("A selected invoice is no longer available.");
    const rounded = roundMoney(amount, decimalPlaces);
    if (rounded > roundMoney(invoice.balanceAmount, decimalPlaces)) {
      throw new Error(`The amount for ${invoice.invoiceNumber} exceeds its outstanding balance.`);
    }
    return [{ invoiceId: invoice.id, amount: rounded }];
  });
  if (allocations.length === 0) throw new Error("Enter a collection amount for at least one invoice.");

  const tenders: MobilePosTenderInput[] = input.tenders.map(tender => {
    const amount = parseMoney(tender.amountText);
    if (amount == null || amount <= 0) throw new Error("Every selected tender needs an amount greater than zero.");
    return {
      paymentMethodId: tender.paymentMethodId,
      amount: roundMoney(amount, decimalPlaces),
      externalReference: tender.externalReference.trim() || undefined,
      bankAccountId: tender.bankAccountId,
    };
  });
  if (tenders.length === 0) throw new Error("Select at least one tender.");

  const allocationTotal = roundMoney(allocations.reduce((total, item) => total + item.amount, 0), decimalPlaces);
  const tenderTotal = roundMoney(tenders.reduce((total, item) => total + item.amount, 0), decimalPlaces);
  if (allocationTotal !== tenderTotal) throw new Error("Tender total must equal the invoice allocation total.");

  return {
    installationId: input.installationId,
    clientMutationId: input.clientMutationId,
    localReference: input.localReference,
    businessPartnerId: input.businessPartnerId,
    businessPartnerRoleId: input.businessPartnerRoleId,
    occurredAtUtc: input.occurredAtUtc,
    allocations,
    tenders,
  };
}
