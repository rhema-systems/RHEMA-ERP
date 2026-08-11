/**
 * Recruitment — slice B: candidates, applications, the pipeline, screening and shortlisting.
 *
 * Four controllers, four route shapes, as everywhere in HR:
 * `api/recruitment-pipelines` (setup), `api/job-candidates`, `api/job-applications`,
 * and `api/applications` (the pipeline board and its bulk operations).
 *
 * ⚠ All four are **HR-gated**. Candidates and applications carry PII — name, date of birth, CV,
 * contact details, demographics — so unlike vacancies in slice A the reads are gated too. The only
 * exceptions are the internal job-board endpoints, which are not part of this slice.
 */

import type { HrPagedResult } from './recruitment';

export type { HrPagedResult };

// ── enums (string unions matching the backend's JSON) ──────────────────────

export const APPLICATION_STATUSES = [
  'Draft',
  'New',
  'Submitted',
  'UnderReview',
  'Shortlisted',
  'InterviewScheduled',
  'InterviewCompleted',
  'AssessmentPending',
  'PreEmploymentCheck',
  'OfferExtended',
  'OfferAccepted',
  'OfferDeclined',
  'Rejected',
  'Withdrawn',
  'Hired',
  'Waitlisted',
] as const;
export type ApplicationStatus = (typeof APPLICATION_STATUSES)[number];

/**
 * Statuses an application can no longer be acted on from. The server owns the real rules — this is
 * only used to dim buttons that would certainly be refused.
 */
export const TERMINAL_APPLICATION_STATUSES: readonly ApplicationStatus[] = [
  'Hired',
  'Withdrawn',
];

export const APPLICATION_SOURCES = [
  'CompanyWebsite',
  'JobBoard',
  'LinkedIn',
  'EmployeeReferral',
  'WalkIn',
  'RecruitmentAgency',
  'CareerFair',
  'SocialMedia',
  'NewspaperAd',
  'Other',
  'InternalPortal',
] as const;
export type ApplicationSource = (typeof APPLICATION_SOURCES)[number];

export const PIPELINE_STAGE_TYPES = [
  'ApplicationReview',
  'Screening',
  'HiringManagerReview',
  'Assessment',
  'Interview',
  'PreEmploymentCheck',
  'Offer',
  'Hired',
  'Other',
] as const;
export type RecruitmentPipelineStageType = (typeof PIPELINE_STAGE_TYPES)[number];

export const STAGE_EXIT_REASONS = ['Progressed', 'Rejected', 'Withdrawn', 'OnHold', 'Merged'] as const;
export type StageExitReason = (typeof STAGE_EXIT_REASONS)[number];

export const SHORTLIST_DECISION_TYPES = [
  'Shortlisted',
  'Rejected',
  'Waitlisted',
  'Unshortlisted',
  'AutoShortlisted',
] as const;
export type ShortlistDecisionType = (typeof SHORTLIST_DECISION_TYPES)[number];

export const SHORTLIST_DECISION_SOURCES = ['Manual', 'AutoScoreThreshold', 'ManualOverride'] as const;
export type ShortlistDecisionSource = (typeof SHORTLIST_DECISION_SOURCES)[number];

export const SHORTLIST_APPROVAL_STATUSES = [
  'NotSubmitted',
  'PendingApproval',
  'Approved',
  'Rejected',
] as const;
export type ShortlistApprovalStatus = (typeof SHORTLIST_APPROVAL_STATUSES)[number];

/** ⚠ Only two members — there is no `Online`, `Aptitude` or `Psychometric`. */
export const APPLICANT_TEST_TYPES = ['Written', 'Practical'] as const;
export type JobApplicantTestType = (typeof APPLICANT_TEST_TYPES)[number];

export const COMMUNICATION_TYPES = [
  'Email',
  'TextMessage',
  'Letter',
  'PortalNotification',
  'PhoneCall',
] as const;
export type CommunicationType = (typeof COMMUNICATION_TYPES)[number];

export const COMMUNICATION_DIRECTIONS = ['Outbound', 'Inbound', 'System'] as const;
export type CommunicationDirection = (typeof COMMUNICATION_DIRECTIONS)[number];

export const CANDIDATE_DOCUMENT_TYPES = [
  'Resume',
  'CoverLetter',
  'Transcript',
  'Certificate',
  'License',
  'Portfolio',
  'ReferenceLetter',
  'IdDocument',
  'Other',
] as const;
export type CandidateDocumentType = (typeof CANDIDATE_DOCUMENT_TYPES)[number];

export const GENDERS = ['Male', 'Female', 'Other', 'PreferNotToSay'] as const;
export type Gender = (typeof GENDERS)[number];

export const QUALIFICATION_TYPES = [
  'Education',
  'Experience',
  'Certification',
  'License',
  'TechnicalSkills',
  'Language',
  'Membership',
  'Other',
] as const;
export type QualificationType = (typeof QUALIFICATION_TYPES)[number];

export const PROFICIENCY_LEVELS = [
  'Basic',
  'WorkingKnowledge',
  'Proficient',
  'Advanced',
  'Expert',
] as const;
export type ProficiencyLevel = (typeof PROFICIENCY_LEVELS)[number];

export const PREFERRED_WORK_ARRANGEMENTS = ['Any', 'OnSite', 'Hybrid', 'Remote'] as const;
export type PreferredWorkArrangement = (typeof PREFERRED_WORK_ARRANGEMENTS)[number];

export const WORK_AUTHORIZATION_STATUSES = [
  'NotSpecified',
  'Citizen',
  'PermanentResident',
  'WorkVisa',
  'RequiresSponsorship',
] as const;
export type WorkAuthorizationStatus = (typeof WORK_AUTHORIZATION_STATUSES)[number];

export const TALENT_POOL_STATUSES = [
  'Active',
  'Passive',
  'Dormant',
  'Expired',
  'Converted',
  'OnHold',
] as const;
export type TalentPoolStatus = (typeof TALENT_POOL_STATUSES)[number];

// ══════════════════════════════════════════════════════════════════════════
// RECRUITMENT PIPELINE (setup)
// ══════════════════════════════════════════════════════════════════════════

/**
 * A stage's `order`, `canRepeat` and `maxAttempts` are the transition rules the server enforces on
 * every move. Editing a stage rewrites the rules every in-flight application is being moved under,
 * which is why stage writes are HR-only while reads are open to the tenant.
 */
export interface RecruitmentPipelineStage {
  id: string;
  tenantId: string;
  recruitmentPipelineId: string;
  pipelineName: string;
  name: string;
  description?: string | null;
  order: number;
  stageType: RecruitmentPipelineStageType;
  stageTypeName: string;
  isActive: boolean;
  isFinalStage: boolean;
  isRequired: boolean;
  defaultTimeToCompleteDays?: number | null;
  canSkip: boolean;
  canRepeat: boolean;
  maxAttempts?: number | null;
  instructions?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface RecruitmentPipeline {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
  defaultTimeToCompleteDays?: number | null;
  stages: RecruitmentPipelineStage[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface RecruitmentPipelineSummary {
  id: string;
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
  stageCount: number;
  defaultTimeToCompleteDays?: number | null;
}

export interface CreateRecruitmentPipelineStage {
  recruitmentPipelineId?: string;
  name: string;
  description?: string | null;
  order: number;
  stageType: RecruitmentPipelineStageType;
  isFinalStage: boolean;
  isActive: boolean;
  isRequired: boolean;
  defaultTimeToCompleteDays?: number | null;
  canSkip: boolean;
  canRepeat: boolean;
  maxAttempts?: number | null;
  instructions?: string | null;
}

export interface UpdateRecruitmentPipelineStage extends CreateRecruitmentPipelineStage {
  id: string;
}

export interface CreateRecruitmentPipeline {
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
  defaultTimeToCompleteDays?: number | null;
  stages: CreateRecruitmentPipelineStage[];
}

export interface UpdateRecruitmentPipeline {
  id: string;
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
  defaultTimeToCompleteDays?: number | null;
}

// ══════════════════════════════════════════════════════════════════════════
// JOB CANDIDATE
// ══════════════════════════════════════════════════════════════════════════

export interface JobCandidateSummary {
  id: string;
  candidateNumber: string;
  fullName: string;
  email: string;
  phone: string;
  city: string;
  countryName: string;
  isInTalentPool: boolean;
  applicationCount: number;
}

/**
 * ⚠ The professional-profile, availability, compensation and work-authorization fields below are
 * **read-only from the HR side**: `CreateJobCandidateDto`/`UpdateJobCandidateDto` do not carry them,
 * so they are written by the candidate portal and the external application path only. Editing a
 * candidate through HR preserves them — the mapper never touches what the DTO does not declare.
 */
export interface JobCandidate {
  id: string;
  tenantId: string;
  candidateNumber: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  fullName: string;
  dateOfBirth: string;
  gender: Gender;
  genderName: string;
  email: string;
  phone: string;
  alternatePhone?: string | null;
  postalAddress?: string | null;
  digitalAddress?: string | null;
  city: string;
  nationality?: string | null;
  countryId: string;
  countryName: string;
  isInTalentPool: boolean;
  talentPoolAddedDate?: string | null;
  linkedInProfile?: string | null;
  portfolioUrl?: string | null;
  gitHubUrl?: string | null;

  // read-only (candidate-supplied)
  headline?: string | null;
  professionalSummary?: string | null;
  currentJobTitle?: string | null;
  currentEmployer?: string | null;
  totalYearsExperience?: number | null;
  noticePeriodDays?: number | null;
  availableFrom?: string | null;
  preferredWorkArrangement: PreferredWorkArrangement;
  preferredWorkArrangementName: string;
  expectedSalaryMin?: number | null;
  expectedSalaryMax?: number | null;
  expectedSalaryCurrency?: string | null;
  workAuthorizationStatus: WorkAuthorizationStatus;
  workAuthorizationStatusName: string;

  cvFilePath?: string | null;
  profilePhotoUrl?: string | null;
  applicationCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface JobCandidateDetail extends JobCandidate {
  qualifications: CandidateQualification[];
  workHistories: CandidateWorkHistory[];
  referees: CandidateReferee[];
  skills: CandidateSkill[];
  languages: CandidateLanguage[];
  interests: CandidateInterest[];
  documents: CandidateDocument[];
  notes: CandidateNote[];
  applications: JobApplicationSummary[];
}

/** Create and update carry the same fields; update adds `id`. */
export interface CreateJobCandidate {
  firstName: string;
  middleName?: string | null;
  lastName: string;
  dateOfBirth: string;
  gender: Gender;
  email: string;
  phone: string;
  alternatePhone?: string | null;
  postalAddress?: string | null;
  digitalAddress?: string | null;
  city: string;
  countryId: string;
  linkedInProfile?: string | null;
  portfolioUrl?: string | null;
  gitHubUrl?: string | null;
  isInTalentPool: boolean;
}

export interface UpdateJobCandidate extends CreateJobCandidate {
  id: string;
}

// ── candidate sub-resources ────────────────────────────────────────────────

export interface CandidateQualification {
  id: string;
  jobCandidateId: string;
  qualificationType: QualificationType;
  qualificationTypeName: string;
  qualificationId?: string | null;
  qualificationName: string;
  institution: string;
  dateAwarded: string;
  grade?: string | null;
}

/**
 * `qualificationId` picks from the qualifications lookup; `qualificationFreeText` is the fallback
 * when the award is not in it. The read DTO collapses both into `qualificationName`.
 */
export interface CandidateQualificationForm {
  qualificationType: QualificationType;
  qualificationId?: string | null;
  qualificationFreeText?: string | null;
  institution: string;
  dateAwarded: string;
  grade?: string | null;
}

export interface CandidateWorkHistory {
  id: string;
  jobCandidateId: string;
  institutionName: string;
  positionHeld: string;
  startDate: string;
  endDate?: string | null;
  isCurrent: boolean;
  responsibilities?: string | null;
  reasonForLeaving?: string | null;
}

export interface CandidateWorkHistoryForm {
  institutionName: string;
  positionHeld: string;
  startDate: string;
  endDate?: string | null;
  responsibilities?: string | null;
  reasonForLeaving?: string | null;
}

export interface CandidateReferee {
  id: string;
  jobCandidateId: string;
  fullName: string;
  position: string;
  organization: string;
  email: string;
  phone: string;
  relationship: string;
  yearsKnown: number;
}

export type CandidateRefereeForm = Omit<CandidateReferee, 'id' | 'jobCandidateId'>;

export interface CandidateSkill {
  id: string;
  jobCandidateId: string;
  skillId?: string | null;
  skillCatalogueName?: string | null;
  skillName: string;
  proficiency?: ProficiencyLevel | null;
  proficiencyName?: string | null;
  yearsOfExperience?: number | null;
  isCertified: boolean;
  certificationName?: string | null;
}

export interface CandidateSkillForm {
  skillId?: string | null;
  skillName: string;
  proficiency?: ProficiencyLevel | null;
  yearsOfExperience?: number | null;
  isCertified: boolean;
  certificationName?: string | null;
}

export interface CandidateInterest {
  id: string;
  jobCandidateId: string;
  detail: string;
}

export interface CandidateLanguage {
  id: string;
  jobCandidateId: string;
  languageName: string;
  proficiency?: ProficiencyLevel | null;
  proficiencyName?: string | null;
}

export interface CandidateDocument {
  id: string;
  jobCandidateId: string;
  documentType: CandidateDocumentType;
  documentTypeName: string;
  fileName: string;
  /** Legacy only — rows written since the upload gate leave this empty. Download by id instead. */
  filePath: string;
  uploadDate: string;
}

export interface CandidateNote {
  id: string;
  jobCandidateId: string;
  noteText: string;
  isPrivate: boolean;
  createdAt: string;
  createdBy: string;
}

export interface CandidateNoteForm {
  noteText: string;
  isPrivate: boolean;
}

// ══════════════════════════════════════════════════════════════════════════
// JOB APPLICATION
// ══════════════════════════════════════════════════════════════════════════

export interface JobApplicationSummary {
  id: string;
  applicationNumber: string;
  jobVacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  jobCandidateId: string;
  candidateName: string;
  candidateEmail: string;
  applicationDate: string;
  status: ApplicationStatus;
  statusName: string;
  source: ApplicationSource;
  sourceName: string;
  yearsOfExperience?: number | null;
  autoScore?: number | null;
  scoreIsStale: boolean;
  snapshotAvailable: boolean;
  scoredAt?: string | null;
  shortlistedDate?: string | null;
  isShortlisted: boolean;
  isInternalCandidate: boolean;
  aggregatedReviewScore?: number | null;
  currentStageId?: string | null;
  currentStageName?: string | null;
}

export interface JobApplication extends JobApplicationSummary {
  tenantId: string;
  candidateNumber: string;
  candidatePhone: string;
  jobPostingId?: string | null;
  jobPostingChannel?: string | null;
  availableFrom?: string | null;
  coverLetter?: string | null;
  /** JSON string holding a `CriterionScore[]`; parse with `parseScoreBreakdown`. */
  autoScoreBreakdown?: string | null;
  decisionSource?: ShortlistDecisionSource | null;
  decisionSourceName?: string | null;
  shortlistedById?: string | null;
  shortlistedByName?: string | null;
  shortlistingNotes?: string | null;
  waitlistedDate?: string | null;
  waitlistReason?: string | null;
  withdrawnDate?: string | null;
  withdrawalReason?: string | null;
  rejectedDate?: string | null;
  rejectedById?: string | null;
  rejectedByName?: string | null;
  rejectionReason?: string | null;
  internalEmployeeId?: string | null;
  hasOffer: boolean;
  hasHireRecord: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface JobApplicationDetail extends JobApplication {
  stageHistories: ApplicationStageHistory[];
  testResults: ApplicantTestResult[];
  communications: ApplicantCommunication[];
  interviewSlots: unknown[];
  offer?: unknown | null;
  hireRecord?: unknown | null;
}

export interface CreateJobApplication {
  jobVacancyId: string;
  jobCandidateId: string;
  source: ApplicationSource;
  jobPostingId?: string | null;
  yearsOfExperience?: number | null;
  availableFrom?: string | null;
  coverLetter?: string | null;
}

export interface ApplicationStageHistory {
  id: string;
  jobApplicationId: string;
  applicationNumber: string;
  pipelineStageId: string;
  stageName: string;
  stageType: RecruitmentPipelineStageType;
  stageTypeName: string;
  enteredAt: string;
  exitedAt?: string | null;
  exitReason?: StageExitReason | null;
  exitReasonName?: string | null;
  notes?: string | null;
  movedById?: string | null;
  movedByName: string;
  isCurrent: boolean;
}

export interface ApplicantTestResult {
  id: string;
  jobApplicationId: string;
  applicationNumber: string;
  candidateName: string;
  testType: JobApplicantTestType;
  testTypeName: string;
  testName: string;
  testDate: string;
  venue?: string | null;
  score?: number | null;
  maxScore?: number | null;
  scorePercentage?: number | null;
  passed?: boolean | null;
  remarks?: string | null;
  invigilatedById?: string | null;
  invigilatedByName?: string | null;
  markedById?: string | null;
  markedByName?: string | null;
  markedDate?: string | null;
}

export interface CreateApplicantTestResult {
  testType: JobApplicantTestType;
  testName: string;
  testDate: string;
  venue?: string | null;
  score?: number | null;
  maxScore?: number | null;
  passed?: boolean | null;
  remarks?: string | null;
  invigilatedById?: string | null;
}

/** ⚠ Marking only. The test's name, type, date and venue cannot be amended after creation. */
export interface UpdateApplicantTestResult {
  id: string;
  score?: number | null;
  maxScore?: number | null;
  passed?: boolean | null;
  remarks?: string | null;
  markedById?: string | null;
  markedDate?: string | null;
}

export interface ApplicantCommunication {
  id: string;
  jobApplicationId: string;
  applicationNumber: string;
  candidateName: string;
  type: CommunicationType;
  typeName: string;
  direction: CommunicationDirection;
  directionName: string;
  subject: string;
  body: string;
  sentAt: string;
  sentById?: string | null;
  sentByName?: string | null;
  templateId?: string | null;
  externalMessageId?: string | null;
}

export interface CreateApplicantCommunication {
  type: CommunicationType;
  direction: CommunicationDirection;
  subject: string;
  body: string;
  templateId?: string | null;
  externalMessageId?: string | null;
}

// ══════════════════════════════════════════════════════════════════════════
// SCORING, SCREENING AND SHORTLISTING
// ══════════════════════════════════════════════════════════════════════════

export interface CriterionScore {
  criteriaId: string;
  criteriaName: string;
  type?: string;
  isMandatory: boolean;
  weight: number;
  passed: boolean;
  rawScore: number;
  weightedScore: number;
  notes?: string | null;
}

export interface ApplicationAutoScore {
  applicationId: string;
  autoScore: number;
  scoredAt: string;
  allMandatoryPassed: boolean;
  totalWeight: number;
  maxPossibleScore: number;
  breakdown: CriterionScore[];
}

export interface ScoringRunResult {
  vacancyId: string;
  total: number;
  succeeded: number;
  failed: number;
  ranAt: string;
  results: ApplicationAutoScore[];
  errors: { applicationId: string; error: string }[];
}

/**
 * `autoScoreBreakdown` is stored as a JSON string, not an object. Malformed or absent breakdowns
 * read as an empty list rather than throwing — a score without a breakdown is still a score.
 */
export function parseScoreBreakdown(raw?: string | null): CriterionScore[] {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as CriterionScore[]) : [];
  } catch {
    return [];
  }
}

export interface ShortlistSummary {
  vacancyId: string;
  totalApplications: number;
  scored: number;
  staleScores: number;
  neverScored: number;
  shortlisted: number;
  waitlisted: number;
  rejected: number;
  withdrawn: number;
  highestScore?: number | null;
  lowestScore?: number | null;
  averageScore?: number | null;
  isShortlistDeadlinePassed: boolean;
  isShortlistApproved: boolean;
  approvalStatus: ShortlistApprovalStatus;
  approvalStatusName: string;
}

export interface ComparisonCell {
  criteriaId: string;
  criteriaName: string;
  isMandatory: boolean;
  weight: number;
  passed: boolean;
  rawScore: number;
  weightedScore: number;
  notes?: string | null;
}

export interface ComparisonRow {
  applicationId: string;
  candidateName: string;
  applicationNumber: string;
  totalScore?: number | null;
  criterionScores: ComparisonCell[];
}

/**
 * With no `applicationIds` the server compares everyone currently shortlisted.
 *
 * ⚠ `criteria` is empty when the vacancy has no shortlisting criteria defined — in which case there
 * is nothing to compare on, and the screen should say so rather than render an empty grid.
 */
export interface CandidateComparison {
  vacancyId: string;
  criteria: { id: string; criteriaName: string; isMandatory: boolean; weight: number }[];
  candidates: ComparisonRow[];
}

export interface BlindApplicationSummary {
  applicationId: string;
  applicationNumber: string;
  status: ApplicationStatus;
  statusName: string;
  applicationDate: string;
  yearsOfExperience?: number | null;
  autoScore?: number | null;
  scoreIsStale: boolean;
  scoredAt?: string | null;
  isInternalCandidate: boolean;
}

export interface ShortlistDecisionLogEntry {
  id: string;
  jobApplicationId: string;
  applicationNumber: string;
  decisionType: ShortlistDecisionType;
  decisionTypeName: string;
  decisionById?: string | null;
  decisionByName?: string | null;
  decisionAt: string;
  autoScoreAtDecision?: number | null;
  isAutoDecision: boolean;
  notes?: string | null;
  overridesDecisionLogId?: string | null;
}

export interface ShortlistReview {
  id: string;
  jobApplicationId: string;
  applicationNumber: string;
  candidateName?: string | null;
  reviewerId: string;
  reviewerName: string;
  score: number;
  notes?: string | null;
  reviewedAt: string;
  isFinalized: boolean;
  finalizedAt?: string | null;
}

/** Only finalized reviews count toward `aggregatedScore`, and only their author may finalize one. */
export interface AggregatedReviewScore {
  applicationId: string;
  applicationNumber: string;
  candidateName?: string | null;
  aggregatedScore?: number | null;
  reviewerCount: number;
  reviews: ShortlistReview[];
}

export interface EeoStage {
  total: number;
  male: number;
  female: number;
  other: number;
  preferNotToSay: number;
  malePercent?: number | null;
  femalePercent?: number | null;
  averageAge?: number | null;
  internalCandidateCount: number;
}

export interface EeoReport {
  vacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  generatedAt: string;
  byStatus: { status: string; total: number; demographics: EeoStage }[];
  allApplicants: EeoStage;
  shortlisted: EeoStage;
  rejected: EeoStage;
  hired: EeoStage;
}

export interface ShortlistSlaStatus {
  vacancyId: string;
  vacancyNumber: string;
  publishDate?: string | null;
  shortlistingDeadline?: string | null;
  shortlistCompletedAt?: string | null;
  timeToShortlistDays?: number | null;
  shortlistingSlaBreached: boolean;
  daysOverdue?: number | null;
  daysRemaining?: number | null;
  approvalStatus: ShortlistApprovalStatus;
  approvalStatusName: string;
}

/**
 * Per-item results, because a bulk operation is not all-or-nothing: an application that is already
 * shortlisted, past the deadline, or belongs to another vacancy is skipped with its own message
 * while the rest go through.
 */
export interface BulkOperationResult {
  succeeded: number;
  skipped: number;
  results: { applicationId: string; success: boolean; message?: string | null }[];
}

// ══════════════════════════════════════════════════════════════════════════
// PIPELINE BOARD (api/applications)
// ══════════════════════════════════════════════════════════════════════════

export interface PipelineCard {
  applicationId: string;
  candidateId: string;
  applicationNumber: string;
  candidateName: string;
  initials: string;
  currentStageId: string;
  autoScore?: number | null;
  status: ApplicationStatus;
  statusName: string;
  dateApplied: string;
  enteredStageAt: string;
}

export interface PipelineColumn {
  stageId: string;
  stageName: string;
  order: number;
  stageType: RecruitmentPipelineStageType;
  stageTypeName: string;
  applications: PipelineCard[];
}

/** `stageId` is all-zeroes for the synthetic inbox bucket — applications not yet in any stage. */
export const INBOX_STAGE_ID = '00000000-0000-0000-0000-000000000000';

export interface PipelineStageHeader {
  stageId: string;
  stageName: string;
  order: number;
  stageType?: RecruitmentPipelineStageType | null;
  stageTypeName: string;
  applicationCount: number;
  isInbox: boolean;
}

/** `hasPipeline` is false when the vacancy has no pipeline assigned — the board cannot be used. */
export interface PipelineOverview {
  vacancyId: string;
  hasPipeline: boolean;
  stages: PipelineStageHeader[];
  totalApplications: number;
}

export interface PipelineApplicationListItem {
  applicationId: string;
  candidateId: string;
  applicationNumber: string;
  candidateName: string;
  candidateEmail: string;
  status: ApplicationStatus;
  statusName: string;
  source: ApplicationSource;
  sourceName: string;
  yearsOfExperience?: number | null;
  autoScore?: number | null;
  scoreIsStale: boolean;
  dateApplied: string;
  enteredStageAt?: string | null;
  isInternalCandidate: boolean;
  isInbox: boolean;
}

/**
 * ⚠ Declared as a `type`, not an `interface`. `apiService.get`'s query-param argument is
 * `Record<string, unknown>`, and only object *type aliases* get an implicit index signature — an
 * `interface` fails to compile at the call site.
 */
export type StageApplicationsQuery = {
  page?: number;
  pageSize?: number;
  nameSearch?: string;
  /** Numeric enum ordinal, not the name — this endpoint takes `int?`. Use `applicationStatusOrdinal`. */
  status?: number;
  source?: number;
  dateFrom?: string;
  dateTo?: string;
  minScore?: number;
  maxScore?: number;
  sortBy?: 'name' | 'date' | 'score' | 'status';
  sortDesc?: boolean;
};

/**
 * The stage-applications filter takes enum **ordinals** where every other endpoint in the area takes
 * names, because the controller declares those two parameters as `int?`.
 *
 * ⚠ The two enums do not share a base. `ApplicationStatus` starts at `Draft = 0`, so its position in
 * the array *is* its ordinal; `ApplicationSource` starts at `CompanyWebsite = 1`, so it needs the
 * offset. Getting this wrong silently filters by the neighbouring member — a "Job Board" filter
 * quietly returning LinkedIn applicants — rather than failing.
 */
export function applicationStatusOrdinal(status: ApplicationStatus): number {
  return APPLICATION_STATUSES.indexOf(status);
}

export function applicationSourceOrdinal(source: ApplicationSource): number {
  return APPLICATION_SOURCES.indexOf(source) + 1;
}
