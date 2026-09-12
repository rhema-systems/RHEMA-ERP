// Types for HR Area 10 (SHE — Safety, Health & Environment), slice 1: the reference/lookup
// catalogue, the dashboard tile read, and hand-reported performance snapshots.
// Mirrors ErpSystem.Core.DTOs.HR.{StaffSafetyDTOs (region A), SafetyDashboardDTOs,
// SafetyEmergencyGovernanceDTOs (region P)}. Backend routes: api/safety/{reference,dashboard,
// performance}. Later slices add their own safety-*.ts files per sub-area.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheIncidentCategory =
  | 'Accident'
  | 'NearMiss'
  | 'DangerousOccurrence'
  | 'OccupationalIllness'
  | 'EnvironmentalIncident'
  | 'PropertyDamage'
  | 'SecurityIncident'
  | 'FireIncident';

export const SHE_INCIDENT_CATEGORY_OPTIONS = opts<SheIncidentCategory>([
  ['Accident', 'Accident'],
  ['NearMiss', 'Near Miss'],
  ['DangerousOccurrence', 'Dangerous Occurrence'],
  ['OccupationalIllness', 'Occupational Illness'],
  ['EnvironmentalIncident', 'Environmental Incident'],
  ['PropertyDamage', 'Property Damage'],
  ['SecurityIncident', 'Security Incident'],
  ['FireIncident', 'Fire Incident'],
]);

export type SheCorrectiveActionCategory =
  | 'Engineering'
  | 'Administrative'
  | 'Behavioural'
  | 'PPE'
  | 'Maintenance'
  | 'Training'
  | 'PolicyProcedure'
  | 'Environmental'
  | 'Other';

export const SHE_CORRECTIVE_ACTION_CATEGORY_OPTIONS = opts<SheCorrectiveActionCategory>([
  ['Engineering', 'Engineering'],
  ['Administrative', 'Administrative'],
  ['Behavioural', 'Behavioural'],
  ['PPE', 'PPE'],
  ['Maintenance', 'Maintenance'],
  ['Training', 'Training'],
  ['PolicyProcedure', 'Policy / Procedure'],
  ['Environmental', 'Environmental'],
  ['Other', 'Other'],
]);

export type SheRegulatoryDomain =
  | 'OccupationalHealth'
  | 'OccupationalSafety'
  | 'EnvironmentalProtection'
  | 'FireSafety'
  | 'ChemicalControl'
  | 'Construction'
  | 'Labour'
  | 'PublicHealth';

export const SHE_REGULATORY_DOMAIN_OPTIONS = opts<SheRegulatoryDomain>([
  ['OccupationalHealth', 'Occupational Health'],
  ['OccupationalSafety', 'Occupational Safety'],
  ['EnvironmentalProtection', 'Environmental Protection'],
  ['FireSafety', 'Fire Safety'],
  ['ChemicalControl', 'Chemical Control'],
  ['Construction', 'Construction'],
  ['Labour', 'Labour'],
  ['PublicHealth', 'Public Health'],
]);

export type SheSnapshotPeriodType = 'Monthly' | 'Quarterly' | 'Annual';

export const SHE_SNAPSHOT_PERIOD_TYPE_OPTIONS = opts<SheSnapshotPeriodType>([
  ['Monthly', 'Monthly'],
  ['Quarterly', 'Quarterly'],
  ['Annual', 'Annual'],
]);

// ── Reference catalogue ───────────────────────────────────────────────────────

export interface SheIncidentTypeCorrectiveAction extends AuditFields {
  incidentTypeId: string;
  correctiveActionTemplateId: string;
  correctiveActionTemplateCode: string;
  correctiveActionTemplateTitle: string;
  displayOrder: number;
  deadlineDays?: number | null;
  isMandatory: boolean;
}

export interface SheIncidentType extends AuditFields {
  tenantId: string;
  code: string;
  name: string;
  description?: string | null;
  category: SheIncidentCategory;
  categoryName: string;
  isReportable: boolean;
  regulatoryBodyId?: string | null;
  regulatoryBodyName?: string | null;
  reportingWindowHours?: number | null;
  isActive: boolean;
  defaultCorrectiveActions: SheIncidentTypeCorrectiveAction[];
}

export interface SheIncidentTypeCorrectiveActionCreateRequest {
  incidentTypeId: string;
  correctiveActionTemplateId: string;
  displayOrder: number;
  deadlineDays?: number | null;
  isMandatory: boolean;
}

export interface SheIncidentTypeCorrectiveActionUpdateRequest {
  id: string;
  displayOrder: number;
  deadlineDays?: number | null;
  isMandatory: boolean;
}

export interface SheIncidentTypeCreateRequest {
  code: string;
  name: string;
  description?: string | null;
  category: SheIncidentCategory;
  isReportable: boolean;
  regulatoryBodyId?: string | null;
  reportingWindowHours?: number | null;
  isActive: boolean;
}

/** Code is immutable after creation — it is not on the update contract. */
export interface SheIncidentTypeUpdateRequest {
  id: string;
  name: string;
  description?: string | null;
  category: SheIncidentCategory;
  isReportable: boolean;
  regulatoryBodyId?: string | null;
  reportingWindowHours?: number | null;
  isActive: boolean;
}

export interface SheInjuryType extends AuditFields {
  tenantId: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface SheInjuryTypeCreateRequest {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface SheInjuryTypeUpdateRequest {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface SheBodyPart extends AuditFields {
  tenantId: string;
  code: string;
  name: string;
  region?: string | null;
  isActive: boolean;
}

export interface SheBodyPartCreateRequest {
  code: string;
  name: string;
  region?: string | null;
  isActive: boolean;
}

export interface SheBodyPartUpdateRequest {
  id: string;
  name: string;
  region?: string | null;
  isActive: boolean;
}

export interface SheCorrectiveActionTemplate extends AuditFields {
  tenantId: string;
  code: string;
  title: string;
  description: string;
  category: SheCorrectiveActionCategory;
  categoryName: string;
  defaultDeadlineDays?: number | null;
  isActive: boolean;
}

export interface SheCorrectiveActionTemplateCreateRequest {
  code: string;
  title: string;
  description: string;
  category: SheCorrectiveActionCategory;
  defaultDeadlineDays?: number | null;
  isActive: boolean;
}

export interface SheCorrectiveActionTemplateUpdateRequest {
  id: string;
  title: string;
  description: string;
  category: SheCorrectiveActionCategory;
  defaultDeadlineDays?: number | null;
  isActive: boolean;
}

export interface SheRegulatoryBody extends AuditFields {
  tenantId: string;
  name: string;
  shortName?: string | null;
  contactAddress?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  domain: SheRegulatoryDomain;
  domainName: string;
  isActive: boolean;
}

export interface SheRegulatoryBodyCreateRequest {
  name: string;
  shortName?: string | null;
  contactAddress?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  domain: SheRegulatoryDomain;
  isActive: boolean;
}

export interface SheRegulatoryBodyUpdateRequest extends SheRegulatoryBodyCreateRequest {
  id: string;
}

// ── Dashboard ────────────────────────────────────────────────────────────────

/** Raw operational counts, computed server-side per tenant. All 17 are live counts of the
 * underlying registers — none of them are the hand-reported snapshot figures. */
export interface SheDashboard {
  openIncidents: number;
  lostTimeInjuries: number;
  incidentsRequiringInvestigation: number;
  overdueCorrectiveActions: number;
  hazardsDueForReview: number;
  highRiskHazards: number;
  riskAssessmentsExpiring: number;
  riskAssessmentsDueForReview: number;
  inspectionsDue: number;
  openInspectionFindings: number;
  activePermits: number;
  expiringPermits: number;
  suspendedPermits: number;
  equipmentDueForInspection: number;
  equipmentExpiringCertification: number;
  equipmentOutOfService: number;
  ppeBelowReorder: number;
  // Environment (slice 17 — Part D core)
  /** FR-ENV-019 — rendered red. */
  expiredEnvironmentalPermits: number;
  environmentalPermitsExpiringSoon: number;
  openEnvironmentalIncidents: number;
  monitoringSchedulesDue: number;
  regulatoryUpdatesOpen: number;
  sustainabilityInitiativesActive: number;
}

// ── Performance snapshots ─────────────────────────────────────────────────────
// Figures are hand-reported at creation, and can be recomputed from the live registers by the
// KPI engine (slice 14): POST /{id}/compute rewrites every derivable figure and stamps
// kpisComputedAt/By. Hand-entered-only inputs (man-hours, inspections planned, drills planned)
// are never overwritten. A snapshot with kpisComputedAt == null is reported-only.

export interface ShePerformanceSnapshot extends AuditFields {
  tenantId: string;
  snapshotNumber: string;
  periodType: SheSnapshotPeriodType;
  periodTypeName: string;
  year: number;
  periodNumber?: number | null;
  locationId?: string | null;
  locationName?: string | null;

  totalAccidents: number;
  totalIncidents: number;
  totalNearMisses: number;
  totalDangerousOccurrences: number;
  totalFatalities: number;
  totalLostTimeInjuries: number;

  lostTimeInjuryFrequencyRate?: number | null;
  totalManHoursWorked: number;
  totalLostDays: number;

  inspectionsPlanned: number;
  inspectionsConducted: number;
  inspectionsOverdue: number;

  correctiveActionsIssued: number;
  correctiveActionsCompleted: number;
  correctiveActionsOverdue: number;
  correctiveActionClosureRate?: number | null;

  trainingProgramsPlanned: number;
  trainingProgramsConducted: number;
  totalTrainingHours: number;

  contractorsOnSite: number;
  contractorInspectionsConducted: number;
  contractorNonComplianceNoticesIssued: number;
  contractorComplianceRate?: number | null;

  environmentalIncidents: number;
  environmentalIncidentsReportedToEpa: number;

  emergencyDrillsPlanned: number;
  emergencyDrillsConducted: number;

  ppeComplianceRate?: number | null;
  housekeepingComplianceRating?: number | null;

  regulatoryObligationsTotal: number;
  regulatoryObligationsCompliant: number;
  regulatoryObligationsNonCompliant: number;
  regulatoryObligationsExpiringSoon: number;

  preparedById: string;
  preparedByName: string;
  preparedDate: string;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedDate?: string | null;
  managementComments?: string | null;
  reportDocumentPath?: string | null;

  // Computed KPIs (slice 14) — written only by POST /{id}/compute, never hand-entered.
  totalRecordableIncidentRate?: number | null;
  nearMissFrequencyRate?: number | null;
  trainingCompletionRate?: number | null;
  fireDrillObjectivesMetRate?: number | null;
  wasteRecyclingRate?: number | null;
  averageInspectionComplianceScore?: number | null;
  kpisComputedAt?: string | null;
  kpisComputedById?: string | null;
  kpisComputedByName?: string | null;
}

export interface ShePerformanceSnapshotSummary {
  id: string;
  snapshotNumber: string;
  periodType: SheSnapshotPeriodType;
  periodTypeName: string;
  year: number;
  periodNumber?: number | null;
  locationName?: string | null;
  totalIncidents: number;
  totalLostTimeInjuries: number;
  lostTimeInjuryFrequencyRate?: number | null;
  preparedDate: string;
}

/** The reported figures, shared by create and update. */
export interface ShePerformanceSnapshotFigures {
  totalAccidents: number;
  totalIncidents: number;
  totalNearMisses: number;
  totalDangerousOccurrences: number;
  totalFatalities: number;
  totalLostTimeInjuries: number;
  lostTimeInjuryFrequencyRate?: number | null;
  totalManHoursWorked: number;
  totalLostDays: number;
  inspectionsPlanned: number;
  inspectionsConducted: number;
  inspectionsOverdue: number;
  correctiveActionsIssued: number;
  correctiveActionsCompleted: number;
  correctiveActionsOverdue: number;
  correctiveActionClosureRate?: number | null;
  trainingProgramsPlanned: number;
  trainingProgramsConducted: number;
  totalTrainingHours: number;
  contractorsOnSite: number;
  contractorInspectionsConducted: number;
  contractorNonComplianceNoticesIssued: number;
  contractorComplianceRate?: number | null;
  environmentalIncidents: number;
  environmentalIncidentsReportedToEpa: number;
  emergencyDrillsPlanned: number;
  emergencyDrillsConducted: number;
  ppeComplianceRate?: number | null;
  housekeepingComplianceRating?: number | null;
  regulatoryObligationsTotal: number;
  regulatoryObligationsCompliant: number;
  regulatoryObligationsNonCompliant: number;
  regulatoryObligationsExpiringSoon: number;
}

/** The server refuses (422) a duplicate snapshot number, and a second snapshot for the same
 * period + location. */
export interface ShePerformanceSnapshotCreateRequest extends ShePerformanceSnapshotFigures {
  snapshotNumber: string;
  periodType: SheSnapshotPeriodType;
  year: number;
  periodNumber?: number | null;
  locationId?: string | null;
  preparedById: string;
  preparedDate?: string;
  managementComments?: string | null;
  reportDocumentPath?: string | null;
}

/** Figures only — identity fields are fixed at creation. Refused (422) once reviewed. */
export interface ShePerformanceSnapshotUpdateRequest extends ShePerformanceSnapshotFigures {
  id: string;
  managementComments?: string | null;
  reportDocumentPath?: string | null;
}

export interface ShePerformanceSnapshotReviewRequest {
  snapshotId: string;
  reviewedById: string;
  reviewedDate?: string;
  managementComments?: string | null;
}
