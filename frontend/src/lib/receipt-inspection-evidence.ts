import type { CentralDocumentRecord, CentralDocumentRecordDetail } from '@/services/document-management.service';
import type { ProcurementReceiptSourceEvidenceDto } from '@/services/purchasingService';

export type ReceiptEvidenceScope = {
  receiptId: string;
  inspectionId?: string;
  // Undefined for external users: the internal receipt-attachment API is not available to them.
  attachments?: ProcurementReceiptSourceEvidenceDto[];
  waybillReady?: boolean;
};

const sameId = (left?: string | null, right?: string | null) =>
  Boolean(left && right && left.toLowerCase() === right.toLowerCase());

export const isReceiptDeliveryRequirement = (requirement: string) =>
  ['waybill', 'deliverynote', 'supplierdeliverynote'].includes(requirement.replace(/[^a-z0-9]/gi, '').toLowerCase());

export function receiptInspectionEvidenceOptions(
  records: CentralDocumentRecord[], scope: ReceiptEvidenceScope, requirement?: string,
): CentralDocumentRecord[] {
  const attachments = scope.attachments?.filter(item => item.isCurrent) ?? [];
  return records.filter(record => {
    if (record.lifecycleStatus !== 'Active' || record.versionStatus !== 'Published' || !record.currentVersion)
      return false;
    // GRN/MRN are generated register outputs, not inspection input evidence.
    if (record.sourceEntityType === 'ProcurementReceiptDocument') return false;
    const attachment = attachments.find(item => sameId(item.centralDocumentRecordId, record.id));
    const belongsToReceipt = sameId(record.sourceRecordId, scope.receiptId) ||
      sameId(record.sourceRecordId, scope.inspectionId) || Boolean(attachment);
    if (!belongsToReceipt) return false;
    if (requirement && isReceiptDeliveryRequirement(requirement) && scope.attachments !== undefined)
      return scope.waybillReady === true && attachment?.evidenceKind === 1;
    return true;
  });
}

export function receiptInspectionEvidenceVersion(detail: CentralDocumentRecordDetail, exactVersionId?: string) {
  return detail.versions.find(version =>
    sameId(version.documentRecordId, detail.record.id) &&
    (!exactVersionId || sameId(version.id, exactVersionId)) &&
    version.versionNumber === detail.record.currentVersion &&
    version.status === 'Published' && Boolean(version.fileUploadRecordId));
}
