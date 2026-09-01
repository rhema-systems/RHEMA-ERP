// Types for HR Orientation (area 15) — catalogue, delivery, enrollment, assessment and
// completion artifacts. Mirrors ErpSystem.Core.DTOs.HR.OrientationDTOs.
//
// Backend routes are NOT uniformly prefixed: api/orientation-{programs,sessions,categories,
// notifications,dashboard} and api/employee-orientations. Each service names its own.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type OrientationProgramType =
  | 'Onboarding'
  | 'PolicyAwareness'
  | 'ProductLaunch'
  | 'Compliance'
  | 'HealthAndSafety'
  | 'SystemsAndTools'
  | 'CultureAndValues'
  | 'General';

export const ORIENTATION_PROGRAM_TYPE_OPTIONS = opts<OrientationProgramType>([
  ['Onboarding', 'Onboarding'],
  ['PolicyAwareness', 'Policy Awareness'],
  ['ProductLaunch', 'Product Launch'],
  ['Compliance', 'Compliance'],
  ['HealthAndSafety', 'Health & Safety'],
  ['SystemsAndTools', 'Systems & Tools'],
  ['CultureAndValues', 'Culture & Values'],
  ['General', 'General'],
]);

export type OrientationProgramStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Active'
  | 'Suspended'
  | 'Retired'
  | 'Archived';

export const ORIENTATION_PROGRAM_STATUS_OPTIONS = opts<OrientationProgramStatus>([
  ['Draft', 'Draft'],
  ['PendingApproval', 'Pending Approval'],
  ['Active', 'Active'],
  ['Suspended', 'Suspended'],
  ['Retired', 'Retired'],
  ['Archived', 'Archived'],
]);

export type OrientationDeliveryMode =
  | 'InPerson'
  | 'VirtualInstructor'
  | 'SelfPacedOnline'
  | 'Blended'
  | 'VideoOnDemand'
  | 'PrintedMaterial';

export const ORIENTATION_DELIVERY_MODE_OPTIONS = opts<OrientationDeliveryMode>([
  ['InPerson', 'In Person'],
  ['VirtualInstructor', 'Virtual (Instructor-led)'],
  ['SelfPacedOnline', 'Self-paced Online'],
  ['Blended', 'Blended'],
  ['VideoOnDemand', 'Video on Demand'],
  ['PrintedMaterial', 'Printed Material'],
]);

export type OrientationPriority = 'Low' | 'Medium' | 'High' | 'Critical' | 'Mandatory';

export const ORIENTATION_PRIORITY_OPTIONS = opts<OrientationPriority>([
  ['Low', 'Low'],
  ['Medium', 'Medium'],
  ['High', 'High'],
  ['Critical', 'Critical'],
  ['Mandatory', 'Mandatory'],
]);

export type OrientationAudienceScope =
  | 'AllEmployees'
  | 'NewHires'
  | 'OrganizationUnit'
  | 'JobGrade'
  | 'Location'
  | 'Role'
  | 'Management'
  | 'Contractors'
  | 'Custom';

export const ORIENTATION_AUDIENCE_SCOPE_OPTIONS = opts<OrientationAudienceScope>([
  ['AllEmployees', 'All Employees'],
  ['NewHires', 'New Hires'],
  ['OrganizationUnit', 'Organization Unit'],
  ['JobGrade', 'Job Grade'],
  ['Location', 'Location'],
  ['Role', 'Role'],
  ['Management', 'Management'],
  ['Contractors', 'Contractors'],
  ['Custom', 'Custom'],
]);

export type OrientationModuleType =
  | 'InformationContent'
  | 'VideoLesson'
  | 'Interactive'
  | 'Assessment'
  | 'Acknowledgement'
  | 'Survey'
  | 'LiveSession';

export const ORIENTATION_MODULE_TYPE_OPTIONS = opts<OrientationModuleType>([
  ['InformationContent', 'Information Content'],
  ['VideoLesson', 'Video Lesson'],
  ['Interactive', 'Interactive'],
  ['Assessment', 'Assessment'],
  ['Acknowledgement', 'Acknowledgement'],
  ['Survey', 'Survey'],
  ['LiveSession', 'Live Session'],
]);

export type OrientationContentType =
  | 'Video'
  | 'Audio'
  | 'PDF'
  | 'Document'
  | 'Presentation'
  | 'ExternalLink'
  | 'EmbeddedWebPage'
  | 'Image'
  | 'Text'
  | 'Quiz';

export const ORIENTATION_CONTENT_TYPE_OPTIONS = opts<OrientationContentType>([
  ['Video', 'Video'],
  ['Audio', 'Audio'],
  ['PDF', 'PDF'],
  ['Document', 'Document'],
  ['Presentation', 'Presentation'],
  ['ExternalLink', 'External Link'],
  ['EmbeddedWebPage', 'Embedded Web Page'],
  ['Image', 'Image'],
  ['Text', 'Text'],
  ['Quiz', 'Quiz'],
]);

export type OrientationSessionStatus =
  | 'Draft'
  | 'Published'
  | 'EnrollmentOpen'
  | 'EnrollmentClosed'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'Postponed';

export const ORIENTATION_SESSION_STATUS_OPTIONS = opts<OrientationSessionStatus>([
  ['Draft', 'Draft'],
  ['Published', 'Published'],
  ['EnrollmentOpen', 'Enrollment Open'],
  ['EnrollmentClosed', 'Enrollment Closed'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
  ['Postponed', 'Postponed'],
]);

export type OrientationEnrollmentStatus =
  | 'PendingConfirmation'
  | 'Confirmed'
  | 'Waitlisted'
  | 'Active'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow'
  | 'Withdrawn';

export const ORIENTATION_ENROLLMENT_STATUS_OPTIONS = opts<OrientationEnrollmentStatus>([
  ['PendingConfirmation', 'Pending Confirmation'],
  ['Confirmed', 'Confirmed'],
  ['Waitlisted', 'Waitlisted'],
  ['Active', 'Active'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
  ['NoShow', 'No Show'],
  ['Withdrawn', 'Withdrawn'],
]);

/**
 * Statuses that occupy a seat, mirroring `OrientationEnrollmentStatuses.Occupying` on the server.
 * Kept in step deliberately — a withdrawn participant must not consume capacity on one screen and
 * not another.
 */
export const OCCUPYING_ENROLLMENT_STATUSES: OrientationEnrollmentStatus[] = [
  'PendingConfirmation',
  'Confirmed',
  'Active',
  'Completed',
];

export type OrientationEnrollmentSource =
  | 'AutoRule'
  | 'SelfEnrollment'
  | 'HrAssigned'
  | 'ManagerAssigned';

export const ORIENTATION_ENROLLMENT_SOURCE_OPTIONS = opts<OrientationEnrollmentSource>([
  ['HrAssigned', 'HR Assigned'],
  ['ManagerAssigned', 'Manager Assigned'],
  ['SelfEnrollment', 'Self Enrollment'],
  ['AutoRule', 'Automatic Rule'],
]);

export type OrientationCompletionStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'PendingAssessment'
  | 'PendingAcknowledgement'
  | 'Completed'
  | 'Failed'
  | 'Overdue'
  | 'Exempted';

export const ORIENTATION_COMPLETION_STATUS_OPTIONS = opts<OrientationCompletionStatus>([
  ['NotStarted', 'Not Started'],
  ['InProgress', 'In Progress'],
  ['PendingAssessment', 'Pending Assessment'],
  ['PendingAcknowledgement', 'Pending Acknowledgement'],
  ['Completed', 'Completed'],
  ['Failed', 'Failed'],
  ['Overdue', 'Overdue'],
  ['Exempted', 'Exempted'],
]);

export type OrientationContentProgressStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'Completed'
  | 'Skipped';

export type OrientationAttendanceStatus =
  | 'NotRecorded'
  | 'Present'
  | 'Absent'
  | 'Excused'
  | 'Partial'
  | 'Late';

export const ORIENTATION_ATTENDANCE_STATUS_OPTIONS = opts<OrientationAttendanceStatus>([
  ['Present', 'Present'],
  ['Late', 'Late'],
  ['Partial', 'Partial'],
  ['Excused', 'Excused'],
  ['Absent', 'Absent'],
  ['NotRecorded', 'Not Recorded'],
]);

export type OrientationFacilitatorRole =
  | 'Lead'
  | 'CoFacilitator'
  | 'SubjectMatterExpert'
  | 'Observer';

export const ORIENTATION_FACILITATOR_ROLE_OPTIONS = opts<OrientationFacilitatorRole>([
  ['Lead', 'Lead Facilitator'],
  ['CoFacilitator', 'Co-Facilitator'],
  ['SubjectMatterExpert', 'Subject Matter Expert'],
  ['Observer', 'Observer'],
]);

export type OrientationEnrollmentTrigger =
  | 'OnHire'
  | 'OnTransfer'
  | 'OnPromotion'
  | 'OnProgramPublish'
  | 'Scheduled'
  | 'Manual';

export const ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS = opts<OrientationEnrollmentTrigger>([
  ['OnHire', 'On Hire'],
  ['OnTransfer', 'On Transfer'],
  ['OnPromotion', 'On Promotion'],
  ['OnProgramPublish', 'On Program Publish'],
  ['Scheduled', 'Scheduled'],
  ['Manual', 'Manual'],
]);

export type OrientationQuestionType = 'SingleChoice' | 'MultiSelect' | 'TrueFalse' | 'FreeText';

export const ORIENTATION_QUESTION_TYPE_OPTIONS = opts<OrientationQuestionType>([
  ['SingleChoice', 'Single Choice'],
  ['MultiSelect', 'Multi Select'],
  ['TrueFalse', 'True / False'],
  ['FreeText', 'Free Text'],
]);

export type OrientationAcknowledgementStatus =
  | 'Pending'
  | 'Presented'
  | 'Signed'
  | 'Declined'
  | 'Expired';

export type OrientationCertificateStatus = 'Active' | 'Expired' | 'Revoked' | 'Reissued';

export type OrientationNotificationType =
  | 'Enrollment'
  | 'Reminder'
  | 'DeadlineApproaching'
  | 'Overdue'
  | 'Completion'
  | 'CertificateIssued'
  | 'Cancellation';

export type OrientationRecurrenceFrequency =
  | 'Monthly'
  | 'Quarterly'
  | 'SemiAnnually'
  | 'Annually'
  | 'Biennially';

export const ORIENTATION_RECURRENCE_FREQUENCY_OPTIONS = opts<OrientationRecurrenceFrequency>([
  ['Monthly', 'Monthly'],
  ['Quarterly', 'Quarterly'],
  ['SemiAnnually', 'Semi-annually'],
  ['Annually', 'Annually'],
  ['Biennially', 'Every two years'],
]);

// ── Category ──────────────────────────────────────────────────────────────────

export interface OrientationCategoryLookup {
  id: string;
  name: string;
  parentCategoryId?: string | null;
}

export interface OrientationCategory extends AuditFields {
  tenantId: string;
  name: string;
  description?: string | null;
  parentCategoryId?: string | null;
  parentCategoryName?: string | null;
  displayOrder: number;
  isActive: boolean;
  programCount: number;
  subCategories: OrientationCategoryLookup[];
}

export interface OrientationCategoryCreateRequest {
  name: string;
  description?: string | null;
  parentCategoryId?: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface OrientationCategoryUpdateRequest extends OrientationCategoryCreateRequest {
  id: string;
}

// ── Content item / module ─────────────────────────────────────────────────────

export interface OrientationContentItem extends AuditFields {
  tenantId: string;
  moduleId: string;
  moduleTitle?: string | null;
  title: string;
  description?: string | null;
  contentType: OrientationContentType;
  resourceUrl?: string | null;
  originalFileName?: string | null;
  fileSizeBytes?: number | null;
  mediaDurationSeconds?: number | null;
  sequenceOrder: number;
  isRequired: boolean;
  isActive: boolean;
}

export interface OrientationContentItemCreateRequest {
  moduleId: string;
  title: string;
  description?: string | null;
  contentType: OrientationContentType;
  resourceUrl?: string | null;
  originalFileName?: string | null;
  fileSizeBytes?: number | null;
  mediaDurationSeconds?: number | null;
  sequenceOrder: number;
  isRequired: boolean;
  isActive: boolean;
}

export interface OrientationContentItemUpdateRequest
  extends Omit<OrientationContentItemCreateRequest, 'moduleId'> {
  id: string;
}

export interface OrientationModule extends AuditFields {
  tenantId: string;
  programId: string;
  programTitle?: string | null;
  title: string;
  description?: string | null;
  sequenceOrder: number;
  moduleType: OrientationModuleType;
  estimatedDurationMinutes?: number | null;
  isSequentiallyRequired: boolean;
  isOptional: boolean;
  isActive: boolean;
  contentItemCount: number;
  contentItems: OrientationContentItem[];
}

export interface OrientationModuleCreateRequest {
  programId: string;
  title: string;
  description?: string | null;
  sequenceOrder: number;
  moduleType: OrientationModuleType;
  estimatedDurationMinutes?: number | null;
  isSequentiallyRequired: boolean;
  isOptional: boolean;
  isActive: boolean;
}

export interface OrientationModuleUpdateRequest
  extends Omit<OrientationModuleCreateRequest, 'programId'> {
  id: string;
}

// ── Prerequisites / audience rules ────────────────────────────────────────────

export interface OrientationPrerequisite extends AuditFields {
  tenantId: string;
  programId: string;
  programTitle?: string | null;
  prerequisiteProgramId: string;
  prerequisiteProgramCode?: string | null;
  prerequisiteProgramTitle?: string | null;
  isMandatory: boolean;
  notes?: string | null;
}

export interface OrientationPrerequisiteCreateRequest {
  programId: string;
  prerequisiteProgramId: string;
  isMandatory: boolean;
  notes?: string | null;
}

export interface OrientationAudienceRule extends AuditFields {
  tenantId: string;
  programId: string;
  programTitle?: string | null;
  ruleName: string;
  description?: string | null;
  targetType: OrientationAudienceScope;
  targetEntityId?: string | null;
  trigger: OrientationEnrollmentTrigger;
  enrollmentDelayDays: number;
  isInclusive: boolean;
  isActive: boolean;
}

export interface OrientationAudienceRuleCreateRequest {
  programId: string;
  ruleName: string;
  description?: string | null;
  targetType: OrientationAudienceScope;
  targetEntityId?: string | null;
  trigger: OrientationEnrollmentTrigger;
  enrollmentDelayDays: number;
  isInclusive: boolean;
  isActive: boolean;
}

export interface OrientationAudienceRuleUpdateRequest
  extends Omit<OrientationAudienceRuleCreateRequest, 'programId'> {
  id: string;
}

// ── Assessment authoring ──────────────────────────────────────────────────────

export interface OrientationAssessmentOption extends AuditFields {
  tenantId: string;
  questionId: string;
  optionText: string;
  /**
   * ⚠ Only meaningful on the AUTHORING read (`orientation-programs/{id}/questions`, HR-only).
   * The participant read (`employee-orientations/{id}/assessment`) forces this to false until the
   * attempt has been graded — do not treat it as the answer key on a participant screen.
   */
  isCorrect: boolean;
  displayOrder: number;
}

export interface OrientationAssessmentOptionCreateRequest {
  optionText: string;
  isCorrect: boolean;
  displayOrder: number;
}

export interface OrientationAssessmentQuestion extends AuditFields {
  tenantId: string;
  programId: string;
  programTitle?: string | null;
  questionText: string;
  questionType: OrientationQuestionType;
  points: number;
  /** Withheld by the server on the participant read until the attempt is graded. */
  explanation?: string | null;
  sequenceOrder: number;
  isActive: boolean;
  options: OrientationAssessmentOption[];
}

export interface OrientationAssessmentQuestionCreateRequest {
  programId: string;
  questionText: string;
  questionType: OrientationQuestionType;
  points: number;
  explanation?: string | null;
  sequenceOrder: number;
  isActive: boolean;
  options: OrientationAssessmentOptionCreateRequest[];
}

export interface OrientationAssessmentQuestionUpdateRequest
  extends Omit<OrientationAssessmentQuestionCreateRequest, 'programId'> {
  id: string;
}

// ── Program ───────────────────────────────────────────────────────────────────

export interface OrientationProgramSummary {
  id: string;
  programCode: string;
  title: string;
  categoryName?: string | null;
  programType: OrientationProgramType;
  status: OrientationProgramStatus;
  priority: OrientationPriority;
  defaultDeliveryMode: OrientationDeliveryMode;
  estimatedDurationMinutes?: number | null;
  isCertificateIssued: boolean;
  requiresAssessment: boolean;
  moduleCount: number;
  enrollmentCount: number;
  completedCount: number;
}

export interface OrientationProgram extends AuditFields {
  tenantId: string;
  programCode: string;
  title: string;
  description?: string | null;
  objectives?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  programType: OrientationProgramType;
  defaultDeliveryMode: OrientationDeliveryMode;
  status: OrientationProgramStatus;
  priority: OrientationPriority;
  audienceScope: OrientationAudienceScope;
  estimatedDurationMinutes?: number | null;
  requiresAssessment: boolean;
  passingScorePercent?: number | null;
  requiresAcknowledgement: boolean;
  completionDeadlineDays?: number | null;
  isCertificateIssued: boolean;
  certificateValidityMonths?: number | null;
  isRecurring: boolean;
  recurrenceFrequency?: OrientationRecurrenceFrequency | null;
  enableReminders: boolean;
  version?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  tags?: string | null;
  ownerEmployeeId?: string | null;
  ownerOrganizationUnitId?: string | null;
  ownerOrganizationUnitName?: string | null;
  moduleCount: number;
  sessionCount: number;
  enrollmentCount: number;
  completedCount: number;
  modules: OrientationModule[];
  audienceRules: OrientationAudienceRule[];
  prerequisites: OrientationPrerequisite[];
  assessmentQuestions: OrientationAssessmentQuestion[];
}

export interface OrientationProgramCreateRequest {
  /** Omit to have the server generate ORI-{year}-NNNN. */
  programCode?: string | null;
  title: string;
  description?: string | null;
  objectives?: string | null;
  categoryId?: string | null;
  programType: OrientationProgramType;
  defaultDeliveryMode: OrientationDeliveryMode;
  priority: OrientationPriority;
  audienceScope: OrientationAudienceScope;
  estimatedDurationMinutes?: number | null;
  requiresAssessment: boolean;
  passingScorePercent?: number | null;
  requiresAcknowledgement: boolean;
  completionDeadlineDays?: number | null;
  isCertificateIssued: boolean;
  certificateValidityMonths?: number | null;
  isRecurring: boolean;
  recurrenceFrequency?: OrientationRecurrenceFrequency | null;
  enableReminders: boolean;
  version?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  tags?: string | null;
  ownerEmployeeId?: string | null;
  ownerOrganizationUnitId?: string | null;
}

export interface OrientationProgramUpdateRequest
  extends Omit<OrientationProgramCreateRequest, 'programCode'> {
  id: string;
}

export interface ChangeOrientationProgramStatusRequest {
  programId: string;
  newStatus: OrientationProgramStatus;
}

// ── Session / facilitator / attendance ────────────────────────────────────────

export interface OrientationSessionFacilitator extends AuditFields {
  tenantId: string;
  sessionId: string;
  employeeId?: string | null;
  employeeName?: string | null;
  externalFacilitatorName?: string | null;
  externalFacilitatorEmail?: string | null;
  externalFacilitatorOrganization?: string | null;
  role: OrientationFacilitatorRole;
  hasConfirmed: boolean;
  notes?: string | null;
}

export interface OrientationSessionFacilitatorCreateRequest {
  sessionId: string;
  employeeId?: string | null;
  externalFacilitatorName?: string | null;
  externalFacilitatorEmail?: string | null;
  externalFacilitatorOrganization?: string | null;
  role: OrientationFacilitatorRole;
  hasConfirmed: boolean;
  notes?: string | null;
}

export interface OrientationSessionFacilitatorUpdateRequest
  extends Omit<OrientationSessionFacilitatorCreateRequest, 'sessionId'> {
  id: string;
}

export interface OrientationSessionSummary {
  id: string;
  sessionCode: string;
  title: string;
  programId: string;
  programTitle?: string | null;
  deliveryMode: OrientationDeliveryMode;
  status: OrientationSessionStatus;
  scheduledStartAt?: string | null;
  maxParticipants?: number | null;
  enrolledCount: number;
}

export interface OrientationSession extends AuditFields {
  tenantId: string;
  programId: string;
  programCode?: string | null;
  programTitle?: string | null;
  sessionCode: string;
  title: string;
  description?: string | null;
  deliveryMode: OrientationDeliveryMode;
  status: OrientationSessionStatus;
  scheduledStartAt?: string | null;
  scheduledEndAt?: string | null;
  actualStartAt?: string | null;
  actualEndAt?: string | null;
  venueDescription?: string | null;
  virtualMeetingUrl?: string | null;
  maxParticipants?: number | null;
  enrollmentDeadlineAt?: string | null;
  allowWaitlist: boolean;
  requiresApproval: boolean;
  recordingUrl?: string | null;
  participantInstructions?: string | null;
  enrolledCount: number;
  /** 0 when maxParticipants is null (unlimited) — check maxParticipants before showing it. */
  availableSeats: number;
  facilitators: OrientationSessionFacilitator[];
}

export interface OrientationSessionCreateRequest {
  programId: string;
  /** Omit to have the server generate OSN-{year}-NNNN. */
  sessionCode?: string | null;
  title: string;
  description?: string | null;
  deliveryMode: OrientationDeliveryMode;
  scheduledStartAt?: string | null;
  scheduledEndAt?: string | null;
  venueDescription?: string | null;
  virtualMeetingUrl?: string | null;
  maxParticipants?: number | null;
  enrollmentDeadlineAt?: string | null;
  allowWaitlist: boolean;
  requiresApproval: boolean;
  recordingUrl?: string | null;
  participantInstructions?: string | null;
}

export interface OrientationSessionUpdateRequest
  extends Omit<OrientationSessionCreateRequest, 'programId' | 'sessionCode'> {
  id: string;
  actualStartAt?: string | null;
  actualEndAt?: string | null;
}

export interface ChangeOrientationSessionStatusRequest {
  sessionId: string;
  newStatus: OrientationSessionStatus;
}

export interface OrientationAttendanceRecord extends AuditFields {
  tenantId: string;
  enrollmentId: string;
  employeeId?: string | null;
  employeeName?: string | null;
  sessionDay: number;
  attendanceStatus: OrientationAttendanceStatus;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  attendedMinutes?: number | null;
  markedLate: boolean;
  absenceReason?: string | null;
  markedByEmployeeId?: string | null;
  markedByName?: string | null;
}

export interface OrientationAttendanceEntry {
  enrollmentId: string;
  attendanceStatus: OrientationAttendanceStatus;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  markedLate: boolean;
  absenceReason?: string | null;
}

export interface MarkOrientationAttendanceRequest {
  sessionId: string;
  sessionDay: number;
  entries: OrientationAttendanceEntry[];
}

// ── Enrollment & progress ─────────────────────────────────────────────────────

export interface OrientationContentProgress extends AuditFields {
  tenantId: string;
  employeeOrientationId: string;
  contentItemId: string;
  contentItemTitle?: string | null;
  contentType?: OrientationContentType | null;
  status: OrientationContentProgressStatus;
  firstAccessedAt?: string | null;
  lastAccessedAt?: string | null;
  completedAt?: string | null;
  totalTimeSpentSeconds: number;
  accessCount: number;
  isAcknowledged: boolean;
}

export interface TrackOrientationContentProgressRequest {
  employeeOrientationId: string;
  contentItemId: string;
  timeSpentSecondsDelta: number;
  markCompleted: boolean;
  acknowledge: boolean;
}

export interface OrientationAssessmentResponse extends AuditFields {
  tenantId: string;
  employeeOrientationId: string;
  questionId: string;
  questionText?: string | null;
  selectedOptionId?: string | null;
  selectedOptionText?: string | null;
  freeTextAnswer?: string | null;
  isCorrect: boolean;
  pointsAwarded?: number | null;
  answeredAt: string;
}

export interface OrientationAssessmentAnswer {
  questionId: string;
  selectedOptionIds: string[];
  freeTextAnswer?: string | null;
}

export interface SubmitOrientationAssessmentRequest {
  employeeOrientationId: string;
  answers: OrientationAssessmentAnswer[];
}

export interface OrientationAssessmentResult {
  employeeOrientationId: string;
  scorePercent: number;
  pointsAwarded: number;
  totalPoints: number;
  correctCount: number;
  /** Every gradable question on the paper, not just the ones answered. */
  totalQuestions: number;
  passed: boolean;
  attemptNumber: number;
  responses: OrientationAssessmentResponse[];
}

export interface OrientationAcknowledgement extends AuditFields {
  tenantId: string;
  employeeOrientationId: string;
  title: string;
  acknowledgementText: string;
  status: OrientationAcknowledgementStatus;
  presentedAt?: string | null;
  signedAt?: string | null;
  declinedAt?: string | null;
  declineReason?: string | null;
  signatureIpAddress?: string | null;
}

export interface OrientationAcknowledgementCreateRequest {
  employeeOrientationId: string;
  title: string;
  acknowledgementText: string;
}

export interface SignOrientationAcknowledgementRequest {
  acknowledgementId: string;
  accept: boolean;
  declineReason?: string | null;
}

export interface OrientationFeedback extends AuditFields {
  tenantId: string;
  employeeOrientationId: string;
  overallRating?: number | null;
  contentRating?: number | null;
  facilitatorRating?: number | null;
  relevanceRating?: number | null;
  comments?: string | null;
  isAnonymous: boolean;
  submittedByEmployeeId?: string | null;
  submittedByName?: string | null;
  submittedAt: string;
}

export interface OrientationFeedbackCreateRequest {
  employeeOrientationId: string;
  overallRating?: number | null;
  contentRating?: number | null;
  facilitatorRating?: number | null;
  relevanceRating?: number | null;
  comments?: string | null;
  /** Withholds the submitter's name from everyone except the author. */
  isAnonymous: boolean;
  // No submittedByEmployeeId: the server stamps the caller. A payload id let anyone file
  // feedback in somebody else's name.
}

export interface OrientationCertificate extends AuditFields {
  tenantId: string;
  employeeOrientationId: string;
  employeeId?: string | null;
  employeeName?: string | null;
  programTitle?: string | null;
  certificateNumber: string;
  issuedAt: string;
  expiresAt?: string | null;
  issuedByEmployeeId?: string | null;
  issuedByName?: string | null;
  filePath?: string | null;
  verificationUrl?: string | null;
  status: OrientationCertificateStatus;
  revokedAt?: string | null;
  revocationReason?: string | null;
}

export interface IssueOrientationCertificateRequest {
  employeeOrientationId: string;
  /** Omit to have the server generate OCERT-{year}-NNNNN. */
  certificateNumber?: string | null;
  expiresAt?: string | null;
  issuedByEmployeeId?: string | null;
}

export interface RevokeOrientationCertificateRequest {
  certificateId: string;
  revocationReason: string;
}

export interface EmployeeOrientationSummary {
  id: string;
  programId: string;
  programCode?: string | null;
  programTitle?: string | null;
  sessionId?: string | null;
  sessionTitle?: string | null;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  enrollmentStatus: OrientationEnrollmentStatus;
  completionStatus: OrientationCompletionStatus;
  progressPercentage: number;
  finalScore?: number | null;
  isPassed: boolean;
  enrolledAt: string;
  completedAt?: string | null;
  nextDueDate?: string | null;
}

export interface EmployeeOrientation extends AuditFields {
  tenantId: string;
  programId: string;
  programCode?: string | null;
  programTitle?: string | null;
  sessionId?: string | null;
  sessionTitle?: string | null;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  enrollmentStatus: OrientationEnrollmentStatus;
  enrollmentSource: OrientationEnrollmentSource;
  enrolledAt: string;
  enrolledByEmployeeId?: string | null;
  enrolledByName?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  lastActivityAt?: string | null;
  progressPercentage: number;
  completionStatus: OrientationCompletionStatus;
  finalScore?: number | null;
  attemptCount: number;
  isPassed: boolean;
  acknowledgementSigned: boolean;
  certificateIssued: boolean;
  certificateSerialNumber?: string | null;
  certificateExpiresAt?: string | null;
  waitlistPosition?: number | null;
  nextDueDate?: string | null;
  withdrawalReason?: string | null;
  contentProgress: OrientationContentProgress[];
  assessmentResponses: OrientationAssessmentResponse[];
  acknowledgements: OrientationAcknowledgement[];
  feedbacks: OrientationFeedback[];
  attendanceRecords: OrientationAttendanceRecord[];
  certificates: OrientationCertificate[];
}

export interface EmployeeOrientationCreateRequest {
  programId: string;
  sessionId?: string | null;
  employeeId: string;
  enrollmentSource: OrientationEnrollmentSource;
  enrolledByEmployeeId?: string | null;
}

export interface EmployeeOrientationUpdateRequest {
  id: string;
  sessionId?: string | null;
  enrollmentStatus: OrientationEnrollmentStatus;
  nextDueDate?: string | null;
}

export interface BulkEnrollOrientationRequest {
  programId: string;
  sessionId?: string | null;
  employeeIds: string[];
  enrollmentSource: OrientationEnrollmentSource;
  enrolledByEmployeeId?: string | null;
}

export interface WithdrawOrientationRequest {
  enrollmentId: string;
  withdrawalReason: string;
}

// ── Notifications ─────────────────────────────────────────────────────────────

export interface OrientationNotification extends AuditFields {
  tenantId: string;
  programId?: string | null;
  programTitle?: string | null;
  employeeOrientationId?: string | null;
  recipientEmployeeId: string;
  type: OrientationNotificationType;
  subject: string;
  message?: string | null;
  navigationUrl?: string | null;
  isRead: boolean;
  readAt?: string | null;
  sentAt: string;
}

export interface OrientationNotificationCreateRequest {
  programId?: string | null;
  employeeOrientationId?: string | null;
  recipientEmployeeId: string;
  type: OrientationNotificationType;
  subject: string;
  message?: string | null;
  navigationUrl?: string | null;
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

export interface OrientationStatusCount {
  status: OrientationProgramStatus;
  count: number;
}

export interface OrientationCompletionCount {
  status: OrientationCompletionStatus;
  count: number;
}

export interface OrientationDashboard {
  totalPrograms: number;
  activePrograms: number;
  draftPrograms: number;
  totalEnrollments: number;
  notStartedEnrollments: number;
  inProgressEnrollments: number;
  completedEnrollments: number;
  overdueEnrollments: number;
  overallCompletionRate: number;
  upcomingSessions: number;
  expiringCertificates: number;
  programsByStatus: OrientationStatusCount[];
  enrollmentsByCompletionStatus: OrientationCompletionCount[];
  overdueList: EmployeeOrientationSummary[];
  upcomingSessionList: OrientationSessionSummary[];
  expiringCertificateList: OrientationCertificate[];
}
