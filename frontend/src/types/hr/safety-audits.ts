// Types for HR Area 10 slice 15: SHE audit management (FRD §12 / FR-SHE-229),
// stop-work authority (FR-SHE-200) and statutory incident submissions (FR-SHE-103).
// Mirrors ErpSystem.Core.DTOs.HR.SafetyAuditStopWorkDTOs. Backend routes:
// api/safety/audits, api/safety/stop-work, api/safety/incidents/*/statutory-submissions.

import type { AuditFields } from './common';
import type { SheCorrectiveActionStatus } from './safety-incidents';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Audits ───────────────────────────────────────────────────────────────────

export type SheAuditType = 'Internal' | 'External' | 'Regulatory' | 'Certification';

export const SHE_AUDIT_TYPE_OPTIONS = opts<SheAuditType>([
  ['Internal', 'Internal'],
  ['External', 'External'],
  ['Regulatory', 'Regulatory'],
  ['Certification', 'Certification'],
]);

/** Planned → InProgress → ReportIssued → Closed, with Cancelled aside. */
export type SheAuditStatus = 'Planned' | 'InProgress' | 'ReportIssued' | 'Closed' | 'Cancelled';

export const SHE_AUDIT_STATUS_OPTIONS = opts<SheAuditStatus>([
  ['Planned', 'Planned'],
  ['InProgress', 'In Progress'],
  ['ReportIssued', 'Report Issued'],
  ['Closed', 'Closed'],
  ['Cancelled', 'Cancelled'],
]);

export type SheAuditFindingClassification =
  | 'MajorNonConformity'
  | 'MinorNonConformity'
  | 'Observation'
  | 'OpportunityForImprovement';

export const SHE_AUDIT_FINDING_CLASSIFICATION_OPTIONS = opts<SheAuditFindingClassification>([
  ['MajorNonConformity', 'Major Non-Conformity'],
  ['MinorNonConformity', 'Minor Non-Conformity'],
  ['Observation', 'Observation'],
  ['OpportunityForImprovement', 'Opportunity for Improvement'],
]);

/** Open → Resolved → Verified → Closed. Closing needs verification AND completed actions. */
export type SheAuditFindingStatus = 'Open' | 'Resolved' | 'Verified' | 'Closed';

export interface SheAuditFindingAction extends AuditFields {
  findingId: string;
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

export interface SheAuditFinding extends AuditFields {
  auditId: string;
  auditNumber?: string | null;
  findingNumber: number;
  classification: SheAuditFindingClassification;
  classificationName: string;
  clauseReference?: string | null;
  description: string;
  evidence?: string | null;
  status: SheAuditFindingStatus;
  statusName: string;
  responsiblePersonId?: string | null;
  responsiblePersonName?: string | null;
  dueDate?: string | null;
  resolutionNotes?: string | null;
  resolvedDate?: string | null;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verifiedDate?: string | null;
  verificationNotes?: string | null;
  actions: SheAuditFindingAction[];
}

export interface SheAuditTeamMember extends AuditFields {
  auditId: string;
  employeeId: string;
  employeeName: string;
  role: string;
}

export interface SheAudit extends AuditFields {
  tenantId: string;
  auditNumber: string;
  title: string;
  type: SheAuditType;
  typeName: string;
  standard?: string | null;
  scope?: string | null;
  objectives?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  leadAuditorId: string;
  leadAuditorName: string;
  externalAuditorName?: string | null;
  externalAuditorOrganization?: string | null;
  plannedStartDate: string;
  plannedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  status: SheAuditStatus;
  statusName: string;
  summary?: string | null;
  reportDocumentPath?: string | null;
  reportIssuedDate?: string | null;
  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  closureNotes?: string | null;
  teamMembers: SheAuditTeamMember[];
  findings: SheAuditFinding[];
}

export interface SheAuditSummary {
  id: string;
  auditNumber: string;
  title: string;
  type: SheAuditType;
  typeName: string;
  standard?: string | null;
  locationName?: string | null;
  leadAuditorName: string;
  plannedStartDate: string;
  status: SheAuditStatus;
  statusName: string;
  findingCount: number;
  openFindingCount: number;
}

export interface SheAuditCreateRequest {
  /** Omit for a server-assigned AUD-YYYY-NNNN. */
  auditNumber?: string | null;
  title: string;
  type: SheAuditType;
  standard?: string | null;
  scope?: string | null;
  objectives?: string | null;
  locationId?: string | null;
  organizationUnitId?: string | null;
  leadAuditorId: string;
  externalAuditorName?: string | null;
  externalAuditorOrganization?: string | null;
  plannedStartDate: string;
  plannedEndDate?: string | null;
}

export interface SheAuditUpdateRequest extends Omit<SheAuditCreateRequest, 'auditNumber'> {
  id: string;
}

export interface SheAuditFindingCreateRequest {
  auditId: string;
  classification: SheAuditFindingClassification;
  clauseReference?: string | null;
  description: string;
  evidence?: string | null;
  responsiblePersonId?: string | null;
  dueDate?: string | null;
}

export interface SheAuditFindingUpdateRequest {
  id: string;
  classification: SheAuditFindingClassification;
  clauseReference?: string | null;
  description: string;
  evidence?: string | null;
  responsiblePersonId?: string | null;
  dueDate?: string | null;
  resolutionNotes?: string | null;
  resolvedDate?: string | null;
}

export interface SheAuditFindingActionCreateRequest {
  findingId: string;
  correctiveActionTemplateId: string;
  status?: SheCorrectiveActionStatus;
  dueDate?: string | null;
  assignedToId?: string | null;
}

export interface SheAuditFindingActionUpdateRequest {
  id: string;
  status: SheCorrectiveActionStatus;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
  assignedToId?: string | null;
}

// ── Stop-work authority ──────────────────────────────────────────────────────

/** Raised → UnderReview → Resolved → Cleared, with Cancelled for false alarms. */
export type SheStopWorkStatus = 'Raised' | 'UnderReview' | 'Resolved' | 'Cleared' | 'Cancelled';

export const SHE_STOP_WORK_STATUS_OPTIONS = opts<SheStopWorkStatus>([
  ['Raised', 'Raised'],
  ['UnderReview', 'Under Review'],
  ['Resolved', 'Resolved'],
  ['Cleared', 'Cleared'],
  ['Cancelled', 'Cancelled'],
]);

export interface SheStopWorkOrder extends AuditFields {
  tenantId: string;
  orderNumber: string;
  raisedById: string;
  raisedByName: string;
  raisedDate: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  workDescription: string;
  reasonDescription: string;
  immediateActionsTaken?: string | null;
  permitToWorkId?: string | null;
  permitNumber?: string | null;
  hazardId?: string | null;
  hazardName?: string | null;
  incidentId?: string | null;
  incidentNumber?: string | null;
  status: SheStopWorkStatus;
  statusName: string;
  routedToId?: string | null;
  routedToName?: string | null;
  routedDate?: string | null;
  resolutionDescription?: string | null;
  resolvedById?: string | null;
  resolvedByName?: string | null;
  resolvedDate?: string | null;
  clearedById?: string | null;
  clearedByName?: string | null;
  clearedDate?: string | null;
  clearanceNotes?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  cancelledDate?: string | null;
  cancellationReason?: string | null;
}

/** Raising is open to every employee; non-HR raisers are forced onto the token's employee. */
export interface SheStopWorkOrderCreateRequest {
  raisedById?: string | null;
  locationId?: string | null;
  specificArea?: string | null;
  workDescription: string;
  reasonDescription: string;
  immediateActionsTaken?: string | null;
  permitToWorkId?: string | null;
  hazardId?: string | null;
  incidentId?: string | null;
}
