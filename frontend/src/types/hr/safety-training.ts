// Types for HR Area 10 (SHE) slice 12: safety training plans, programs and attendance.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyContractorTrainingDTOs (region I).
// Backend route: api/safety/training.
//
// ⚠ Ownership boundary (FR-SHE-121, recorded): this is the SHE training record — separate
// from the corporate Training module (area 7), which stays untouched. SHE attendance covers
// NON-employees too (contractor workers/visitors sign by name + company) and tracks
// per-attendee certificate expiry; sharing into the employee's corporate training record is
// a later read-only projection, never a merge.
//
// Plan numbers and program codes are USER-entered, unique per tenant (duplicate → 422),
// and immutable. Employee attendance rows carry the employee's real name (server-derived).
// Certificate renewal notices fire automatically (slice-13 reminder engine, 90/60/30/14/7
// ladder); the expiring-certificates view remains the work queue.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheTrainingPlanStatus = 'Draft' | 'Approved' | 'Active' | 'Completed' | 'Archived';

export const SHE_TRAINING_PLAN_STATUS_OPTIONS = opts<SheTrainingPlanStatus>([
  ['Draft', 'Draft'],
  ['Approved', 'Approved'],
  ['Active', 'Active'],
  ['Completed', 'Completed'],
  ['Archived', 'Archived'],
]);

export type SheTrainingCategory =
  | 'GeneralInduction'
  | 'FireSafetyAndEvacuation'
  | 'FirstAid'
  | 'PPEUsage'
  | 'HazardCommunication'
  | 'ManualHandling'
  | 'WorkingAtHeight'
  | 'ConfinedSpaceEntry'
  | 'HotWork'
  | 'EnvironmentalAwareness'
  | 'IncidentReporting'
  | 'EmergencyResponse'
  | 'ToolboxTalk'
  | 'ConstructionSafety'
  | 'OccupationalHealth'
  | 'BehaviouralSafety';

export const SHE_TRAINING_CATEGORY_OPTIONS = opts<SheTrainingCategory>([
  ['GeneralInduction', 'General Induction'],
  ['FireSafetyAndEvacuation', 'Fire Safety & Evacuation'],
  ['FirstAid', 'First Aid'],
  ['PPEUsage', 'PPE Usage'],
  ['HazardCommunication', 'Hazard Communication'],
  ['ManualHandling', 'Manual Handling'],
  ['WorkingAtHeight', 'Working at Height'],
  ['ConfinedSpaceEntry', 'Confined Space Entry'],
  ['HotWork', 'Hot Work'],
  ['EnvironmentalAwareness', 'Environmental Awareness'],
  ['IncidentReporting', 'Incident Reporting'],
  ['EmergencyResponse', 'Emergency Response'],
  ['ToolboxTalk', 'Toolbox Talk'],
  ['ConstructionSafety', 'Construction Safety'],
  ['OccupationalHealth', 'Occupational Health'],
  ['BehaviouralSafety', 'Behavioural Safety'],
]);

export type SheTrainingDeliveryMethod =
  | 'Classroom'
  | 'OnTheJob'
  | 'PracticalDrill'
  | 'Online'
  | 'ToolboxTalk'
  | 'SiteWalkthrough'
  | 'Workshop'
  | 'Simulation';

export const SHE_TRAINING_DELIVERY_OPTIONS = opts<SheTrainingDeliveryMethod>([
  ['Classroom', 'Classroom'],
  ['OnTheJob', 'On the Job'],
  ['PracticalDrill', 'Practical Drill'],
  ['Online', 'Online'],
  ['ToolboxTalk', 'Toolbox Talk'],
  ['SiteWalkthrough', 'Site Walkthrough'],
  ['Workshop', 'Workshop'],
  ['Simulation', 'Simulation'],
]);

export type SheTrainingStatus =
  | 'Planned'
  | 'Scheduled'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'Postponed';

export const SHE_TRAINING_STATUS_OPTIONS = opts<SheTrainingStatus>([
  ['Planned', 'Planned'],
  ['Scheduled', 'Scheduled'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
  ['Postponed', 'Postponed'],
]);

// ── Plans ─────────────────────────────────────────────────────────────────────

export interface SheTrainingProgramSummary {
  id: string;
  programCode: string;
  title: string;
  category: SheTrainingCategory;
  categoryName: string;
  deliveryMethod: SheTrainingDeliveryMethod;
  deliveryMethodName: string;
  scheduledDate?: string | null;
  actualDate?: string | null;
  status: SheTrainingStatus;
  statusName: string;
  actualAttendees?: number | null;
}

export interface SheTrainingPlan extends AuditFields {
  tenantId: string;
  planNumber: string;
  title: string;
  year: number;
  quarter?: number | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  status: SheTrainingPlanStatus;
  statusName: string;
  preparedById: string;
  preparedByName: string;
  preparedDate: string;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedDate?: string | null;
  notes?: string | null;
  programs: SheTrainingProgramSummary[];
}

export interface SheTrainingPlanCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  planNumber: string;
  title: string;
  year: number;
  quarter?: number | null;
  organizationUnitId?: string | null;
  preparedById: string;
  preparedDate?: string;
  notes?: string | null;
}

/** Approving = setting status Approved — which requires an approver on record (422 otherwise). */
export interface SheTrainingPlanUpdateRequest {
  id: string;
  title: string;
  year: number;
  quarter?: number | null;
  organizationUnitId?: string | null;
  status: SheTrainingPlanStatus;
  approvedById?: string | null;
  approvedDate?: string | null;
  notes?: string | null;
}

// ── Programs ──────────────────────────────────────────────────────────────────

export interface SheTrainingProgram extends AuditFields {
  tenantId: string;
  programCode: string;
  title: string;
  description?: string | null;
  category: SheTrainingCategory;
  categoryName: string;
  planId?: string | null;
  planNumber?: string | null;
  deliveryMethod: SheTrainingDeliveryMethod;
  deliveryMethodName: string;
  durationMinutes: number;
  scheduledDate?: string | null;
  actualDate?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  trainerId?: string | null;
  trainerName?: string | null;
  externalTrainerName?: string | null;
  externalTrainerOrganization?: string | null;
  status: SheTrainingStatus;
  statusName: string;
  maxParticipants?: number | null;
  /** Hand-entered headcount — may exceed the recorded attendance rows. */
  actualAttendees?: number | null;
  materialPath?: string | null;
  wasEvaluated: boolean;
  evaluationSummary?: string | null;
  evaluatedById?: string | null;
  evaluatedByName?: string | null;
  evaluationDate?: string | null;
  attendances: SheTrainingAttendance[];
}

export interface SheTrainingProgramCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  programCode: string;
  title: string;
  description?: string | null;
  category: SheTrainingCategory;
  planId?: string | null;
  deliveryMethod: SheTrainingDeliveryMethod;
  durationMinutes: number;
  scheduledDate?: string | null;
  locationId?: string | null;
  trainerId?: string | null;
  externalTrainerName?: string | null;
  externalTrainerOrganization?: string | null;
  maxParticipants?: number | null;
  materialPath?: string | null;
}

export interface SheTrainingProgramUpdateRequest {
  id: string;
  title: string;
  description?: string | null;
  category: SheTrainingCategory;
  planId?: string | null;
  deliveryMethod: SheTrainingDeliveryMethod;
  durationMinutes: number;
  scheduledDate?: string | null;
  actualDate?: string | null;
  locationId?: string | null;
  trainerId?: string | null;
  externalTrainerName?: string | null;
  externalTrainerOrganization?: string | null;
  status: SheTrainingStatus;
  maxParticipants?: number | null;
  actualAttendees?: number | null;
  materialPath?: string | null;
}

/** One-shot: a program that has been evaluated refuses a second evaluation (422). */
export interface SheTrainingProgramEvaluateRequest {
  programId: string;
  evaluatedById: string;
  evaluationDate?: string;
  evaluationSummary?: string | null;
}

// ── Attendance ────────────────────────────────────────────────────────────────

export interface SheTrainingAttendance extends AuditFields {
  programId: string;
  isEmployee: boolean;
  employeeId?: string | null;
  attendanceName: string;
  companyName?: string | null;
  attended: boolean;
  signedDate?: string | null;
  signaturePath?: string | null;
  assessmentPassed?: boolean | null;
  assessmentScore?: number | null;
  certificateExpiryDate?: string | null;
  certificateDocumentPath?: string | null;
  programCode?: string | null;
  programTitle?: string | null;
  programCategory?: SheTrainingCategory | null;
  programCategoryName?: string | null;
  programDate?: string | null;
  programStatus?: SheTrainingStatus | null;
  programStatusName?: string | null;
}

/**
 * Employee rows: employeeId required, name is server-derived from the employee record, and a
 * repeat row for the same employee is refused. Visitor rows: name + company, no employeeId.
 */
export interface SheTrainingAttendanceCreateRequest {
  programId: string;
  isEmployee: boolean;
  employeeId?: string | null;
  attendanceName: string;
  companyName?: string | null;
  attended: boolean;
  signedDate?: string | null;
  signaturePath?: string | null;
  assessmentPassed?: boolean | null;
  assessmentScore?: number | null;
  certificateExpiryDate?: string | null;
  certificateDocumentPath?: string | null;
}

/** Employee rows keep their server-derived name — edits to it are ignored. */
export interface SheTrainingAttendanceUpdateRequest {
  id: string;
  attendanceName: string;
  companyName?: string | null;
  attended: boolean;
  signedDate?: string | null;
  signaturePath?: string | null;
  assessmentPassed?: boolean | null;
  assessmentScore?: number | null;
  certificateExpiryDate?: string | null;
  certificateDocumentPath?: string | null;
}
