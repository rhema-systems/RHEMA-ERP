// Types for HR Training & Learning — Slice 6 (Learning Paths).
//
// A *path* is an ordered curriculum: programmes in sequence, each optionally gated on a prerequisite
// step. Enrolling an employee generates one *step* per programme; completing steps moves the
// enrolment's progress percentage. Mirrors the same-named DTOs in TrainingDTOs.cs.

import type { TrainingLevel, ProficiencyLevel, MaterialType } from './training';
import type { NominationStatus, ScheduleStatus, TrainingCompletionStatus } from './training-delivery';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export type LearningPathStatus = 'Draft' | 'Active' | 'Inactive';

export const LEARNING_PATH_STATUS_OPTIONS = opts<LearningPathStatus>([
  ['Draft', 'Draft'],
  ['Active', 'Active'],
  ['Inactive', 'Inactive'],
]);

// ── Path definition ────────────────────────────────────────────────────────────────────────

export interface LearningPathProgram {
  id: string;
  learningPathId: string;
  learningPathName: string;
  programId: string;
  programCode: string;
  programName: string;
  sequenceOrder: number;
  isMandatory: boolean;
  notes?: string | null;
  /** The step that must be finished first — this is what expresses the sequence. */
  prerequisitePathProgramId?: string | null;
  prerequisiteProgramName?: string | null;
}

export interface LearningPathSkill {
  id: string;
  learningPathId: string;
  skillId: string;
  skillName: string;
  targetProficiency: ProficiencyLevel;
  targetProficiencyName?: string;
}

export interface LearningPath {
  id: string;
  name: string;
  description: string;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  status: LearningPathStatus;
  estimatedDurationDays?: number | null;
  estimatedDurationHours?: number | null;
  providesCertificate: boolean;
  completionCertificateName?: string | null;
  totalProgramsCount: number;
  enrollmentsCount: number;
  /** Server-ordered by sequence. */
  programs: LearningPathProgram[];
  targetSkills: LearningPathSkill[];
}

export interface LearningPathSummary {
  id: string;
  name: string;
  organizationUnitName?: string | null;
  positionTitle?: string | null;
  status: LearningPathStatus;
  estimatedDurationDays?: number | null;
  totalProgramsCount: number;
  providesCertificate: boolean;
}

export interface LearningPathCreate {
  name: string;
  description: string;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  status: LearningPathStatus;
  estimatedDurationDays?: number | null;
  estimatedDurationHours?: number | null;
  providesCertificate: boolean;
  completionCertificateName?: string | null;
}

export type LearningPathUpdate = LearningPathCreate;

export interface LearningPathProgramCreate {
  learningPathId: string;
  programId: string;
  sequenceOrder: number;
  isMandatory: boolean;
  notes?: string | null;
  prerequisitePathProgramId?: string | null;
}

export type LearningPathProgramUpdate = Omit<LearningPathProgramCreate, 'learningPathId' | 'programId'>;

export interface LearningPathSkillCreate {
  learningPathId: string;
  skillId: string;
  targetProficiency: ProficiencyLevel;
}

// ── Enrolment ──────────────────────────────────────────────────────────────────────────────

export interface EmployeeLearningPathStep {
  id: string;
  employeeLearningPathId: string;
  learningPathProgramId: string;
  programName: string;
  sequenceOrder: number;
  isMandatory: boolean;
  isCompleted: boolean;
  completedDate?: string | null;
  /** The nomination that evidences this step — completion is evidenced, not asserted. */
  nominationId?: string | null;
  nominationNumber?: string | null;
  prerequisitePathProgramId?: string | null;
  prerequisiteProgramName?: string | null;
}

export interface EmployeeLearningPath {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  learningPathId: string;
  learningPathName: string;
  enrolledDate: string;
  targetCompletionDate?: string | null;
  actualCompletionDate?: string | null;
  progressPercentage: number;
  isCompleted: boolean;
  assignedById?: string | null;
  assignedByName?: string | null;
  notes?: string | null;
  /** Server-ordered by sequence. */
  steps: EmployeeLearningPathStep[];
}

export interface EmployeeLearningPathSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  /** Added so a "My Learning" row can open the path it refers to. */
  learningPathId: string;
  learningPathName: string;
  enrolledDate: string;
  targetCompletionDate?: string | null;
  progressPercentage: number;
  isCompleted: boolean;
}

/** The richer org-wide enrolment list — carries where the learner sits. */
export interface EnrollmentListItem {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  organizationUnitName?: string | null;
  positionTitle?: string | null;
  learningPathId: string;
  learningPathName: string;
  enrolledDate: string;
  targetCompletionDate?: string | null;
  actualCompletionDate?: string | null;
  progressPercentage: number;
  isCompleted: boolean;
  assignedByName?: string | null;
  notes?: string | null;
}

export interface EnrollEmployeeRequest {
  employeeId: string;
  learningPathId: string;
  enrolledDate: string;
  targetCompletionDate?: string | null;
  /** Defaults server-side to the caller — HR can override to record it on someone's behalf. */
  assignedById?: string | null;
  notes?: string | null;
}

export interface UpdateEnrollmentRequest {
  id: string;
  targetCompletionDate?: string | null;
  notes?: string | null;
}

export interface UpdateLearningPathStepRequest {
  id: string;
  isCompleted: boolean;
  completedDate?: string | null;
  nominationId?: string | null;
  /** Mandatory when HR completes a step with no attendance or completion record behind it. */
  reason?: string | null;
}

// ── The completion audit trail ──────────────────────────────────────────────────────────────

/**
 * How a step's completion was arrived at. Mirrors `LearningPathStepStatus` server-side, and is what
 * the history row's `toStatus` carries — a path can award a certificate that outside parties verify,
 * so "evidenced" and "recorded by HR" must stay distinguishable on the record.
 */
export const LEARNING_PATH_STEP_STATUS = {
  NotCompleted: 0,
  Completed: 1,
  CompletedByOverride: 2,
} as const;

export interface TrainingStatusHistoryEntry {
  id: string;
  createdAt: string;
  entityType: string;
  entityId: string;
  entityReference?: string | null;
  fromStatus?: number | null;
  fromStatusName?: string | null;
  toStatus: number;
  toStatusName: string;
  changedByEmployeeId?: string | null;
  changedByName?: string | null;
  changedAt: string;
  reason?: string | null;
}

// ── Step detail (a purpose-built read model for the learner's step page) ────────────────────

export interface StepMaterial {
  id: string;
  materialName: string;
  type: MaterialType;
  filePath?: string | null;
  externalUrl?: string | null;
  isPublic: boolean;
}

export interface StepScheduleSummary {
  id: string;
  scheduleNumber: string;
  startDate: string;
  endDate: string;
  venue?: string | null;
  venueAddress?: string | null;
  onlineLink?: string | null;
  trainerName?: string | null;
  vendorName?: string | null;
  maxParticipants: number;
  confirmedParticipantsCount: number;
  slotsAvailable: number;
  registrationCloseDate: string;
  status: ScheduleStatus;
  isRegistrationOpen: boolean;
}

export interface StepNomination {
  id: string;
  nominationNumber: string;
  scheduleId: string;
  scheduleNumber: string;
  trainingStartDate: string;
  trainingEndDate: string;
  status: NominationStatus;
  nominationDate: string;
  justification?: string | null;
}

export interface StepAttendance {
  id: string;
  attendanceDate: string;
  isPresent: boolean;
  absenceReason?: string | null;
  checkInTime?: string | null;
  checkOutTime?: string | null;
}

export interface StepCompletion {
  id: string;
  completionDate: string;
  finalScore?: number | null;
  isPassed: boolean;
  status: TrainingCompletionStatus;
  isVerifiedByManager: boolean;
}

export interface StepFeedback {
  id: string;
  contentRelevanceRating?: number | null;
  trainerKnowledgeRating?: number | null;
  deliveryMethodRating?: number | null;
  materialQualityRating?: number | null;
  overallSatisfactionRating?: number | null;
  strengthsOfTraining?: string | null;
  areasForImprovement?: string | null;
  suggestionsForFuture?: string | null;
  wouldRecommend: boolean;
  feedbackDate: string;
}

export interface StepDetailPage {
  stepId: string;
  enrollmentId: string;
  learningPathName: string;
  stepSequence: number;
  totalSteps: number;
  isCompleted: boolean;
  completedDate?: string | null;
  /** True while the prerequisite step is unfinished. */
  isLocked: boolean;
  prerequisiteProgramName?: string | null;
  isMandatory: boolean;
  /** Server-computed: true only when there is attendance or a completion record to justify it. */
  canMarkComplete: boolean;
  /** Whose step this is — not always the caller's, since HR can open it from the org-wide list. */
  learnerId: string;
  learnerName: string;
  isOwnStep: boolean;
  programId: string;
  programCode: string;
  programName: string;
  description: string;
  level: TrainingLevel;
  durationDays: number;
  durationHours: number;
  learningObjectives?: string | null;
  prerequisites?: string | null;
  providesCertificate: boolean;
  certificateName?: string | null;
  materials: StepMaterial[];
  availableSchedules: StepScheduleSummary[];
  myNomination?: StepNomination | null;
  myAttendance: StepAttendance[];
  myCompletion?: StepCompletion | null;
  myFeedback?: StepFeedback | null;
}
