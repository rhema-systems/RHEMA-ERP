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
  // Round 4, lane B. Written by the server when HR invites a pooled candidate to apply; it is in
  // the list so the "Record an application" and "Correct the source" dialogs can show what the
  // server already stores, not so a recruiter picks it by hand.
  'TalentPool',
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

/**
 * The types the GENERIC document dropdown offers (round 3, lane C2; decision D-17). A reference
 * letter is attached from the referee it vouches for and an ID scan from the national-ID trio —
 * each upload fixes the type itself — so neither is offered here. The enum members stay: the rows
 * they write are read back through the same type column.
 */
export const CANDIDATE_GENERIC_DOCUMENT_TYPES = CANDIDATE_DOCUMENT_TYPES.filter(
  (t) => t !== 'ReferenceLetter' && t !== 'IdDocument',
);

// LanguageProficiency — HREnums.cs (Basic=1 … Native=5). Not the skill ladder above.
export const LANGUAGE_PROFICIENCIES = [
  'Basic',
  'Conversational',
  'ProfessionalWorking',
  'Fluent',
  'Native',
] as const;
export type LanguageProficiency = (typeof LANGUAGE_PROFICIENCIES)[number];

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
  /** Round 4, lane A — resolved from the area, so the list can print "Tema, Greater Accra". */
  region?: string | null;
  countryName: string;
  isInTalentPool: boolean;
  /** A photograph is on file — fetch `GET /job-candidates/{id}/photo` only then (round 3, lane C2). */
  hasPhoto: boolean;
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
  /** ⚠ Round 4, lane A — a display snapshot the server rewrites from `geoAreaId` when one is set. */
  city: string;
  /** ⚠ Round 4, lane A — display snapshot, as `city`. Never sent on a write; it is derived. */
  region?: string | null;
  /**
   * The candidate's administrative area — the lowest tier they chose, from the shared geography
   * tree. The edit form re-opens its cascade by asking the server for this area's ancestors.
   */
  geoAreaId?: string | null;
  nationality?: string | null;
  /** Optional since slice 13b: an internal candidate is a shadow record with no country on file. */
  countryId?: string | null;
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

  // National identity (round 3, lane C1) — the employee's trio; the type is a seeded
  // IdentificationType ("Ghana Card" is a row, not a column).
  nationalIdTypeId?: string | null;
  nationalIdTypeName?: string | null;
  nationalIdNumber?: string | null;
  nationalIdExpiryDate?: string | null;

  cvFilePath?: string | null;
  profilePhotoUrl?: string | null;
  /** A photograph is on file — derived from the gated upload record (round 3, lane C2). */
  hasPhoto: boolean;
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
  /**
   * ⚠ Round 4, lane A — optional now, and the server overwrites it from `geoAreaId` when one is
   * sent. Send one or the other: a payload with neither is refused, by the form and by the server.
   */
  city?: string | null;
  /** The chosen administrative area. Null clears it — this payload replaces the address wholesale. */
  geoAreaId?: string | null;
  /**
   * The candidate's nationality, as free text.
   *
   * ⚠ G-7.3: settable since 2026-09-15. It existed on the read DTO alone, so the Personal card
   * rendered it and nothing could ever write it — the only assignment anywhere in the solution was
   * the TDC demo seeder, which sets "Ghanaian". On a real tenant the row always read "—"; on the
   * demo tenant it always looked fine. Distinct from `countryId`, which is where they are.
   */
  nationality?: string | null;
  /** Optional since 2026-09-14, matching the entity — send null for "no country". */
  countryId?: string | null;
  linkedInProfile?: string | null;
  portfolioUrl?: string | null;
  gitHubUrl?: string | null;
  /** Round 3, lane C1: refused (422) for a type the tenant does not accept. */
  nationalIdTypeId?: string | null;
  nationalIdNumber?: string | null;
  nationalIdExpiryDate?: string | null;
  /**
   * The professional profile and availability — round 4, lane B.
   *
   * ⚠ These were readable on the candidate DTO and writable ONLY through the candidate's own
   * portal profile, while the talent pool's match rubric scores on three of them. So a candidate
   * HR typed in — a career fair, a referral, an unsolicited CV — could never rank above the
   * "nothing on file" tier, whatever HR knew, and there was no box to put it in.
   *
   * ⚠ Sent on EVERY save. The update replaces the record wholesale, so omitting one clears it.
   */
  headline?: string | null;
  professionalSummary?: string | null;
  currentJobTitle?: string | null;
  currentEmployer?: string | null;
  totalYearsExperience?: number | null;
  noticePeriodDays?: number | null;
  availableFrom?: string | null;
  preferredWorkArrangement?: PreferredWorkArrangement;
  isInTalentPool: boolean;
}

/** HR corrects how an application arrived (round 3, lane A) — PATCH job-applications/{id}/source. */
export interface UpdateJobApplicationSource {
  source: ApplicationSource;
  /** Must be one of the application's vacancy's adverts; null clears it. */
  jobPostingId?: string | null;
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
  /** Round 4, lane Q: the rung the row itself states — what a form edits. */
  qualificationLevelId?: string | null;
  /** The rung the engine scores: the row's own, or else its catalogue entry's. */
  effectiveQualificationLevelId?: string | null;
  effectiveQualificationLevelName?: string | null;
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
  /**
   * Round 4, lane Q (decision Q-D1): the rung of the qualification ladder. The server refuses an
   * Education row without one, unless the catalogue entry picked already sits on a rung.
   */
  qualificationLevelId?: string | null;
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
  /**
   * The relationship-catalogue row behind `relationship` (round 2, lane D2).
   *
   * ⚠ `relationship` already carries the row's NAME — the server mirrors it on every save — so this
   * is for re-opening the dropdown, not for display. PROFESSIONAL and OTHER values only: a
   * candidate may name a pastor or a family friend, not their mother.
   */
  relationshipTypeId?: string | null;
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
  // Round 3, lane C1 — cleared together with the name whenever `isCertified` is off.
  certificationNumber?: string | null;
  certifyingBody?: string | null;
  certificationExpiryDate?: string | null;
}

export interface CandidateSkillForm {
  skillId?: string | null;
  skillName: string;
  proficiency?: ProficiencyLevel | null;
  yearsOfExperience?: number | null;
  isCertified: boolean;
  certificationName?: string | null;
  certificationNumber?: string | null;
  certifyingBody?: string | null;
  certificationExpiryDate?: string | null;
}

export interface CandidateInterest {
  id: string;
  jobCandidateId: string;
  detail: string;
}

/**
 * A language the candidate speaks (round 3, lane C1). `languageId` is the catalogue row when one
 * was picked; `languageName` is ALWAYS filled — mirrored from the catalogue, or typed when the
 * language is not listed. The proficiency scale is the language one, not the skill ladder.
 */
export interface CandidateLanguage {
  id: string;
  jobCandidateId: string;
  languageId?: string | null;
  languageCode?: string | null;
  languageName: string;
  proficiency: LanguageProficiency;
  proficiencyName?: string | null;
}

/** Either `languageId` or `languageName`; the server refuses neither, and an id it does not hold. */
export interface CandidateLanguageForm {
  languageId?: string | null;
  languageName?: string | null;
  proficiency: LanguageProficiency;
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
  /** What the file is, in the uploader's words (round 3, lane C1). */
  description?: string | null;
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
  /**
   * Whether the candidate has a photograph on file (round 4, lane B5).
   *
   * ⚠ A flag, not the image. Feed it to `GatedPhoto`'s `enabled` so a list of thirty applications
   * does not fire thirty requests that each come back 404 — the photograph itself streams through
   * the gated `jobCandidateService.photoUrl(candidateId)`.
   */
  candidateHasPhoto: boolean;
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
  /** The advert's own title (round 3, lane A). */
  jobPostingTitle?: string | null;
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

// ── internal job board ──────────────────────────────────────────────────────
// The one exception to this file's HR gate — see `api/job-applications`' class doc comment.
// All three act on the caller's own applications only, taking the employee from the token.

export interface InternalApplyForVacancy {
  vacancyId: string;
  yearsOfExperience?: number | null;
  availableFrom?: string | null;
  coverLetter?: string | null;
}

export type InternalSaveDraft = InternalApplyForVacancy;

export interface InternalSubmitDraft {
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
  /**
   * False for a criterion the engine did not score — `Other`, an empty one, an unanswerable
   * numeric bound. Its weight is left out of the total, so it neither lifts nor lowers the
   * candidate (round 3 lane K, extended by round 4 lane A).
   *
   * ⚠ Server-sent since lane A and simply absent from this type until lane B, so `passed: true`
   * on such a row reads as a pass it never was. Check this before the tick.
   */
  autoEvaluated?: boolean;
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
  /** @see JobApplicationSummary.candidateHasPhoto — a flag, not the image (round 4, lane B5). */
  candidateHasPhoto: boolean;
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

/**
 * One internal application as the APPLICANT sees it (area 25 slice 13b).
 *
 * Transcribed from a live `GET api/job-applications/my-applications/{id}` (`probe-slice13b.mjs`).
 *
 * ⚠ Deliberately not {@link JobApplicationDetail}. That is the recruiter's view: it carries
 * `autoScore` and `autoScoreBreakdown`, `shortlistingNotes`, `shortlistedByName`,
 * `rejectedByName`, the applicant-communication log and the test results. Telling candidates
 * where they stand is not the same act as handing them the scoring they were assessed under, or
 * the internal notes written about them. If a screen needs a field that is not here, add it to
 * the server projection on purpose — do not reach for the detail type.
 *
 * `rejectionReason` IS here, following the module's precedent from slice 6 (a training request's
 * rejection reason finally reaching the person who asked for it).
 */
export interface MyJobApplication {
  id: string;
  applicationNumber: string;

  jobVacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  positionTitle: string;
  orgUnitName?: string | null;
  applicationDeadline?: string | null;

  status: ApplicationStatus;
  statusName: string;
  applicationDate: string;

  /** What the applicant themselves sent. */
  yearsOfExperience?: number | null;
  availableFrom?: string | null;
  coverLetter?: string | null;

  isShortlisted: boolean;
  shortlistedDate?: string | null;
  withdrawnDate?: string | null;
  withdrawalReason?: string | null;
  rejectedDate?: string | null;
  rejectionReason?: string | null;

  /** Computed server-side, so the screen does not re-derive the terminal states. */
  canWithdraw: boolean;
}
