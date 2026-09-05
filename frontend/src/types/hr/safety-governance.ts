// Types for HR Area 10 (SHE) slice 11: safety committees & meetings, regulatory
// compliance obligations + evidence, and the safety signage register.
// Mirrors ErpSystem.Core.DTOs.HR.SafetyEmergencyGovernanceDTOs (regions N, O and Q).
// Backend routes: api/safety/committees, api/safety/regulatory, api/safety/signs.
//
// Obligation codes, sign codes and meeting numbers are USER-entered and unique per tenant —
// a duplicate is refused (422). There is no server-side number generator for these three.
// GNFS liaison (FR-SHE-084) is modelled through this register: GNFS is a regulatory body,
// statutory fire inspections/certifications are FireSafety-domain obligations with evidence.
// Due-date alerting (FR-SHE-181) rides the reminder engine's statutory 180/90/60/30/14/7
// ladder on obligation reviews; sign inspections get a 30/14/7 ladder. Committee action items
// are NOT swept — corrective-action chasing is the slice-14 unified CA tracker.

import type { AuditFields } from './common';
import type { SheRegulatoryDomain } from './safety';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheComplianceStatus =
  | 'Compliant'
  | 'NonCompliant'
  | 'PartiallyCompliant'
  | 'NotApplicable'
  | 'NotAssessed';

export const SHE_COMPLIANCE_STATUS_OPTIONS = opts<SheComplianceStatus>([
  ['Compliant', 'Compliant'],
  ['NonCompliant', 'Non-compliant'],
  ['PartiallyCompliant', 'Partially Compliant'],
  ['NotApplicable', 'Not Applicable'],
  ['NotAssessed', 'Not Assessed'],
]);

export type SheSafetySignType =
  | 'Prohibition'
  | 'Warning'
  | 'Mandatory'
  | 'EmergencyEscape'
  | 'FireEquipment'
  | 'DangerousGoods'
  | 'Traffic'
  | 'Informational';

export const SHE_SIGN_TYPE_OPTIONS = opts<SheSafetySignType>([
  ['Prohibition', 'Prohibition'],
  ['Warning', 'Warning'],
  ['Mandatory', 'Mandatory'],
  ['EmergencyEscape', 'Emergency Escape'],
  ['FireEquipment', 'Fire Equipment'],
  ['DangerousGoods', 'Dangerous Goods'],
  ['Traffic', 'Traffic'],
  ['Informational', 'Informational'],
]);

export type SheSafetySignStatus =
  | 'Good'
  | 'Faded'
  | 'Damaged'
  | 'Missing'
  | 'Replaced'
  | 'Decommissioned';

export const SHE_SIGN_STATUS_OPTIONS = opts<SheSafetySignStatus>([
  ['Good', 'Good'],
  ['Faded', 'Faded'],
  ['Damaged', 'Damaged'],
  ['Missing', 'Missing'],
  ['Replaced', 'Replaced'],
  ['Decommissioned', 'Decommissioned'],
]);

export type SheSafetyMeetingType =
  | 'CommitteeMeeting'
  | 'ToolboxTalk'
  | 'SafetyBriefing'
  | 'EmergencyDrillDebriefing'
  | 'ManagementReview'
  | 'TrainingSession';

export const SHE_MEETING_TYPE_OPTIONS = opts<SheSafetyMeetingType>([
  ['CommitteeMeeting', 'Committee Meeting'],
  ['ToolboxTalk', 'Toolbox Talk'],
  ['SafetyBriefing', 'Safety Briefing'],
  ['EmergencyDrillDebriefing', 'Emergency Drill Debriefing'],
  ['ManagementReview', 'Management Review'],
  ['TrainingSession', 'Training Session'],
]);

export type SheActionItemPriority = 'Critical' | 'High' | 'Medium' | 'Low';

export const SHE_ACTION_PRIORITY_OPTIONS = opts<SheActionItemPriority>([
  ['Critical', 'Critical'],
  ['High', 'High'],
  ['Medium', 'Medium'],
  ['Low', 'Low'],
]);

export type SheActionItemStatus = 'Open' | 'InProgress' | 'Completed' | 'Overdue' | 'Cancelled';

export const SHE_ACTION_STATUS_OPTIONS = opts<SheActionItemStatus>([
  ['Open', 'Open'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Overdue', 'Overdue'],
  ['Cancelled', 'Cancelled'],
]);

// ── Regulatory obligations + evidence ─────────────────────────────────────────

export interface SheRegulatoryObligationSummary {
  id: string;
  obligationCode: string;
  title: string;
  domain: SheRegulatoryDomain;
  domainName: string;
  regulatoryBodyName?: string | null;
  complianceStatus: SheComplianceStatus;
  complianceStatusName: string;
  obligationOwnerName?: string | null;
  nextReviewDate?: string | null;
  isActive: boolean;
}

export interface SheRegulatoryComplianceEvidence extends AuditFields {
  obligationId: string;
  evidenceTitle: string;
  description?: string | null;
  evidenceDate: string;
  expiryDate?: string | null;
  documentPath?: string | null;
  recordedById: string;
  recordedByName: string;
}

export interface SheRegulatoryObligation extends AuditFields {
  tenantId: string;
  obligationCode: string;
  title: string;
  description?: string | null;
  domain: SheRegulatoryDomain;
  domainName: string;
  legislationName?: string | null;
  sectionOrClause?: string | null;
  regulatoryBodyId?: string | null;
  regulatoryBodyName?: string | null;
  complianceStatus: SheComplianceStatus;
  complianceStatusName: string;
  complianceNotes?: string | null;
  obligationOwnerId?: string | null;
  obligationOwnerName?: string | null;
  lastReviewedDate?: string | null;
  nextReviewDate?: string | null;
  lastAmendmentNotes?: string | null;
  lastAmendmentDate?: string | null;
  isActive: boolean;
  evidenceRecords: SheRegulatoryComplianceEvidence[];
}

export interface SheRegulatoryObligationCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  obligationCode: string;
  title: string;
  description?: string | null;
  domain: SheRegulatoryDomain;
  legislationName?: string | null;
  sectionOrClause?: string | null;
  regulatoryBodyId?: string | null;
  complianceStatus: SheComplianceStatus;
  complianceNotes?: string | null;
  obligationOwnerId?: string | null;
  nextReviewDate?: string | null;
  isActive: boolean;
}

export interface SheRegulatoryObligationUpdateRequest {
  id: string;
  title: string;
  description?: string | null;
  domain: SheRegulatoryDomain;
  legislationName?: string | null;
  sectionOrClause?: string | null;
  regulatoryBodyId?: string | null;
  complianceStatus: SheComplianceStatus;
  complianceNotes?: string | null;
  obligationOwnerId?: string | null;
  lastReviewedDate?: string | null;
  nextReviewDate?: string | null;
  lastAmendmentNotes?: string | null;
  lastAmendmentDate?: string | null;
  isActive: boolean;
}

export interface SheRegulatoryComplianceEvidenceCreateRequest {
  obligationId: string;
  evidenceTitle: string;
  description?: string | null;
  evidenceDate: string;
  expiryDate?: string | null;
  documentPath?: string | null;
  recordedById: string;
}

// ── Safety signs ──────────────────────────────────────────────────────────────

export interface SafetySign extends AuditFields {
  tenantId: string;
  signCode: string;
  description: string;
  signType: SheSafetySignType;
  signTypeName: string;
  locationId: string;
  locationName: string;
  specificPosition?: string | null;
  installationDate: string;
  manufacturer?: string | null;
  material?: string | null;
  isPhotoluminescent: boolean;
  status: SheSafetySignStatus;
  statusName: string;
  lastInspectionDate?: string | null;
  nextInspectionDate?: string | null;
  inspectionNotes?: string | null;
  isActive: boolean;
}

export interface SafetySignCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  signCode: string;
  description: string;
  signType: SheSafetySignType;
  locationId: string;
  specificPosition?: string | null;
  installationDate: string;
  manufacturer?: string | null;
  material?: string | null;
  isPhotoluminescent: boolean;
  status: SheSafetySignStatus;
  isActive: boolean;
}

export interface SafetySignUpdateRequest {
  id: string;
  description: string;
  signType: SheSafetySignType;
  locationId: string;
  specificPosition?: string | null;
  installationDate: string;
  manufacturer?: string | null;
  material?: string | null;
  isPhotoluminescent: boolean;
  status: SheSafetySignStatus;
  lastInspectionDate?: string | null;
  nextInspectionDate?: string | null;
  inspectionNotes?: string | null;
  isActive: boolean;
}

// ── Committees ────────────────────────────────────────────────────────────────

export interface SafetyCommitteeMember extends AuditFields {
  committeeId: string;
  employeeId: string;
  employeeName: string;
  role: string;
  joinDate: string;
  endDate?: string | null;
  isActive: boolean;
}

export interface SafetyCommittee extends AuditFields {
  tenantId: string;
  committeeName: string;
  description?: string | null;
  establishedDate: string;
  chairPersonId?: string | null;
  chairPersonName?: string | null;
  meetingFrequencyDays?: number | null;
  meetingSchedule?: string | null;
  isActive: boolean;
  members: SafetyCommitteeMember[];
}

export interface SafetyCommitteeCreateRequest {
  committeeName: string;
  description?: string | null;
  establishedDate: string;
  chairPersonId?: string | null;
  meetingFrequencyDays?: number | null;
  meetingSchedule?: string | null;
  isActive: boolean;
}

export interface SafetyCommitteeUpdateRequest extends SafetyCommitteeCreateRequest {
  id: string;
}

export interface SafetyCommitteeMemberCreateRequest {
  committeeId: string;
  employeeId: string;
  role: string;
  joinDate: string;
  endDate?: string | null;
  isActive: boolean;
}

/** The member's identity is fixed — role/tenure only. Rejoining is a new membership row. */
export interface SafetyCommitteeMemberUpdateRequest {
  id: string;
  role: string;
  endDate?: string | null;
  isActive: boolean;
}

// ── Meetings ──────────────────────────────────────────────────────────────────

export interface SafetyMeetingAttendee extends AuditFields {
  meetingId: string;
  employeeId: string;
  employeeName: string;
  attended: boolean;
  signedDate?: string | null;
}

export interface SafetyMeetingActionItem extends AuditFields {
  meetingId: string;
  meetingNumber?: string | null;
  actionDescription: string;
  priority: SheActionItemPriority;
  priorityName: string;
  status: SheActionItemStatus;
  statusName: string;
  assignedToId?: string | null;
  assignedToName?: string | null;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
}

export interface SafetyMeetingDocument extends AuditFields {
  meetingId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
}

export interface SafetyMeetingSummary {
  id: string;
  meetingNumber: string;
  committeeName?: string | null;
  meetingDate: string;
  location: string;
  type: SheSafetyMeetingType;
  typeName: string;
  /** Hand-entered headcount (may include visitors) — not the recorded attendee list length. */
  attendeesCount?: number | null;
  openActionItemCount: number;
}

export interface SafetyMeeting extends AuditFields {
  committeeId?: string | null;
  committeeName?: string | null;
  meetingNumber: string;
  meetingDate: string;
  startTime?: string | null;
  endTime?: string | null;
  location: string;
  type: SheSafetyMeetingType;
  typeName: string;
  agenda?: string | null;
  minutes?: string | null;
  topicsDiscussed?: string | null;
  decisionsMade?: string | null;
  facilitatorId?: string | null;
  facilitatorName?: string | null;
  attendeesCount?: number | null;
  attendees: SafetyMeetingAttendee[];
  actionItems: SafetyMeetingActionItem[];
  documents: SafetyMeetingDocument[];
}

/** Minutes/topics/decisions are debrief fields — recorded via update after the meeting. */
export interface SafetyMeetingCreateRequest {
  committeeId?: string | null;
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  meetingNumber: string;
  meetingDate: string;
  startTime?: string | null;
  endTime?: string | null;
  location: string;
  type: SheSafetyMeetingType;
  agenda?: string | null;
  facilitatorId?: string | null;
}

export interface SafetyMeetingUpdateRequest {
  id: string;
  committeeId?: string | null;
  meetingDate: string;
  startTime?: string | null;
  endTime?: string | null;
  location: string;
  type: SheSafetyMeetingType;
  agenda?: string | null;
  minutes?: string | null;
  topicsDiscussed?: string | null;
  decisionsMade?: string | null;
  facilitatorId?: string | null;
  attendeesCount?: number | null;
}

export interface SafetyMeetingAttendeeCreateRequest {
  meetingId: string;
  employeeId: string;
  attended: boolean;
  signedDate?: string | null;
}

export interface SafetyMeetingActionItemCreateRequest {
  meetingId: string;
  actionDescription: string;
  priority: SheActionItemPriority;
  assignedToId?: string | null;
  dueDate?: string | null;
}

export interface SafetyMeetingActionItemUpdateRequest {
  id: string;
  actionDescription: string;
  priority: SheActionItemPriority;
  status: SheActionItemStatus;
  assignedToId?: string | null;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
}

export interface SafetyMeetingDocumentCreateRequest {
  meetingId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
}
