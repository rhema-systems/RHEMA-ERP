import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { toast } from 'sonner';

vi.mock('@/services/document-management.service', () => ({ documentManagementService: { getRecords: vi.fn().mockResolvedValue([]), getRecord: vi.fn() } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { getWarehouseLocations: vi.fn().mockResolvedValue([]) } }));
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

import { purchasingService, type ProcurementReceiptInspectionOverviewDto, type ProcurementReceiptSourceEvidenceOverviewDto } from '@/services/purchasingService';
import { documentManagementService, type CentralDocumentRecord, type CentralDocumentRecordDetail } from '@/services/document-management.service';
import { inventoryManagementService } from '@/services/inventoryManagementService';
beforeEach(() => {
  vi.mocked(documentManagementService.getRecords).mockResolvedValue([]);
  vi.mocked(inventoryManagementService.getWarehouseLocations).mockResolvedValue([]);
  vi.spyOn(purchasingService, 'getReceiptSourceEvidence').mockResolvedValue({ receiptId: 'receipt-0502', waybillReady: false, evidence: [] } as unknown as ProcurementReceiptSourceEvidenceOverviewDto);
});
import {
  buildReceiptInspectionEvidenceRequests,
  receiptInspectionResolutionActionKey,
  ReceiptInspectionControl,
} from './ReceiptInspectionControl';

const overview: ProcurementReceiptInspectionOverviewDto = {
  purchaseOrderReceiptId: 'receipt-0502',
  warehouseId: 'warehouse-0502',
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
  it('hides approval decisions for a direct inspection even if a stale capability says otherwise', async () => {
    const direct = { ...overview, canDecide: true, canAcknowledge: false,
      current: { ...requireCurrentInspection(), approvalRequired: false } };
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={direct} />);
    expect(await screen.findByLabelText('Comments (optional)')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
    expect(screen.getByText('Record accepted and rejected quantities, then submit to complete the inspection.')).toBeInTheDocument();
  });

  it.each([false, true])('reports the actual direct submission result (quality hold %s), not human approval', async (qualityHold) => {
    const current = { ...requireCurrentInspection(), approvalRequired: false, status: 0 as const };
    const draft = { ...overview, canSubmit: true, canAcknowledge: false, evidenceRequirementKeys: ['Waybill'], current };
    const waybill = { id: 'direct-waybill', sourceRecordId: 'direct-evidence', sourceEntityType: 'ProcurementReceiptSourceEvidence', documentReference: 'WB-DIRECT', title: 'Delivery.pdf', lifecycleStatus: 'Active', versionStatus: 'Published', currentVersion: 'v1' } as CentralDocumentRecord;
    vi.mocked(documentManagementService.getRecords).mockResolvedValue([waybill]);
    vi.mocked(purchasingService.getReceiptSourceEvidence).mockResolvedValue({ receiptId: 'receipt-0502', waybillReady: true,
      evidence: [{ evidenceKind: 1, isCurrent: true, centralDocumentRecordId: waybill.id, centralDocumentVersionId: 'direct-v1' }],
    } as ProcurementReceiptSourceEvidenceOverviewDto);
    vi.mocked(documentManagementService.getRecord).mockResolvedValue({ record: waybill,
      versions: [{ id: 'direct-v1', documentRecordId: waybill.id, versionNumber: 'v1', status: 'Published', fileUploadRecordId: 'direct-upload' }],
    } as CentralDocumentRecordDetail);
    const completed = { ...current, qualityHold, status: qualityHold ? 4 as const : 8 as const };
    const submit = vi.spyOn(purchasingService, 'submitReceiptInspection').mockResolvedValue(completed);
    const success = vi.spyOn(toast, 'success');
    vi.spyOn(purchasingService, 'getReceiptInspectionControl').mockResolvedValue({ ...draft, canSubmit: false, current: completed });
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={draft} />);
    await screen.findByDisplayValue('WB-DIRECT / v1');
    fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(submit).toHaveBeenCalledOnce());
    await waitFor(() => expect(success).toHaveBeenCalledWith(qualityHold
      ? 'Inspection completed. Rejected quantities remain on quality hold.'
      : 'Inspection completed and accepted quantities recorded.'));
    expect(success).not.toHaveBeenCalledWith('Inspection submitted for independent approval.');
  });

  it('does not disable saving or submission because an optional comment is empty', async () => {
    const draft = { ...overview, canEdit: true, canSubmit: true, canAcknowledge: false };
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={draft} />);
    expect(await screen.findByLabelText('Comments (optional)')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Save inspection' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Submit' })).toBeEnabled();
  });

  it('still requires an explicit comment for an independent decision', async () => {
    const deciding = { ...overview, canDecide: true, canAcknowledge: false };
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={deciding} />);
    const comment = await screen.findByLabelText('Comments (required for the decision)');
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    fireEvent.change(comment, { target: { value: 'Rejected after review' } });
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
  });

  it('sends a save request without a comment and keeps the inspection lines', async () => {
    const draft = { ...overview, canEdit: true, canSubmit: true, canAcknowledge: false };
    const save = vi.spyOn(purchasingService, 'saveReceiptInspection').mockResolvedValue(requireCurrentInspection());
    vi.spyOn(purchasingService, 'getReceiptInspectionControl').mockResolvedValue(draft);
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={draft} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Save inspection' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith('receipt-0502', expect.objectContaining({
      comment: undefined,
      rowVersion: 'AQID',
      idempotencyKey: expect.any(String),
      lines: [expect.objectContaining({ purchaseOrderReceiptItemId: 'receipt-line-0502', acceptedQuantity: 7, rejectedQuantity: 3 })],
    })));
    await screen.findByRole('button', { name: 'Save inspection' });
  });

  it('still asks for required evidence when submitting without an optional comment', async () => {
    const draft = { ...overview, canEdit: true, canSubmit: true, canAcknowledge: false };
    const submit = vi.spyOn(purchasingService, 'submitReceiptInspection').mockResolvedValue(requireCurrentInspection());
    const error = vi.spyOn(toast, 'error');
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={draft} />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh inspection evidence' })).toBeEnabled());
    fireEvent.click(await screen.findByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(error).toHaveBeenCalledWith('Select current published evidence for Inspection Report.'));
    expect(submit).not.toHaveBeenCalled();
  });

  it('automatically links this receipt waybill and submits its exact upload without a comment', async () => {
    const draft = { ...overview, canEdit: true, canSubmit: true, canAcknowledge: false, evidenceRequirementKeys: ['Waybill'] };
    const waybill = { id: 'receipt-waybill', sourceRecordId: 'source-evidence-row', sourceEntityType: 'ProcurementReceiptSourceEvidence', documentReference: 'WB-0502', title: 'Our waybill.pdf', lifecycleStatus: 'Active', versionStatus: 'Published', currentVersion: 'v1' } as CentralDocumentRecord;
    vi.mocked(documentManagementService.getRecords).mockResolvedValue([waybill,
      { ...waybill, id: 'grn', sourceEntityType: 'ProcurementReceiptDocument', title: 'Wrong GRN' },
      { ...waybill, id: 'mrn', sourceEntityType: 'ProcurementReceiptDocument', title: 'Wrong MRN' },
      { ...waybill, id: 'unrelated', sourceRecordId: 'other-receipt', title: 'Someone else report' },
    ]);
    vi.mocked(purchasingService.getReceiptSourceEvidence).mockResolvedValue({ receiptId: 'receipt-0502', waybillReady: true,
      evidence: [{ evidenceKind: 1, isCurrent: true, centralDocumentRecordId: waybill.id, centralDocumentVersionId: 'version-1' }],
    } as ProcurementReceiptSourceEvidenceOverviewDto);
    vi.mocked(documentManagementService.getRecord).mockResolvedValue({ record: waybill,
      versions: [{ id: 'version-1', documentRecordId: waybill.id, versionNumber: 'v1', status: 'Published', fileUploadRecordId: 'this-receipt-upload' }],
    } as CentralDocumentRecordDetail);
    const submit = vi.spyOn(purchasingService, 'submitReceiptInspection').mockResolvedValue(requireCurrentInspection());
    vi.spyOn(purchasingService, 'getReceiptInspectionControl').mockResolvedValue({ ...draft, canSubmit: false });
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={draft} />);
    await screen.findByDisplayValue('WB-0502 / v1');
    expect(screen.getByRole('combobox', { name: 'Waybill document for this receipt' })).toHaveTextContent('Our waybill.pdf');
    expect(screen.queryByText('Wrong GRN')).not.toBeInTheDocument();
    expect(screen.queryByText('Wrong MRN')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(submit).toHaveBeenCalledWith('case-0502', expect.objectContaining({ comment: undefined,
      evidence: [expect.objectContaining({ requirementKey: 'Waybill', fileUploadRecordId: 'this-receipt-upload' })],
    })));
  });

  it('fails closed if the receipt evidence lookup fails, without discarding entered comments', async () => {
    vi.mocked(purchasingService.getReceiptSourceEvidence).mockRejectedValue(new Error('Receipt evidence access denied'));
    const draft = { ...overview, canEdit: true, canSubmit: true, canAcknowledge: false };
    const submit = vi.spyOn(purchasingService, 'submitReceiptInspection').mockResolvedValue(requireCurrentInspection());
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={draft} />);
    fireEvent.change(screen.getByLabelText('Comments (optional)'), { target: { value: 'Keep this note' } });
    await screen.findByText('Receipt evidence access denied');
    fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
    expect(submit).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Comments (optional)')).toHaveValue('Keep this note');
  });

  it('omits irrelevant supplier and resolution badges for named API enum values', () => {
    const noResponseNeeded = {
      ...overview, canAcknowledge: false,
      current: { ...requireCurrentInspection(), supplierAcknowledgementStatus: 'NotRequired', resolutionStatus: 'None' },
    } as unknown as ProcurementReceiptInspectionOverviewDto;
    const markup = renderToStaticMarkup(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={noResponseNeeded} external />);
    expect(markup).not.toContain('Supplier:');
    expect(markup).not.toContain('Resolution:');
  });

  it('keeps the quality hold and actions visible while preserving technical history behind disclosure', async () => {
    render(<ReceiptInspectionControl receiptId="receipt-0502" initialOverview={overview} external />);
    expect(await screen.findByText('Rejected quantities remain quarantined.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Acknowledge rejection' })).toBeVisible();
    expect(screen.getByText('DEC-014')).not.toBeVisible();
    expect(screen.getByText('Formal rejection note issued.')).not.toBeVisible();
    expect(screen.getByText('AP eligible')).not.toBeVisible();
    const comment = screen.getByPlaceholderText('State the inspection, decision, acknowledgement, or closure basis.');
    fireEvent.change(comment, { target: { value: 'Preserve this inspection note' } });
    const disclosure = screen.getByRole('button', { name: /Inspection history & technical details/ });
    fireEvent.click(disclosure);
    expect(screen.getByText('DEC-014')).toBeVisible();
    expect(screen.getByText('Formal rejection note issued.')).toBeVisible();
    fireEvent.click(disclosure);
    expect(comment).toHaveValue('Preserve this inspection note');
  });

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
    expect(markup).toContain('Inspection Report');
    expect(markup).toContain('Delivery Note');
    expect(markup).not.toContain('Save inspection');
    expect(markup).not.toContain('File upload record ID');
    expect(markup).not.toContain('Workflow evidence ID');
  });

  it('uses controlled business selectors instead of raw quarantine and evidence identifiers', () => {
    const internal = {
      ...overview,
      canAcknowledge: false,
      canEdit: true,
      canSubmit: true,
    };
    const markup = renderToStaticMarkup(
      <ReceiptInspectionControl receiptId="receipt-0502" initialOverview={internal} />
    );

    expect(markup.toLowerCase()).toContain('quarantine location');
    expect(markup).toContain('Published DMS document');
    expect(markup).not.toContain('Quarantine location ID');
    expect(markup).not.toContain('Evidence source');
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
