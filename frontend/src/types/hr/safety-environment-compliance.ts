// Types for HR Area 10 (SHE) slice 17 — Part D environmental core.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyEnvironmentalComplianceDTOs.
// Backend routes: api/safety/environmental/{permits|monitoring/schedules|
// regulatory-updates|sustainability|reviews|monthly-reports}. HR-only except
// employee environmental-incident reporting (which lives in safety-environment.ts).

import type { SheControlledDocumentVersion } from './safety-documents';
import type { SheEnvironmentalMonitoringType } from './safety-environment';

const opts = <T extends string>(values: readonly T[]): { value: T; label: string }[] =>
  values.map((value) => ({
    value,
    label: value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/([A-Z])([A-Z][a-z])/g, '$1 $2'),
  }));

// ── Permit & licence register (FR-ENV-017–019) ─────────────────────────────

export type SheEnvironmentalPermitType =
  | 'EnvironmentalPermit'
  | 'EpaRegistration'
  | 'OperatingLicence'
  | 'Certificate'
  | 'Consent'
  | 'Other';

export const SHE_ENV_PERMIT_TYPE_OPTIONS = opts<SheEnvironmentalPermitType>([
  'EnvironmentalPermit', 'EpaRegistration', 'OperatingLicence', 'Certificate', 'Consent', 'Other',
]);

export type SheEnvironmentalPermitStatus =
  | 'Active'
  | 'RenewalInProgress'
  | 'Expired'
  | 'Suspended'
  | 'Archived';

export const SHE_ENV_PERMIT_STATUS_OPTIONS = opts<SheEnvironmentalPermitStatus>([
  'Active', 'RenewalInProgress', 'Expired', 'Suspended', 'Archived',
]);

export interface SheEnvironmentalPermitSummary {
  id: string;
  registerNumber: string;
  permitName: string;
  permitType: SheEnvironmentalPermitType;
  permitTypeName: string;
  authorityReferenceNumber?: string | null;
  issuingBodyName?: string | null;
  responsibleOfficerName: string;
  issueDate: string;
  expiryDate: string;
  status: SheEnvironmentalPermitStatus;
  statusName: string;
  currentVersionLabel?: string | null;
  /** Negative once past expiry — drives the FR-ENV-019 red status. */
  daysToExpiry: number;
}

export interface SheEnvironmentalPermit {
  id: string;
  registerNumber: string;
  permitName: string;
  permitType: SheEnvironmentalPermitType;
  permitTypeName: string;
  authorityReferenceNumber?: string | null;
  issuingBodyId?: string | null;
  issuingBodyName?: string | null;
  responsibleOfficerId: string;
  responsibleOfficerName: string;
  locationId?: string | null;
  locationName?: string | null;
  description?: string | null;
  conditions?: string | null;
  issueDate: string;
  expiryDate: string;
  renewalPeriodMonths?: number | null;
  status: SheEnvironmentalPermitStatus;
  statusName: string;
  documentRecordId?: string | null;
  currentVersionLabel?: string | null;
  lastRenewedDate?: string | null;
  lastRenewedByName?: string | null;
  notes?: string | null;
  versions: SheControlledDocumentVersion[];
}

export interface SheEnvironmentalPermitCreateRequest {
  registerNumber?: string | null;
  permitName: string;
  permitType: SheEnvironmentalPermitType;
  authorityReferenceNumber?: string | null;
  issuingBodyId?: string | null;
  responsibleOfficerId: string;
  locationId?: string | null;
  description?: string | null;
  conditions?: string | null;
  issueDate: string;
  expiryDate: string;
  renewalPeriodMonths?: number | null;
  notes?: string | null;
}

export interface SheEnvironmentalPermitUpdateRequest
  extends Omit<SheEnvironmentalPermitCreateRequest, 'registerNumber' | 'issueDate' | 'expiryDate'> {
  id: string;
}

export interface SheEnvironmentalPermitRenewRequest {
  newIssueDate: string;
  newExpiryDate: string;
  newAuthorityReferenceNumber?: string | null;
  notes?: string | null;
}

// ── Monitoring schedules (FR-ENV-023–024) ──────────────────────────────────

export interface SheEnvironmentalMonitoringSchedule {
  id: string;
  scheduleNumber: string;
  monitoringType: SheEnvironmentalMonitoringType;
  monitoringTypeName: string;
  locationId?: string | null;
  locationName?: string | null;
  monitoringPoint?: string | null;
  description?: string | null;
  frequencyDays: number;
  nextDueDate: string;
  lastPerformedDate?: string | null;
  responsibleOfficerId?: string | null;
  responsibleOfficerName?: string | null;
  isActive: boolean;
  /** Negative once the cycle is overdue. */
  daysToDue: number;
}

export interface SheEnvironmentalMonitoringScheduleCreateRequest {
  scheduleNumber?: string | null;
  monitoringType: SheEnvironmentalMonitoringType;
  locationId?: string | null;
  monitoringPoint?: string | null;
  description?: string | null;
  frequencyDays: number;
  nextDueDate: string;
  responsibleOfficerId?: string | null;
  isActive: boolean;
}

export interface SheEnvironmentalMonitoringScheduleUpdateRequest
  extends Omit<SheEnvironmentalMonitoringScheduleCreateRequest, 'scheduleNumber'> {
  id: string;
}

export interface SheMonitoringScheduleCompleteRequest {
  performedDate: string;
  /** Optional — the monitoring record holding the readings/evidence. */
  monitoringRecordId?: string | null;
}

// ── Regulatory updates register (FR-ENV-030–032 / FR-SHE-182) ──────────────

export type SheRegulatoryUpdateRiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';

export const SHE_REG_UPDATE_RISK_OPTIONS = opts<SheRegulatoryUpdateRiskLevel>([
  'Low', 'Medium', 'High', 'Critical',
]);

export type SheRegulatoryUpdateStatus = 'Recorded' | 'ActionsInProgress' | 'Closed';

export const SHE_REG_UPDATE_STATUS_OPTIONS = opts<SheRegulatoryUpdateStatus>([
  'Recorded', 'ActionsInProgress', 'Closed',
]);

export interface SheRegulatoryUpdateSummary {
  id: string;
  updateNumber: string;
  title: string;
  regulationReference?: string | null;
  authorityName?: string | null;
  domain: string;
  domainName: string;
  issueDate: string;
  complianceDeadline?: string | null;
  riskLevel: SheRegulatoryUpdateRiskLevel;
  riskLevelName: string;
  status: SheRegulatoryUpdateStatus;
  statusName: string;
  complianceStatus: string;
  complianceStatusName: string;
  managementNotified: boolean;
}

export interface SheRegulatoryUpdate {
  id: string;
  updateNumber: string;
  title: string;
  regulationReference?: string | null;
  regulatoryBodyId?: string | null;
  regulatoryBodyName?: string | null;
  authorityName?: string | null;
  domain: string;
  domainName: string;
  summary: string;
  issueDate: string;
  effectiveDate?: string | null;
  affectedDepartments?: string | null;
  complianceDeadline?: string | null;
  riskLevel: SheRegulatoryUpdateRiskLevel;
  riskLevelName: string;
  requiredActions?: string | null;
  status: SheRegulatoryUpdateStatus;
  statusName: string;
  complianceStatus: string;
  complianceStatusName: string;
  reviewDate?: string | null;
  officerComments?: string | null;
  linkedObligationId?: string | null;
  linkedObligationCode?: string | null;
  managementNotifiedAt?: string | null;
  managementNotifiedByName?: string | null;
  recordedById: string;
  recordedByName: string;
  closedAt?: string | null;
  closedByName?: string | null;
  closureNotes?: string | null;
}

export interface SheRegulatoryUpdateCreateRequest {
  updateNumber?: string | null;
  title: string;
  regulationReference?: string | null;
  regulatoryBodyId?: string | null;
  authorityName?: string | null;
  domain: string;
  summary: string;
  issueDate: string;
  effectiveDate?: string | null;
  affectedDepartments?: string | null;
  complianceDeadline?: string | null;
  riskLevel: SheRegulatoryUpdateRiskLevel;
  requiredActions?: string | null;
  reviewDate?: string | null;
  officerComments?: string | null;
  linkedObligationId?: string | null;
}

export interface SheRegulatoryUpdateUpdateRequest
  extends Omit<SheRegulatoryUpdateCreateRequest, 'updateNumber'> {
  id: string;
  status: SheRegulatoryUpdateStatus;
  complianceStatus: string;
}

export interface SheRegulatoryUpdateCloseRequest {
  complianceStatus: string;
  closureNotes?: string | null;
}

// ── Sustainability initiatives (FR-ENV-028–029) ────────────────────────────

export type SheSustainabilityCategory =
  | 'EnergySavings'
  | 'WaterSavings'
  | 'PaperReduction'
  | 'TreePlanting'
  | 'Recycling'
  | 'WasteRecycled'
  | 'CarbonReduction'
  | 'CostSavings'
  | 'Other';

export const SHE_SUSTAINABILITY_CATEGORY_OPTIONS = opts<SheSustainabilityCategory>([
  'EnergySavings', 'WaterSavings', 'PaperReduction', 'TreePlanting',
  'Recycling', 'WasteRecycled', 'CarbonReduction', 'CostSavings', 'Other',
]);

export type SheSustainabilityStatus = 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';

export const SHE_SUSTAINABILITY_STATUS_OPTIONS = opts<SheSustainabilityStatus>([
  'Planned', 'InProgress', 'Completed', 'Cancelled',
]);

export interface SheSustainabilityInitiative {
  id: string;
  initiativeNumber: string;
  title: string;
  category: SheSustainabilityCategory;
  categoryName: string;
  description?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  ownerId?: string | null;
  ownerName?: string | null;
  startDate: string;
  endDate?: string | null;
  status: SheSustainabilityStatus;
  statusName: string;
  targetValue?: number | null;
  actualValue?: number | null;
  measurementUnit?: string | null;
  estimatedCostSavings?: number | null;
  notes?: string | null;
}

export interface SheSustainabilityInitiativeCreateRequest {
  initiativeNumber?: string | null;
  title: string;
  category: SheSustainabilityCategory;
  description?: string | null;
  locationId?: string | null;
  ownerId?: string | null;
  startDate: string;
  endDate?: string | null;
  status: SheSustainabilityStatus;
  targetValue?: number | null;
  actualValue?: number | null;
  measurementUnit?: string | null;
  estimatedCostSavings?: number | null;
  notes?: string | null;
}

export interface SheSustainabilityInitiativeUpdateRequest
  extends Omit<SheSustainabilityInitiativeCreateRequest, 'initiativeNumber'> {
  id: string;
}

export interface SheSustainabilityCategorySummary {
  category: SheSustainabilityCategory;
  categoryName: string;
  initiatives: number;
  completed: number;
  targetTotal: number;
  actualTotal: number;
  costSavings: number;
}

export interface SheSustainabilityKpis {
  year?: number | null;
  activeInitiatives: number;
  completedInitiatives: number;
  totalCostSavings: number;
  categories: SheSustainabilityCategorySummary[];
}

// ── Compliance reviews & clearance (FR-ENV-001–016) ────────────────────────

export type SheEnvironmentalWorkClassification =
  | 'PlannedProject'
  | 'Upgrade'
  | 'Maintenance'
  | 'Construction'
  | 'Demolition'
  | 'InfrastructureModification'
  | 'Other';

export const SHE_ENV_WORK_CLASSIFICATION_OPTIONS = opts<SheEnvironmentalWorkClassification>([
  'PlannedProject', 'Upgrade', 'Maintenance', 'Construction', 'Demolition',
  'InfrastructureModification', 'Other',
]);

export type SheEnvironmentalReviewStatus =
  | 'Submitted'
  | 'CorrectionsRequested'
  | 'Approved'
  | 'Rejected'
  | 'ClearanceIssued';

export const SHE_ENV_REVIEW_STATUS_OPTIONS = opts<SheEnvironmentalReviewStatus>([
  'Submitted', 'CorrectionsRequested', 'Approved', 'Rejected', 'ClearanceIssued',
]);

export interface SheEnvironmentalReviewAction {
  id: string;
  action: string;
  notes?: string | null;
  actorId: string;
  actorName: string;
  actionDate: string;
}

export interface SheEnvironmentalReviewSummary {
  id: string;
  reviewNumber: string;
  projectName: string;
  workClassification: SheEnvironmentalWorkClassification;
  workClassificationName: string;
  organizationUnitName?: string | null;
  submittedDate: string;
  plannedStartDate?: string | null;
  status: SheEnvironmentalReviewStatus;
  statusName: string;
  permitRequired: boolean;
  requiresManagementApproval: boolean;
  clearanceIssued: boolean;
}

export interface SheEnvironmentalReview {
  id: string;
  reviewNumber: string;
  projectName: string;
  workClassification: SheEnvironmentalWorkClassification;
  workClassificationName: string;
  projectReference?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  responsibleManagerId?: string | null;
  responsibleManagerName?: string | null;
  submittedById: string;
  submittedByName: string;
  submittedDate: string;
  plannedStartDate?: string | null;
  description: string;
  applicableLaws?: string | null;
  permitRequired: boolean;
  complianceChecklist?: string | null;
  requiresRegistration: boolean;
  requiresEnvironmentalPermit: boolean;
  requiresFullEia: boolean;
  requiresRiskAssessment: boolean;
  requiresEpaSubmission: boolean;
  requiresManagementApproval: boolean;
  screeningNotes?: string | null;
  screeningCompletedDate?: string | null;
  screenedByName?: string | null;
  status: SheEnvironmentalReviewStatus;
  statusName: string;
  officerComments?: string | null;
  approvedDate?: string | null;
  approvedByName?: string | null;
  managementApprovedDate?: string | null;
  managementApprovedByName?: string | null;
  epaSubmissionDate?: string | null;
  epaSubmissionReference?: string | null;
  clearanceIssuedDate?: string | null;
  clearanceIssuedByName?: string | null;
  commencementApprovedDate?: string | null;
  commencementApprovedByName?: string | null;
  notes?: string | null;
  /** The FR-ENV-011 audit trail, newest first. */
  actions: SheEnvironmentalReviewAction[];
}

export interface SheEnvironmentalReviewCreateRequest {
  reviewNumber?: string | null;
  projectName: string;
  workClassification: SheEnvironmentalWorkClassification;
  projectReference?: string | null;
  organizationUnitId?: string | null;
  responsibleManagerId?: string | null;
  plannedStartDate?: string | null;
  description: string;
  applicableLaws?: string | null;
  permitRequired: boolean;
  complianceChecklist?: string | null;
  notes?: string | null;
}

export interface SheEnvironmentalReviewUpdateRequest
  extends Omit<SheEnvironmentalReviewCreateRequest, 'reviewNumber'> {
  id: string;
}

export interface SheEnvironmentalScreeningRequest {
  requiresRegistration: boolean;
  requiresEnvironmentalPermit: boolean;
  requiresFullEia: boolean;
  requiresRiskAssessment: boolean;
  requiresEpaSubmission: boolean;
  requiresManagementApproval: boolean;
  screeningNotes?: string | null;
}

export interface SheEpaSubmissionRequest {
  submissionDate: string;
  referenceNumber?: string | null;
  notes?: string | null;
}

export interface SheEnvironmentalClearanceReport {
  review: SheEnvironmentalReview;
  generatedAt: string;
}

// ── Monthly environmental reports (FR-ENV-033–034) ─────────────────────────

export interface SheMonthlyEnvironmentalReportSummary {
  id: string;
  reportNumber: string;
  year: number;
  month: number;
  generatedAt: string;
  compliancePercentage?: number | null;
  permitsExpired: number;
  environmentalIncidents: number;
  submittedToManagementAt?: string | null;
}

export interface SheMonthlyEnvironmentalReport {
  id: string;
  reportNumber: string;
  year: number;
  month: number;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  generatedByName?: string | null;
  obligationsTotal: number;
  obligationsCompliant: number;
  compliancePercentage?: number | null;
  permitsActive: number;
  permitsExpiringIn90Days: number;
  permitsExpired: number;
  projectsReviewed: number;
  clearancesIssued: number;
  wasteGeneratedKg: number;
  wasteRecycledKg: number;
  wasteRecyclingRate?: number | null;
  environmentalIncidents: number;
  environmentalIncidentsClosed: number;
  monitoringExceedances: number;
  auditFindingsRaised: number;
  correctiveActionsOpen: number;
  newRegulatoryUpdates: number;
  sustainabilityInitiativesActive: number;
  sustainabilityInitiativesCompleted: number;
  sustainabilityCostSavings: number;
  officerSummary?: string | null;
  submittedToManagementAt?: string | null;
  submittedByName?: string | null;
}
