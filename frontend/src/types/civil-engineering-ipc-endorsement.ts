export interface CivilEngineeringIpcEndorsementCertificateLookup {
  id: string;
  label: string;
  status: string;
  approvalStatus: string;
  rowVersion: string;
}

export interface CivilEngineeringIpcEndorsementDocumentLookup {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
}

export interface CivilEngineeringIpcEndorsementLookups {
  paymentCertificates: CivilEngineeringIpcEndorsementCertificateLookup[];
  documents: CivilEngineeringIpcEndorsementDocumentLookup[];
  requiresEndorsementEvidence: boolean;
}

export interface CivilEngineeringIpcEndorsement {
  id: string;
  projectPaymentCertificateId: string;
  paymentCertificateNumber: string;
  paymentCertificateTitle: string;
  sequence: number;
  projectEngineerAssignmentId: string;
  projectEngineerUserId: string;
  projectEngineerName: string;
  submittedById: string;
  submittedByName: string;
  submittedAt: string;
  submissionNotes: string;
  status: string;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
  evidenceDocumentRecordId?: string | null;
  evidenceDocumentVersionId?: string | null;
  requiresEndorsementEvidence: boolean;
  rowVersion: string;
}

export interface SubmitCivilEngineeringIpcEndorsementRequest {
  clientRequestId: string;
  notes: string;
}

export interface ReviewCivilEngineeringIpcEndorsementRequest {
  clientRequestId: string;
  rowVersion: string;
  endorse: boolean;
  notes: string;
  evidenceDocumentRecordId?: string | null;
  evidenceDocumentVersionId?: string | null;
}
