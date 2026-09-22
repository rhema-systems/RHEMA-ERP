// Recruitment tests — round 4, lane E.
//
// ⚠ TWO SHAPES OF A QUESTION, and the split is the whole point of this file:
//
//   RecruitmentTestQuestion   — the AUTHORING shape. Has isCorrect, expectedAnswer, explanation.
//   CandidateTestQuestion     — the SITTING shape. Has none of them, because the SERVER does not
//                               send them. These are not "the same type with fields omitted".
//
// A component that renders a candidate's paper must take CandidateTestQuestion. Typing it as the
// authoring shape would compile, read `isCorrect` as undefined, and hide the day somebody changes
// the projection.

// ── enums (string unions matching the backend's JSON) ──────────────────────────

export const RECRUITMENT_QUESTION_TYPES = [
  'SingleChoice',
  'MultiSelect',
  'TrueFalse',
  'FreeText',
  'Numeric',
] as const;
export type RecruitmentQuestionType = (typeof RECRUITMENT_QUESTION_TYPES)[number];

/** The types the server marks itself. FreeText is the only one it cannot. */
export const AUTO_MARKED_QUESTION_TYPES: readonly RecruitmentQuestionType[] = [
  'SingleChoice',
  'MultiSelect',
  'TrueFalse',
  'Numeric',
];

/** The types that carry a list of choices. */
export const CHOICE_QUESTION_TYPES: readonly RecruitmentQuestionType[] = [
  'SingleChoice',
  'MultiSelect',
  'TrueFalse',
];

export const RECRUITMENT_QUESTION_TYPE_LABELS: Record<RecruitmentQuestionType, string> = {
  SingleChoice: 'Single choice',
  MultiSelect: 'Multiple answers',
  TrueFalse: 'True or false',
  FreeText: 'Written answer',
  Numeric: 'Numeric',
};

export const RECRUITMENT_SITTING_STATUSES = [
  'NotStarted',
  'InProgress',
  'AwaitingMarking',
  'Marked',
  'Expired',
  'Cancelled',
] as const;
export type RecruitmentSittingStatus = (typeof RECRUITMENT_SITTING_STATUSES)[number];

export const RECRUITMENT_SITTING_STATUS_LABELS: Record<RecruitmentSittingStatus, string> = {
  NotStarted: 'Not started',
  InProgress: 'In progress',
  AwaitingMarking: 'Awaiting marking',
  Marked: 'Marked',
  Expired: 'Time expired',
  Cancelled: 'Cancelled',
};

export type JobApplicantTestType = 'Written' | 'Practical';

// ── authoring ─────────────────────────────────────────────────────────────────

export interface RecruitmentTestQuestionOption {
  id: string;
  recruitmentTestQuestionId: string;
  optionText: string;
  /** ⚠ HR-only. Never present on a candidate's payload. */
  isCorrect: boolean;
  displayOrder: number;
}

export interface RecruitmentTestQuestion {
  id: string;
  recruitmentTestId: string;
  recruitmentTestSectionId?: string | null;
  sectionName?: string | null;
  questionText: string;
  questionType: RecruitmentQuestionType;
  questionTypeName: string;
  points: number;
  /** ⚠ HR-only: the right answer for a numeric question, the marking note for a written one. */
  expectedAnswer?: string | null;
  /** ⚠ HR-only. */
  explanation?: string | null;
  displayOrder: number;
  options: RecruitmentTestQuestionOption[];
}

export interface RecruitmentTestSection {
  id: string;
  recruitmentTestId: string;
  name: string;
  description?: string | null;
  displayOrder: number;
}

export interface RecruitmentTest {
  id: string;
  tenantId: string;
  testCode: string;
  name: string;
  description?: string | null;
  instructions?: string | null;
  testType: JobApplicantTestType;
  testTypeName: string;
  durationMinutes?: number | null;
  passMarkPercent?: number | null;
  maxAttempts: number;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  isActive: boolean;
  questionCount: number;
  totalPoints: number;
  /** ⚠ True once anybody has sat it. The builder must be read-only from here. */
  hasSittings: boolean;
  sections: RecruitmentTestSection[];
  questions: RecruitmentTestQuestion[];
  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface CreateRecruitmentTestPayload {
  name: string;
  description?: string | null;
  instructions?: string | null;
  testType: JobApplicantTestType;
  durationMinutes?: number | null;
  passMarkPercent?: number | null;
  maxAttempts: number;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
}

export type UpdateRecruitmentTestPayload = CreateRecruitmentTestPayload & { id: string };

export interface CreateRecruitmentTestSectionPayload {
  recruitmentTestId: string;
  name: string;
  description?: string | null;
  displayOrder: number;
}

export type UpdateRecruitmentTestSectionPayload = CreateRecruitmentTestSectionPayload & { id: string };

export interface CreateRecruitmentTestQuestionOptionPayload {
  optionText: string;
  isCorrect: boolean;
  displayOrder: number;
}

export interface CreateRecruitmentTestQuestionPayload {
  recruitmentTestId: string;
  recruitmentTestSectionId?: string | null;
  questionText: string;
  questionType: RecruitmentQuestionType;
  points: number;
  expectedAnswer?: string | null;
  explanation?: string | null;
  displayOrder: number;
  /**
   * ⚠ A REPLACE-SET on update: the whole list travels and an option left out is removed. Sending
   * `undefined` on a choice question is refused by the server rather than read as "no choices".
   */
  options?: CreateRecruitmentTestQuestionOptionPayload[] | null;
}

export type UpdateRecruitmentTestQuestionPayload = CreateRecruitmentTestQuestionPayload & { id: string };

// ── assignment ────────────────────────────────────────────────────────────────

export interface RecruitmentTestAssignment {
  id: string;
  recruitmentTestId: string;
  testName: string;
  testCode: string;
  durationMinutes?: number | null;
  jobVacancyId?: string | null;
  vacancyNumber?: string | null;
  jobTitle?: string | null;
  jobApplicationId?: string | null;
  applicationNumber?: string | null;
  candidateName?: string | null;
  opensAt?: string | null;
  closesAt?: string | null;
  isRequired: boolean;
  extraAttemptsGranted: number;
  extraAttemptReason?: string | null;
  invitedAt?: string | null;
  sittingCount: number;
  createdAt: string;
}

export interface CreateRecruitmentTestAssignmentPayload {
  recruitmentTestId: string;
  /** ⚠ Exactly one of these two. Both, or neither, is refused. */
  jobVacancyId?: string | null;
  jobApplicationId?: string | null;
  opensAt?: string | null;
  closesAt?: string | null;
  isRequired: boolean;
}

export interface GrantExtraAttemptPayload {
  assignmentId: string;
  extraAttempts: number;
  /** ⚠ Required by the server — a re-sit is granted with a reason on record, or not at all. */
  reason: string;
}

// ── sittings, as HR sees them ─────────────────────────────────────────────────

export interface SittingAnswer {
  id: string;
  questionId: string;
  questionText: string;
  questionType: RecruitmentQuestionType;
  questionTypeName: string;
  questionPoints: number;
  selectedOptionId?: string | null;
  selectedOptionText?: string | null;
  freeTextAnswer?: string | null;
  numericAnswer?: string | null;
  isCorrect: boolean;
  pointsAwarded: number;
  isManuallyMarked: boolean;
  markerComment?: string | null;
}

export interface RecruitmentTestSitting {
  id: string;
  recruitmentTestAssignmentId: string;
  jobApplicationId: string;
  applicationNumber: string;
  candidateName: string;
  testName: string;
  attemptNumber: number;
  status: RecruitmentSittingStatus;
  statusName: string;
  startedAt?: string | null;
  submittedAt?: string | null;
  mustSubmitBy?: string | null;
  autoScore?: number | null;
  manualScore?: number | null;
  finalScore?: number | null;
  totalPoints?: number | null;
  scorePercent?: number | null;
  passed?: boolean | null;
  markedAt?: string | null;
  markerNotes?: string | null;
  /** Set once the sitting has written its row into the applicant test ledger. */
  jobApplicantTestResultId?: string | null;
  awaitingManualMarking: boolean;
  answers: SittingAnswer[];
}

export interface MarkFreeTextAnswerPayload {
  answerId: string;
  pointsAwarded: number;
  markerComment?: string | null;
}

export interface FinaliseSittingPayload {
  sittingId: string;
  markerNotes?: string | null;
}

export interface SittingFilter {
  testId?: string;
  assignmentId?: string;
  applicationId?: string;
  vacancyId?: string;
  status?: RecruitmentSittingStatus;
}

// ── the candidate's shapes ────────────────────────────────────────────────────

/** ⚠ No `isCorrect`. The server does not send one. */
export interface CandidateTestOption {
  id: string;
  optionText: string;
  displayOrder: number;
}

/** ⚠ No `isCorrect`, no `expectedAnswer`, no `explanation`. See the file header. */
export interface CandidateTestQuestion {
  id: string;
  sectionId?: string | null;
  sectionName?: string | null;
  questionText: string;
  questionType: RecruitmentQuestionType;
  questionTypeName: string;
  points: number;
  displayOrder: number;
  options: CandidateTestOption[];
}

export interface CandidateSavedAnswer {
  questionId: string;
  selectedOptionIds: string[];
  freeTextAnswer?: string | null;
  numericAnswer?: string | null;
}

export interface CandidateSitting {
  sittingId: string;
  testName: string;
  instructions?: string | null;
  durationMinutes?: number | null;
  attemptNumber: number;
  status: RecruitmentSittingStatus;
  statusName: string;
  startedAt?: string | null;
  /**
   * ⚠ The deadline the SERVER holds, fixed when the attempt was opened. The countdown counts down
   * to this; it does not compute one of its own, and the server checks it again at submit.
   */
  mustSubmitBy?: string | null;
  submittedAt?: string | null;
  /**
   * ⚠ Returned once per open and required on every write. Opening the attempt again re-issues it
   * and the older window's saves are refused — which is what stops a stale second tab overwriting
   * fresh answers with ten-minute-old ones.
   */
  accessToken?: string | null;
  savedAnswers: CandidateSavedAnswer[];
  questions: CandidateTestQuestion[];
}

export interface CandidateAssessmentSummary {
  assignmentId: string;
  jobApplicationId: string;
  applicationNumber: string;
  jobTitle: string;
  testName: string;
  description?: string | null;
  durationMinutes?: number | null;
  questionCount: number;
  isRequired: boolean;
  opensAt?: string | null;
  closesAt?: string | null;
  canStart: boolean;
  /** Why not, in the server's own words. Show it — a disabled button with no reason is the complaint. */
  blockedReason?: string | null;
  attemptsUsed: number;
  attemptsAllowed: number;
  inProgressSittingId?: string | null;
  lastStatus?: RecruitmentSittingStatus | null;
  lastStatusName?: string | null;
  lastSubmittedAt?: string | null;
  releasedScorePercent?: number | null;
  passed?: boolean | null;
}

export interface SubmitAnswerPayload {
  questionId: string;
  selectedOptionIds: string[];
  freeTextAnswer?: string | null;
  numericAnswer?: string | null;
}

export interface SubmitSittingPayload {
  sittingId: string;
  accessToken: string;
  answers: SubmitAnswerPayload[];
}

export interface CandidateSittingResult {
  sittingId: string;
  status: RecruitmentSittingStatus;
  statusName: string;
  submittedAt?: string | null;
  awaitingMarking: boolean;
  scorePercent?: number | null;
  passed?: boolean | null;
  timedOut: boolean;
  message: string;
}
