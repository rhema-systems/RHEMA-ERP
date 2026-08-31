import type {
  CreateTenderBidDto,
  CreateTenderBidItemDto,
  TenderBidDetailDto,
  UpdateTenderBidDto,
} from '@/services/tenderBidService';
import type { TenderDetailDto } from '@/services/tenderService';

export type EditableTenderBid = CreateTenderBidDto & {
  associationType?: 'AllUsers' | 'Self' | 'SelectedUsers';
};

export function getDraftSelectedLotIds(
  draft: TenderBidDetailDto,
  tender: TenderDetailDto
): string[] {
  const explicitIds = draft.selectedLotIds?.filter(Boolean) ?? [];
  if (explicitIds.length > 0) return [...new Set(explicitIds)];

  const bidLotIds = draft.bidLots?.map((lot) => lot.lotId).filter(Boolean) ?? [];
  if (bidLotIds.length > 0) return [...new Set(bidLotIds)];

  const selectedTenderItemIds = new Set(
    (draft.items ?? []).map((item) => item.tenderItemId)
  );
  return (tender.lots ?? [])
    .filter((lot) =>
      (lot.items ?? []).some((item) => selectedTenderItemIds.has(item.id))
    )
    .map((lot) => lot.id);
}

export function mapDraftToEditableBid(
  draft: TenderBidDetailDto,
  tender: TenderDetailDto
): EditableTenderBid {
  const selectedLotIds = getDraftSelectedLotIds(draft, tender);

  return {
    tenderId: draft.tenderId,
    deliveryDays: draft.deliveryDays,
    paymentTerms: draft.paymentTerms ?? '',
    warrantyTerms: draft.warrantyTerms ?? '',
    technicalProposal: draft.technicalProposal ?? '',
    commercialProposal: draft.commercialProposal ?? '',
    selectedLotIds,
    associationType: draft.associationType,
    acceptedDeclaration: draft.acceptedDeclaration ?? false,
    items: (draft.items ?? []).map((item) => ({
      tenderItemId: item.tenderItemId,
      offeredQuantity: item.offeredQuantity,
      unitPrice: item.unitPrice,
      deliveryDays: item.deliveryDays,
      brand: item.brand ?? '',
      model: item.model ?? '',
      specifications: item.specifications ?? '',
      technicalDetails: item.technicalDetails ?? '',
    })),
  };
}

export function getItemsForSelectedLots(
  tender: TenderDetailDto,
  selectedLotIds: string[],
  existingItems: CreateTenderBidItemDto[]
): CreateTenderBidItemDto[] {
  const existingByTenderItemId = new Map(
    existingItems.map((item) => [item.tenderItemId, item])
  );

  return (tender.lots ?? [])
    .filter((lot) => selectedLotIds.includes(lot.id))
    .flatMap((lot) => lot.items ?? [])
    .map((item) =>
      existingByTenderItemId.get(item.id) ?? {
        tenderItemId: item.id,
        offeredQuantity: item.quantity,
        unitPrice: 0,
        deliveryDays: undefined,
        specifications: '',
        brand: '',
        model: '',
        technicalDetails: '',
      }
    );
}

export function buildDraftUpdate(
  bid: EditableTenderBid,
  selectedLotIds: string[]
): UpdateTenderBidDto {
  return {
    deliveryDays: bid.deliveryDays,
    paymentTerms: bid.paymentTerms,
    warrantyTerms: bid.warrantyTerms,
    technicalProposal: bid.technicalProposal,
    commercialProposal: bid.commercialProposal,
    associationType: bid.associationType,
    acceptedDeclaration: bid.acceptedDeclaration,
    selectedLotIds,
    items: bid.items.map((item) => ({
      tenderItemId: item.tenderItemId,
      offeredQuantity: item.offeredQuantity,
      unitPrice: item.unitPrice,
      deliveryDays: item.deliveryDays,
      specifications: item.specifications,
      brand: item.brand,
      model: item.model,
      technicalDetails: item.technicalDetails,
    })),
  };
}
