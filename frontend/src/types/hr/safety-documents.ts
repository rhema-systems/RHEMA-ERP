// Types for HR Area 10 slice 16: SHE controlled document register
// (FR-SHE-246 version control, FR-SHE-170 filing and retrieval; SRS §14).
// Mirrors ErpSystem.Core.DTOs.HR.SafetyControlledDocumentDTOs. Backend route:
// api/safety/documents. Version rows are projections of the central DMS.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

/** SRS §14 document-library families plus the SoW Module-14 additions. */
export type SheControlledDocumentCategory =
  | 'Policy'
  | 'Procedure'
  | 'SafeWorkProcedure'
  | 'JobSafetyAnalysis'
  | 'RiskAssessment'
  | 'EmergencyPlan'
  | 'TrainingRecord'
  | 'Permit'
  | 'InspectionReport'
  | 'AuditReport'
  | 'IncidentReport'
  | 'InvestigationReport'
  | 'ContractorDocument'
  | 'Form'
  | 'Other';

export const SHE_DOCUMENT_CATEGORY_OPTIONS = opts<SheControlledDocumentCategory>([
  ['Policy', 'Policy'],
  ['Procedure', 'Procedure'],
  ['SafeWorkProcedure', 'Safe Work Procedure (SWP)'],
  ['JobSafetyAnalysis', 'Job Safety Analysis (JSA)'],
  ['RiskAssessment', 'Risk Assessment (HIRA)'],
  ['EmergencyPlan', 'Emergency Plan'],
  ['TrainingRecord', 'Training Record'],
  ['Permit', 'Permit'],
  ['InspectionReport', 'Inspection Report'],
  ['AuditReport', 'Audit Report'],
  ['IncidentReport', 'Incident Report'],
  ['InvestigationReport', 'Investigation Report'],
  ['ContractorDocument', 'Contractor Document'],
  ['Form', 'Form'],
  ['Other', 'Other'],
]);

/**
 * Draft → Active (activation is the approval step; refused with no uploaded
 * version) → UnderReview → Active again on re-approval; Archived is terminal
 * for edits and new versions.
 */
export type SheControlledDocumentStatus = 'Draft' | 'Active' | 'UnderReview' | 'Archived';

export const SHE_DOCUMENT_STATUS_OPTIONS = opts<SheControlledDocumentStatus>([
  ['Draft', 'Draft'],
  ['Active', 'Active'],
  ['UnderReview', 'Under Review'],
  ['Archived', 'Archived'],
]);

/** One revision of the document, projected from the central DMS (newest first). */
export interface SheControlledDocumentVersion {
  id: string;
  versionNumber: string;
  fileName?: string | null;
  contentType?: string | null;
  fileSize?: number | null;
  changeSummary?: string | null;
  uploadedAt: string;
  uploadedByName: string;
}

export interface SheControlledDocument extends AuditFields {
  tenantId: string;
  documentNumber: string;
  title: string;
  description?: string | null;
  category: SheControlledDocumentCategory;
  categoryName: string;
  status: SheControlledDocumentStatus;
  statusName: string;
  keywords?: string | null;
  ownerId: string;
  ownerName: string;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  documentRecordId?: string | null;
  currentVersionLabel?: string | null;
  effectiveDate?: string | null;
  reviewFrequencyMonths?: number | null;
  nextReviewDate?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedDate?: string | null;
  archivedById?: string | null;
  archivedByName?: string | null;
  archivedDate?: string | null;
  archiveReason?: string | null;
  notes?: string | null;
  versions: SheControlledDocumentVersion[];
}

export interface SheControlledDocumentSummary {
  id: string;
  documentNumber: string;
  title: string;
  category: SheControlledDocumentCategory;
  categoryName: string;
  status: SheControlledDocumentStatus;
  statusName: string;
  ownerName: string;
  organizationUnitName?: string | null;
  currentVersionLabel?: string | null;
  effectiveDate?: string | null;
  nextReviewDate?: string | null;
}

export interface SheControlledDocumentCreateRequest {
  documentNumber?: string | null;
  title: string;
  description?: string | null;
  category: SheControlledDocumentCategory;
  keywords?: string | null;
  ownerId: string;
  organizationUnitId?: string | null;
  locationId?: string | null;
  reviewFrequencyMonths?: number | null;
  notes?: string | null;
}

export interface SheControlledDocumentUpdateRequest {
  id: string;
  title: string;
  description?: string | null;
  category: SheControlledDocumentCategory;
  keywords?: string | null;
  ownerId: string;
  organizationUnitId?: string | null;
  locationId?: string | null;
  reviewFrequencyMonths?: number | null;
  notes?: string | null;
}
