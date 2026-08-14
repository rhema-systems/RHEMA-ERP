// Types for HR Area 10 (SHE) slice 9: occupational health (health surveillance, first-aid
// stations, wellness programs) and return-to-work plans with phases and reviews.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyEnvironmentHealthDTOs (occ-health) and
// SafetyEmergencyGovernanceDTOs (return-to-work).
// Backend routes: api/safety/occupational-health, api/safety/return-to-work.
//
// ⚠ Access: both controllers are gated on the HR.Medical.* permission policies (not the
// SHE HR-role gate) — surveillance results, work restrictions and medical clearance are
// medical-grade data per the SHE↔Medical boundary. HR-role users pass via the role fallback
// until the permission seed propagates.
// Surveillance may reference the Medical module's healthcare-facility register by id (the
// agreed bridge); the examining physician stays free text for now.
// Phase and review numbers are SERVER-ASSIGNED sequences — never sent by the client.
// Due/expiry queues are polled — no automatic reminders until the slice-13 job engine.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheHealthSurveillanceType =
  | 'Audiometry'
  | 'LungFunctionSpirometry'
  | 'VisionTest'
  | 'BloodTest'
  | 'SkinCheck'
  | 'MusculoskeletalAssessment'
  | 'PreEmploymentMedical'
  | 'PeriodicMedical'
  | 'FitnessForWork'
  | 'HazardousSubstanceExposure';

export const SHE_HEALTH_SURVEILLANCE_TYPE_OPTIONS = opts<SheHealthSurveillanceType>([
  ['Audiometry', 'Audiometry'],
  ['LungFunctionSpirometry', 'Lung Function / Spirometry'],
  ['VisionTest', 'Vision Test'],
  ['BloodTest', 'Blood Test'],
  ['SkinCheck', 'Skin Check'],
  ['MusculoskeletalAssessment', 'Musculoskeletal Assessment'],
  ['PreEmploymentMedical', 'Pre-employment Medical'],
  ['PeriodicMedical', 'Periodic Medical'],
  ['FitnessForWork', 'Fitness for Work'],
  ['HazardousSubstanceExposure', 'Hazardous-substance Exposure'],
]);

export type SheHealthSurveillanceResult =
  | 'Normal'
  | 'ActionRequired'
  | 'Referral'
  | 'WorkRestrictionsIssued'
  | 'TemporarilyUnfit'
  | 'PermanentlyUnfit';

export const SHE_HEALTH_SURVEILLANCE_RESULT_OPTIONS = opts<SheHealthSurveillanceResult>([
  ['Normal', 'Normal'],
  ['ActionRequired', 'Action Required'],
  ['Referral', 'Referral'],
  ['WorkRestrictionsIssued', 'Work Restrictions Issued'],
  ['TemporarilyUnfit', 'Temporarily Unfit'],
  ['PermanentlyUnfit', 'Permanently Unfit'],
]);

export type SheFirstAidStationType = 'BasicKit' | 'FullKit' | 'MedicalRoom' | 'AED' | 'TraumaBag';

export const SHE_FIRST_AID_STATION_TYPE_OPTIONS = opts<SheFirstAidStationType>([
  ['BasicKit', 'Basic Kit'],
  ['FullKit', 'Full Kit'],
  ['MedicalRoom', 'Medical Room'],
  ['AED', 'AED'],
  ['TraumaBag', 'Trauma Bag'],
]);

export type SheWellnessProgramType =
  | 'HealthScreening'
  | 'MentalHealthSupport'
  | 'ErgonomicsAssessment'
  | 'Nutrition'
  | 'FitnessAndExercise'
  | 'StressManagement'
  | 'SubstanceAbuseAwareness'
  | 'HivAidsAwareness';

export const SHE_WELLNESS_PROGRAM_TYPE_OPTIONS = opts<SheWellnessProgramType>([
  ['HealthScreening', 'Health Screening'],
  ['MentalHealthSupport', 'Mental Health Support'],
  ['ErgonomicsAssessment', 'Ergonomics Assessment'],
  ['Nutrition', 'Nutrition'],
  ['FitnessAndExercise', 'Fitness & Exercise'],
  ['StressManagement', 'Stress Management'],
  ['SubstanceAbuseAwareness', 'Substance-abuse Awareness'],
  ['HivAidsAwareness', 'HIV/AIDS Awareness'],
]);

export type SheWellnessProgramStatus = 'Planned' | 'Active' | 'Completed' | 'Cancelled';

export const SHE_WELLNESS_PROGRAM_STATUS_OPTIONS = opts<SheWellnessProgramStatus>([
  ['Planned', 'Planned'],
  ['Active', 'Active'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

export type SheReturnToWorkStatus =
  | 'PendingMedicalClearance'
  | 'Active'
  | 'OnHold'
  | 'Completed'
  | 'Discontinued';

export const SHE_RETURN_TO_WORK_STATUS_OPTIONS = opts<SheReturnToWorkStatus>([
  ['PendingMedicalClearance', 'Pending Medical Clearance'],
  ['Active', 'Active'],
  ['OnHold', 'On Hold'],
  ['Completed', 'Completed'],
  ['Discontinued', 'Discontinued'],
]);

// ── Health surveillance ───────────────────────────────────────────────────────

export interface SheHealthSurveillanceSummary {
  id: string;
  surveillanceNumber: string;
  employeeName: string;
  type: SheHealthSurveillanceType;
  typeName: string;
  examinationDate: string;
  nextExaminationDate?: string | null;
  result: SheHealthSurveillanceResult;
  resultName: string;
  workRestrictionIssued: boolean;
}

export interface SheHealthSurveillance extends AuditFields {
  tenantId: string;
  surveillanceNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  type: SheHealthSurveillanceType;
  typeName: string;
  exposureHazard?: string | null;
  examinationDate: string;
  nextExaminationDate?: string | null;
  /** Bridge into the Medical module's facility register — reference only. */
  healthcareFacilityId?: string | null;
  healthcareFacilityName?: string | null;
  examiningPhysician?: string | null;
  result: SheHealthSurveillanceResult;
  resultName: string;
  findings?: string | null;
  recommendations?: string | null;
  workRestrictionIssued: boolean;
  workRestrictionDetails?: string | null;
  documentPath?: string | null;
  recordedById: string;
  recordedByName: string;
}

export interface SheHealthSurveillanceCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  surveillanceNumber: string;
  employeeId: string;
  type: SheHealthSurveillanceType;
  exposureHazard?: string | null;
  examinationDate: string;
  nextExaminationDate?: string | null;
  healthcareFacilityId?: string | null;
  examiningPhysician?: string | null;
  result: SheHealthSurveillanceResult;
  findings?: string | null;
  recommendations?: string | null;
  workRestrictionIssued: boolean;
  workRestrictionDetails?: string | null;
  documentPath?: string | null;
  recordedById: string;
}

/** The employee and recorder are fixed once recorded. */
export interface SheHealthSurveillanceUpdateRequest {
  id: string;
  type: SheHealthSurveillanceType;
  exposureHazard?: string | null;
  examinationDate: string;
  nextExaminationDate?: string | null;
  healthcareFacilityId?: string | null;
  examiningPhysician?: string | null;
  result: SheHealthSurveillanceResult;
  findings?: string | null;
  recommendations?: string | null;
  workRestrictionIssued: boolean;
  workRestrictionDetails?: string | null;
  documentPath?: string | null;
}

// ── First-aid stations ────────────────────────────────────────────────────────

export interface SheFirstAidStation extends AuditFields {
  tenantId: string;
  stationCode: string;
  name: string;
  locationId: string;
  locationName: string;
  specificArea?: string | null;
  type: SheFirstAidStationType;
  typeName: string;
  responsibleAiderId?: string | null;
  responsibleAiderName?: string | null;
  lastInspectionDate?: string | null;
  nextInspectionDate?: string | null;
  isFullyStocked: boolean;
  stockingDeficiencies?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface SheFirstAidStationCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  stationCode: string;
  name: string;
  locationId: string;
  specificArea?: string | null;
  type: SheFirstAidStationType;
  responsibleAiderId?: string | null;
  isFullyStocked: boolean;
  isActive: boolean;
  notes?: string | null;
}

export interface SheFirstAidStationUpdateRequest {
  id: string;
  name: string;
  locationId: string;
  specificArea?: string | null;
  type: SheFirstAidStationType;
  responsibleAiderId?: string | null;
  lastInspectionDate?: string | null;
  nextInspectionDate?: string | null;
  isFullyStocked: boolean;
  stockingDeficiencies?: string | null;
  isActive: boolean;
  notes?: string | null;
}

// ── Wellness programs ─────────────────────────────────────────────────────────

export interface SheWellnessProgram extends AuditFields {
  tenantId: string;
  programCode: string;
  title: string;
  description?: string | null;
  type: SheWellnessProgramType;
  typeName: string;
  startDate: string;
  endDate?: string | null;
  coordinatorId?: string | null;
  coordinatorName?: string | null;
  status: SheWellnessProgramStatus;
  statusName: string;
  participantsCount?: number | null;
  outcomes?: string | null;
  isActive: boolean;
}

export interface SheWellnessProgramCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  programCode: string;
  title: string;
  description?: string | null;
  type: SheWellnessProgramType;
  startDate: string;
  endDate?: string | null;
  coordinatorId?: string | null;
  isActive: boolean;
}

export interface SheWellnessProgramUpdateRequest {
  id: string;
  title: string;
  description?: string | null;
  type: SheWellnessProgramType;
  startDate: string;
  endDate?: string | null;
  coordinatorId?: string | null;
  status: SheWellnessProgramStatus;
  participantsCount?: number | null;
  outcomes?: string | null;
  isActive: boolean;
}

// ── Return-to-work plans ──────────────────────────────────────────────────────

export interface SheReturnToWorkPlanSummary {
  id: string;
  planNumber: string;
  employeeName: string;
  safetyIncidentNumber?: string | null;
  planDate: string;
  plannedReturnDate?: string | null;
  actualReturnDate?: string | null;
  status: SheReturnToWorkStatus;
  statusName: string;
  successfullyCompleted: boolean;
}

export interface SheReturnToWorkPlan extends AuditFields {
  tenantId: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  safetyIncidentId?: string | null;
  safetyIncidentNumber?: string | null;
  planNumber: string;
  planDate: string;
  plannedReturnDate?: string | null;
  actualReturnDate?: string | null;
  medicalRestrictions?: string | null;
  medicalClearanceDate?: string | null;
  medicalClearanceNotes?: string | null;
  requiresWorkplaceModifications: boolean;
  workplaceModificationsDescription?: string | null;
  status: SheReturnToWorkStatus;
  statusName: string;
  coordinatorId?: string | null;
  coordinatorName?: string | null;
  supervisorId?: string | null;
  supervisorName?: string | null;
  completionDate?: string | null;
  successfullyCompleted: boolean;
  completionNotes?: string | null;
  phases: SheReturnToWorkPhase[];
  reviews: SheReturnToWorkReview[];
}

export interface SheReturnToWorkPlanCreateRequest {
  employeeId: string;
  safetyIncidentId?: string | null;
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  planNumber: string;
  planDate: string;
  plannedReturnDate?: string | null;
  medicalRestrictions?: string | null;
  medicalClearanceDate?: string | null;
  medicalClearanceNotes?: string | null;
  requiresWorkplaceModifications: boolean;
  workplaceModificationsDescription?: string | null;
  coordinatorId?: string | null;
  supervisorId?: string | null;
}

/** The employee is fixed once the plan exists. */
export interface SheReturnToWorkPlanUpdateRequest {
  id: string;
  safetyIncidentId?: string | null;
  planDate: string;
  plannedReturnDate?: string | null;
  actualReturnDate?: string | null;
  medicalRestrictions?: string | null;
  medicalClearanceDate?: string | null;
  medicalClearanceNotes?: string | null;
  requiresWorkplaceModifications: boolean;
  workplaceModificationsDescription?: string | null;
  status: SheReturnToWorkStatus;
  coordinatorId?: string | null;
  supervisorId?: string | null;
  completionDate?: string | null;
  successfullyCompleted: boolean;
  completionNotes?: string | null;
}

// ── Phases (numbers are server-assigned) ──────────────────────────────────────

export interface SheReturnToWorkPhase extends AuditFields {
  returnToWorkPlanId: string;
  phaseName: string;
  phaseNumber: number;
  startDate: string;
  endDate?: string | null;
  requiresReducedHours: boolean;
  hoursPerDay?: number | null;
  daysPerWeek?: number | null;
  duties: string;
  restrictions: string;
  assessmentDate: string;
  assessedById: string;
  assessedByName: string;
  employeeProgress?: string | null;
  challengesFaced?: string | null;
  accommodationsEffectiveness?: string | null;
  recommendedAdjustments?: string | null;
  employeeFeedback?: string | null;
  phaseCompleted: boolean;
  actualEndDate?: string | null;
  canContinuePlan: boolean;
  completionNotes?: string | null;
}

export interface SheReturnToWorkPhaseCreateRequest {
  returnToWorkPlanId: string;
  phaseName: string;
  startDate: string;
  endDate?: string | null;
  requiresReducedHours: boolean;
  hoursPerDay?: number | null;
  daysPerWeek?: number | null;
  duties: string;
  restrictions: string;
  assessmentDate: string;
  assessedById: string;
}

/** The phase number and assessor are fixed; the debrief fields are captured on edit. */
export interface SheReturnToWorkPhaseUpdateRequest {
  id: string;
  phaseName: string;
  startDate: string;
  endDate?: string | null;
  requiresReducedHours: boolean;
  hoursPerDay?: number | null;
  daysPerWeek?: number | null;
  duties: string;
  restrictions: string;
  assessmentDate: string;
  employeeProgress?: string | null;
  challengesFaced?: string | null;
  accommodationsEffectiveness?: string | null;
  recommendedAdjustments?: string | null;
  employeeFeedback?: string | null;
  phaseCompleted: boolean;
  actualEndDate?: string | null;
  canContinuePlan: boolean;
  completionNotes?: string | null;
}

// ── Reviews (add-only; numbers are server-assigned) ───────────────────────────

export interface SheReturnToWorkReview extends AuditFields {
  returnToWorkPlanId: string;
  reviewDate: string;
  reviewNumber: number;
  employeeCondition?: string | null;
  workProgress?: string | null;
  issuesIdentified?: string | null;
  recommendedActions?: string | null;
  reviewedById: string;
  reviewedByName: string;
  nextReviewDate?: string | null;
}

export interface SheReturnToWorkReviewCreateRequest {
  returnToWorkPlanId: string;
  reviewDate: string;
  employeeCondition?: string | null;
  workProgress?: string | null;
  issuesIdentified?: string | null;
  recommendedActions?: string | null;
  reviewedById: string;
  nextReviewDate?: string | null;
}
