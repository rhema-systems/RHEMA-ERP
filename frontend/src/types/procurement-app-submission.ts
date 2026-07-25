import type {
  ProcurementControlEventResult,
  ProcurementControlEvidenceReferenceKind,
} from './procurement-control-event';

export type ProcurementAppSubmissionStatus =
  | 'Exported'
  | 'Submitted'
  | 'Acknowledged'
  | 'Rejected';

export interface ProcurementAppSubmissionSearch {
  procurementPlanId?: string;
  fiscalYear?: number;
  status?: ProcurementAppSubmissionStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ProcurementAppSubmissionPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementAppSubmission[];
}

export interface ProcurementAppSubmissionSummary {
  publishedPlanCount: number;
  registeredPlanCount: number;
  totalAttemptCount: number;
  exportedCount: number;
  submittedCount: number;
  acknowledgedCount: number;
  rejectedCount: number;
  resubmissionCount: number;
  latestActivityAtUtc?: string;
}

export interface ProcurementAppSubmissionPlanOption {
  id: string;
  planNumber: string;
  title: string;
  fiscalYear: number;
  revisionNumber: number;
  publishedDate: string;
  itemCount: number;
  hasSubmissionRegister: boolean;
}

export interface ProcurementAppSubmissionEvidence {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference: string;
  label?: string;
  requirementKey?: string;
  fileName?: string;
  sha256?: string;
  verificationStatus?: string;
  referenceAvailable: boolean;
}

export interface ProcurementAppSubmissionTimelineEvent {
  id: string;
  eventType: string;
  action: string;
  result: ProcurementControlEventResult;
  sourceReference: string;
  actorName: string;
  reason?: string;
  occurredAtUtc: string;
  integrityHash: string;
  evidence: ProcurementAppSubmissionEvidence[];
}

export interface ProcurementAppSubmission {
  id: string;
  procurementPlanId: string;
  planNumber: string;
  planTitle: string;
  fiscalYear: number;
  planRevisionNumber: number;
  planPublishedDate?: string;
  submissionNumber: string;
  attemptNumber: number;
  status: ProcurementAppSubmissionStatus;
  timelineCorrelationId: string;
  supersedesSubmissionId?: string;
  exportFileName: string;
  exportFormat: string;
  exportTemplateVersion: string;
  exportChecksumSha256: string;
  exportedAtUtc: string;
  exportedById: string;
  exportedByName: string;
  externalSubmissionReference?: string;
  submittedAtUtc?: string;
  submittedById?: string;
  submittedByName?: string;
  acknowledgementReference?: string;
  acknowledgedAtUtc?: string;
  acknowledgedById?: string;
  acknowledgedByName?: string;
  rejectionReference?: string;
  rejectionReason?: string;
  rejectedAtUtc?: string;
  rejectedById?: string;
  rejectedByName?: string;
  notes?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
  timeline: ProcurementAppSubmissionTimelineEvent[];
}

export interface ProcurementAppEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface RecordProcurementAppExport {
  procurementPlanId: string;
  exportFileName: string;
  exportFormat: string;
  exportTemplateVersion: string;
  exportChecksumSha256: string;
  notes?: string;
  evidence?: ProcurementAppEvidenceReference[];
}

export interface SubmitProcurementApp {
  externalSubmissionReference: string;
  submittedAtUtc: string;
  notes?: string;
  rowVersion: string;
  evidence?: ProcurementAppEvidenceReference[];
}

export interface AcknowledgeProcurementApp {
  acknowledgementReference: string;
  acknowledgedAtUtc: string;
  notes?: string;
  rowVersion: string;
  evidence?: ProcurementAppEvidenceReference[];
}

export interface RejectProcurementApp {
  rejectionReference: string;
  rejectionReason: string;
  rejectedAtUtc: string;
  notes?: string;
  rowVersion: string;
  evidence?: ProcurementAppEvidenceReference[];
}

export interface ResubmitProcurementApp {
  exportFileName: string;
  exportFormat: string;
  exportTemplateVersion: string;
  exportChecksumSha256: string;
  notes?: string;
  rowVersion: string;
  evidence?: ProcurementAppEvidenceReference[];
}
