import { describe, expect, it } from 'vitest';
import type { TenderBidDetailDto } from '@/services/tenderBidService';
import type { TenderDetailDto } from '@/services/tenderService';
import {
  buildDraftUpdate,
  getItemsForSelectedLots,
  mapDraftToEditableBid,
} from './tender-bid-draft';

const tender = {
  id: 'tender-1',
  lots: [
    {
      id: 'lot-1',
      items: [
        {
          id: 'item-1',
          quantity: 12,
        },
      ],
    },
  ],
  // Some tender detail projections do not duplicate nested lot items here.
  items: [],
} as TenderDetailDto;

const savedDraft = {
  id: 'bid-1',
  tenderId: 'tender-1',
  status: 'Draft',
  selectedLotIds: ['lot-1'],
  bidLots: [
    {
      id: 'bid-lot-1',
      tenderBidId: 'bid-1',
      lotId: 'lot-1',
      lotCode: 'LOT-01',
      lotTitle: 'Equipment',
      totalLotAmount: 4500,
      status: 'Draft',
      itemCount: 1,
    },
  ],
  deliveryDays: 14,
  paymentTerms: 'Thirty days after delivery',
  warrantyTerms: 'Two years',
  technicalProposal: 'Technical proposal retained',
  commercialProposal: 'Commercial proposal retained',
  associationType: 'Self',
  acceptedDeclaration: true,
  items: [
    {
      id: 'bid-item-1',
      tenderBidId: 'bid-1',
      bidLotId: 'bid-lot-1',
      tenderItemId: 'item-1',
      tenderItemDescription: 'Laptop',
      requestedQuantity: 12,
      offeredQuantity: 10,
      unitPrice: 450,
      totalPrice: 4500,
      deliveryDays: 9,
      specifications: '16 GB RAM',
      brand: 'Example Brand',
      model: 'Model X',
      technicalDetails: 'Exact saved technical details',
    },
  ],
} as TenderBidDetailDto;

describe('tender bid draft rehydration', () => {
  it('restores selected lots and every editable line value after reopen', () => {
    const editable = mapDraftToEditableBid(savedDraft, tender);

    expect(editable.selectedLotIds).toEqual(['lot-1']);
    expect(editable.deliveryDays).toBe(14);
    expect(editable.items).toEqual([
      expect.objectContaining({
        tenderItemId: 'item-1',
        offeredQuantity: 10,
        unitPrice: 450,
        deliveryDays: 9,
        specifications: '16 GB RAM',
        brand: 'Example Brand',
        model: 'Model X',
        technicalDetails: 'Exact saved technical details',
      }),
    ]);
  });

  it('keeps saved values while reconciling the selected lot and resaving', () => {
    const editable = mapDraftToEditableBid(savedDraft, tender);
    const items = getItemsForSelectedLots(
      tender,
      editable.selectedLotIds,
      editable.items
    );
    const update = buildDraftUpdate(
      { ...editable, items },
      editable.selectedLotIds
    );

    expect(update.selectedLotIds).toEqual(['lot-1']);
    expect(update.items).toEqual([
      expect.objectContaining({
        tenderItemId: 'item-1',
        offeredQuantity: 10,
        unitPrice: 450,
        deliveryDays: 9,
        brand: 'Example Brand',
        model: 'Model X',
      }),
    ]);
  });
});
