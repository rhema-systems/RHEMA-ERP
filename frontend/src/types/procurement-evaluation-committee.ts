export type ProcurementEvaluationSourceType =
  | 'Tender'
  | 'RequestForQuotation';

export type ProcurementEvaluationPhase = 'Technical' | 'Financial' | 'Combined';

export type ProcurementEvaluationCommitteeControlStatus =
  | 'Draft'
  | 'Active'
  | 'Closed';

export type ProcurementEvaluationAppointmentStatus =
  | 'Pending'
  | 'Accepted'
  | 'Declined'
  | 'Withdrawn';

export type ProcurementEvaluationConflictOutcome =
  | 'NoConflict'
  | 'ConflictDeclared'
  | 'Withdrawn';

export type ProcurementEvaluationMeetingStatus =
  | 'Draft'
  | 'QuorumConfirmed'
  | 'QuorumFailed'
  | 'Closed';

export type ProcurementEvaluationScoreSheetStatus = 'Locked' | 'Recalled';

export type ProcurementEvaluationScoreRecallStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected';

export type ProcurementCommitteeMemberKind =
  | 'Chair'
  | 'VotingMember'
  | 'NonVotingMember'
  | 'Observer'
  | 'Secretary';

export type ProcurementEvaluationCommitteeAllowedAction = string;

export interface ProcurementEvaluationCommitteeReadiness {
  sourceType: ProcurementEvaluationSourceType;
  sourceId: string;
  sourceReference: string;
  sourceExists: boolean;
  hasControl: boolean;
  committeeControlId?: string;
  status?: ProcurementEvaluationCommitteeControlStatus;
  compositionReady: boolean;
  appointmentsReady: boolean;
  declarationsReady: boolean;
  quorumMet: boolean;
  requiredQuorum: number;
  eligibleVotingMemberCount: number;
  signedVotingAttendanceCount: number;
  blockedReasons: string[];
  allowedActions: ProcurementEvaluationCommitteeAllowedAction[];
}

export interface ProcurementEvaluationCommitteeOptions {
  committees: ProcurementEvaluationCommitteeTemplateOption[];
  workflows: ProcurementEvaluationWorkflowOption[];
  users: ProcurementEvaluationUserOption[];
}

export interface ProcurementEvaluationCommitteeTemplateOption {
  id: string;
  code: string;
  name: string;
  requiredQuorum: number;
  compositionReady: boolean;
  activeMemberCount: number;
  issues: string[];
}

export interface ProcurementEvaluationWorkflowOption {
  id: string;
  name: string;
  version: number;
}

export interface ProcurementEvaluationUserOption {
  userId: string;
  username: string;
  displayName: string;
}

export interface ProcurementEvaluationRoleRequirement {
  id: string;
  memberKind: ProcurementCommitteeMemberKind;
  roleName: string;
  minimumCount: number;
  isVoting: boolean;
  isRequiredForQuorum: boolean;
  matchedCount: number;
  isMet: boolean;
}

export interface ProcurementEvaluationConflictDeclaration {
  id: string;
  version: number;
  outcome: ProcurementEvaluationConflictOutcome;
  declaration: string;
  conflictDetails?: string;
  signatureReference: string;
  evidenceReference: string;
  validFromUtc: string;
  validToUtc?: string;
  declaredAtUtc: string;
  declaredByUserId: string;
  integrityHash: string;
}

export interface ProcurementEvaluationAppointment {
  id: string;
  committeeMemberId: string;
  responsibilityAssignmentId: string;
  userId: string;
  userDisplayName: string;
  roleName: string;
  memberKind: ProcurementCommitteeMemberKind;
  isVoting: boolean;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  status: ProcurementEvaluationAppointmentStatus;
  acceptedAtUtc?: string;
  acceptanceSignatureReference?: string;
  acceptanceEvidenceReference?: string;
  currentDeclaration?: ProcurementEvaluationConflictDeclaration;
  eligibleToScore: boolean;
  blockedReasons: string[];
  rowVersion: string;
}

export interface ProcurementEvaluationAttendance {
  id: string;
  appointmentId: string;
  userId: string;
  userDisplayName: string;
  memberKind: ProcurementCommitteeMemberKind;
  isVoting: boolean;
  isPresent: boolean;
  signedAtUtc?: string;
  signatureReference?: string;
  evidenceReference?: string;
  wasEligibleAtSignature: boolean;
  integrityHash: string;
}

export interface ProcurementEvaluationMeeting {
  id: string;
  sequence: number;
  phase: ProcurementEvaluationPhase;
  status: ProcurementEvaluationMeetingStatus;
  meetingMode: string;
  meetingChannel: string;
  scheduledAtUtc: string;
  startedAtUtc?: string;
  closedAtUtc?: string;
  eligibleVotingMemberCount: number;
  signedVotingAttendanceCount: number;
  chairPresent: boolean;
  secretaryPresent: boolean;
  quorumMet: boolean;
  evidenceReference: string;
  remoteMeetingEvidenceReference?: string;
  quorumIntegrityHash: string;
  attendance: ProcurementEvaluationAttendance[];
  rowVersion: string;
}

export interface ProcurementEvaluationScoreSheet {
  id: string;
  meetingId: string;
  appointmentId: string;
  submittedByUserId: string;
  submittedByName: string;
  phase: ProcurementEvaluationPhase;
  scoreSubjectType: string;
  scoreSubjectId: string;
  attempt: number;
  status: ProcurementEvaluationScoreSheetStatus;
  submittedAtUtc: string;
  scoreSnapshotJson: string;
  signatureReference: string;
  evidenceReference: string;
  integrityHash: string;
  rowVersion: string;
}

export interface ProcurementEvaluationScoreRecall {
  id: string;
  scoreSheetId: string;
  status: ProcurementEvaluationScoreRecallStatus;
  reason: string;
  evidenceReference: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  requestedByUserId: string;
  requestedByName: string;
  requestedAtUtc: string;
  decidedByUserId?: string;
  decidedByName?: string;
  decidedAtUtc?: string;
  decisionReference?: string;
  decisionEvidenceReference?: string;
  authorizedNewAttempt?: number;
  integrityHash: string;
  rowVersion: string;
}

export interface ProcurementEvaluationTimelineEntry {
  occurredAtUtc: string;
  action: string;
  outcome: string;
  actorUserId: string;
  actorName: string;
  reference?: string;
}

export interface ProcurementEvaluationCommitteeControl {
  id: string;
  sourceType: ProcurementEvaluationSourceType;
  sourceId: string;
  version: number;
  sourceReference: string;
  purpose: string;
  status: ProcurementEvaluationCommitteeControlStatus;
  committeeTemplateId: string;
  committeeCode: string;
  committeeName: string;
  requiredQuorum: number;
  compositionReady: boolean;
  quorumMet: boolean;
  policySetId: string;
  policyCode: string;
  policyVersion: number;
  configurationProfileId?: string;
  configurationProfileCode?: string;
  configurationProfileVersion?: number;
  methodRuleId: string;
  methodRuleCode: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  activatedAtUtc?: string;
  activatedByUserId?: string;
  activationEvidenceReference?: string;
  compositionIntegrityHash: string;
  requiredRoles: ProcurementEvaluationRoleRequirement[];
  members: ProcurementEvaluationAppointment[];
  meetings: ProcurementEvaluationMeeting[];
  scoreSheets: ProcurementEvaluationScoreSheet[];
  recalls: ProcurementEvaluationScoreRecall[];
  timeline: ProcurementEvaluationTimelineEntry[];
  allowedActions: ProcurementEvaluationCommitteeAllowedAction[];
  blockedReasons: string[];
  rowVersion: string;
}

export interface SaveProcurementEvaluationRoleRequirement {
  memberKind: ProcurementCommitteeMemberKind;
  roleName: string;
  minimumCount: number;
  isVoting: boolean;
  isRequiredForQuorum: boolean;
}

export interface BindProcurementEvaluationCommitteeRequest {
  sourceType: ProcurementEvaluationSourceType;
  sourceId: string;
  committeeTemplateId: string;
  purpose: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  requiredRoles: SaveProcurementEvaluationRoleRequirement[];
  idempotencyKey: string;
}

export interface ActivateProcurementEvaluationCommitteeRequest {
  rowVersion: string;
  evidenceReference: string;
  idempotencyKey: string;
}

export interface RespondProcurementEvaluationAppointmentRequest {
  accept: boolean;
  rowVersion: string;
  signatureReference?: string;
  evidenceReference?: string;
  reason?: string;
  idempotencyKey: string;
}

export interface SubmitProcurementEvaluationConflictDeclarationRequest {
  outcome: ProcurementEvaluationConflictOutcome;
  declaration: string;
  conflictDetails?: string;
  signatureReference: string;
  evidenceReference: string;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  validFromUtc: string;
  validToUtc?: string;
  appointmentRowVersion: string;
  idempotencyKey: string;
}

export interface CreateProcurementEvaluationMeetingRequest {
  phase: ProcurementEvaluationPhase;
  meetingMode: string;
  meetingChannel: string;
  scheduledAtUtc: string;
  evidenceReference: string;
  remoteMeetingEvidenceReference?: string;
  committeeRowVersion: string;
  idempotencyKey: string;
}

export interface SignProcurementEvaluationAttendanceRequest {
  isPresent: boolean;
  signatureReference: string;
  evidenceReference: string;
  meetingRowVersion: string;
  appointmentRowVersion: string;
  idempotencyKey: string;
}

export interface ConfirmProcurementEvaluationQuorumRequest {
  rowVersion: string;
  evidenceReference: string;
  remoteMeetingEvidenceReference?: string;
  idempotencyKey: string;
}

export interface ProcurementEvaluationScorerEligibility {
  allowed: boolean;
  sourceType: ProcurementEvaluationSourceType;
  sourceId: string;
  phase: ProcurementEvaluationPhase;
  actorUserId: string;
  committeeControlId?: string;
  appointmentId?: string;
  meetingId?: string;
  authorizedAttempt: number;
  blockedReasons: string[];
}

export interface RequestProcurementEvaluationScoreRecallRequest {
  scoreSheetRowVersion: string;
  reason: string;
  evidenceReference: string;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  workflowDefinitionId: string;
  idempotencyKey: string;
}

export interface DecideProcurementEvaluationScoreRecallRequest {
  approve: boolean;
  rowVersion: string;
  decisionReference: string;
  evidenceReference: string;
  idempotencyKey: string;
}
