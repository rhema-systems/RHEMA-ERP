import { describe, expect, it } from 'vitest';
import type { TenderBidDetailDto } from '@/services/tenderBidService';
import type { TenderDetailDto } from '@/services/tenderService';
import {
  buildDraftUpdate,
  getItemsForSelectedLots,
  mapDraftToEditableBid,
} from './tender-bid-draft';

const tender: TenderDetailDto = {
  id: 'tender-1',
  tenderNumber: 'TND-001', title: 'Equipment procurement', tenderType: 'ITB', status: 'Published',
  bidCount: 1, invitationCount: 1, createdAt: '2026-09-01T10:00:00Z',
  requiresPrequalification: false, allowPartialBids: true, priceWeightage: 40, qualityWeightage: 30,
  deliveryWeightage: 20, experienceWeightage: 10, useQCBSEvaluation: false,
  technicalWeight: 0, financialWeight: 0, minimumTechnicalScore: 0,
  documents: [], invitations: [], bids: [], fees: [], evaluators: [], clarifications: [], revisions: [],
  totalViews: 1, totalDownloads: 0, lotCount: 1,
  lots: [
    {
      id: 'lot-1',
      tenderId: 'tender-1', lotNumber: 1, lotCode: 'LOT-01', title: 'Equipment', status: 'Published',
      displayOrder: 1, itemCount: 1, bidCount: 1, isAwarded: false,
      items: [
        {
          id: 'item-1',
          tenderId: 'tender-1', lotId: 'lot-1', lineNumber: 1, description: 'Laptop', unitOfMeasure: 'EA',
          quantity: 12,
        },
      ],
    },
  ],
  // Some tender detail projections do not duplicate nested lot items here.
  items: [],
};

const savedDraft: TenderBidDetailDto = {
  id: 'bid-1',
  tenderId: 'tender-1',
  tenderNumber: 'TND-001', tenderTitle: 'Equipment procurement', businessPartnerId: 'supplier-1',
  businessPartnerName: 'Equipment supplier', bidNumber: 'BID-001', submittedDate: '',
  totalBidAmount: 4500, isCompliant: false, createdAt: '2026-09-02T10:00:00Z', updatedAt: '2026-09-02T11:00:00Z',
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
};

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
