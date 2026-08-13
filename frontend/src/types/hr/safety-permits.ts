// Types for HR Area 10 (SHE) slice 5: Permit to Work — the authorisation lifecycle
// (draft → approve/issue → active → suspend/resume → close), authorised workers, extensions
// and documents. Mirrors ErpSystem.Core.DTOs.HR.SafetyPermitPpeEquipmentDTOs (region E).
// Backend route: api/safety/permits.
//
// ⚠ Nothing here is automatic yet: permit expiry is NOT auto-detected and no reminders fire —
// that is the slice-13 job engine. Screens show the validity window and the expiring query,
// and must not promise otherwise.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type ShePermitType =
  | 'HotWork'
  | 'ConfinedSpaceEntry'
  | 'WorkingAtHeight'
  | 'Excavation'
  | 'ElectricalIsolation'
  | 'ChemicalHandling'
  | 'CriticalLift'
  | 'Demolition'
  | 'General'
  | 'RoadClosure';

export const SHE_PERMIT_TYPE_OPTIONS = opts<ShePermitType>([
  ['HotWork', 'Hot Work'],
  ['ConfinedSpaceEntry', 'Confined Space Entry'],
  ['WorkingAtHeight', 'Working at Height'],
  ['Excavation', 'Excavation'],
  ['ElectricalIsolation', 'Electrical Isolation'],
  ['ChemicalHandling', 'Chemical Handling'],
  ['CriticalLift', 'Critical Lift'],
  ['Demolition', 'Demolition'],
  ['General', 'General'],
  ['RoadClosure', 'Road Closure'],
]);

/** Hot work and confined-space permits cannot be approved without gas test results. */
export const GAS_TEST_MANDATORY_TYPES: ShePermitType[] = ['HotWork', 'ConfinedSpaceEntry'];

export type ShePermitStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Active'
  | 'Suspended'
  | 'Completed'
  | 'Cancelled'
  | 'Expired';

export const SHE_PERMIT_STATUS_OPTIONS = opts<ShePermitStatus>([
  ['Draft', 'Draft'],
  ['PendingApproval', 'Pending Approval'],
  ['Approved', 'Approved'],
  ['Active', 'Active'],
  ['Suspended', 'Suspended'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
  ['Expired', 'Expired'],
]);

// ── Permit ────────────────────────────────────────────────────────────────────

export interface ShePermitToWorkWorker extends AuditFields {
  permitToWorkId: string;
  employeeId?: string | null;
  workerName: string;
  companyName?: string | null;
  tradeOrRole?: string | null;
  briefed: boolean;
  briefedDate?: string | null;
  signedOff: boolean;
  signedDate?: string | null;
}

export interface ShePermitToWorkExtension extends AuditFields {
  permitToWorkId: string;
  /** Assigned server-side in sequence — the client does not control it. */
  extensionNumber: number;
  newEndDate: string;
  /** TimeSpan — "HH:mm:ss". */
  newEndTime: string;
  reason: string;
  approvedById: string;
  approvedByName: string;
  approvedDate: string;
}

export interface ShePermitToWorkDocument extends AuditFields {
  permitToWorkId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

export interface ShePermitToWork extends AuditFields {
  tenantId: string;
  permitNumber: string;
  permitType: ShePermitType;
  permitTypeName: string;
  workDescription: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;

  requestedById: string;
  requestedByName: string;
  contractorId?: string | null;
  contractorName?: string | null;
  requestedDate: string;

  plannedStartDate: string;
  /** TimeSpan — "HH:mm:ss". */
  plannedStartTime: string;
  plannedEndDate: string;
  plannedEndTime: string;
  actualStartDate?: string | null;
  actualEndDate?: string | null;

  status: ShePermitStatus;
  statusName: string;
  issuedById?: string | null;
  issuedByName?: string | null;
  issuedDate?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedDate?: string | null;

  hazardsIdentified?: string | null;
  controlMeasures?: string | null;
  ppeRequired?: string | null;
  gasTestResults?: string | null;
  isolationDetails?: string | null;
  riskAssessmentId?: string | null;
  riskAssessmentNumber?: string | null;

  isSuspended: boolean;
  suspendedDate?: string | null;
  suspensionReason?: string | null;
  suspendedById?: string | null;
  suspendedByName?: string | null;

  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  closureNotes?: string | null;
  workCompletedSatisfactorily: boolean;
  areaLeftSafe: boolean;
  reinstatementNotes?: string | null;

  authorisedWorkers: ShePermitToWorkWorker[];
  extensions: ShePermitToWorkExtension[];
  documents: ShePermitToWorkDocument[];
}

export interface ShePermitToWorkSummary {
  id: string;
  permitNumber: string;
  permitType: ShePermitType;
  permitTypeName: string;
  workDescription: string;
  locationName?: string | null;
  requestedByName: string;
  contractorName?: string | null;
  plannedStartDate: string;
  plannedEndDate: string;
  status: ShePermitStatus;
  statusName: string;
  isSuspended: boolean;
}

/** The number (PTW-YYYY-NNNN) is generated server-side; status starts at Draft. */
export interface ShePermitToWorkCreateRequest {
  permitType: ShePermitType;
  workDescription: string;
  locationId?: string | null;
  specificArea?: string | null;
  requestedById: string;
  contractorId?: string | null;
  requestedDate?: string;
  plannedStartDate: string;
  plannedStartTime?: string;
  plannedEndDate: string;
  plannedEndTime?: string;
  hazardsIdentified?: string | null;
  controlMeasures?: string | null;
  ppeRequired?: string | null;
  gasTestResults?: string | null;
  isolationDetails?: string | null;
  riskAssessmentId?: string | null;
}

/** Refused (422) once the permit is Completed or Cancelled. */
export interface ShePermitToWorkUpdateRequest {
  id: string;
  permitType: ShePermitType;
  workDescription: string;
  locationId?: string | null;
  specificArea?: string | null;
  contractorId?: string | null;
  plannedStartDate: string;
  plannedStartTime?: string;
  plannedEndDate: string;
  plannedEndTime?: string;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  hazardsIdentified?: string | null;
  controlMeasures?: string | null;
  ppeRequired?: string | null;
  gasTestResults?: string | null;
  isolationDetails?: string | null;
  riskAssessmentId?: string | null;
}

/** Refused (422) unless Draft/PendingApproval, and — FR-PTW-002 — until the mandatory safety
 * sections are complete: hazards identified, control measures, and gas test results for hot
 * work / confined space. Approval activates the permit. */
export interface ShePermitToWorkApproveRequest {
  permitId: string;
  approvedById: string;
  approvedDate?: string;
  issuedById?: string | null;
  issuedDate?: string | null;
}

/** Refused (422) unless the permit is Active. */
export interface ShePermitToWorkSuspendRequest {
  permitId: string;
  suspendedById: string;
  suspendedDate?: string;
  suspensionReason: string;
}

/** Refused (422) unless the permit is Active or Suspended — a draft is deleted, not closed,
 * and a completed/cancelled permit does not close twice. */
export interface ShePermitToWorkCloseRequest {
  permitId: string;
  closedById: string;
  closedDate?: string;
  workCompletedSatisfactorily: boolean;
  areaLeftSafe: boolean;
  reinstatementNotes?: string | null;
  closureNotes?: string | null;
}

export interface ShePermitToWorkWorkerCreateRequest {
  permitToWorkId: string;
  employeeId?: string | null;
  workerName: string;
  companyName?: string | null;
  tradeOrRole?: string | null;
  briefed: boolean;
  briefedDate?: string | null;
  signedOff: boolean;
  signedDate?: string | null;
}

export interface ShePermitToWorkWorkerUpdateRequest {
  id: string;
  workerName: string;
  companyName?: string | null;
  tradeOrRole?: string | null;
  briefed: boolean;
  briefedDate?: string | null;
  signedOff: boolean;
  signedDate?: string | null;
}

/** Refused (422) unless the permit is Active. Extends the permit's planned end. */
export interface ShePermitToWorkExtensionCreateRequest {
  permitToWorkId: string;
  newEndDate: string;
  newEndTime?: string;
  reason: string;
  approvedById: string;
  approvedDate?: string;
}

export interface ShePermitToWorkDocumentCreateRequest {
  permitToWorkId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadedById: string;
}
