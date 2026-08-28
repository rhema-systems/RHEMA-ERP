import { apiService } from '@/services/api.service';

const root = '/quantity-survey/price-index-imports';

export interface QuantitySurveyPriceIndexIssue {
  rowNumber?: number | null;
  field: string;
  code: string;
  message: string;
  severity: string;
}

export interface QuantitySurveyPriceIndexValue {
  id: string;
  sequence: number;
  indexPeriod: string;
  indexValue: number;
  publicationDate: string;
  sourceReference: string;
  status: string;
  isCurrent: boolean;
  valueKey: string;
  version: number;
  supersedesValueId?: string | null;
}

export interface QuantitySurveyPriceIndexImport {
  id: string;
  indexFamilyId: string;
  indexFamilyCode: string;
  indexFamilyName: string;
  indexSource: string | number;
  importFormat: string;
  originalFileName: string;
  fileHash: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  evidenceLabel: string;
  configurationProfileId: string;
  configurationDecisionId: string;
  approvalWorkflowDefinitionId: string;
  authorityRoleId: string;
  authorityRoleName: string;
  lineCount: number;
  errorCount: number;
  status: string;
  approvalStatus: string;
  preparedById: string;
  preparedAt: string;
  submittedById?: string | null;
  submittedAt?: string | null;
  approvedById?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
  values: QuantitySurveyPriceIndexValue[];
  issues: QuantitySurveyPriceIndexIssue[];
}

export interface QuantitySurveyPriceIndexImportPage {
  items: QuantitySurveyPriceIndexImport[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface QuantitySurveyPriceIndexImportRevision {
  id: string;
  action: string;
  actorUserId: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  createdAt: string;
}

export const quantitySurveyPriceIndexImportService = {
  template: (indexFamilyId: string) =>
    apiService.downloadBlob(
      `${root}/template/${encodeURIComponent(indexFamilyId)}`
    ),
  stage: (
    indexFamilyId: string,
    file: File,
    authorityRoleId: string,
    reason: string,
    clientRequestId: string
  ) => {
    const form = new FormData();
    form.append('file', file);
    form.append('clientRequestId', clientRequestId);
    form.append('authorityRoleId', authorityRoleId);
    form.append('reason', reason);
    return apiService.post<QuantitySurveyPriceIndexImport>(
      `${root}/stage/${encodeURIComponent(indexFamilyId)}`,
      form
    );
  },
  list: (query: {
    indexFamilyId?: string;
    status?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }) => apiService.get<QuantitySurveyPriceIndexImportPage>(root, query),
  get: (id: string) =>
    apiService.get<QuantitySurveyPriceIndexImport>(
      `${root}/${encodeURIComponent(id)}`
    ),
  lifecycle: (
    id: string,
    action: 'submit' | 'approve' | 'reject',
    rowVersion: string,
    reason: string
  ) =>
    apiService.post<QuantitySurveyPriceIndexImport>(
      `${root}/${encodeURIComponent(id)}/${action}`,
      { rowVersion, reason }
    ),
  history: (id: string) =>
    apiService.get<QuantitySurveyPriceIndexImportRevision[]>(
      `${root}/${encodeURIComponent(id)}/history`
    ),
};

export function saveQuantitySurveyPriceIndexTemplate(
  blob: Blob,
  familyCode: string,
  importFormat: string
) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${familyCode || 'price-index'}-import.${importFormat === 'CSV' ? 'csv' : 'xlsx'}`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
