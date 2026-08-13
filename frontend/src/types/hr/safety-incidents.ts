// Types for HR Area 10 slice 2 — the incident register and investigation lifecycle.
// Mirrors ErpSystem.Core.DTOs.HR.StaffSafetyDTOs (region B). Backend route: api/safety/incidents.
//
// Reporting an incident (POST) is open to every authenticated employee, and non-HR reporters are
// always recorded as themselves (the token wins over the body). Everything else is HR-only.

import type { AuditFields } from './common';
import type { SheIncidentCategory } from './safety';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheIncidentSeverity = 'Negligible' | 'Minor' | 'Moderate' | 'Major' | 'Catastrophic';

export const SHE_INCIDENT_SEVERITY_OPTIONS = opts<SheIncidentSeverity>([
  ['Negligible', 'Negligible'],
  ['Minor', 'Minor'],
  ['Moderate', 'Moderate'],
  ['Major', 'Major'],
  ['Catastrophic', 'Catastrophic'],
]);

export type SheIncidentStatus =
  | 'Reported'
  | 'UnderReview'
  | 'InvestigationInProgress'
  | 'PendingCorrective'
  | 'PendingClosure'
  | 'Closed'
  | 'Reopened';

export const SHE_INCIDENT_STATUS_OPTIONS = opts<SheIncidentStatus>([
  ['Reported', 'Reported'],
  ['UnderReview', 'Under Review'],
  ['InvestigationInProgress', 'Investigation In Progress'],
  ['PendingCorrective', 'Pending Corrective Actions'],
  ['PendingClosure', 'Pending Closure'],
  ['Closed', 'Closed'],
  ['Reopened', 'Reopened'],
]);

export type SheInvolvedPersonRole =
  | 'PrimaryVictim'
  | 'SecondaryVictim'
  | 'Perpetrator'
  | 'Bystander'
  | 'Responder';

export const SHE_INVOLVED_PERSON_ROLE_OPTIONS = opts<SheInvolvedPersonRole>([
  ['PrimaryVictim', 'Primary Victim'],
  ['SecondaryVictim', 'Secondary Victim'],
  ['Perpetrator', 'Perpetrator'],
  ['Bystander', 'Bystander'],
  ['Responder', 'Responder'],
]);

export type SheInjuryClassification =
  | 'FirstAidCase'
  | 'MedicalTreatmentCase'
  | 'RestrictedWorkCase'
  | 'LostTimeInjury'
  | 'PermanentDisability'
  | 'Fatality';

export const SHE_INJURY_CLASSIFICATION_OPTIONS = opts<SheInjuryClassification>([
  ['FirstAidCase', 'First Aid Case'],
  ['MedicalTreatmentCase', 'Medical Treatment Case'],
  ['RestrictedWorkCase', 'Restricted Work Case'],
  ['LostTimeInjury', 'Lost Time Injury'],
  ['PermanentDisability', 'Permanent Disability'],
  ['Fatality', 'Fatality'],
]);

export type SheBodySide = 'Left' | 'Right' | 'Central' | 'Both';

export const SHE_BODY_SIDE_OPTIONS = opts<SheBodySide>([
  ['Left', 'Left'],
  ['Right', 'Right'],
  ['Central', 'Central'],
  ['Both', 'Both'],
]);

export type SheRootCauseMethod =
  | 'FiveWhy'
  | 'Fishbone'
  | 'FaultTree'
  | 'SCAT'
  | 'BowTie'
  | 'ICAM'
  | 'Other';

export const SHE_ROOT_CAUSE_METHOD_OPTIONS = opts<SheRootCauseMethod>([
  ['FiveWhy', '5 Why'],
  ['Fishbone', 'Fishbone (Ishikawa)'],
  ['FaultTree', 'Fault Tree'],
  ['SCAT', 'SCAT'],
  ['BowTie', 'Bow Tie'],
  ['ICAM', 'ICAM'],
  ['Other', 'Other'],
]);

export type SheIncidentDocumentType =
  | 'Photo'
  | 'WitnessStatement'
  | 'MedicalReport'
  | 'InvestigationReport'
  | 'RegulatoryNotification'
  | 'InsuranceClaim'
  | 'CCTV'
  | 'Other';

export const SHE_INCIDENT_DOCUMENT_TYPE_OPTIONS = opts<SheIncidentDocumentType>([
  ['Photo', 'Photo'],
  ['WitnessStatement', 'Witness Statement'],
  ['MedicalReport', 'Medical Report'],
  ['InvestigationReport', 'Investigation Report'],
  ['RegulatoryNotification', 'Regulatory Notification'],
  ['InsuranceClaim', 'Insurance Claim'],
  ['CCTV', 'CCTV Footage'],
  ['Other', 'Other'],
]);

export type SheCorrectiveActionPriority = 'Critical' | 'High' | 'Medium' | 'Low';

export const SHE_CORRECTIVE_ACTION_PRIORITY_OPTIONS = opts<SheCorrectiveActionPriority>([
  ['Critical', 'Critical'],
  ['High', 'High'],
  ['Medium', 'Medium'],
  ['Low', 'Low'],
]);

export type SheCorrectiveActionStatus =
  | 'Pending'
  | 'InProgress'
  | 'Completed'
  | 'Verified'
  | 'Overdue'
  | 'Cancelled';

export const SHE_CORRECTIVE_ACTION_STATUS_OPTIONS = opts<SheCorrectiveActionStatus>([
  ['Pending', 'Pending'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Verified', 'Verified'],
  ['Overdue', 'Overdue'],
  ['Cancelled', 'Cancelled'],
]);

// ── Children ─────────────────────────────────────────────────────────────────

export interface SafetyIncidentInjuredBodyPart extends AuditFields {
  involvedPersonId: string;
  bodyPartId: string;
  bodyPartName: string;
  side?: SheBodySide | null;
  sideName?: string | null;
  notes?: string | null;
}

export interface SafetyIncidentInvolvedPerson extends AuditFields {
  incidentId: string;
  isEmployee: boolean;
  employeeId?: string | null;
  employeeNumber?: string | null;
  fullName: string;
  organizationOrCompany?: string | null;
  roleInIncident: SheInvolvedPersonRole;
  roleInIncidentName: string;
  wasOnDuty: boolean;
  activityBeingPerformed?: string | null;
  wasUsingPpe: boolean;
  ppeUsed?: string | null;
  ppeWasAdequate: boolean;
  wasInjured: boolean;
  injuryDescription?: string | null;
  injuryClassification?: SheInjuryClassification | null;
  injuryClassificationName?: string | null;
  injuryTypeId?: string | null;
  injuryTypeName?: string | null;
  isFatal: boolean;
  injuredBodyParts: SafetyIncidentInjuredBodyPart[];
  firstAidGiven: boolean;
  firstAidDetails?: string | null;
  firstAidProviderId?: string | null;
  firstAidProviderName?: string | null;
  medicalTreatmentRequired: boolean;
  healthcareFacilityId?: string | null;
  healthcareFacilityName?: string | null;
  treatmentDate?: string | null;
  diagnosisGiven?: string | null;
  medicalExpenseClaimId?: string | null;
  resultedInTimeOff: boolean;
  timeOffStartDate?: string | null;
  timeOffEndDate?: string | null;
  lostDays?: number | null;
  onLightDuty: boolean;
  lightDutyRestrictions?: string | null;
  returnToWorkPlanId?: string | null;
}

export interface SafetyIncidentWitness extends AuditFields {
  incidentId: string;
  isEmployee: boolean;
  employeeId?: string | null;
  name: string;
  emailAddress?: string | null;
  phoneNumber?: string | null;
  statement?: string | null;
  statementDate?: string | null;
  statementSigned: boolean;
  statementDocumentPath?: string | null;
  interviewedById?: string | null;
  interviewedByName?: string | null;
  interviewDate?: string | null;
}

export interface SafetyIncidentInvestigationTeamMember extends AuditFields {
  incidentId: string;
  employeeId: string;
  employeeName: string;
  role: string;
  joinedDate: string;
}

export interface SafetyIncidentCorrectiveAction extends AuditFields {
  incidentId: string;
  incidentNumber?: string | null;
  incidentTypeCorrectiveActionId?: string | null;
  actionDescription: string;
  priority: SheCorrectiveActionPriority;
  priorityName: string;
  status: SheCorrectiveActionStatus;
  statusName: string;
  responsiblePersonId: string;
  responsiblePersonName: string;
  dueDate: string;
  completionDate?: string | null;
  completionNotes?: string | null;
  effectivenessVerified: boolean;
  verificationDate?: string | null;
  effectivenessReviewNotes?: string | null;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  isOverdue: boolean;
}

export interface SafetyIncidentFollowUp extends AuditFields {
  incidentId: string;
  followUpDate: string;
  actionsTaken?: string | null;
  personCondition?: string | null;
  notes: string;
  furtherFollowUpRequired: boolean;
  nextFollowUpDate?: string | null;
  conductedById: string;
  conductedByName: string;
}

export interface SafetyIncidentDocument extends AuditFields {
  incidentId: string;
  fileName: string;
  filePath: string;
  type: SheIncidentDocumentType;
  typeName: string;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

// ── The incident ─────────────────────────────────────────────────────────────

export interface SafetyIncident extends AuditFields {
  tenantId: string;
  incidentNumber: string;
  category: SheIncidentCategory;
  categoryName: string;
  severity: SheIncidentSeverity;
  severityName: string;
  status: SheIncidentStatus;
  statusName: string;
  incidentTypeId?: string | null;
  incidentTypeName?: string | null;
  incidentDate: string;
  incidentTime?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  supervisorId?: string | null;
  supervisorName?: string | null;
  description: string;
  immediateCause?: string | null;
  underlyingCause?: string | null;
  contributingFactors?: string | null;
  couldHaveCausedInjury: boolean;
  potentialConsequence?: string | null;
  reportedById: string;
  reportedByName: string;
  reportedDate: string;
  immediateActionTaken?: string | null;
  likelihoodBefore?: number | null;
  severityBefore?: number | null;
  riskScoreBefore?: number | null;
  likelihoodAfter?: number | null;
  severityAfter?: number | null;
  riskScoreAfter?: number | null;
  requiresInvestigation: boolean;
  leadInvestigatorId?: string | null;
  leadInvestigatorName?: string | null;
  investigationStartDate?: string | null;
  investigationTargetDate?: string | null;
  investigationCompleteDate?: string | null;
  rootCauseAnalysis?: string | null;
  investigationFindings?: string | null;
  rootCauseMethod?: SheRootCauseMethod | null;
  rootCauseMethodName?: string | null;
  reportableToAuthority: boolean;
  reportedToBodyId?: string | null;
  reportedToBodyName?: string | null;
  authorityNotificationDate?: string | null;
  authorityReferenceNumber?: string | null;
  authorityNotifiedById?: string | null;
  authorityNotifiedByName?: string | null;
  insuranceClaimFiled: boolean;
  claimFiledDate?: string | null;
  claimReferenceNumber?: string | null;
  insuranceProviderId?: string | null;
  insuranceProviderName?: string | null;
  claimAmount?: number | null;
  claimApproved: boolean;
  amountPaid?: number | null;
  totalLostDays: number;
  isLostTimeInjury: boolean;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedDate?: string | null;
  reviewComments?: string | null;
  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  closureNotes?: string | null;
  involvedPersons: SafetyIncidentInvolvedPerson[];
  witnesses: SafetyIncidentWitness[];
  investigationTeam: SafetyIncidentInvestigationTeamMember[];
  correctiveActions: SafetyIncidentCorrectiveAction[];
  followUps: SafetyIncidentFollowUp[];
  documents: SafetyIncidentDocument[];
}

export interface SafetyIncidentSummary {
  id: string;
  incidentNumber: string;
  category: SheIncidentCategory;
  categoryName: string;
  severity: SheIncidentSeverity;
  severityName: string;
  status: SheIncidentStatus;
  statusName: string;
  incidentTypeName?: string | null;
  incidentDate: string;
  locationName?: string | null;
  reportedByName: string;
  requiresInvestigation: boolean;
  isLostTimeInjury: boolean;
  totalLostDays: number;
  involvedPersonCount: number;
  openCorrectiveActionCount: number;
}

// ── Requests ─────────────────────────────────────────────────────────────────

/** `reportedById` is overridden with the token's employee for non-HR callers — the server
 * does not trust the body on the open reporting endpoint. */
export interface SafetyIncidentCreateRequest {
  category: SheIncidentCategory;
  severity: SheIncidentSeverity;
  incidentTypeId?: string | null;
  incidentDate: string;
  incidentTime?: string | null;
  locationId?: string | null;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  supervisorId?: string | null;
  description: string;
  immediateCause?: string | null;
  contributingFactors?: string | null;
  couldHaveCausedInjury: boolean;
  potentialConsequence?: string | null;
  reportedById?: string | null;
  reportedDate?: string;
  immediateActionTaken?: string | null;
  likelihoodBefore?: number | null;
  severityBefore?: number | null;
  requiresInvestigation: boolean;
  reportableToAuthority: boolean;
}

export interface SafetyIncidentUpdateRequest {
  id: string;
  category: SheIncidentCategory;
  severity: SheIncidentSeverity;
  incidentTypeId?: string | null;
  incidentDate: string;
  incidentTime?: string | null;
  locationId?: string | null;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  supervisorId?: string | null;
  description: string;
  immediateCause?: string | null;
  underlyingCause?: string | null;
  contributingFactors?: string | null;
  couldHaveCausedInjury: boolean;
  potentialConsequence?: string | null;
  immediateActionTaken?: string | null;
  likelihoodBefore?: number | null;
  severityBefore?: number | null;
  requiresInvestigation: boolean;
  reportableToAuthority: boolean;
}

export interface AssignInvestigationRequest {
  incidentId: string;
  leadInvestigatorId: string;
  investigationStartDate?: string | null;
  investigationTargetDate?: string | null;
  rootCauseMethod?: SheRootCauseMethod | null;
}

export interface RecordInvestigationRequest {
  incidentId: string;
  rootCauseAnalysis?: string | null;
  investigationFindings?: string | null;
  rootCauseMethod?: SheRootCauseMethod | null;
  investigationCompleteDate?: string | null;
  likelihoodAfter?: number | null;
  severityAfter?: number | null;
}

export interface NotifyAuthorityRequest {
  incidentId: string;
  reportedToBodyId: string;
  authorityNotificationDate?: string;
  authorityReferenceNumber?: string | null;
  authorityNotifiedById: string;
}

export interface FileClaimRequest {
  incidentId: string;
  claimFiledDate?: string;
  claimReferenceNumber?: string | null;
  insuranceProviderId?: string | null;
  claimAmount?: number | null;
  claimApproved: boolean;
  amountPaid?: number | null;
}

export interface ReviewIncidentRequest {
  incidentId: string;
  reviewedById: string;
  reviewedDate?: string;
  reviewComments?: string | null;
  newStatus?: SheIncidentStatus | null;
}

/** Refused with 422 while any corrective action is still open. */
export interface CloseIncidentRequest {
  incidentId: string;
  closedById: string;
  closedDate?: string;
  closureNotes?: string | null;
}

export interface InvolvedPersonCreateRequest {
  incidentId: string;
  isEmployee: boolean;
  employeeId?: string | null;
  fullName: string;
  organizationOrCompany?: string | null;
  roleInIncident: SheInvolvedPersonRole;
  wasOnDuty: boolean;
  activityBeingPerformed?: string | null;
  wasUsingPpe: boolean;
  ppeUsed?: string | null;
  ppeWasAdequate: boolean;
  wasInjured: boolean;
  injuryDescription?: string | null;
  injuryClassification?: SheInjuryClassification | null;
  injuryTypeId?: string | null;
  isFatal: boolean;
  firstAidGiven: boolean;
  firstAidDetails?: string | null;
  firstAidProviderId?: string | null;
  medicalTreatmentRequired: boolean;
  healthcareFacilityId?: string | null;
  treatmentDate?: string | null;
  diagnosisGiven?: string | null;
  resultedInTimeOff: boolean;
  timeOffStartDate?: string | null;
  timeOffEndDate?: string | null;
  lostDays?: number | null;
  onLightDuty: boolean;
  lightDutyRestrictions?: string | null;
}

export interface InvolvedPersonUpdateRequest extends Omit<InvolvedPersonCreateRequest, 'incidentId' | 'isEmployee' | 'employeeId'> {
  id: string;
  medicalExpenseClaimId?: string | null;
  returnToWorkPlanId?: string | null;
}

export interface InjuredBodyPartCreateRequest {
  involvedPersonId: string;
  bodyPartId: string;
  side?: SheBodySide | null;
  notes?: string | null;
}

export interface WitnessCreateRequest {
  incidentId: string;
  isEmployee: boolean;
  employeeId?: string | null;
  name: string;
  emailAddress?: string | null;
  phoneNumber?: string | null;
  statement?: string | null;
  statementDate?: string | null;
  statementSigned: boolean;
  statementDocumentPath?: string | null;
  interviewedById?: string | null;
  interviewDate?: string | null;
}

export interface WitnessUpdateRequest extends Omit<WitnessCreateRequest, 'incidentId' | 'isEmployee' | 'employeeId'> {
  id: string;
}

export interface InvestigationTeamMemberCreateRequest {
  incidentId: string;
  employeeId: string;
  role: string;
  joinedDate?: string;
}

export interface CorrectiveActionCreateRequest {
  incidentId: string;
  incidentTypeCorrectiveActionId?: string | null;
  actionDescription: string;
  priority: SheCorrectiveActionPriority;
  responsiblePersonId: string;
  dueDate: string;
}

export interface CorrectiveActionUpdateRequest {
  id: string;
  actionDescription: string;
  priority: SheCorrectiveActionPriority;
  status: SheCorrectiveActionStatus;
  responsiblePersonId: string;
  dueDate: string;
  completionDate?: string | null;
  completionNotes?: string | null;
}

export interface VerifyCorrectiveActionRequest {
  correctiveActionId: string;
  verifiedById: string;
  verificationDate?: string;
  effectivenessReviewNotes?: string | null;
}

export interface FollowUpCreateRequest {
  incidentId: string;
  followUpDate: string;
  actionsTaken?: string | null;
  personCondition?: string | null;
  notes: string;
  furtherFollowUpRequired: boolean;
  nextFollowUpDate?: string | null;
  conductedById: string;
}

export interface IncidentDocumentCreateRequest {
  incidentId: string;
  fileName: string;
  filePath: string;
  type: SheIncidentDocumentType;
  description?: string | null;
  uploadedById: string;
}
