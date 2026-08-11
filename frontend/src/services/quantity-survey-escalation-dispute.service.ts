import { apiService } from '@/services/api.service';

const root = '/quantity-survey/escalation-disputes';

export type QuantitySurveyEscalationDisputeAttachmentType =
  'ContractorSubmission' | 'TdcReview' | 'ResolutionEvidence';
export type QuantitySurveyEscalationDisputeOutcome =
  'Accepted' | 'PartiallyAccepted' | 'Rejected';

export interface QuantitySurveyEscalationDisputeLookup {
  id: string;
  label: string;
  group: string;
  status: string;
}

export interface QuantitySurveyEscalationDisputeAttachment {
  id: string;
  attachmentType: QuantitySurveyEscalationDisputeAttachmentType | number;
  title: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  uploadedByName: string;
  createdAt: string;
}

export interface QuantitySurveyEscalationDispute {
  id: string;
  calculationRunId: string;
  calculationRunReference: string;
  calculationSnapshotHash: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  contractId: string;
  contractNumber: string;
  contractorBusinessPartnerId: string;
  contractorName: string;
  disputeReference: string;
  subject: string;
  disputeReason: string;
  status: 'Open' | 'ContractorResponded' | 'Resolved';
  openedById: string;
  openedAt: string;
  contractorResponse?: string | null;
  contractorRespondedById?: string | null;
  contractorRespondedAt?: string | null;
  outcome?: QuantitySurveyEscalationDisputeOutcome | number | null;
  resolutionNotes?: string | null;
  resolvedById?: string | null;
  resolvedAt?: string | null;
  rowVersion: string;
  attachments: QuantitySurveyEscalationDisputeAttachment[];
}

export interface QuantitySurveyEscalationDisputePage {
  items: QuantitySurveyEscalationDispute[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface QuantitySurveyEscalationDisputeRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  createdAt: string;
}

export const quantitySurveyEscalationDisputeService = {
  calculationLookups: () =>
    apiService.get<QuantitySurveyEscalationDisputeLookup[]>(
      `${root}/calculation-lookups`
    ),
  list: (query: {
    projectId?: string;
    calculationRunId?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }) => apiService.get<QuantitySurveyEscalationDisputePage>(root, query),
  get: (id: string) =>
    apiService.get<QuantitySurveyEscalationDispute>(`${root}/${id}`),
  open: (request: {
    clientRequestId: string;
    calculationRunId: string;
    subject: string;
    disputeReason: string;
  }) => apiService.post<QuantitySurveyEscalationDispute>(root, request),
  respond: (
    id: string,
    request: { clientRequestId: string; rowVersion: string; response: string }
  ) =>
    apiService.post<QuantitySurveyEscalationDispute>(
      `${root}/${id}/contractor-response`,
      request
    ),
  resolve: (
    id: string,
    request: {
      clientRequestId: string;
      rowVersion: string;
      outcome: QuantitySurveyEscalationDisputeOutcome;
      resolutionNotes: string;
    }
  ) =>
    apiService.post<QuantitySurveyEscalationDispute>(
      `${root}/${id}/resolve`,
      request
    ),
  addAttachment: (
    id: string,
    request: {
      clientRequestId: string;
      attachmentType: QuantitySurveyEscalationDisputeAttachmentType;
      title: string;
      file: File;
    }
  ) => {
    const form = new FormData();
    form.append('clientRequestId', request.clientRequestId);
    form.append('attachmentType', request.attachmentType);
    form.append('title', request.title);
    form.append('file', request.file, request.file.name);
    return apiService.post<QuantitySurveyEscalationDisputeAttachment>(
      `${root}/${id}/attachments`,
      form
    );
  },
  history: (id: string) =>
    apiService.get<QuantitySurveyEscalationDisputeRevision[]>(
      `${root}/${id}/history`
    ),
  attachmentContent: (id: string, attachmentId: string) =>
    apiService.downloadBlob(
      `${root}/${id}/attachments/${attachmentId}/content`
    ),
  auditPack: (id: string, format: 'pdf' | 'zip') =>
    apiService.downloadBlob(`${root}/${id}/audit-pack`, { format }),
};
