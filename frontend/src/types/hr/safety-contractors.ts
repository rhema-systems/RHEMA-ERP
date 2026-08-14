// Types for HR Area 10 (SHE) slice 8: Contractor SHE Management — the contractor register with
// pre-qualification, worker inductions, contractor SHE inspections, non-compliance notices with
// sanctions, and the document file with verification.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyContractorTrainingDTOs (region H).
// Backend route: api/safety/contractors.
//
// FR-CON-001: the pre-qualification score and per-inspection compliance score (0–100) are
// hand-entered by the assessor/inspector; the AGGREGATE is computed — the slice-14 contractor
// ranking (api/safety/performance/kpis/contractor-ranking, surfaced on the SHE Analytics
// screen) averages the period's inspection scores per contractor and ranks them.
// Repeat-violation flags on notices ARE computed server-side from the contractor's prior notices.
// Expiring pre-qualifications, documents and inductions alert automatically via the slice-13
// reminder engine; the polled queries remain the screens' work views.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheContractorStatus =
  | 'PendingAssessment'
  | 'Approved'
  | 'ConditionalApproval'
  | 'Suspended'
  | 'Revoked'
  | 'Blacklisted';

export const SHE_CONTRACTOR_STATUS_OPTIONS = opts<SheContractorStatus>([
  ['PendingAssessment', 'Pending Assessment'],
  ['Approved', 'Approved'],
  ['ConditionalApproval', 'Conditional Approval'],
  ['Suspended', 'Suspended'],
  ['Revoked', 'Revoked'],
  ['Blacklisted', 'Blacklisted'],
]);

/** Pre-qualification must record an outcome — 'PendingAssessment' is refused by the backend (422). */
export const SHE_PREQUALIFICATION_OUTCOME_OPTIONS = SHE_CONTRACTOR_STATUS_OPTIONS.filter(
  (o) => o.value !== 'PendingAssessment',
);

export type SheContractorDocumentType =
  | 'ShePolicy'
  | 'MethodStatement'
  | 'RiskAssessment'
  | 'InsuranceCertificate'
  | 'CompetencyCertificate'
  | 'InductionRecord'
  | 'SafetyPlan'
  | 'AccidentRecord'
  | 'Other';

export const SHE_CONTRACTOR_DOCUMENT_TYPE_OPTIONS = opts<SheContractorDocumentType>([
  ['ShePolicy', 'SHE Policy'],
  ['MethodStatement', 'Method Statement'],
  ['RiskAssessment', 'Risk Assessment'],
  ['InsuranceCertificate', 'Insurance Certificate'],
  ['CompetencyCertificate', 'Competency Certificate'],
  ['InductionRecord', 'Induction Record'],
  ['SafetyPlan', 'Safety Plan'],
  ['AccidentRecord', 'Accident Record'],
  ['Other', 'Other'],
]);

export type SheInspectionResult = 'Pass' | 'ConditionalPass' | 'Fail' | 'NeedsFollowUp';

export const SHE_INSPECTION_RESULT_OPTIONS = opts<SheInspectionResult>([
  ['Pass', 'Pass'],
  ['ConditionalPass', 'Conditional Pass'],
  ['Fail', 'Fail'],
  ['NeedsFollowUp', 'Needs Follow-up'],
]);

export type SheInspectionStatus =
  | 'Scheduled'
  | 'InProgress'
  | 'PendingCorrectiveActions'
  | 'Completed'
  | 'Closed'
  | 'Overdue';

export const SHE_INSPECTION_STATUS_OPTIONS = opts<SheInspectionStatus>([
  ['Scheduled', 'Scheduled'],
  ['InProgress', 'In Progress'],
  ['PendingCorrectiveActions', 'Pending Corrective Actions'],
  ['Completed', 'Completed'],
  ['Closed', 'Closed'],
  ['Overdue', 'Overdue'],
]);

export type SheNonComplianceSeverity = 'Advisory' | 'Minor' | 'Major' | 'Critical' | 'Imminent';

export const SHE_NON_COMPLIANCE_SEVERITY_OPTIONS = opts<SheNonComplianceSeverity>([
  ['Advisory', 'Advisory'],
  ['Minor', 'Minor'],
  ['Major', 'Major'],
  ['Critical', 'Critical'],
  ['Imminent', 'Imminent Danger'],
]);

export type SheNonComplianceStatus =
  | 'Open'
  | 'InProgress'
  | 'PendingVerification'
  | 'Closed'
  | 'Escalated';

/** 'Closed' is deliberately absent — closing goes through the close endpoint, never the edit form. */
export const SHE_NON_COMPLIANCE_EDIT_STATUS_OPTIONS = opts<SheNonComplianceStatus>([
  ['Open', 'Open'],
  ['InProgress', 'In Progress'],
  ['PendingVerification', 'Pending Verification'],
  ['Escalated', 'Escalated'],
]);

export type SheContractorSanction =
  | 'VerbalWarning'
  | 'WrittenWarning'
  | 'WorkSuspension'
  | 'PartialSuspension'
  | 'ContractTermination';

export const SHE_CONTRACTOR_SANCTION_OPTIONS = opts<SheContractorSanction>([
  ['VerbalWarning', 'Verbal Warning'],
  ['WrittenWarning', 'Written Warning'],
  ['WorkSuspension', 'Work Suspension'],
  ['PartialSuspension', 'Partial Suspension'],
  ['ContractTermination', 'Contract Termination'],
]);

// ── Contractor ────────────────────────────────────────────────────────────────

export interface SheContractorSummary {
  id: string;
  contractorCode: string;
  companyName: string;
  primaryContactName?: string | null;
  sheStatus: SheContractorStatus;
  sheStatusName: string;
  /** Hand-entered at pre-qualification (0–100); feeds the computed slice-14 ranking as a tie-breaker. */
  preQualificationScore?: number | null;
  preQualificationExpiryDate?: string | null;
  isActive: boolean;
  openNonComplianceCount: number;
}

export interface SheContractor extends AuditFields {
  tenantId: string;
  contractorCode: string;
  companyName: string;
  tradingName?: string | null;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  registrationNumber?: string | null;
  primaryContactName?: string | null;
  primaryContactPhone?: string | null;
  primaryContactEmail?: string | null;
  sheStatus: SheContractorStatus;
  sheStatusName: string;
  preQualificationScore?: number | null;
  preQualificationDate?: string | null;
  preQualificationExpiryDate?: string | null;
  preQualifiedById?: string | null;
  preQualifiedByName?: string | null;
  sheConditions?: string | null;
  isActive: boolean;
  inductions: SheContractorInduction[];
  sheInspections: SheContractorInspection[];
  nonCompliances: SheContractorNonCompliance[];
  documents: SheContractorDocument[];
}

export interface SheContractorCreateRequest {
  /** Unique per tenant — a duplicate code is refused (422). Immutable after creation. */
  contractorCode: string;
  companyName: string;
  tradingName?: string | null;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  registrationNumber?: string | null;
  primaryContactName?: string | null;
  primaryContactPhone?: string | null;
  primaryContactEmail?: string | null;
  isActive: boolean;
}

export interface SheContractorUpdateRequest {
  id: string;
  companyName: string;
  tradingName?: string | null;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  registrationNumber?: string | null;
  primaryContactName?: string | null;
  primaryContactPhone?: string | null;
  primaryContactEmail?: string | null;
  isActive: boolean;
}

export interface SheContractorPreQualifyRequest {
  contractorId: string;
  /** The assessment outcome — 'PendingAssessment' is refused (422). */
  sheStatus: SheContractorStatus;
  preQualificationScore?: number | null;
  preQualificationDate: string;
  preQualificationExpiryDate?: string | null;
  preQualifiedById: string;
  sheConditions?: string | null;
}

// ── Inductions ────────────────────────────────────────────────────────────────

export interface SheContractorInduction extends AuditFields {
  contractorId: string;
  workerName: string;
  workerIdOrPassport?: string | null;
  trade?: string | null;
  inductionDate: string;
  conductedById: string;
  conductedByName: string;
  inductionPassed: boolean;
  inductionExpiryDate?: string | null;
  signaturePath?: string | null;
  notes?: string | null;
}

export interface SheContractorInductionCreateRequest {
  contractorId: string;
  workerName: string;
  workerIdOrPassport?: string | null;
  trade?: string | null;
  inductionDate: string;
  conductedById: string;
  inductionPassed: boolean;
  inductionExpiryDate?: string | null;
  signaturePath?: string | null;
  notes?: string | null;
}

export interface SheContractorInductionUpdateRequest {
  id: string;
  workerName: string;
  workerIdOrPassport?: string | null;
  trade?: string | null;
  inductionDate: string;
  inductionPassed: boolean;
  inductionExpiryDate?: string | null;
  signaturePath?: string | null;
  notes?: string | null;
}

// ── Contractor SHE inspections ────────────────────────────────────────────────

export interface SheContractorInspection extends AuditFields {
  contractorId: string;
  inspectionNumber: string;
  inspectionDate: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  inspectorId: string;
  inspectorName: string;
  /** Hand-entered by the inspector (0–100); the slice-14 ranking averages these per contractor. */
  complianceScore?: number | null;
  result: SheInspectionResult;
  resultName: string;
  findings?: string | null;
  recommendedActions?: string | null;
  nextInspectionDate?: string | null;
  status: SheInspectionStatus;
  statusName: string;
}

export interface SheContractorInspectionCreateRequest {
  contractorId: string;
  /** Unique per tenant — a duplicate number is refused (422). Immutable after creation. */
  inspectionNumber: string;
  inspectionDate: string;
  locationId?: string | null;
  specificArea?: string | null;
  inspectorId: string;
  complianceScore?: number | null;
  result: SheInspectionResult;
  findings?: string | null;
  recommendedActions?: string | null;
  nextInspectionDate?: string | null;
  status: SheInspectionStatus;
}

export interface SheContractorInspectionUpdateRequest {
  id: string;
  inspectionDate: string;
  locationId?: string | null;
  specificArea?: string | null;
  complianceScore?: number | null;
  result: SheInspectionResult;
  findings?: string | null;
  recommendedActions?: string | null;
  nextInspectionDate?: string | null;
  status: SheInspectionStatus;
}

// ── Non-compliance notices ────────────────────────────────────────────────────

export interface SheContractorNonCompliance extends AuditFields {
  contractorId: string;
  noticeNumber: string;
  issuedDate: string;
  issuedById: string;
  issuedByName: string;
  violationDescription: string;
  severity: SheNonComplianceSeverity;
  severityName: string;
  rectificationDeadline: string;
  /** Computed server-side from the contractor's prior notices — never client-supplied. */
  isRepeatViolation: boolean;
  repeatCount: number;
  status: SheNonComplianceStatus;
  statusName: string;
  rectificationDate?: string | null;
  contractorResponse?: string | null;
  closureNotes?: string | null;
  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  sanctionApplied?: SheContractorSanction | null;
  sanctionAppliedName?: string | null;
}

export interface SheContractorNonComplianceCreateRequest {
  contractorId: string;
  /** Unique per tenant — a duplicate number is refused (422). Immutable after creation. */
  noticeNumber: string;
  issuedDate: string;
  issuedById: string;
  violationDescription: string;
  severity: SheNonComplianceSeverity;
  rectificationDeadline: string;
}

/** Editing a closed notice, or setting status to Closed here, is refused (422). */
export interface SheContractorNonComplianceUpdateRequest {
  id: string;
  violationDescription: string;
  severity: SheNonComplianceSeverity;
  rectificationDeadline: string;
  status: SheNonComplianceStatus;
  rectificationDate?: string | null;
  contractorResponse?: string | null;
  sanctionApplied?: SheContractorSanction | null;
}

export interface SheContractorNonComplianceCloseRequest {
  nonComplianceId: string;
  closedById: string;
  closedDate: string;
  rectificationDate?: string | null;
  closureNotes?: string | null;
}

// ── Documents ─────────────────────────────────────────────────────────────────

export interface SheContractorDocument extends AuditFields {
  contractorId: string;
  documentType: SheContractorDocumentType;
  documentTypeName: string;
  fileName: string;
  filePath: string;
  title?: string | null;
  documentDate?: string | null;
  expiryDate?: string | null;
  isVerified: boolean;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verifiedDate?: string | null;
  verificationNotes?: string | null;
  uploadedDate: string;
  uploadedById: string;
  uploadedByName: string;
}

export interface SheContractorDocumentCreateRequest {
  contractorId: string;
  documentType: SheContractorDocumentType;
  fileName: string;
  filePath: string;
  title?: string | null;
  documentDate?: string | null;
  expiryDate?: string | null;
  uploadedById: string;
}

/** Re-verifying an already-verified document is refused (422). */
export interface SheContractorDocumentVerifyRequest {
  documentId: string;
  verifiedById: string;
  verifiedDate: string;
  verificationNotes?: string | null;
}
