import { apiService } from '@/services/api.service';

export interface TenderBoqContext {
  tenderBidId: string;
  bidNumber: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  tenderBoqVersionId: string;
  tenderBoqVersionNumber: number;
  tenderBoqSnapshotHash: string;
  currency: string;
  lineCount: number;
  tenderBoqTotal: number;
  canUpload: boolean;
  requiresSignature: boolean;
  maximumFileSizeMb: number;
  reconciliationDeclaration: string;
}

export interface TenderBoqIssue {
  rowNumber?: number;
  code: string;
  severity: string;
  message: string;
}

export interface TenderBoqLine {
  tenderItemId: string;
  projectBoqVersionLineId: string;
  lineKey: string;
  rowNumber: number;
  lineNumber?: string;
  itemCode?: string;
  description: string;
  unitOfMeasure?: string;
  tenderQuantity: number;
  offeredQuantity: number;
  unitPrice: number;
  submittedLineTotal: number;
  calculatedLineTotal: number;
  arithmeticDifference: number;
  comparisonStatus: string;
  findings: TenderBoqIssue[];
}

export interface TenderBoqSubmission {
  id: string;
  tenderBidId: string;
  tenderBoqVersionId: string;
  status: string;
  vettingStatus: string;
  channel: string;
  originalFileName: string;
  lineCount: number;
  errorCount: number;
  warningCount: number;
  committedLineCount: number;
  tenderBoqTotal: number;
  submittedTotal: number;
  submittedAt: string;
  expiresAt: string;
  committedAt?: string;
  vettedAt?: string;
  vettingNote?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  rowVersion: string;
  previewToken?: string;
  issues: TenderBoqIssue[];
  lines: TenderBoqLine[];
}

const externalRoot = (bidId: string) =>
  `/external-portal/tender-bids/${encodeURIComponent(bidId)}/quantity-survey-boq`;
const internalRoot = (bidId: string) =>
  `/quantity-survey/tender-bids/${encodeURIComponent(bidId)}/boq-submissions`;

export const quantitySurveyTenderBoqService = {
  context: (bidId: string) =>
    apiService.get<TenderBoqContext>(`${externalRoot(bidId)}/context`),
  latest: (bidId: string) =>
    apiService.get<TenderBoqSubmission | undefined>(
      `${externalRoot(bidId)}/submissions/latest`
    ),
  downloadTemplate: (bidId: string) =>
    apiService.downloadBlob(`${externalRoot(bidId)}/template`),
  preview: (bidId: string, file: File) => {
    const form = new FormData();
    form.append('file', file);
    return apiService.post<TenderBoqSubmission>(
      `${externalRoot(bidId)}/preview`,
      form
    );
  },
  commit: (
    bidId: string,
    submissionId: string,
    request: {
      previewToken: string;
      reconciliationDeclaration: string;
      signatoryName?: string;
    }
  ) =>
    apiService.post<TenderBoqSubmission>(
      `${externalRoot(bidId)}/submissions/${encodeURIComponent(submissionId)}/commit`,
      request
    ),
  history: (bidId: string) =>
    apiService.get<TenderBoqSubmission[]>(internalRoot(bidId)),
  vet: (
    bidId: string,
    submissionId: string,
    request: {
      decision: 'Accepted' | 'Rejected';
      note: string;
      rowVersion: string;
    }
  ) =>
    apiService.post<TenderBoqSubmission>(
      `${internalRoot(bidId)}/${encodeURIComponent(submissionId)}/vet`,
      request
    ),
};

export function saveTenderBoqTemplate(blob: Blob, tenderNumber: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${tenderNumber || 'tender'}-protected-boq.xlsx`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
