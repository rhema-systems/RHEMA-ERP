// Types for HR Area 10 (SHE) slice 4: inspection checklists (the reusable templates) and safety
// inspections (scheduling, checklist findings, discovered hazards with corrective actions,
// documents, guarded close-out). Mirrors ErpSystem.Core.DTOs.HR.SafetyHazardInspectionDTOs
// (region D). Backend routes: api/safety/{inspection-checklists,inspections}.

import type { AuditFields } from './common';
import type { SheRiskLevel, SheHazardStatus, SheHazardRiskLevel } from './safety-hazards';
import type { SheCorrectiveActionStatus } from './safety-incidents';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheInspectionType =
  | 'Routine'
  | 'Planned'
  | 'Unplanned'
  | 'FollowUp'
  | 'PreTask'
  | 'PostIncident'
  | 'Regulatory'
  | 'Management';

export const SHE_INSPECTION_TYPE_OPTIONS = opts<SheInspectionType>([
  ['Routine', 'Routine'],
  ['Planned', 'Planned'],
  ['Unplanned', 'Unplanned'],
  ['FollowUp', 'Follow-up'],
  ['PreTask', 'Pre-task'],
  ['PostIncident', 'Post-incident'],
  ['Regulatory', 'Regulatory'],
  ['Management', 'Management walk-through'],
]);

export type SheInspectionCategory =
  | 'General'
  | 'FireSafety'
  | 'Construction'
  | 'Equipment'
  | 'Chemical'
  | 'Electrical'
  | 'WorkingAtHeight'
  | 'ConfinedSpace'
  | 'ManualHandling'
  | 'Environmental'
  | 'Housekeeping';

export const SHE_INSPECTION_CATEGORY_OPTIONS = opts<SheInspectionCategory>([
  ['General', 'General'],
  ['FireSafety', 'Fire Safety'],
  ['Construction', 'Construction'],
  ['Equipment', 'Equipment'],
  ['Chemical', 'Chemical'],
  ['Electrical', 'Electrical'],
  ['WorkingAtHeight', 'Working at Height'],
  ['ConfinedSpace', 'Confined Space'],
  ['ManualHandling', 'Manual Handling'],
  ['Environmental', 'Environmental'],
  ['Housekeeping', 'Housekeeping'],
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

export type SheComplianceStatus =
  | 'Compliant'
  | 'NonCompliant'
  | 'PartiallyCompliant'
  | 'NotApplicable'
  | 'NotAssessed';

export const SHE_COMPLIANCE_STATUS_OPTIONS = opts<SheComplianceStatus>([
  ['Compliant', 'Compliant'],
  ['NonCompliant', 'Non-compliant'],
  ['PartiallyCompliant', 'Partially compliant'],
  ['NotApplicable', 'Not applicable'],
  ['NotAssessed', 'Not assessed'],
]);

// ── Inspection checklists (templates) ─────────────────────────────────────────

export interface SheInspectionChecklistItem extends AuditFields {
  checklistId: string;
  itemOrder: number;
  category: string;
  itemDescription: string;
  isMandatory: boolean;
  regulatoryReference?: string | null;
  associatedRiskLevel?: SheRiskLevel | null;
  associatedRiskLevelName?: string | null;
}

export interface SheInspectionChecklist extends AuditFields {
  tenantId: string;
  checklistNumber: string;
  name: string;
  description?: string | null;
  type: SheInspectionType;
  typeName: string;
  version: number;
  isActive: boolean;
  /** Loaded on the detail read only — list reads return it empty. */
  items: SheInspectionChecklistItem[];
}

/** The server refuses (422) a duplicate checklist number within the tenant. */
export interface SheInspectionChecklistCreateRequest {
  checklistNumber: string;
  name: string;
  description?: string | null;
  type: SheInspectionType;
  version?: number;
  isActive?: boolean;
}

/** The number is immutable after creation — it is not on the update contract. */
export interface SheInspectionChecklistUpdateRequest {
  id: string;
  name: string;
  description?: string | null;
  type: SheInspectionType;
  version?: number;
  isActive?: boolean;
}

export interface SheInspectionChecklistItemCreateRequest {
  checklistId: string;
  itemOrder: number;
  category: string;
  itemDescription: string;
  isMandatory: boolean;
  regulatoryReference?: string | null;
  associatedRiskLevel?: SheRiskLevel | null;
}

export interface SheInspectionChecklistItemUpdateRequest {
  id: string;
  itemOrder: number;
  category: string;
  itemDescription: string;
  isMandatory: boolean;
  regulatoryReference?: string | null;
  associatedRiskLevel?: SheRiskLevel | null;
}

// ── Safety inspections ────────────────────────────────────────────────────────

export interface SafetyInspectionItem extends AuditFields {
  inspectionId: string;
  checklistItemId?: string | null;
  itemDescription: string;
  status: SheComplianceStatus;
  statusName: string;
  deficiencyNoted?: string | null;
  actionRequired?: string | null;
  riskLevel?: SheRiskLevel | null;
  riskLevelName?: string | null;
  targetDate?: string | null;
  responsiblePersonId?: string | null;
  responsiblePersonName?: string | null;
  isResolved: boolean;
  resolvedDate?: string | null;
  resolutionNotes?: string | null;
  resolvedById?: string | null;
  resolvedByName?: string | null;
}

export interface SafetyInspectionHazardAction extends AuditFields {
  inspectionHazardId: string;
  correctiveActionTemplateId: string;
  correctiveActionTemplateTitle: string;
  status: SheCorrectiveActionStatus;
  statusName: string;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
  assignedToId?: string | null;
  assignedToName?: string | null;
}

export interface SafetyInspectionHazard extends AuditFields {
  inspectionId: string;
  /** Optional link to the master hazard register. */
  hazardId?: string | null;
  hazardDescription: string;
  status: SheHazardStatus;
  statusName: string;
  initialRiskLevel: SheHazardRiskLevel;
  initialRiskLevelName: string;
  residualRiskLevel: SheHazardRiskLevel;
  residualRiskLevelName: string;
  reviewDueDate?: string | null;
  ownerId?: string | null;
  ownerName?: string | null;
  actions: SafetyInspectionHazardAction[];
}

export interface SafetyInspectionDocument extends AuditFields {
  inspectionId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

export interface SafetyInspection extends AuditFields {
  tenantId: string;
  inspectionNumber: string;
  inspectionDate: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  type: SheInspectionType;
  typeName: string;
  category: SheInspectionCategory;
  categoryName: string;
  checklistId?: string | null;
  checklistName?: string | null;
  inspectorId: string;
  inspectorName: string;
  externalInspectorName?: string | null;
  externalInspectorOrganization?: string | null;
  findingsAndObservations?: string | null;
  recommendedActions?: string | null;
  positiveObservations?: string | null;
  status: SheInspectionStatus;
  statusName: string;
  overallRiskRating?: SheRiskLevel | null;
  overallRiskRatingName?: string | null;
  complianceScore?: number | null;
  complianceDeadline?: string | null;
  nextInspectionDueDate?: string | null;
  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  items: SafetyInspectionItem[];
  hazards: SafetyInspectionHazard[];
  documents: SafetyInspectionDocument[];
}

export interface SafetyInspectionSummary {
  id: string;
  inspectionNumber: string;
  inspectionDate: string;
  type: SheInspectionType;
  typeName: string;
  category: SheInspectionCategory;
  categoryName: string;
  locationName?: string | null;
  inspectorName: string;
  status: SheInspectionStatus;
  statusName: string;
  overallRiskRating?: SheRiskLevel | null;
  complianceScore?: number | null;
  nextInspectionDueDate?: string | null;
  openItemCount: number;
}

/** The number (INSP-YYYY-NNNN) is generated server-side; status starts at Scheduled. */
export interface SafetyInspectionCreateRequest {
  inspectionDate: string;
  locationId?: string | null;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  type: SheInspectionType;
  category: SheInspectionCategory;
  checklistId?: string | null;
  inspectorId: string;
  externalInspectorName?: string | null;
  externalInspectorOrganization?: string | null;
  nextInspectionDueDate?: string | null;
}

export interface SafetyInspectionUpdateRequest {
  id: string;
  inspectionDate: string;
  locationId?: string | null;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  type: SheInspectionType;
  category: SheInspectionCategory;
  checklistId?: string | null;
  inspectorId: string;
  externalInspectorName?: string | null;
  externalInspectorOrganization?: string | null;
  findingsAndObservations?: string | null;
  recommendedActions?: string | null;
  positiveObservations?: string | null;
  status: SheInspectionStatus;
  overallRiskRating?: SheRiskLevel | null;
  complianceScore?: number | null;
  complianceDeadline?: string | null;
  nextInspectionDueDate?: string | null;
}

/** Refused (422) when already closed, while any finding is unresolved, or while any
 * corrective action on a discovered hazard is still open. */
export interface SafetyInspectionCloseRequest {
  inspectionId: string;
  closedById: string;
  closedDate?: string;
}

export interface SafetyInspectionItemCreateRequest {
  inspectionId: string;
  checklistItemId?: string | null;
  itemDescription: string;
  status: SheComplianceStatus;
  deficiencyNoted?: string | null;
  actionRequired?: string | null;
  riskLevel?: SheRiskLevel | null;
  targetDate?: string | null;
  responsiblePersonId?: string | null;
}

export interface SafetyInspectionItemUpdateRequest {
  id: string;
  itemDescription: string;
  status: SheComplianceStatus;
  deficiencyNoted?: string | null;
  actionRequired?: string | null;
  riskLevel?: SheRiskLevel | null;
  targetDate?: string | null;
  responsiblePersonId?: string | null;
  isResolved: boolean;
  resolvedDate?: string | null;
  resolutionNotes?: string | null;
  resolvedById?: string | null;
}

export interface SafetyInspectionHazardCreateRequest {
  inspectionId: string;
  hazardId?: string | null;
  hazardDescription: string;
  status: SheHazardStatus;
  initialRiskLevel: SheHazardRiskLevel;
  residualRiskLevel: SheHazardRiskLevel;
  reviewDueDate?: string | null;
  ownerId?: string | null;
}

export interface SafetyInspectionHazardUpdateRequest {
  id: string;
  hazardId?: string | null;
  hazardDescription: string;
  status: SheHazardStatus;
  initialRiskLevel: SheHazardRiskLevel;
  residualRiskLevel: SheHazardRiskLevel;
  reviewDueDate?: string | null;
  ownerId?: string | null;
}

export interface SafetyInspectionHazardActionCreateRequest {
  inspectionHazardId: string;
  correctiveActionTemplateId: string;
  status: SheCorrectiveActionStatus;
  dueDate?: string | null;
  assignedToId?: string | null;
}

export interface SafetyInspectionHazardActionUpdateRequest {
  id: string;
  status: SheCorrectiveActionStatus;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
  assignedToId?: string | null;
}

export interface SafetyInspectionDocumentCreateRequest {
  inspectionId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadedById: string;
}
