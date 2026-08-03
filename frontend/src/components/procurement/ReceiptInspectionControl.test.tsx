import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import type { ProcurementReceiptInspectionOverviewDto } from '@/services/purchasingService';
import {
  buildReceiptInspectionEvidenceRequests,
  receiptInspectionResolutionActionKey,
  ReceiptInspectionControl,
} from './ReceiptInspectionControl';

const overview: ProcurementReceiptInspectionOverviewDto = {
  purchaseOrderReceiptId: 'receipt-0502',
  receiptNumber: 'POR-0502',
  purchaseOrderNumber: 'PO-0502',
  supplierName: 'Governed Supplier',
  canEdit: false,
  canSubmit: false,
  canDecide: false,
  canAcknowledge: true,
  canResolve: false,
  canClose: false,
  decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
  evidenceRequirementKeys: ['INSPECTION_REPORT', 'DELIVERY_NOTE'],
  history: [],
  current: {
    id: 'case-0502',
    purchaseOrderReceiptId: 'receipt-0502',
    sequence: 1,
    status: 4,
    receivedQuantity: 10,
    acceptedQuantity: 7,
    rejectedQuantity: 3,
    pendingQuantity: 0,
    qualityHold: true,
    qualityHoldReason: 'Rejected quantities remain quarantined.',
    rejectionNoteNumber: 'RN-POR-0502-01',
    supplierAcknowledgementStatus: 1,
    resolutionKind: 0,
    resolutionStatus: 1,
    stockEligibleQuantity: 7,
    stockPostedQuantity: 7,
    apEligibleQuantity: 7,
    apBlockedQuantity: 3,
    rowVersion: 'AQID',
    evidence: [],
    lines: [{
      id: 'line-0502',
      purchaseOrderReceiptItemId: 'receipt-line-0502',
      purchaseOrderItemId: 'po-line-0502',
      itemCode: 'ITEM-0502',
      itemName: 'Controlled item',
      unitOfMeasure: 'EA',
      receivedQuantity: 10,
      acceptedQuantity: 7,
      rejectedQuantity: 3,
      pendingQuantity: 0,
      disposition: 3,
      rejectionReason: 'Damaged',
    }],
    actions: [{
      id: 'action-0502',
      sequence: 2,
      actionType: 5,
      statusAfter: 4,
      resolutionKind: 0,
      quantity: 3,
      reference: 'RN-POR-0502-01',
      comment: 'Formal rejection note issued.',
      actorName: 'Independent checker',
      occurredAtUtc: '2026-07-31T12:00:00Z',
    }],
  },
};

const requireCurrentInspection = () => {
  if (!overview.current) throw new Error('The test fixture requires a current inspection.');
  return overview.current;
};

describe('ReceiptInspectionControl', () => {
  it('submits one controlled evidence reference for every configured requirement', () => {
    const evidence = buildReceiptInspectionEvidenceRequests('SubmitReceiptInspection', [
      {
        requirementKey: 'INSPECTION_REPORT',
        evidenceKind: 1,
        evidenceId: 'upload-report',
        evidenceReference: 'Inspection report',
      },
      {
        requirementKey: 'DELIVERY_NOTE',
        evidenceKind: 0,
        evidenceId: 'workflow-delivery-note',
        evidenceReference: 'Signed delivery note',
      },
    ]);

    expect(evidence).toHaveLength(2);
    expect(evidence[0]).toMatchObject({
      actionKey: 'SubmitReceiptInspection',
      requirementKey: 'INSPECTION_REPORT',
      fileUploadRecordId: 'upload-report',
    });
    expect(evidence[1]).toMatchObject({
      actionKey: 'SubmitReceiptInspection',
      requirementKey: 'DELIVERY_NOTE',
      workflowEvidenceDocumentId: 'workflow-delivery-note',
    });
  });

  it('shows supplier-scoped quality hold, eligibility, decision lineage and acknowledgement', () => {
    const markup = renderToStaticMarkup(
      <ReceiptInspectionControl
        receiptId="receipt-0502"
        initialOverview={overview}
        external
      />
    );

    expect(markup).toContain('Quality hold');
    expect(markup).toContain('RN-POR-0502-01');
    expect(markup).toContain('AP eligible');
    expect(markup).toContain('Acknowledge rejection');
    expect(markup).toContain('DEC-001');
    expect(markup).toContain('DEC-014');
    expect(markup).toContain('INSPECTION_REPORT');
    expect(markup).toContain('DELIVERY_NOTE');
    expect(markup).not.toContain('Save inspection');
  });

  it('binds return and replacement evidence to each distinct lifecycle stage', () => {
    expect(receiptInspectionResolutionActionKey(1, 1)).toBe('ReturnAuthorization');
    expect(receiptInspectionResolutionActionKey(1, 2)).toBe('ReturnDispatch');
    expect(receiptInspectionResolutionActionKey(2, 1)).toBe('ReplacementRequest');
    expect(receiptInspectionResolutionActionKey(2, 4)).toBe('ReplacementReceipt');
  });

  it('exposes the supported replacement inspection after rejection', () => {
    const rejected = {
      ...overview,
      canAcknowledge: false,
      current: {
        ...requireCurrentInspection(),
        status: 3 as const,
        qualityHold: false,
      },
    };

    const markup = renderToStaticMarkup(
      <ReceiptInspectionControl
        receiptId="receipt-0502"
        initialOverview={rejected}
      />
    );

    expect(markup).toContain('Reinitialize inspection');
  });

  it('lets a supplier acknowledge a disputed note without offering another dispute', () => {
    const disputed = {
      ...overview,
      current: {
        ...requireCurrentInspection(),
        supplierAcknowledgementStatus: 3 as const,
      },
    };

    const markup = renderToStaticMarkup(
      <ReceiptInspectionControl
        receiptId="receipt-0502"
        initialOverview={disputed}
        external
      />
    );

    expect(markup).toContain('Acknowledge after dispute');
    expect(markup).not.toContain('>Dispute</button>');
  });
});
