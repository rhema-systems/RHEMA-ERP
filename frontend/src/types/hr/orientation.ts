// Types for HR Orientation (area 15) — catalogue, delivery, enrollment, assessment and
// completion artifacts. Mirrors ErpSystem.Core.DTOs.HR.OrientationDTOs.
//
// Backend routes are NOT uniformly prefixed: api/orientation-{programs,sessions,categories,
// notifications,dashboard} and api/employee-orientations. Each service names its own.

import type { AuditFields } from './common';
// The shared HR audience axis (announcements, policies, and since round 4 lane I orientation).
import type { HrAudienceTargetType } from '@/services/hr/announcements.service';

export type { HrAudienceTargetType };

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
  | 'ManagerAssigned'
  // Round 4, lane I-b: the next cycle of a recurring programme, opened by the nightly sweep.
  // ⚠ Deliberately NOT in the options below — they are the manual-enrolment picker, and a person
  //   enrolling somebody by hand is not a renewal.
  | 'Recurrence';

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

/**
 * What each trigger actually does since round 4 lane I made them fire — shown under the picker, so
 * the choice is made knowing its consequence rather than from a label.
 */
export const ORIENTATION_TRIGGER_HINTS: Record<OrientationEnrollmentTrigger, string> = {
  OnHire: 'When an employee is created — the form, the import or a confirmed hire. Counts the delay from the employment date.',
  OnTransfer: 'When a transfer, lateral move or secondment is implemented. Counts the delay from its effective date.',
  OnPromotion: 'When a promotion is implemented. Counts the delay from its effective date.',
  OnProgramPublish: 'Once, when the programme is made Active. Afterwards only “Enrol audience now” runs it.',
  Scheduled: 'Every night: anyone the rule reaches who is not yet on the programme.',
  Manual: 'Only when HR presses “Enrol audience now” on this programme.',
};

/** Hire, transfer and promotion have a date a delay can count from; the rest enrol immediately. */
export const DATED_TRIGGERS: OrientationEnrollmentTrigger[] = ['OnHire', 'OnTransfer', 'OnPromotion'];

/** Who, of the people at the target, a rule means — derived from data, never typed in. */
export type OrientationAudiencePopulation = 'Anyone' | 'NewHires' | 'Management' | 'Contractors';

export const ORIENTATION_AUDIENCE_POPULATION_OPTIONS: {
  value: OrientationAudiencePopulation;
  label: string;
  hint: string;
}[] = [
  { value: 'Anyone', label: 'Anyone there', hint: 'Everyone the target reaches.' },
  { value: 'NewHires', label: 'New hires', hint: 'Employed within the last 90 days.' },
  { value: 'Management', label: 'Management', hint: 'Heads a unit, or has at least one direct report.' },
  { value: 'Contractors', label: 'Contractors', hint: 'Contract, fixed-term, consultant or freelance staff.' },
];

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

/**
 * An audience rule. Since round 4 lane I the target is the shared HR audience axis (the same one
 * announcements use) and `population` narrows it; the old single `OrientationAudienceScope` value
 * is now only a descriptive label on the programme.
 */
export interface OrientationAudienceRule extends AuditFields {
  tenantId: string;
  programId: string;
  programTitle?: string | null;
  ruleName: string;
  description?: string | null;
  targetType: HrAudienceTargetType;
  targetEntityId?: string | null;
  /** "Organisation unit: Operations and the units beneath it" — resolved by the server. */
  targetEntityName?: string | null;
  population: OrientationAudiencePopulation;
  trigger: OrientationEnrollmentTrigger;
  enrollmentDelayDays: number;
  isInclusive: boolean;
  isActive: boolean;
  /** Active employees the rule reaches today (for an exclusion, the number it keeps out). */
  reachCount?: number | null;
}

export interface OrientationAudienceRuleCreateRequest {
  programId: string;
  ruleName: string;
  description?: string | null;
  targetType: HrAudienceTargetType;
  targetEntityId?: string | null;
  population: OrientationAudiencePopulation;
  trigger: OrientationEnrollmentTrigger;
  enrollmentDelayDays: number;
  isInclusive: boolean;
  isActive: boolean;
}

export interface OrientationAudienceReach {
  count: number;
  description: string;
}

/** What one run of the rules did — or, on a preview, would do. */
export interface OrientationTriggerRunResult {
  trigger: string;
  isPreview: boolean;
  asOf: string;
  programsEvaluated: number;
  rulesEvaluated: number;
  enrolled: number;
  alreadyEnrolled: number;
  excluded: number;
  waitingOnPrerequisite: number;
  /** Of `enrolled`, the next cycles of recurring programmes (lane I-b). */
  renewed: number;
  /** Due for a next cycle, but no rule of the programme reaches them any longer. */
  leftAudience: number;
  error?: string | null;
  /** At most 500 rows; the counts above are complete. */
  enrolments: {
    enrollmentId?: string | null;
    employeeId: string;
    employeeName?: string | null;
    employeeNumber?: string | null;
    programId: string;
    programTitle: string;
    /** Null for a renewal — no rule creates it. */
    ruleId?: string | null;
    ruleName: string;
    triggerEvent?: OrientationEnrollmentTrigger | null;
    /** The hire/movement date — or, for a renewal, the completion it renews. */
    triggerDate?: string | null;
    isRenewal: boolean;
  }[];
}

export type OrientationTriggerVerdict =
  | 'WouldEnrolNow'
  | 'Enrolled'
  | 'Excluded'
  | 'WaitingForDate'
  | 'WindowLapsed'
  | 'WaitingOnPrerequisite'
  | 'OnlyWhenHrEnrols'
  | 'NoTriggeringEvent'
  | 'NotInAudience'
  | 'ProgramNotActive'
  // Round 4, lane I-b — a recurring programme after the last cycle was completed.
  | 'RenewalDue'
  | 'RenewalScheduled'
  // Withdrawn, cancelled or a no-show — never undone by the system; HR can re-enrol by hand.
  | 'EndedByHr';

export interface OrientationRuleDiagnosis {
  ruleId: string;
  ruleName: string;
  isInclusive: boolean;
  isActive: boolean;
  trigger: OrientationEnrollmentTrigger;
  targetType: HrAudienceTargetType;
  targetName?: string | null;
  population: OrientationAudiencePopulation;
  enrollmentDelayDays: number;
  matchesTarget: boolean;
  matchesPopulation: boolean;
  triggerDate?: string | null;
  firesFrom?: string | null;
  firesUntil?: string | null;
  timing: 'Due' | 'NotYet' | 'Lapsed' | 'NoEvent' | 'Nightly' | 'AtPublish' | 'OnlyByHr';
  explanation: string;
}

export interface OrientationProgramDiagnosis {
  programId: string;
  programCode: string;
  programTitle: string;
  programStatus: OrientationProgramStatus;
  verdict: OrientationTriggerVerdict;
  explanation: string;
  enrollmentId?: string | null;
  enrollmentStatus?: string | null;
  enrollmentSource?: string | null;
  enrolledByRuleName?: string | null;
  missingPrerequisites: string[];
  rules: OrientationRuleDiagnosis[];
  /** Lane I-b — a recurring programme after the latest cycle was completed. */
  lastCompletedOn?: string | null;
  nextCycleOpensOn?: string | null;
  nextCycleDueOn?: string | null;
}

/** "Which rules would fire for this employee, and why" (lane I5). */
export interface OrientationTriggerDiagnosis {
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  isActive: boolean;
  asOf: string;
  hireDate?: string | null;
  organizationUnitName?: string | null;
  organizationLevelName?: string | null;
  positionTitle?: string | null;
  locationName?: string | null;
  employmentType: string;
  populations: string[];
  recentMovements: {
    movementId: string;
    movementNumber: string;
    movementType: string;
    trigger: OrientationEnrollmentTrigger;
    effectiveDate: string;
  }[];
  programs: OrientationProgramDiagnosis[];
  onboarding?: OnboardingTemplateApplicability | null;
  onboardingPlanId?: string | null;
  onboardingPlanTemplateName?: string | null;
  onboardingPlanSelectionReason?: string | null;
}

// ── Onboarding template applicability (lane I4) ───────────────────────────────

export interface OnboardingPlanTemplateAudience {
  id: string;
  planTemplateId: string;
  targetType: HrAudienceTargetType;
  targetEntityId?: string | null;
  targetEntityName?: string | null;
  isInclusive: boolean;
}

export interface OnboardingTemplateCandidate {
  templateId: string;
  templateName: string;
  isDefault: boolean;
  matchedOn: string;
  matchedTargetName?: string | null;
  specificity: number;
  isExcluded: boolean;
  excludedBy?: string | null;
}

export interface OnboardingTemplateApplicability {
  templateId?: string | null;
  templateName?: string | null;
  reason: string;
  matchedOn: string;
  isAmbiguous: boolean;
  candidates: OnboardingTemplateCandidate[];
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
  /** Round 4, lane I-b — a list needs it to offer "open the next cycle" on a completion. */
  isRecurring?: boolean;
  recurrenceFrequency?: OrientationRecurrenceFrequency | null;
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
  /** Round 4, lane I3 — how the person came to be on it. */
  enrollmentSource?: OrientationEnrollmentSource;
  audienceRuleId?: string | null;
  triggerEvent?: OrientationEnrollmentTrigger | null;
  triggerDate?: string | null;
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
  audienceRuleId?: string | null;
  triggerEvent?: OrientationEnrollmentTrigger | null;
  triggerDate?: string | null;
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
