/**
 * Appraisal outcomes — what an appraisal says should happen next, and the intake records that
 * follow from it. Area 5's fourth slice, and all of area 24.
 *
 * **The chain.** A manager (or HR) proposes a `RecommendationType` on a finished appraisal. HR
 * approves it, and approval *dispatches* it: a registered handler in the owning module creates a
 * real downstream record and the recommendation becomes `Actioned` with a `targetEntityType` /
 * `targetEntityId` back-link. Where each type lands:
 *
 *   MeritIncrease, Bonus                                   → SalaryReviewProposal
 *   Promotion, Demotion, ContractRenewal,
 *   Termination, Recognition                               → EmploymentActionProposal
 *   TrainingNomination                                     → a training request
 *   SuccessionNomination                                   → a succession nomination
 *   PerformanceImprovementPlan                             → a PIP
 *   ConfirmProbation, ExtendProbation                      → the probation record
 *
 * ⚠ A recommendation that comes back **Approved rather than Actioned** means the dispatch
 * failed. It is not a terminal state — it is a work item. Retry it, or action the outcome in its
 * own module. The handlers reuse an existing downstream record, so retrying cannot double up.
 *
 * **The proposals are on the generic workflow engine.** Both are single-writer approval
 * lifecycles whose routing is genuinely a policy decision — who signs off a 3% merit increase is
 * not who signs off a termination — so submit/approve/reject/recall go through
 * `WorkflowApprovalActions`, and the terminal step (Applied / Actioned) stays a direct HR action
 * because it records that another module did the work, not that anyone approved it.
 *
 * ⚠ Submit/approve/reject are inoperable until a `SalaryReviewProposal` /
 * `EmploymentActionProposal` workflow definition has been published — approval authority comes
 * from the definition, not from a role.
 *
 * Routes: `api/AppraisalOutcomeRecommendations`, `api/SalaryReviewProposals`,
 * `api/EmploymentActionProposals`, `api/DeadlineEnforcement`, `api/training-service-bonds`.
 */
import type { AuditFields } from './common';

// ── Recommendations ──────────────────────────────────────────────────────────────

export type RecommendationType =
  | 'MeritIncrease'
  | 'Bonus'
  | 'Promotion'
  | 'TrainingNomination'
  | 'SuccessionNomination'
  | 'PerformanceImprovementPlan'
  | 'ConfirmProbation'
  | 'ExtendProbation'
  | 'ContractRenewal'
  | 'Demotion'
  | 'Termination'
  | 'Recognition';

export type RecommendationStatus =
  | 'Proposed'
  | 'Approved'
  | 'Actioned'
  | 'Rejected'
  | 'Dismissed';

export interface AppraisalOutcomeRecommendation extends AuditFields {
  tenantId: string;
  performanceAppraisalId: string;
  appraisalNumber?: string | null;
  employeeName?: string | null;
  recommendationType: RecommendationType;
  status: RecommendationStatus;
  recommendedById?: string | null;
  recommendedDate?: string | null;
  approvedById?: string | null;
  approvedDate?: string | null;
  actionedDate?: string | null;
  notes?: string | null;
  resolutionNotes?: string | null;
  /** Set once dispatched — the module and row the recommendation became. */
  targetEntityType?: string | null;
  targetEntityId?: string | null;
}

export interface CreateAppraisalOutcomeRecommendation {
  performanceAppraisalId: string;
  recommendationType: RecommendationType;
  notes?: string | null;
}

export interface ResolveRecommendation {
  notes?: string | null;
}

/** Grouping used by the worklist tabs. */
export const OPEN_RECOMMENDATION_STATUSES: RecommendationStatus[] = ['Proposed'];
export const STALLED_RECOMMENDATION_STATUSES: RecommendationStatus[] = ['Approved'];
export const CLOSED_RECOMMENDATION_STATUSES: RecommendationStatus[] = [
  'Actioned',
  'Rejected',
  'Dismissed',
];

/** Which downstream screen a dispatched recommendation points at, by `targetEntityType`. */
export const RECOMMENDATION_TARGET_ROUTES: Record<string, (id: string) => string> = {
  SalaryReviewProposal: (id) => `/hr/performance/proposals/salary-review/${id}`,
  EmploymentActionProposal: (id) => `/hr/performance/proposals/employment-action/${id}`,
};

// ── Salary review proposals ──────────────────────────────────────────────────────

export type SalaryReviewProposalType = 'MeritIncrease' | 'Bonus';

export type SalaryReviewProposalStatus =
  | 'Proposed'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Applied';

export interface SalaryReviewProposal extends AuditFields {
  employeeId: string;
  employeeName?: string | null;
  sourceAppraisalId?: string | null;
  appraisalNumber?: string | null;
  proposalType: SalaryReviewProposalType;
  /** For a merit increase. Required before the proposal can be submitted. */
  proposedPercent?: number | null;
  /** For a bonus. Required before the proposal can be submitted. */
  proposedAmount?: number | null;
  status: SalaryReviewProposalStatus;
  notes?: string | null;
}

export interface UpdateSalaryReviewProposal {
  proposedPercent?: number | null;
  proposedAmount?: number | null;
  notes?: string | null;
}

// ── Employment action proposals ──────────────────────────────────────────────────

export type EmploymentActionType =
  | 'Promotion'
  | 'Demotion'
  | 'ContractRenewal'
  | 'Termination'
  | 'Recognition';

export type EmploymentActionProposalStatus =
  | 'Proposed'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Actioned';

export interface EmploymentActionProposal extends AuditFields {
  employeeId: string;
  employeeName?: string | null;
  sourceAppraisalId?: string | null;
  appraisalNumber?: string | null;
  actionType: EmploymentActionType;
  status: EmploymentActionProposalStatus;
  notes?: string | null;
}

/** Notes carried alongside a decision or a terminal mark. */
export interface ProposalNotes {
  notes?: string | null;
}

/**
 * Where HR goes to finish the job once a proposal is approved. These are the modules that own
 * the real record; the proposal is only the intake note.
 */
export const EMPLOYMENT_ACTION_DESTINATIONS: Record<EmploymentActionType, string> = {
  Promotion: 'Staff promotions',
  Demotion: 'Staff demotions',
  ContractRenewal: 'Employee contracts',
  Termination: 'Employee lifecycle',
  Recognition: 'Awards and nominations',
};

// ── Deadline enforcement ─────────────────────────────────────────────────────────

/**
 * The fine-grained step an appraisal is stuck on. Derived from live state, never stored — the
 * same values `AppraisalSubStatus` uses server-side.
 */
export type AppraisalSubStatus =
  | 'GoalSetting'
  | 'PeerNomination'
  | 'SelfEvaluation'
  | 'PeerEvaluation'
  | 'ManagerEvaluation'
  | 'PendingCalibration'
  | 'CalibrationInProgress'
  | 'PendingHRReview'
  | 'HRReviewInProgress'
  | 'PendingConversation'
  | 'PendingAcknowledgment'
  | 'AppealSubmitted'
  | 'AppealUnderReview'
  | 'AppealResolved'
  | 'Completed'
  | 'Closed'
  /** Taken out of the cycle (D-10) — not being appraised. */
  | 'Withdrawn';

/**
 * Result of the cycle-wide "advance overdue appraisals" sweep.
 *
 * ⚠ It honours the cycle's `autoLockOnDeadline` setting: with that off, nothing is changed and
 * `advanced` is 0 however many appraisals are overdue. Say so on screen rather than reporting a
 * successful no-op.
 */
export interface DeadlineEnforcementResult {
  autoLockEnabled: boolean;
  /** Active appraisals examined in the cycle. */
  evaluated: number;
  advanced: number;
  /** Per-appraisal notes: advanced steps and any failures. */
  messages: string[];
}

export interface ManualAdvanceRequest {
  /** Omit to advance past whatever step is currently blocking. */
  targetSubStatus?: AppraisalSubStatus | null;
  /** Stored verbatim in the audit log. Required. */
  reason: string;
}

export interface ManualAdvanceResult {
  success: boolean;
  errorMessage?: string | null;
  previousSubStatus: AppraisalSubStatus;
  newSubStatus: AppraisalSubStatus;
  previousMajorStatus: string;
  newMajorStatus: string;
  actionsPerformed: string[];
}

// ── Training service bonds ───────────────────────────────────────────────────────

/**
 * A binding service obligation attached to a training nomination: the employer pays for the
 * training, the employee agrees to stay for `bondDurationMonths`, and leaving early owes a
 * pro-rated `repaymentAmount`.
 *
 * Route `api/training-service-bonds`. Reads are open to any authenticated user (there is a
 * `/mine` self-service list); every write is HR.
 */
export type TrainingBondStatus =
  | 'PendingAcceptance'
  | 'Active'
  | 'Fulfilled'
  | 'Breached'
  | 'Settled'
  | 'Waived'
  /** Cancelled before acceptance — e.g. the nomination was withdrawn or rejected. */
  | 'Cancelled';

export interface TrainingServiceBond extends AuditFields {
  nominationId: string;
  nominationNumber?: string | null;
  employeeId: string;
  employeeName?: string | null;
  programId: string;
  programName?: string | null;

  bondDurationMonths: number;
  bondAmount: number;
  currency: string;
  termsText?: string | null;

  status: TrainingBondStatus;
  statusName: string;

  acceptedByEmployee: boolean;
  acceptedDate?: string | null;
  acceptanceRecordedById?: string | null;
  acceptanceRecordedByName?: string | null;
  acceptanceNotes?: string | null;

  bondStartDate?: string | null;
  bondEndDate?: string | null;

  exitDate?: string | null;
  repaymentAmount?: number | null;
  settledDate?: string | null;

  waivedDate?: string | null;
  waivedById?: string | null;
  waiverReason?: string | null;

  notes?: string | null;
  /** Whole months left on the obligation as of today; 0 once served or ended. */
  monthsRemaining: number;
}
