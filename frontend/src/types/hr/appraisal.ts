/**
 * Appraisal cycles and the forms they run on — area 5's second slice.
 *
 * The goal cascade (`types/hr/goals.ts`) only ever *read* a cycle, to scope itself to one.
 * This file owns the cycle itself and everything HR configures before one can run:
 *
 *   settings profile → cycle → targets (who is in scope) → template assignments (which form)
 *   template → sections → items → grade ranges, drawing on criteria and grade definitions
 *
 * Routes are per-controller and inconsistent by design (the port keeps HRApi's shapes):
 *   api/AppraisalCycle, api/AppraisalCycleTarget, api/AppraisalCycleTemplates,
 *   api/AppraisalTemplates, api/AppraisalCompetency, api/AppraisalGradeDefinitions,
 *   api/AppraisalSettings, api/AppraisalNotifications
 *
 * Enums serialize as strings (JsonStringEnumConverter), so every union below is the enum
 * member name and is what the API expects back.
 */
import type { AuditFields } from './common';

// ── Enums ────────────────────────────────────────────────────────────────────────

/** Quarterly/MidYear/Annual/OneOff plus the short new-hire confirmation cycle. */
export type AppraisalType = 'Quarterly' | 'MidYear' | 'Annual' | 'OneOff' | 'Probation';

/**
 * Draft → Open → InProgress → Closed. Only Draft can be deleted, and only a cycle that has
 * never been opened; a closed cycle refuses every edit.
 */
export type AppraisalCycleStatus = 'Draft' | 'Open' | 'InProgress' | 'Closed';

/** What a target group selects. Each row names exactly one of the four scope ids. */
export type AppraisalTargetType = 'OrganizationLevel' | 'OrganizationUnit' | 'Position' | 'Employee';

/** Templates are drafted by a unit and signed off centrally before a cycle may use them. */
export type TemplateApprovalStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';

/** Per-employee verdict from the coverage simulation. Only `Covered` is safe to generate from. */
export type EmployeeCoverageStatus = 'Covered' | 'NoTemplate' | 'Conflict' | 'Excluded';

export type PeerNominationMode = 'Employee' | 'Manager';
export type PeerEvaluationOpenMode = 'WithSelfEval' | 'AfterSelfEval';
export type HRReviewTiming = 'BeforeCalibration' | 'AfterCalibration';
export type ReviewFrequency = 'None' | 'MidYearOnly' | 'Quarterly' | 'Custom';
export type InterimReviewDepth = 'LightTouch' | 'FullAppraisal';

export type ReadinessLevel =
  | 'ReadyNow'
  | 'ReadyIn12Months'
  | 'ReadyIn24Months'
  | 'ReadyIn36PlusMonths';

/** The band an overall score maps to. Optional on a grade definition. */
export type PerformanceRating =
  | 'Outstanding'
  | 'ExceedsExpectations'
  | 'MeetsExpectations'
  | 'BelowExpectations'
  | 'Unsatisfactory';

/** Severity attached to a deadline or bottleneck on the cycle progress dashboard. */
export type RiskLevel = 'None' | 'Low' | 'Medium' | 'High' | 'Critical';

export type NotificationUrgency = 'Normal' | 'Warning' | 'Urgent';

export type AppraisalNotificationType =
  | 'SelfEvalWindowOpen'
  | 'SelfEvalSubmitted'
  | 'PeerNominationSubmitted'
  | 'PeerEvaluationAssigned'
  | 'PeerEvaluationCompleted'
  | 'AllPeerEvalsComplete'
  | 'ManagerEvalSubmitted'
  | 'CalibrationComplete'
  | 'HRReviewApproved'
  | 'ConversationCompleted'
  | 'EmployeeAcknowledged'
  | 'AppealSubmitted'
  | 'AppealResolved'
  | 'DeadlineApproaching'
  | 'DeadlineImminent'
  | 'DeadlinePassed'
  | 'AutoLocked'
  | 'PeerEvaluationReminder'
  | 'ActionRequired'
  // Added with the development / PIP / conversation slice.
  | 'ConversationScheduled'
  | 'DevelopmentPlanActivated'
  | 'DevelopmentFeedbackAdded'
  | 'PipOpened'
  | 'PipMeetingScheduled'
  | 'PipOutcomeRecorded';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const APPRAISAL_TYPE_OPTIONS = opts<AppraisalType>([
  ['Annual', 'Annual'],
  ['MidYear', 'Mid-year'],
  ['Quarterly', 'Quarterly'],
  ['Probation', 'Probation / confirmation'],
  ['OneOff', 'One-off'],
]);

export const APPRAISAL_CYCLE_STATUS_OPTIONS = opts<AppraisalCycleStatus>([
  ['Draft', 'Draft'],
  ['Open', 'Open'],
  ['InProgress', 'In progress'],
  ['Closed', 'Closed'],
]);

export const APPRAISAL_TARGET_TYPE_OPTIONS = opts<AppraisalTargetType>([
  ['OrganizationLevel', 'Organisation level'],
  ['OrganizationUnit', 'Organisation unit'],
  ['Position', 'Position'],
  ['Employee', 'Individual employee'],
]);

export const PEER_NOMINATION_MODE_OPTIONS = opts<PeerNominationMode>([
  ['Employee', 'Employee nominates their own peers'],
  ['Manager', 'Manager nominates the peers'],
]);

export const PEER_EVALUATION_OPEN_MODE_OPTIONS = opts<PeerEvaluationOpenMode>([
  ['WithSelfEval', 'Alongside self-evaluation'],
  ['AfterSelfEval', 'Only after self-evaluation is submitted'],
]);

export const HR_REVIEW_TIMING_OPTIONS = opts<HRReviewTiming>([
  ['BeforeCalibration', 'Before calibration'],
  ['AfterCalibration', 'After calibration (final sign-off)'],
]);

export const REVIEW_FREQUENCY_OPTIONS = opts<ReviewFrequency>([
  ['None', 'None — year-end only'],
  ['MidYearOnly', 'Mid-year only'],
  ['Quarterly', 'Quarterly (Q1, mid-year, Q3)'],
  ['Custom', 'Custom — HR creates review events'],
]);

export const INTERIM_REVIEW_DEPTH_OPTIONS = opts<InterimReviewDepth>([
  ['LightTouch', 'Light touch — progress and a conversation'],
  ['FullAppraisal', 'Full appraisal — scored against the period’s goals'],
]);

export const READINESS_LEVEL_OPTIONS = opts<ReadinessLevel>([
  ['ReadyNow', 'Ready now'],
  ['ReadyIn12Months', 'Ready in 12 months'],
  ['ReadyIn24Months', 'Ready in 24 months'],
  ['ReadyIn36PlusMonths', 'Ready in 36+ months'],
]);

export const PERFORMANCE_RATING_OPTIONS = opts<PerformanceRating>([
  ['Outstanding', 'Outstanding'],
  ['ExceedsExpectations', 'Exceeds expectations'],
  ['MeetsExpectations', 'Meets expectations'],
  ['BelowExpectations', 'Below expectations'],
  ['Unsatisfactory', 'Unsatisfactory'],
]);

// ── Appraisal settings ───────────────────────────────────────────────────────────

/**
 * A named policy profile a cycle runs under: who evaluates, how their scores are weighted,
 * what has to happen before a score is final, and the operational thresholds the dashboards
 * read.
 *
 * ⚠ Self, peer and manager weights must total exactly 1.0, but the API zeroes any weight
 * whose `require…` switch is off *before* checking — so with peer reviews disabled the other
 * two must total 1.0 on their own. `evaluationWeightTotal` below applies the same rule so a
 * form can show the number the server will actually validate.
 */
export interface AppraisalSettings extends AuditFields {
  tenantId: string;
  settingsName: string;

  requireSelfEvaluation: boolean;
  allowSelfSoftSkillRating: boolean;
  selfEvaluationWeight: number;

  requirePeerReviews: boolean;
  peerNominationMode: PeerNominationMode;
  minPeerEvaluators: number;
  maxPeerEvaluators: number;
  peerReviewsAnonymous: boolean;
  allowPeerKpiEvaluation: boolean;
  peerEvaluationWeight: number;
  peerEvaluationOpenMode: PeerEvaluationOpenMode;

  requireManagerEvaluation: boolean;
  managerEvaluationWeight: number;

  showSelfScoreToManager: boolean;
  showPeerScoresToManager: boolean;
  showScoreBreakdownToEmployee: boolean;

  requireCalibration: boolean;

  requireHRReview: boolean;
  hrCanModifyScores: boolean;
  hrReviewTiming: HRReviewTiming;

  requireEmployeeAcknowledgment: boolean;
  allowEmployeeResponse: boolean;
  allowAcknowledgmentWithoutConversation: boolean;

  enableAppeals: boolean;
  appealWindowDays: number;
  appealReevaluationWindowDays: number;

  requireGoalSetting: boolean;
  requireManagerGoalApproval: boolean;
  maxGoalsPerEmployee?: number | null;
  minGoalsPerEmployee?: number | null;

  enableCheckIns: boolean;
  enablePrivateJournal: boolean;

  requireKickOffConversation: boolean;
  requireMidYearConversation: boolean;
  requireFinalConversation: boolean;

  reviewFrequency: ReviewFrequency;
  interimReviewDepth: InterimReviewDepth;
  requireMidYearSelfAssessment: boolean;
  requireGoalProgressUpdateAtReview: boolean;

  /** When on, HR's "advance overdue appraisals" action will move stalled steps along. */
  autoLockOnDeadline: boolean;

  defaultHRReviewerId?: string | null;
  probationExtensionMonths: number;
  managerWorkloadThreshold: number;
  deadlineRiskHighDays: number;
  deadlineRiskMediumDays: number;
  deadlineRiskLowDays: number;
  successionPoolName: string;
  successionDefaultReadiness: ReadinessLevel;
}

export type CreateAppraisalSettings = Omit<AppraisalSettings, keyof AuditFields | 'tenantId'>;
export type UpdateAppraisalSettings = CreateAppraisalSettings & { id: string };

/**
 * The total the API validates against 1.0: a disabled evaluator contributes nothing,
 * whatever weight is still sitting in its field.
 */
export function evaluationWeightTotal(s: {
  requireSelfEvaluation: boolean;
  selfEvaluationWeight: number;
  requirePeerReviews: boolean;
  peerEvaluationWeight: number;
  requireManagerEvaluation: boolean;
  managerEvaluationWeight: number;
}): number {
  const self = s.requireSelfEvaluation ? Number(s.selfEvaluationWeight) || 0 : 0;
  const peer = s.requirePeerReviews ? Number(s.peerEvaluationWeight) || 0 : 0;
  const manager = s.requireManagerEvaluation ? Number(s.managerEvaluationWeight) || 0 : 0;
  return Math.round((self + peer + manager) * 1000) / 1000;
}

// ── Appraisal criteria (soft skills / competencies) ──────────────────────────────

/**
 * A named thing an appraisal can score, other than a KPI — "Teamwork", "Judgement".
 * The controller is `api/AppraisalCompetency` but the field is `criteriaName`; the two names
 * are used interchangeably in the ported code and both are kept here rather than invented over.
 */
export interface AppraisalCriterion extends AuditFields {
  tenantId: string;
  code?: string | null;
  criteriaName: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateAppraisalCriterion {
  code?: string | null;
  criteriaName: string;
  description?: string | null;
  isActive: boolean;
}

export type UpdateAppraisalCriterion = CreateAppraisalCriterion & { id: string };

// ── Grade definitions ────────────────────────────────────────────────────────────

/**
 * A named grade ("A", "Exceeds") that template items score into. The optional overall band
 * additionally maps a whole appraisal's score onto a rating — leave it blank for a grade that
 * is only ever used per item.
 */
export interface AppraisalGradeDefinition extends AuditFields {
  tenantId: string;
  gradeName: string;
  description?: string | null;
  isActive: boolean;
  overallMinScore?: number | null;
  overallMaxScore?: number | null;
  mappedRating?: PerformanceRating | null;
}

export interface CreateAppraisalGradeDefinition {
  gradeName: string;
  description?: string | null;
  isActive: boolean;
  overallMinScore?: number | null;
  overallMaxScore?: number | null;
  mappedRating?: PerformanceRating | null;
}

export type UpdateAppraisalGradeDefinition = CreateAppraisalGradeDefinition & { id: string };

// ── Appraisal templates ──────────────────────────────────────────────────────────

/**
 * The form an appraisal is scored on. Scope is what decides who gets which form: a template
 * naming a position beats one naming a unit, which beats one naming a level, which beats an
 * unscoped (global) one. All three blank means global.
 */
export interface AppraisalTemplate extends AuditFields {
  tenantId: string;
  templateName: string;
  description?: string | null;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  isActive: boolean;
  approvalStatus: TemplateApprovalStatus;
  submittedDate?: string | null;
  approvalDate?: string | null;
  rejectionReason?: string | null;
}

/** List projection: adds the counts and the in-use flag without loading the whole graph. */
export interface AppraisalTemplateSummary extends AuditFields {
  templateName: string;
  description?: string | null;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  isActive: boolean;
  approvalStatus: TemplateApprovalStatus;
  sectionsCount: number;
  totalItemsCount: number;
  /** True once assigned to any cycle — assignment to a live cycle is what freezes edits. */
  hasCycleAssignments: boolean;
}

export interface CreateAppraisalTemplate {
  templateName: string;
  description?: string | null;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  isActive: boolean;
}

export type UpdateAppraisalTemplate = CreateAppraisalTemplate & { id: string };

/** The copy's scope is stated outright; it is not inherited from the source. */
export interface CopyAppraisalTemplate {
  newTemplateName: string;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
}

// ── Template sections and items ──────────────────────────────────────────────────

/**
 * What fills a template section: its own items (`Fixed`), or — on each appraisal — the employee's
 * locked goals (`EmployeeGoals`). A template has at most one goals section, and it takes no items.
 */
export type AppraisalSectionKind = 'Fixed' | 'EmployeeGoals';

/** A weighted group of items. Section weights must total 100 before a template activates. */
export interface AppraisalTemplateSection extends AuditFields {
  tenantId: string;
  appraisalTemplateId: string;
  templateName: string;
  sectionName: string;
  description?: string | null;
  displayOrder: number;
  weight: number;
  kind: AppraisalSectionKind;
}

export interface CreateAppraisalTemplateSection {
  appraisalTemplateId: string;
  sectionName: string;
  description?: string | null;
  displayOrder: number;
  weight: number;
  /** Fixed when omitted; an update that omits it keeps the section's kind. */
  kind?: AppraisalSectionKind;
}

export type UpdateAppraisalTemplateSection = CreateAppraisalTemplateSection & { id: string };

/**
 * One scored line. It is a criterion, a KPI or a free-text question — the API rejects a
 * second copy of the same criterion or KPI anywhere in the template with 409.
 * Item weights must total 100 within each section.
 */
export interface AppraisalTemplateItem extends AuditFields {
  tenantId: string;
  appraisalTemplateSectionId: string;
  sectionName: string;
  competencyId?: string | null;
  competencyName?: string | null;
  kpiDefinitionId?: string | null;
  kpiName?: string | null;
  kpiTargetValue?: number | null;
  kpiMinValue?: number | null;
  kpiMaxValue?: number | null;
  customQuestion?: string | null;
  displayOrder: number;
  weight: number;
  /** Zero blocks activation — every item needs its score-to-grade bands. */
  gradeRangeCount: number;
}

export interface CreateAppraisalTemplateItem {
  appraisalTemplateSectionId: string;
  competencyId?: string | null;
  kpiDefinitionId?: string | null;
  kpiTargetValue?: number | null;
  kpiMinValue?: number | null;
  kpiMaxValue?: number | null;
  customQuestion?: string | null;
  displayOrder: number;
  weight: number;
}

export type UpdateAppraisalTemplateItem = CreateAppraisalTemplateItem & { id: string };

/** A score band on one item. Bands may not overlap and each grade may appear only once. */
export interface TemplateItemGradeRange extends AuditFields {
  tenantId: string;
  appraisalTemplateItemId: string;
  gradeDefinitionId: string;
  gradeName: string;
  lowScore: number;
  highScore: number;
}

/** Replace-all: the payload is the complete set of bands for the item. */
export interface UpsertTemplateItemGradeRanges {
  ranges: { gradeDefinitionId: string; lowScore: number; highScore: number }[];
}

// ── Appraisal cycles ─────────────────────────────────────────────────────────────

/** Every phase date a cycle carries, in pipeline order. Shared by the read and write shapes. */
export interface AppraisalCyclePhaseDates {
  goalSettingOpenDate?: string | null;
  goalSettingDeadline?: string | null;
  q1ReviewOpenDate?: string | null;
  q1ReviewDeadline?: string | null;
  midYearOpenDate?: string | null;
  midYearDeadline?: string | null;
  q3ReviewOpenDate?: string | null;
  q3ReviewDeadline?: string | null;
  peerNominationDeadline?: string | null;
  selfEvaluationOpenDate?: string | null;
  selfEvaluationDeadline?: string | null;
  peerEvaluationOpenDate?: string | null;
  peerEvaluationDeadline?: string | null;
  managerEvaluationOpenDate?: string | null;
  managerEvaluationDeadline?: string | null;
  calibrationOpenDate?: string | null;
  calibrationDeadline?: string | null;
  hrReviewOpenDate?: string | null;
  hrReviewDeadline?: string | null;
  employeeAcknowledgeDeadline?: string | null;
  finalConversationDeadline?: string | null;
}

export interface AppraisalCycle extends AuditFields, AppraisalCyclePhaseDates {
  tenantId: string;
  cycleCode: string;
  cycleName: string;
  year: number;
  appraisalType: AppraisalType;
  startDate: string;
  endDate: string;
  appraisalSettingsId: string;
  appraisalSettingsName?: string | null;
  status: AppraisalCycleStatus;
  openedById?: string | null;
  openedByName?: string | null;
  openedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  closedDate?: string | null;
}

export interface CreateAppraisalCycle extends AppraisalCyclePhaseDates {
  cycleCode: string;
  cycleName: string;
  year: number;
  appraisalType: AppraisalType;
  startDate: string;
  endDate: string;
  appraisalSettingsId: string;
  status: AppraisalCycleStatus;
}

/**
 * ⚠ No `status` — the server ignores it on this path and it is off the type so nobody sends one.
 * A cycle moves between Draft / Open / Closed through the open, close and reopen endpoints, which
 * run the scope-overlap checks and stamp who acted. This form used to post a hardcoded `'Draft'`
 * on every save, so editing an Open cycle's phase dates quietly reverted it to Draft.
 */
export type UpdateAppraisalCycle = Omit<CreateAppraisalCycle, 'status'> & { id: string };

/** What `POST {cycle}/generate-appraisals` reports back. */
export interface GenerateAppraisalsResult {
  appraisalsCreated: number;
  evaluationsCreated: number;
  reviewEventsCreated: number;
  cycle: AppraisalCycle;
}

// ── Cycle targets and their exclusions ───────────────────────────────────────────

/**
 * One rule saying who this cycle covers. `estimatedEmployeeCount` is HR's own planning
 * figure; `activeEmployeeCount` is what the rule actually resolves to right now.
 */
export interface AppraisalCycleTarget extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  appraisalCycleCode?: string | null;
  targetType: AppraisalTargetType;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  estimatedEmployeeCount: number;
  activeEmployeeCount: number;
  notes?: string | null;
  isActive: boolean;
}

export interface CreateAppraisalCycleTarget {
  appraisalCycleId: string;
  targetType: AppraisalTargetType;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  estimatedEmployeeCount: number;
  notes?: string | null;
  isActive: boolean;
}

export type UpdateAppraisalCycleTarget = CreateAppraisalCycleTarget & { id: string };

/** Carves people back out of a target — a unit is in scope except these two positions. */
export interface AppraisalCycleTargetExclusion extends AuditFields {
  tenantId: string;
  appraisalCycleTargetId: string;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  employeeId?: string | null;
  employeeName?: string | null;
  reason: string;
  isActive: boolean;
}

export interface CreateAppraisalCycleTargetExclusion {
  appraisalCycleTargetId: string;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  employeeId?: string | null;
  reason: string;
  isActive: boolean;
}

export type UpdateAppraisalCycleTargetExclusion = CreateAppraisalCycleTargetExclusion & {
  id: string;
};

// ── Cycle ↔ template assignments ─────────────────────────────────────────────────

/**
 * Which forms a cycle may use. Scope comes through from the template — it is not stored
 * here — and `priority` breaks a tie when two templates match the same employee equally.
 */
export interface AppraisalCycleTemplate extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  appraisalTemplateId: string;
  templateName: string;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  priority: number;
  isActive: boolean;
}

export interface CreateAppraisalCycleTemplate {
  appraisalCycleId: string;
  appraisalTemplateId: string;
  priority: number;
  isActive: boolean;
}

export type UpdateAppraisalCycleTemplate = CreateAppraisalCycleTemplate & { id: string };

// ── Coverage preview ─────────────────────────────────────────────────────────────

/** One employee's simulated outcome. Conflicts and gaps are what block generation. */
export interface EmployeeCoverageItem {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionTitle?: string | null;
  unitName?: string | null;
  resolvedTemplateId?: string | null;
  resolvedTemplateName?: string | null;
  /** Which scope level won: Position / OrgUnit / OrgLevel / Global. */
  sourceScope?: string | null;
  templatePriority?: number | null;
  status: EmployeeCoverageStatus;
  conflictingTemplateNames: string[];
  exclusionReason?: string | null;
}

export interface TemplateCoverageBreakdown {
  templateId: string;
  templateName: string;
  priority: number;
  scopeType?: string | null;
  scopeName?: string | null;
  assignedEmployeeCount: number;
}

/**
 * Another cycle of the same type and year competing for some of the same employees.
 *
 * Only `blocksOpening` entries actually refuse an open — those are the ones already running.
 * A draft cycle appraises nobody, so it is reported here as a heads-up while it is still cheap
 * to re-scope, rather than stopping someone at the moment they try to open.
 */
export interface CycleScopeOverlap {
  cycleId: string;
  cycleCode: string;
  cycleName: string;
  status: AppraisalCycleStatus;
  sharedEmployeeCount: number;
  blocksOpening: boolean;
}

/**
 * A dry run of generation. Nothing is written. `isGenerationSafe` is the single thing to
 * check before generating: it is true only when nobody is uncovered and nothing conflicts.
 */
export interface CoveragePreview {
  cycleId: string;
  cycleName: string;
  totalTargetedEmployees: number;
  employeesWithTemplate: number;
  employeesWithoutTemplate: number;
  conflictCount: number;
  excludedCount: number;
  coveragePercentage: number;
  isGenerationSafe: boolean;
  hasActiveTemplates: boolean;
  hasActiveTargets: boolean;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  items: EmployeeCoverageItem[];
  templateBreakdown: TemplateCoverageBreakdown[];
  /** Advisory; only the `blocksOpening` entries would refuse an open. */
  scopeOverlaps: CycleScopeOverlap[];
}

// ── Cycle progress dashboard ─────────────────────────────────────────────────────

export interface ProgressMetric {
  completed: number;
  total: number;
  percentageCompleted: number;
  /** False when the settings profile switched this step off — then it is not a gap. */
  isRequired: boolean;
}

export interface TargetBreakdown {
  organizationLevelTargets: number;
  organizationUnitTargets: number;
  positionTargets: number;
  individualEmployeeTargets: number;
}

export interface DeadlineRisk {
  phase: string;
  deadline: string;
  daysUntilDeadline: number;
  relativeTime: string;
  isOverdue: boolean;
  riskLevel: RiskLevel;
}

export interface Bottleneck {
  category: string;
  description: string;
  count: number;
  icon: string;
  severity: RiskLevel;
}

export interface AppraisalCycleProgress {
  cycleId: string;
  cycleName: string;
  cycleCode: string;
  year: number;
  startDate: string;
  endDate: string;
  status: AppraisalCycleStatus;
  currentPhase: string;
  appraisalSettingsName?: string | null;
  requirePeerReviews: boolean;
  requireHRReview: boolean;
  selfEvaluationDeadline?: string | null;
  peerEvaluationDeadline?: string | null;
  managerEvaluationDeadline?: string | null;
  hrReviewDeadline?: string | null;
  employeeAcknowledgeDeadline?: string | null;
  selfEvaluationProgress: ProgressMetric;
  peerEvaluationProgress: ProgressMetric;
  managerEvaluationProgress: ProgressMetric;
  hrReviewProgress: ProgressMetric;
  totalEmployeesTargeted: number;
  totalEmployeesExcluded: number;
  targetBreakdown: TargetBreakdown;
  employeesNotStartedSelfEvaluation: number;
  peerReviewsPendingPastMidpoint: number;
  managersWithHighWorkload: number;
  deadlineRisks: DeadlineRisk[];
  topBottlenecks: Bottleneck[];
}

/** Derived, not persisted: every dated thing on a cycle, in one list. */
export interface AppraisalCalendarEvent {
  date: string;
  title: string;
  /** "Deadline", "Opens", "Review", "Check-in". */
  category: string;
  phase?: string | null;
  cycleId: string;
  cycleName?: string | null;
}

// ── Notifications ────────────────────────────────────────────────────────────────

/** `timeAgo` arrives empty — the server leaves it to the client, and so does this. */
export interface AppraisalNotification {
  notificationId: string;
  recipientEmployeeId: string;
  type: AppraisalNotificationType;
  title: string;
  message: string;
  subjectEmployeeName?: string | null;
  cycleName?: string | null;
  navigationUrl?: string | null;
  appraisalId?: string | null;
  createdDate: string;
  isRead: boolean;
  readDate?: string | null;
  timeAgo: string;
  urgency: NotificationUrgency;
}

export interface AppraisalNotificationSummary {
  unreadCount: number;
  recentNotifications: AppraisalNotification[];
}
