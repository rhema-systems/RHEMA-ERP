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
  /** Null only on rows that pre-date the builder. */
  sectionId?: string | null;
  itemOrder: number;
  /** Printed running number across the standard sections; 0 for critical-section items. */
  itemNumber: number;
  category?: string | null;
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
  /** Offered for new inspections only when active AND published. */
  isActive: boolean;
  status: SheChecklistStatus;
  statusName: string;
  /** True once published or retired — structure writes are refused (422). */
  isStructureLocked: boolean;
  scoringMode: SheChecklistScoringMode;
  scoringModeName: string;
  allowPartialCompliance: boolean;
  printTitle?: string | null;
  printSubtitle?: string | null;
  instructions?: string | null;
  criticalSectionNote?: string | null;
  publishedAt?: string | null;
  publishedById?: string | null;
  publishedByName?: string | null;
  retiredAt?: string | null;
  previousVersionId?: string | null;
  /** Item count across all sections — carried by list reads too. */
  itemCount: number;
  /** Structure below is loaded on the detail read only — list reads return the arrays empty. */
  fields: SheInspectionChecklistField[];
  /** Ordered sections with their items. Legacy section-less items come under a synthetic section whose id is the empty GUID. */
  sections: SheInspectionChecklistSection[];
  /** The flat item list in form order. */
  items: SheInspectionChecklistItem[];
  outcomes: SheInspectionChecklistOutcome[];
  signatories: SheInspectionChecklistSignatory[];
}

/** The server refuses (422) a duplicate checklist number + version within the tenant. */
export interface SheInspectionChecklistCreateRequest {
  checklistNumber: string;
  name: string;
  description?: string | null;
  type: SheInspectionType;
  version?: number;
  isActive?: boolean;
  scoringMode?: SheChecklistScoringMode;
  allowPartialCompliance?: boolean;
  printTitle?: string | null;
  printSubtitle?: string | null;
  instructions?: string | null;
  criticalSectionNote?: string | null;
}

/**
 * The number is immutable after creation — it is not on the update contract. On a locked
 * (published / retired) template only name, description and isActive apply; a change to any
 * structural field is refused with 422.
 */
export interface SheInspectionChecklistUpdateRequest {
  id: string;
  name: string;
  description?: string | null;
  type: SheInspectionType;
  version?: number;
  isActive?: boolean;
  scoringMode?: SheChecklistScoringMode;
  allowPartialCompliance?: boolean;
  printTitle?: string | null;
  printSubtitle?: string | null;
  instructions?: string | null;
  criticalSectionNote?: string | null;
}

export interface SheInspectionChecklistItemCreateRequest {
  checklistId: string;
  sectionId?: string | null;
  itemOrder: number;
  /** Defaults to the section title when blank. */
  category?: string | null;
  itemDescription: string;
  isMandatory: boolean;
  regulatoryReference?: string | null;
  associatedRiskLevel?: SheRiskLevel | null;
}

export interface SheInspectionChecklistItemUpdateRequest {
  id: string;
  sectionId?: string | null;
  itemOrder: number;
  category?: string | null;
  itemDescription: string;
  isMandatory: boolean;
  regulatoryReference?: string | null;
  associatedRiskLevel?: SheRiskLevel | null;
}

// ── Safety inspections ────────────────────────────────────────────────────────

export interface SafetyInspectionItem extends AuditFields {
  inspectionId: string;
  checklistItemId?: string | null;
  /** Form position when materialised from a template; 0 for hand-added findings. */
  displayOrder: number;
  /** Printed running number across the standard sections; 0 for critical items and hand-added findings. */
  itemNumber: number;
  sectionId?: string | null;
  sectionCode?: string | null;
  sectionTitle?: string | null;
  sectionKind?: SheChecklistSectionKind | null;
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
  // ── Checklist run ──
  checklistNumber?: string | null;
  checklistVersion?: number | null;
  scoringMode?: SheChecklistScoringMode | null;
  scoringModeName?: string | null;
  totalApplicableItems?: number | null;
  totalCompliantItems?: number | null;
  totalNonCompliantItems?: number | null;
  totalPartiallyCompliantItems?: number | null;
  criticalNonConformityCount?: number | null;
  compliancePercentage?: number | null;
  recommendedOutcomeId?: string | null;
  recommendedOutcomeLabel?: string | null;
  outcomeId?: string | null;
  outcomeLabel?: string | null;
  outcomeIsDisqualifying?: boolean | null;
  outcomeReinspectionWithinDays?: number | null;
  outcomeOverrideReason?: string | null;
  subjectComments?: string | null;
  completedAt?: string | null;
  completedById?: string | null;
  completedByName?: string | null;
  /** The pinned template with its structure — detail read only; null on free-form inspections. */
  checklist?: SheInspectionChecklist | null;
  fieldValues: SafetyInspectionFieldValue[];
  signatures: SafetyInspectionSignature[];
  /** In form order — materialised items first, hand-added findings after. */
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

// ── Checklist builder (docs/HR/areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md) ─────────────
// A template is header fields + lettered sections of items (+ critical Yes/No sections) + a
// scoring mode + outcomes + signatories. Structure is writable only while Draft; publishing
// validates and freezes it; "new version" clones it into Draft v+1 under the same number.

export type SheChecklistStatus = 'Draft' | 'Published' | 'Retired';

export const SHE_CHECKLIST_STATUS_OPTIONS = opts<SheChecklistStatus>([
  ['Draft', 'Draft'],
  ['Published', 'Published'],
  ['Retired', 'Retired'],
]);

export type SheChecklistScoringMode = 'None' | 'CompliancePercentage' | 'QualitativeRating';

export const SHE_CHECKLIST_SCORING_MODE_OPTIONS = opts<SheChecklistScoringMode>([
  ['CompliancePercentage', 'Compliance percentage with decision bands'],
  ['QualitativeRating', 'Qualitative rating (inspector picks an outcome)'],
  ['None', 'No score — items and findings only'],
]);

export type SheChecklistSectionKind = 'Standard' | 'Critical';

export const SHE_CHECKLIST_SECTION_KIND_OPTIONS = opts<SheChecklistSectionKind>([
  ['Standard', 'Standard (C / NC / NA)'],
  ['Critical', 'Critical non-conformities (Yes / No — any Yes disqualifies)'],
]);

export type SheChecklistFieldType =
  | 'Text'
  | 'LongText'
  | 'Number'
  | 'Date'
  | 'Time'
  | 'YesNo'
  | 'Choice'
  | 'Employee'
  | 'Location'
  | 'OrganizationUnit';

export const SHE_CHECKLIST_FIELD_TYPE_OPTIONS = opts<SheChecklistFieldType>([
  ['Text', 'Text'],
  ['LongText', 'Long text'],
  ['Number', 'Number'],
  ['Date', 'Date'],
  ['Time', 'Time'],
  ['YesNo', 'Yes / No'],
  ['Choice', 'Choice (one of a list)'],
  ['Employee', 'Employee'],
  ['Location', 'Location'],
  ['OrganizationUnit', 'Organization unit'],
]);

export type SheChecklistSignatoryKind = 'SystemUser' | 'External';

export const SHE_CHECKLIST_SIGNATORY_KIND_OPTIONS = opts<SheChecklistSignatoryKind>([
  ['SystemUser', 'System user (signs in-app as themselves)'],
  ['External', 'External party (typed name and date)'],
]);

export interface SheInspectionChecklistField extends AuditFields {
  checklistId: string;
  displayOrder: number;
  label: string;
  fieldType: SheChecklistFieldType;
  fieldTypeName: string;
  isRequired: boolean;
  /** '|'-separated options for Choice fields. */
  choiceOptions?: string | null;
  helpText?: string | null;
  /** choiceOptions split, trimmed, blanks dropped. */
  choices: string[];
}

export interface SheInspectionChecklistFieldCreateRequest {
  checklistId: string;
  displayOrder: number;
  label: string;
  fieldType: SheChecklistFieldType;
  isRequired: boolean;
  choiceOptions?: string | null;
  helpText?: string | null;
}

export interface SheInspectionChecklistFieldUpdateRequest {
  id: string;
  displayOrder: number;
  label: string;
  fieldType: SheChecklistFieldType;
  isRequired: boolean;
  choiceOptions?: string | null;
  helpText?: string | null;
}

export interface SheInspectionChecklistSection extends AuditFields {
  checklistId: string;
  displayOrder: number;
  code?: string | null;
  title: string;
  description?: string | null;
  kind: SheChecklistSectionKind;
  kindName: string;
  items: SheInspectionChecklistItem[];
}

export interface SheInspectionChecklistSectionCreateRequest {
  checklistId: string;
  displayOrder: number;
  code?: string | null;
  title: string;
  description?: string | null;
  kind: SheChecklistSectionKind;
}

export interface SheInspectionChecklistSectionUpdateRequest {
  id: string;
  displayOrder: number;
  code?: string | null;
  title: string;
  description?: string | null;
  kind: SheChecklistSectionKind;
}

export interface SheInspectionChecklistOutcome extends AuditFields {
  checklistId: string;
  displayOrder: number;
  label: string;
  description?: string | null;
  minPercent?: number | null;
  maxPercent?: number | null;
  reinspectionWithinDays?: number | null;
  isDisqualifying: boolean;
}

export interface SheInspectionChecklistOutcomeCreateRequest {
  checklistId: string;
  displayOrder: number;
  label: string;
  description?: string | null;
  minPercent?: number | null;
  maxPercent?: number | null;
  reinspectionWithinDays?: number | null;
  isDisqualifying: boolean;
}

export interface SheInspectionChecklistOutcomeUpdateRequest {
  id: string;
  displayOrder: number;
  label: string;
  description?: string | null;
  minPercent?: number | null;
  maxPercent?: number | null;
  reinspectionWithinDays?: number | null;
  isDisqualifying: boolean;
}

export interface SheInspectionChecklistSignatory extends AuditFields {
  checklistId: string;
  displayOrder: number;
  roleLabel: string;
  kind: SheChecklistSignatoryKind;
  kindName: string;
  isRequired: boolean;
}

export interface SheInspectionChecklistSignatoryCreateRequest {
  checklistId: string;
  displayOrder: number;
  roleLabel: string;
  kind: SheChecklistSignatoryKind;
  isRequired: boolean;
}

export interface SheInspectionChecklistSignatoryUpdateRequest {
  id: string;
  displayOrder: number;
  roleLabel: string;
  kind: SheChecklistSignatoryKind;
  isRequired: boolean;
}

/** Replace-set ordering: every current child id in its new order (a missing or foreign id is refused). */
export interface SheChecklistReorderRequest {
  orderedIds: string[];
}

// ── Checklist run (inspection side) ───────────────────────────────────────────

export interface SafetyInspectionFieldValue extends AuditFields {
  inspectionId: string;
  checklistFieldId: string;
  label: string;
  fieldType: SheChecklistFieldType;
  fieldTypeName: string;
  valueText?: string | null;
  valueReferenceId?: string | null;
  /** The resolved name for reference fields; valueText otherwise. */
  valueDisplay?: string | null;
}

export interface SafetyInspectionFieldValueWrite {
  checklistFieldId: string;
  valueText?: string | null;
  valueReferenceId?: string | null;
}

/** One item's answer in the bulk walk. */
export interface SafetyInspectionResponse {
  itemId: string;
  status: SheComplianceStatus;
  deficiencyNoted?: string | null;
  actionRequired?: string | null;
  riskLevel?: SheRiskLevel | null;
}

export interface SafetyInspectionCompleteRequest {
  /** Required in QualitativeRating mode; in CompliancePercentage mode defaults to the recommendation. */
  outcomeId?: string | null;
  outcomeOverrideReason?: string | null;
  subjectComments?: string | null;
  findingsAndObservations?: string | null;
  recommendedActions?: string | null;
  overallRiskRating?: SheRiskLevel | null;
  complianceDeadline?: string | null;
  nextInspectionDueDate?: string | null;
}

export interface SafetyInspectionSignatureCreateRequest {
  checklistSignatoryId: string;
  /** External signatories only — ignored for SystemUser rows, which sign as the logged-in employee. */
  signedName?: string | null;
  signedAt?: string | null;
  notes?: string | null;
}

export interface SafetyInspectionSignature extends AuditFields {
  inspectionId: string;
  checklistSignatoryId: string;
  roleLabel: string;
  kind: SheChecklistSignatoryKind;
  kindName: string;
  signedByEmployeeId?: string | null;
  signedByName?: string | null;
  signedName: string;
  signedAt: string;
  notes?: string | null;
}

/** The live computation over the current answers — what Complete will persist. */
export interface SafetyInspectionScore {
  scoringMode: SheChecklistScoringMode;
  scoringModeName: string;
  totalItems: number;
  totalApplicableItems: number;
  totalCompliantItems: number;
  totalNonCompliantItems: number;
  totalPartiallyCompliantItems: number;
  totalNotApplicableItems: number;
  totalNotAssessedItems: number;
  criticalNonConformityCount: number;
  compliancePercentage?: number | null;
  isDisqualified: boolean;
  recommendedOutcomeId?: string | null;
  recommendedOutcomeLabel?: string | null;
  isReadyToComplete: boolean;
  missingRequiredFields: string[];
}
