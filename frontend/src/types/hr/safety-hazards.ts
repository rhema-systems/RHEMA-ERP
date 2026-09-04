// Types for HR Area 10 (SHE) slice 3: the living hazard register (hierarchy-of-controls,
// corrective-action links) and formal risk assessments (HIRA/JHA/Pre-Task) with hazard lines,
// approval and workforce acknowledgements. Mirrors ErpSystem.Core.DTOs.HR.SafetyHazardInspectionDTOs
// (regions Hazard + Risk Assessment). Backend routes: api/safety/{hazards,risk-assessments}.
//
// Risk scoring is FIXED IN CODE (OI-12 verified): likelihood and severity are 1–5, the score is
// their product, and the level bands are hard-coded server-side — the client never computes or
// submits a score, only the two factors.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheHazardCategory =
  | 'Physical'
  | 'Chemical'
  | 'Biological'
  | 'Ergonomic'
  | 'Psychosocial'
  | 'Electrical'
  | 'Mechanical'
  | 'FireExplosion'
  | 'SlipTripFall'
  | 'WorkingAtHeight'
  | 'ConfinedSpace'
  | 'Radiation'
  | 'Environmental'
  | 'Traffic'
  | 'Other';

export const SHE_HAZARD_CATEGORY_OPTIONS = opts<SheHazardCategory>([
  ['Physical', 'Physical'],
  ['Chemical', 'Chemical'],
  ['Biological', 'Biological'],
  ['Ergonomic', 'Ergonomic'],
  ['Psychosocial', 'Psychosocial'],
  ['Electrical', 'Electrical'],
  ['Mechanical', 'Mechanical'],
  ['FireExplosion', 'Fire / Explosion'],
  ['SlipTripFall', 'Slip / Trip / Fall'],
  ['WorkingAtHeight', 'Working at Height'],
  ['ConfinedSpace', 'Confined Space'],
  ['Radiation', 'Radiation'],
  ['Environmental', 'Environmental'],
  ['Traffic', 'Traffic'],
  ['Other', 'Other'],
]);

export type SheHazardStatus =
  | 'Identified'
  | 'UnderAssessment'
  | 'ControlsInPlace'
  | 'Monitoring'
  | 'Resolved'
  | 'Closed';

export const SHE_HAZARD_STATUS_OPTIONS = opts<SheHazardStatus>([
  ['Identified', 'Identified'],
  ['UnderAssessment', 'Under Assessment'],
  ['ControlsInPlace', 'Controls in Place'],
  ['Monitoring', 'Monitoring'],
  ['Resolved', 'Resolved'],
  ['Closed', 'Closed'],
]);

/** Level bands over score = likelihood × severity: ≤3 VeryLow, ≤6 Low, ≤10 Medium, ≤15 High,
 * ≤20 VeryHigh, else Critical. Derived server-side; shown, never submitted. */
export type SheHazardRiskLevel = 'VeryLow' | 'Low' | 'Medium' | 'High' | 'VeryHigh' | 'Critical';

export const SHE_HAZARD_RISK_LEVEL_OPTIONS = opts<SheHazardRiskLevel>([
  ['VeryLow', 'Very Low'],
  ['Low', 'Low'],
  ['Medium', 'Medium'],
  ['High', 'High'],
  ['VeryHigh', 'Very High'],
  ['Critical', 'Critical'],
]);

export type SheHierarchyOfControl =
  | 'Elimination'
  | 'Substitution'
  | 'Engineering'
  | 'Administrative'
  | 'PPE';

/** Ordered most→least effective — render in this order, it IS the hierarchy. */
export const SHE_HIERARCHY_OF_CONTROL_OPTIONS = opts<SheHierarchyOfControl>([
  ['Elimination', '1. Elimination'],
  ['Substitution', '2. Substitution'],
  ['Engineering', '3. Engineering'],
  ['Administrative', '4. Administrative'],
  ['PPE', '5. PPE'],
]);

export type SheControlStatus = 'Planned' | 'Implemented' | 'Verified' | 'Ineffective' | 'Superseded';

export const SHE_CONTROL_STATUS_OPTIONS = opts<SheControlStatus>([
  ['Planned', 'Planned'],
  ['Implemented', 'Implemented'],
  ['Verified', 'Verified'],
  ['Ineffective', 'Ineffective'],
  ['Superseded', 'Superseded'],
]);

/** RA hazard-line bands: ≤4 Negligible, ≤8 Low, ≤12 Medium, ≤16 High, else Critical. */
export type SheRiskLevel = 'Negligible' | 'Low' | 'Medium' | 'High' | 'Critical';

export const SHE_RISK_LEVEL_OPTIONS = opts<SheRiskLevel>([
  ['Negligible', 'Negligible'],
  ['Low', 'Low'],
  ['Medium', 'Medium'],
  ['High', 'High'],
  ['Critical', 'Critical'],
]);

export type SheRiskAssessmentType =
  | 'HIRA'
  | 'JHA'
  | 'PreTask'
  | 'COSHH'
  | 'FireRisk'
  | 'EnvironmentalImpact'
  | 'ErgoAssessment'
  | 'Other';

export const SHE_RISK_ASSESSMENT_TYPE_OPTIONS = opts<SheRiskAssessmentType>([
  ['HIRA', 'HIRA — Hazard Identification & Risk Assessment'],
  ['JHA', 'JHA — Job Hazard Analysis'],
  ['PreTask', 'Pre-Task Risk Assessment'],
  ['COSHH', 'COSHH — Hazardous Substances'],
  ['FireRisk', 'Fire Risk Assessment'],
  ['EnvironmentalImpact', 'Environmental Impact'],
  ['ErgoAssessment', 'Ergonomic Assessment'],
  ['Other', 'Other'],
]);

export type SheRiskAssessmentStatus =
  | 'Draft'
  | 'PendingReview'
  | 'PendingApproval'
  | 'Approved'
  | 'Active'
  | 'Expired'
  | 'Superseded'
  | 'Withdrawn';

export const SHE_RISK_ASSESSMENT_STATUS_OPTIONS = opts<SheRiskAssessmentStatus>([
  ['Draft', 'Draft'],
  ['PendingReview', 'Pending Review'],
  ['PendingApproval', 'Pending Approval'],
  ['Approved', 'Approved'],
  ['Active', 'Active'],
  ['Expired', 'Expired'],
  ['Superseded', 'Superseded'],
  ['Withdrawn', 'Withdrawn'],
]);

// ── Hazard register ───────────────────────────────────────────────────────────

export interface SheHazardControl extends AuditFields {
  hazardId: string;
  controlLevel: SheHierarchyOfControl;
  controlLevelName: string;
  controlDescription: string;
  status: SheControlStatus;
  statusName: string;
  responsiblePersonId?: string | null;
  responsiblePersonName?: string | null;
  implementationDate?: string | null;
  reviewDate?: string | null;
}

export interface SheHazardCorrectiveAction extends AuditFields {
  hazardId: string;
  correctiveActionTemplateId: string;
  correctiveActionTemplateTitle: string;
  deadlineDays?: number | null;
  isMandatory: boolean;
  displayOrder: number;
}

export interface SheHazard extends AuditFields {
  tenantId: string;
  code?: string | null;
  name: string;
  category: SheHazardCategory;
  categoryName: string;
  description: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  inherentLikelihood: number;
  inherentSeverity: number;
  inherentRiskScore: number;
  residualLikelihood: number;
  residualSeverity: number;
  residualRiskScore: number;
  residualRiskLevel: SheHazardRiskLevel;
  residualRiskLevelName: string;
  status: SheHazardStatus;
  statusName: string;
  ownerId?: string | null;
  ownerName?: string | null;
  reviewDueDate?: string | null;
  lastReviewedDate?: string | null;
  lastReviewedById?: string | null;
  lastReviewedByName?: string | null;
  /** Who reported it (2026-09-04): the token's employee for a self-service report, or the
   * person the SHE desk named. Null on hazards recorded before the column existed. */
  reportedById?: string | null;
  reportedByName?: string | null;
  reportedDate?: string | null;
  isActive: boolean;
  controls: SheHazardControl[];
  correctiveActions: SheHazardCorrectiveAction[];
}

export interface SheHazardSummary {
  id: string;
  code?: string | null;
  name: string;
  category: SheHazardCategory;
  categoryName: string;
  locationName?: string | null;
  inherentRiskScore: number;
  residualRiskScore: number;
  residualRiskLevel: SheHazardRiskLevel;
  residualRiskLevelName: string;
  status: SheHazardStatus;
  statusName: string;
  ownerName?: string | null;
  reportedByName?: string | null;
  reviewDueDate?: string | null;
  isActive: boolean;
}

/** Open to every authenticated employee (the reporting surface). Status is forced to
 * Identified server-side; scores/levels are computed from the four factors.
 * `reportedById` is honoured only for a SHE Write holder (the desk recording on behalf);
 * everyone else is stamped with the token's employee. */
export interface SheHazardCreateRequest {
  code?: string | null;
  name: string;
  category: SheHazardCategory;
  description: string;
  locationId?: string | null;
  specificArea?: string | null;
  inherentLikelihood: number;
  inherentSeverity: number;
  residualLikelihood: number;
  residualSeverity: number;
  ownerId?: string | null;
  reviewDueDate?: string | null;
  isActive?: boolean;
  reportedById?: string | null;
}

export interface SheHazardUpdateRequest {
  id: string;
  code?: string | null;
  name: string;
  category: SheHazardCategory;
  description: string;
  locationId?: string | null;
  specificArea?: string | null;
  inherentLikelihood: number;
  inherentSeverity: number;
  residualLikelihood: number;
  residualSeverity: number;
  status: SheHazardStatus;
  ownerId?: string | null;
  reviewDueDate?: string | null;
  lastReviewedDate?: string | null;
  lastReviewedById?: string | null;
  isActive?: boolean;
}

export interface SheHazardControlCreateRequest {
  hazardId: string;
  controlLevel: SheHierarchyOfControl;
  controlDescription: string;
  status: SheControlStatus;
  responsiblePersonId?: string | null;
  implementationDate?: string | null;
  reviewDate?: string | null;
}

export interface SheHazardControlUpdateRequest {
  id: string;
  controlLevel: SheHierarchyOfControl;
  controlDescription: string;
  status: SheControlStatus;
  responsiblePersonId?: string | null;
  implementationDate?: string | null;
  reviewDate?: string | null;
}

export interface SheHazardCorrectiveActionCreateRequest {
  hazardId: string;
  correctiveActionTemplateId: string;
  deadlineDays?: number | null;
  isMandatory: boolean;
  displayOrder: number;
}

// ── Risk assessments ──────────────────────────────────────────────────────────

export interface SheRiskAssessmentHazard extends AuditFields {
  riskAssessmentId: string;
  hazardId?: string | null;
  itemNumber: number;
  hazardDescription: string;
  potentialConsequences?: string | null;
  affectedPersons?: string | null;
  inherentLikelihood: number;
  inherentSeverity: number;
  inherentRiskScore: number;
  inherentRiskLevel: SheRiskLevel;
  inherentRiskLevelName: string;
  controlMeasures?: string | null;
  residualLikelihood: number;
  residualSeverity: number;
  residualRiskScore: number;
  residualRiskLevel: SheRiskLevel;
  residualRiskLevelName: string;
  responsiblePerson?: string | null;
  targetDate?: string | null;
}

export interface SheRiskAssessmentAcknowledgement extends AuditFields {
  riskAssessmentId: string;
  employeeId: string;
  employeeName: string;
  acknowledgedDate: string;
  signaturePath?: string | null;
  comments?: string | null;
}

export interface SheRiskAssessment extends AuditFields {
  tenantId: string;
  assessmentNumber: string;
  title: string;
  type: SheRiskAssessmentType;
  typeName: string;
  scope?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  specificActivity?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  status: SheRiskAssessmentStatus;
  statusName: string;
  preparedById: string;
  preparedByName: string;
  preparedDate: string;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedDate?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedDate?: string | null;
  validFrom?: string | null;
  validUntil?: string | null;
  nextReviewDate?: string | null;
  version: number;
  documentPath?: string | null;
  assessedHazards: SheRiskAssessmentHazard[];
  acknowledgements: SheRiskAssessmentAcknowledgement[];
}

export interface SheRiskAssessmentSummary {
  id: string;
  assessmentNumber: string;
  title: string;
  type: SheRiskAssessmentType;
  typeName: string;
  status: SheRiskAssessmentStatus;
  statusName: string;
  locationName?: string | null;
  preparedByName: string;
  preparedDate: string;
  validUntil?: string | null;
  nextReviewDate?: string | null;
  version: number;
  hazardCount: number;
}

/** The number (RA-YYYY-NNNN) is generated server-side; status starts at Draft. */
export interface SheRiskAssessmentCreateRequest {
  title: string;
  type: SheRiskAssessmentType;
  scope?: string | null;
  locationId?: string | null;
  specificActivity?: string | null;
  organizationUnitId?: string | null;
  preparedById: string;
  preparedDate?: string;
  validFrom?: string | null;
  validUntil?: string | null;
  nextReviewDate?: string | null;
  documentPath?: string | null;
}

export interface SheRiskAssessmentUpdateRequest {
  id: string;
  title: string;
  type: SheRiskAssessmentType;
  scope?: string | null;
  locationId?: string | null;
  specificActivity?: string | null;
  organizationUnitId?: string | null;
  status: SheRiskAssessmentStatus;
  validFrom?: string | null;
  validUntil?: string | null;
  nextReviewDate?: string | null;
  documentPath?: string | null;
}

/** Refused (422) once already Approved/Active, and for Expired/Superseded/Withdrawn —
 * a retired assessment needs a new version, not a revival. */
export interface SheRiskAssessmentApproveRequest {
  riskAssessmentId: string;
  approvedById: string;
  approvedDate?: string;
  validFrom?: string | null;
  validUntil?: string | null;
  nextReviewDate?: string | null;
}

export interface SheRiskAssessmentHazardCreateRequest {
  riskAssessmentId: string;
  hazardId?: string | null;
  itemNumber: number;
  hazardDescription: string;
  potentialConsequences?: string | null;
  affectedPersons?: string | null;
  inherentLikelihood: number;
  inherentSeverity: number;
  controlMeasures?: string | null;
  residualLikelihood: number;
  residualSeverity: number;
  responsiblePerson?: string | null;
  targetDate?: string | null;
}

export interface SheRiskAssessmentHazardUpdateRequest {
  id: string;
  itemNumber: number;
  hazardDescription: string;
  potentialConsequences?: string | null;
  affectedPersons?: string | null;
  inherentLikelihood: number;
  inherentSeverity: number;
  controlMeasures?: string | null;
  residualLikelihood: number;
  residualSeverity: number;
  responsiblePerson?: string | null;
  targetDate?: string | null;
}

/** Open to every authenticated employee. Non-HR callers always sign as themselves — the
 * employee comes from the token, never this body. Refused (422) unless the assessment is
 * Approved/Active, and on a repeat signature by the same employee. */
export interface SheRiskAssessmentAcknowledgementCreateRequest {
  riskAssessmentId: string;
  employeeId: string;
  acknowledgedDate?: string;
  signaturePath?: string | null;
  comments?: string | null;
}

/** Self-service view: an active assessment with this employee's acknowledgement status. */
export interface MyRiskAcknowledgement {
  riskAssessmentId: string;
  assessmentNumber: string;
  title: string;
  type: SheRiskAssessmentType;
  typeName: string;
  locationName?: string | null;
  validUntil?: string | null;
  acknowledgedByMe: boolean;
  acknowledgedDate?: string | null;
}
