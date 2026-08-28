import { apiService } from '@/services/api.service';

export type ProcurementDocumentFamilyCode =
  | 'Requisition'
  | 'Tender'
  | 'Evaluation'
  | 'Approval'
  | 'Supplier'
  | 'Contract'
  | 'GoodsReceiptNote'
  | 'MaterialReceiptNote'
  | 'VendorInvoice'
  | 'Disposal';

export interface ProcurementDocumentFamily {
  code: ProcurementDocumentFamilyCode;
  name: string;
  description: string;
  templateCode: string;
  accessProfile: string;
  classifications: string[];
  canRead: boolean;
  canUpload: boolean;
}

export interface ProcurementDocumentSourceOption {
  id: string;
  reference: string;
  label: string;
  status: string;
}

export interface ProcurementManagedDocument {
  documentRecordId: string;
  documentVersionId: string;
  family: ProcurementDocumentFamilyCode;
  sourceRecordId: string;
  sourceReference: string;
  documentReference: string;
  title: string;
  classification: string;
  templateCode: string;
  versionNumber: string;
  versionStatus: string;
  accessProfile: string;
  retentionStatus: string;
  lifecycleStatus: string;
  malwareStatus: string;
  metadataComplete: boolean;
  accessCovered: boolean;
  retentionCovered: boolean;
  legalHold: boolean;
  createdAtUtc: string;
  createdBy: string;
}

const root = '/procurement/document-management';

export const procurementDocumentManagementService = {
  catalogue: () =>
    apiService.get<ProcurementDocumentFamily[]>(`${root}/catalogue`),

  sources: (family: ProcurementDocumentFamilyCode) =>
    apiService.get<ProcurementDocumentSourceOption[]>(`${root}/sources`, {
      family,
    }),

  records: (family: ProcurementDocumentFamilyCode, sourceRecordId?: string) =>
    apiService.get<ProcurementManagedDocument[]>(`${root}/records`, {
      family,
      ...(sourceRecordId ? { sourceRecordId } : {}),
    }),

  upload: (
    family: ProcurementDocumentFamilyCode,
    sourceRecordId: string,
    classification: string,
    file: File,
    title?: string
  ) => {
    const form = new FormData();
    form.append('family', family);
    form.append('sourceRecordId', sourceRecordId);
    form.append('classification', classification);
    form.append('file', file);
    if (title?.trim()) form.append('title', title.trim());
    return apiService.post<ProcurementManagedDocument>(`${root}/records`, form);
  },

  download: (recordId: string, versionId: string) =>
    apiService.downloadBlob(
      `${root}/records/${recordId}/versions/${versionId}/download`
    ),

  removeRequisitionDocument: (recordId: string) =>
    apiService.delete<void>(`${root}/records/${recordId}`),
};
