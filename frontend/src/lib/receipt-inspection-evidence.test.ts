import { describe, expect, it } from 'vitest';
import type { CentralDocumentRecord, CentralDocumentRecordDetail } from '@/services/document-management.service';
import type { ProcurementReceiptSourceEvidenceDto } from '@/services/purchasingService';
import { receiptInspectionEvidenceOptions, receiptInspectionEvidenceVersion } from './receipt-inspection-evidence';

const record = (id: string, sourceRecordId: string, extra: Partial<CentralDocumentRecord> = {}) => ({
  id, sourceRecordId, sourceEntityType: 'PurchaseOrderReceipt', lifecycleStatus: 'Active',
  versionStatus: 'Published', currentVersion: 'v1', title: id, ...extra,
}) as CentralDocumentRecord;
const attachment = (id: string, kind = 1, isCurrent = true) => ({
  centralDocumentRecordId: id, evidenceKind: kind, isCurrent,
}) as ProcurementReceiptSourceEvidenceDto;
const scope = { receiptId: 'receipt-1', inspectionId: 'inspection-1', attachments: [attachment('waybill'), attachment('invoice', 2)], waybillReady: true };

describe('receipt inspection evidence scoping', () => {
  it('only lists current published evidence belonging to this receipt, case or explicit receipt attachment', () => {
    const options = receiptInspectionEvidenceOptions([
      record('report', 'receipt-1'), record('case-report', 'inspection-1'),
      record('waybill', 'attachment-row'), record('invoice', 'attachment-invoice'),
      record('unrelated', 'receipt-2'), record('po-document', 'shared-po'),
      record('draft', 'receipt-1', { versionStatus: 'Draft' }),
      record('archived', 'receipt-1', { lifecycleStatus: 'Archived' }),
      record('no-version', 'receipt-1', { currentVersion: null }),
    ], scope);
    expect(options.map(item => item.id)).toEqual(['report', 'case-report', 'waybill', 'invoice']);
  });

  it('excludes both generated GRN and MRN even when linked directly to this receipt', () => {
    expect(receiptInspectionEvidenceOptions([
      record('grn', 'receipt-1', { sourceEntityType: 'ProcurementReceiptDocument' }),
      record('mrn', 'receipt-1', { sourceEntityType: 'ProcurementReceiptDocument' }),
      record('other-grn', 'other-receipt', { sourceEntityType: 'ProcurementReceiptDocument' }),
    ], scope)).toEqual([]);
  });

  it.each(['Waybill', 'WAY_BILL', 'DELIVERY_NOTE', 'Supplier delivery note'])('uses only the saved current waybill for %s', requirement => {
    const options = receiptInspectionEvidenceOptions([record('report', 'receipt-1'), record('waybill', 'attachment-row'), record('invoice', 'attachment-invoice')], scope, requirement);
    expect(options.map(item => item.id)).toEqual(['waybill']);
  });

  it('does not infer receipt ownership from a filename or a matching display reference', () => {
    expect(receiptInspectionEvidenceOptions([record('wrong', 'receipt-2', { title: 'Waybill receipt-1.pdf', sourceRecordReference: 'receipt-1' })], scope)).toEqual([]);
  });

  it('rejects superseded attachments and waybills that are not ready', () => {
    const rows = [record('waybill', 'attachment-row')];
    expect(receiptInspectionEvidenceOptions(rows, { ...scope, attachments: [attachment('waybill', 1, false)] }, 'Waybill')).toEqual([]);
    expect(receiptInspectionEvidenceOptions(rows, { ...scope, waybillReady: false }, 'Waybill')).toEqual([]);
  });

  it('handles GUID casing without broadening the receipt scope', () => {
    expect(receiptInspectionEvidenceOptions([record('report', 'RECEIPT-1')], scope)).toHaveLength(1);
  });

  it('requires the exact current published attachment version and an actual upload', () => {
    const detail = { record: record('waybill', 'attachment-row'), versions: [
      { id: 'old', documentRecordId: 'waybill', versionNumber: 'v0', status: 'Published', fileUploadRecordId: 'old-upload' },
      { id: 'current', documentRecordId: 'waybill', versionNumber: 'v1', status: 'Published', fileUploadRecordId: 'current-upload' },
    ] } as CentralDocumentRecordDetail;
    expect(receiptInspectionEvidenceVersion(detail, 'current')?.fileUploadRecordId).toBe('current-upload');
    expect(receiptInspectionEvidenceVersion(detail, 'old')).toBeUndefined();
    detail.versions[1].fileUploadRecordId = null;
    expect(receiptInspectionEvidenceVersion(detail, 'current')).toBeUndefined();
  });
});
