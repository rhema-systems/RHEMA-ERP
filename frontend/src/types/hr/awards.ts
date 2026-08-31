/**
 * Staff Awards & Recognition (area 14). Backend routes: `api/Awards` and `api/awards/me`.
 *
 * ⚠ **Every shape here was transcribed from a live payload**, captured by
 * `dev-harness/hr-awards/probe-ui-payloads.mjs`, not from the C# DTO names. Area 12's lesson: a
 * TypeScript type written from an endpoint name is fiction that type-checks — `tsc` verifies the
 * screen against the invented interface and the cells render blank at runtime with nothing failing.
 *
 * Three things the probe corrected that a hand-written type would have got wrong:
 *
 *   1. **Enums come back as STRINGS**, with a separate `*Name` field beside them for display. A
 *      union of numbers would have compiled and matched nothing.
 *   2. **`AwardCommitteeMember.role` is free text**, max 100 — not an enum. It is whatever HR typed.
 *   3. **Two paging envelopes are in play.** Most lists use HR's `PagedResult` (`totalCount` /
 *      `hasNext` / `hasPrevious`). The eligibility endpoint carries three extra unfiltered counts,
 *      so it cannot be a `PagedResult` — but it deliberately uses the same words.
 */
import type { PagedResult } from './common';

// ─── enums, as the wire actually carries them ────────────────────────────────

export type AwardCategory =
  | 'Performance'
  | 'LongService'
  | 'Innovation'
  | 'CustomerService'
  | 'Safety'
  | 'TeamPlayer'
  | 'Leadership'
  | 'SpecialRecognition'
  | 'Other';

export type AwardFrequency = 'Monthly' | 'Quarterly' | 'Annually' | 'AdHoc';

/** Where an award's candidates come from (D-3, decided in slice 2). */
export type AwardNominationSource =
  | 'OpenNomination'
  | 'ManagementDirect'
  | 'PerformanceTriggered';

/** How the winner is picked (D-3). The two axes are independent. */
export type AwardWinnerDecision = 'StaffVote' | 'CommitteeScore' | 'ManagementDecision';

export type AwardCycleStatus = 'Draft' | 'Published' | 'Closed' | 'Cancelled';

export type AwardNominationStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Approved'
  | 'Rejected'
  | 'Withdrawn';

/**
 * Where one employee stands against a long-service ladder.
 *
 * ⚠ `AlreadyGranted` and `NotYetAtMilestone` are deliberately distinct. Collapsed into "not
 * eligible", a thirty-year veteran and a new joiner look identical on the page.
 */
export type LongServiceStanding =
  | 'Eligible'
  | 'Exempt'
  | 'NotYetAtMilestone'
  | 'AlreadyGranted'
  | 'ServiceUnknown';

// ─── award types ─────────────────────────────────────────────────────────────

/** Row shape of `GET /api/Awards/types`. Note: no `description` — that is detail-only. */
/**
 * The lean self projection served by `GET api/awards/me/types/{id}` (area 25 slice 9) — what a
 * NOMINATOR needs to know about an award. The administrative side of the catalogue (eligibility
 * windows, budgets, review configuration) is deliberately absent.
 */
export interface MyAwardType {
  id: string;
  name: string;
  description: string;
  categoryName: string;
  frequencyName: string;
  isTeamAward: boolean;
  allowSelfNomination: boolean;
  hasMonetaryReward: boolean;
  hasCertificate: boolean;
  hasTrophy: boolean;
  hasLevels: boolean;
  nominationSource: AwardNominationSource;
  nominationSourceName: string;
  winnerDecision: AwardWinnerDecision;
  winnerDecisionName: string;
}

export interface AwardTypeSummary {
  id: string;
  code: string;
  name: string;
  category: AwardCategory;
  categoryName: string;
  frequency: AwardFrequency;
  frequencyName: string;
  isTeamAward: boolean;
  hasMonetaryReward: boolean;
  minMonetaryAmount: number | null;
  maxMonetaryAmount: number | null;
  hasLevels: boolean;
  requiresFormalReview: boolean;
  nominationSource: AwardNominationSource;
  nominationSourceName: string;
  winnerDecision: AwardWinnerDecision;
  winnerDecisionName: string;
  isActive: boolean;
  /** How many awards have been conferred under this type. */
  awardCount: number;
}

export interface AwardType {
  id: string;
  tenantId: string;
  code: string;
  name: string;
  description: string;
  category: AwardCategory;
  categoryName: string;
  frequency: AwardFrequency;
  frequencyName: string;
  isTeamAward: boolean;

  // Eligibility criteria. Every one of these was inert until slice 3 — they existed on the entity
  // and nothing read them.
  minServiceYears: number | null;
  maxServiceYears: number | null;
  minAge: number | null;
  maxAge: number | null;
  maxAwardsPerPeriod: number | null;
  maxAwardsPerEmployee: number | null;

  hasMonetaryReward: boolean;
  minMonetaryAmount: number | null;
  maxMonetaryAmount: number | null;
  hasCertificate: boolean;
  hasTrophy: boolean;
  leaveDaysBonus: number | null;
  hasLevels: boolean;

  requiresFormalReview: boolean;
  minRequiredReviewers: number | null;

  nominationSource: AwardNominationSource;
  nominationSourceName: string;
  winnerDecision: AwardWinnerDecision;
  winnerDecisionName: string;

  /** Off by default. Enterprise norm is peer or manager nomination; see the entity remarks. */
  allowSelfNomination: boolean;

  /** AWD-15. Off by default — TDC stated the rule for long service only. */
  disqualifyOnDisciplinaryRecord: boolean;
  /** Months a disciplinary record reaches back. **Null means forever**, which is the rule as TDC
   *  wrote it; a value *relaxes* it. */
  disqualifyingDisciplineMonths: number | null;

  minPerformanceScore: number | null;
  minGoalsAchieved: number | null;

  notes: string | null;
  isActive: boolean;

  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface AwardLevel {
  id: string;
  tenantId: string;
  awardTypeId: string;
  code: string;
  name: string;
  description: string | null;
  rank: number;
  monetaryAmount: number | null;
  leaveDaysBonus: number | null;
  benefits: string | null;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface AwardBudget {
  id: string;
  tenantId: string;
  budgetCode: string;
  awardTypeId: string;
  awardTypeName: string;
  year: number;
  budgetAmount: number;
  /** Paid out. Nothing incremented this before slice 8. */
  spentAmount: number;
  /** Conferred but unpaid — a real commitment against the year. */
  reservedAmount: number;
  /** `budgetAmount - spentAmount - reservedAmount`. */
  availableAmount: number;
  notes: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

// ─── cycles ──────────────────────────────────────────────────────────────────

export interface AwardCycleSummary {
  id: string;
  cycleCode: string;
  name: string;
  awardTypeId: string;
  awardTypeName: string;
  year: number;
  quarter: number | null;
  month: number | null;
  status: AwardCycleStatus;
  statusName: string;
  /** Computed server-side from the windows and the clock — do not re-derive from the dates. */
  isNominationOpen: boolean;
  isVotingOpen: boolean;
  nominationCount: number;
}

// ─── nominations ─────────────────────────────────────────────────────────────

export interface AwardNominationSummary {
  id: string;
  nominationNumber: string;
  awardTypeId: string;
  awardTypeName: string;
  nomineeName: string;
  nominatedByName: string;
  nominationDate: string;
  year: number;
  teamName: string | null;
  quarter: number | null;
  month: number | null;
  status: AwardNominationStatus;
  statusName: string;
}

export interface AwardNomination {
  id: string;
  tenantId: string;
  nominationNumber: string;

  awardTypeId: string;
  awardTypeName: string;
  awardCycleId: string | null;
  awardCycleName: string | null;
  committeeId: string | null;
  committeeName: string | null;
  awardLevelId: string | null;
  awardLevelName: string | null;

  nomineeId: string | null;
  nomineeName: string;
  nomineeEmployeeNumber: string | null;
  nomineeDepartment: string | null;
  nominatedById: string;
  nominatedByName: string;

  nominationDate: string;
  year: number;
  teamName: string | null;
  quarter: number | null;
  month: number | null;

  justification: string;
  proposedMonetaryAmount: number | null;
  proposedLeaveDays: number | null;

  status: AwardNominationStatus;
  statusName: string;
  outcomeDate: string | null;
  outcomeReason: string | null;

  /** Set once the nomination has produced an award. Both directions are closed by hand — EF treats
   *  them as two independent one-way relationships. */
  awardId: string | null;
  awardNumber: string | null;

  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

/**
 * A score one committee member gave one nomination.
 *
 * ⚠ `score`, not a verdict. Until slice 6 this carried `approved: boolean | null`, which cannot
 * express "the highest average wins" — AWD-12/13. The score-write endpoints were removed from the
 * HR desk controller at the same time: a member scores from their own surface, never on someone
 * else's behalf.
 */
export interface AwardCommitteeReview {
  id: string;
  tenantId: string;
  awardNominationId: string;
  nominationNumber: string;
  reviewerId: string;
  reviewerName: string;
  reviewDate: string;
  score: number;
  comments: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

/**
 * What a committee member still owes a score on.
 *
 * ⚠ The same shape as any other nomination summary — measured, not assumed. `GetPendingReviewsAsync`
 * returned nothing at all until slice 6, so this list was permanently empty.
 */
export type AwardPendingReview = AwardNominationSummary;

// ─── conferred awards ────────────────────────────────────────────────────────

export interface EmployeeAwardSummary {
  id: string;
  awardNumber: string;
  employeeName: string;
  employeeNumber: string;
  awardTypeName: string;
  /** ⚠ Both present since slice 8. Before it, a Gold and a Bronze award were indistinguishable. */
  awardLevelId: string | null;
  awardLevelName: string | null;
  awardDate: string;
  monetaryAmount: number | null;
  presentationDate: string | null;
}

// ─── committees ──────────────────────────────────────────────────────────────

export interface AwardCommitteeMember {
  id: string;
  tenantId: string;
  committeeId: string;
  committeeName: string;
  employeeId: string;
  employeeName: string;
  /** ⚠ Null on every read until slice 11 — the mapper never set it. */
  employeeNumber: string | null;
  /** Free text, max 100. **Not** an enum. */
  role: string | null;
  startDate: string;
  endDate: string | null;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface AwardCommittee {
  id: string;
  tenantId: string;
  name: string;
  description: string | null;
  quorumRequired: number;
  reviewDeadlineDays: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  isActive: boolean;
  memberCount: number;
  /** Populated only by `…/with-members`; an empty array on the plain list. */
  members: AwardCommitteeMember[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

// ─── eligibility (D-9) ───────────────────────────────────────────────────────

export interface AwardEligibilityVerdict {
  employeeId: string;
  employeeName: string;
  employeeNumber: string | null;
  isEligible: boolean;
  /** Empty when eligible. One entry per criterion failed. */
  reasons: string[];
}

/**
 * `GET /api/Awards/types/{id}/eligible`.
 *
 * ⚠ **Not** a `PagedResult`, because it carries three counts that are computed over *everybody*
 * regardless of the filter or the page — "12 eligible" beside a page of 12 rows says nothing, while
 * "12 of 5,579" says everything. It uses `PagedResult`'s vocabulary anyway so the module speaks one
 * paging language.
 */
export interface AwardEligibilityResult {
  awardTypeId: string;
  awardTypeName: string;
  asOf: string;

  /** Active employees considered — the denominator behind the two counts below. */
  consideredCount: number;
  eligibleCount: number;
  ineligibleCount: number;

  items: AwardEligibilityVerdict[];
  filter: 'all' | 'eligible' | 'ineligible';
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

/** Who an employee may nominate. A plain paged list — no ineligible counts to leak. */
export type AwardCandidatePage = PagedResult<AwardEligibilityVerdict>;

// ─── long service (AWD-14 / AWD-15) ──────────────────────────────────────────

export interface LongServiceMilestone {
  id: string;
  tenantId: string;
  awardTypeId: string;
  awardTypeName: string | null;
  years: number;
  name: string | null;
  /** ⚠ Null on a seeded ladder. That is an unanswered question, **not** a value of zero — TDC has
   *  not said what a rung is worth. Do not render it as 0. */
  monetaryAmount: number | null;
  leaveDaysBonus: number | null;
  benefits: string | null;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface LongServiceLadderSeedResult {
  awardTypeId: string;
  /** `Default (D-8)` or `Supplied by the caller`. */
  source: string;
  created: number[];
  alreadyPresent: number[];
  /** The tenant-wide list, offered for a one-click alternative — never applied automatically. */
  companyPolicyYears: number[];
}

export interface LongServiceCandidate {
  employeeId: string;
  employeeName: string;
  employeeNumber: string | null;
  /** Completed years actually served — can exceed the rung reached. */
  yearsOfService: number;
  milestoneYears: number;
  milestoneId: string;
  serviceStartDate: string | null;
  /** Null when qualified; why not when exempt. */
  reason: string | null;
}

export interface LongServiceSweepResult {
  awardTypeId: string;
  asOf: string;
  /** False for a preview. The preview and the run are the same calculation. */
  committed: boolean;
  milestonesConfigured: number;
  employeesConsidered: number;
  /** ⚠ 3,476 of 5,579 on the live tenant. Show this — a qualified count without it describes the
   *  data while reading as a statement about staff. */
  withoutEmploymentDate: number;
  /** Whether AWD-15 ran at all. Distinct from how many it caught: "ran and exempted nobody" and
   *  "switched off" look identical on screen and mean opposite things. */
  disciplinaryCheckApplied: boolean;
  awardsCreated: number;
  qualified: LongServiceCandidate[];
  disqualified: LongServiceCandidate[];
}

export interface LongServiceAwardSummary {
  id: string;
  employeeName: string;
  employeeNumber: string | null;
  /** The rung granted, not the service given — what a certificate prints. */
  yearsOfService: number;
  milestoneDate: string;
  monetaryAmount: number | null;
  isProcessed: boolean;
  presentationDate: string | null;
}

// ─── write payloads ──────────────────────────────────────────────────────────

export interface CreateLongServiceMilestone {
  awardTypeId: string;
  years: number;
  name?: string | null;
  monetaryAmount?: number | null;
  leaveDaysBonus?: number | null;
  benefits?: string | null;
  isActive: boolean;
}

export interface UpdateLongServiceMilestone {
  id: string;
  years: number;
  name?: string | null;
  monetaryAmount?: number | null;
  leaveDaysBonus?: number | null;
  benefits?: string | null;
  isActive: boolean;
}

export interface SeedLongServiceLadder {
  /** Empty or omitted seeds D-8's 10/15/20/25/30. */
  years?: number[];
}

export interface CreateAwardNomination {
  awardTypeId: string;
  awardCycleId?: string | null;
  nomineeId?: string | null;
  teamName?: string | null;
  year: number;
  justification: string;
  proposedMonetaryAmount?: number | null;
  proposedLeaveDays?: number | null;
}

// ─── ballot & voting ─────────────────────────────────────────────────────────

export interface AwardBallotOption {
  nominationId: string;
  nominationNumber: string;
  nomineeId: string | null;
  nomineeName: string;
  teamName: string | null;
  justification: string;
}

/** What an employee sees when they come to vote. */
export interface AwardBallot {
  awardCycleId: string;
  cycleName: string;
  awardTypeId: string;
  awardTypeName: string;
  votingOpensOn: string | null;
  votingClosesOn: string | null;
  isVotingOpen: boolean;
  /** Whether this employee is in the electorate. An award with no electorate target admits everyone. */
  isInElectorate: boolean;
  /** Null means they have not voted. */
  myVoteNominationId: string | null;
  options: AwardBallotOption[];
}

export interface AwardVote {
  id: string;
  awardCycleId: string;
  awardNominationId: string;
  voterId: string;
  justification: string | null;
  votedAt: string;
}

export interface CastAwardVote {
  awardNominationId: string;
  justification?: string | null;
}

/**
 * The tally.
 *
 * ⚠ Withheld until the voting window closes — see the voting service. A screen must not treat a
 * missing tally as "nobody voted".
 */
export interface AwardVoteResult {
  awardCycleId: string;
  cycleName: string;
  isVotingOpen: boolean;
  totalVotes: number;
  winningNominationId: string | null;
  winnerName: string | null;
  isTied: boolean;
  tally: { nominationId: string; nomineeName: string; votes: number }[];
}

/** The committee's average scores. Ties are reported, never broken. */
export interface AwardCommitteeResult {
  awardCycleId: string;
  cycleName: string;
  minRequiredReviewers: number | null;
  winningNominationId: string | null;
  winnerName: string | null;
  isTied: boolean;
  scores: {
    nominationId: string;
    nomineeName: string;
    averageScore: number;
    reviewerCount: number;
    meetsMinimumReviewers: boolean;
  }[];
}

export interface SubmitCommitteeReview {
  score: number;
  comments?: string | null;
}

// ─── write payloads: catalogue ───────────────────────────────────────────────

export interface UpsertAwardType {
  code: string;
  name: string;
  description: string;
  category: AwardCategory;
  frequency: AwardFrequency;
  isTeamAward: boolean;
  nominationSource: AwardNominationSource;
  winnerDecision: AwardWinnerDecision;
  allowSelfNomination: boolean;
  disqualifyOnDisciplinaryRecord: boolean;
  disqualifyingDisciplineMonths?: number | null;
  minServiceYears?: number | null;
  maxServiceYears?: number | null;
  minAge?: number | null;
  maxAge?: number | null;
  maxAwardsPerPeriod?: number | null;
  maxAwardsPerEmployee?: number | null;
  hasMonetaryReward: boolean;
  minMonetaryAmount?: number | null;
  maxMonetaryAmount?: number | null;
  hasCertificate: boolean;
  hasTrophy: boolean;
  leaveDaysBonus?: number | null;
  hasLevels: boolean;
  requiresFormalReview: boolean;
  minRequiredReviewers?: number | null;
  minPerformanceScore?: number | null;
  minGoalsAchieved?: number | null;
  notes?: string | null;
  isActive: boolean;
}

export interface UpsertAwardLevel {
  awardTypeId: string;
  code: string;
  name: string;
  description?: string | null;
  rank: number;
  monetaryAmount?: number | null;
  leaveDaysBonus?: number | null;
  benefits?: string | null;
  isActive: boolean;
}

/** Who an award is scoped to. `targetType` is `AwardScope`; `purpose` says eligibility vs electorate. */
export interface UpsertAwardTarget {
  awardTypeId: string;
  targetType: number;
  targetId?: string | null;
  isExclusion: boolean;
  reason?: string | null;
  purpose?: number;
  /**
   * The age band and the dates the rule is in force. Present on `Create/UpdateAwardTypeTargetDto`
   * since the port and settable by no form until the edit was built — the same class section E
   * counts, found here by reading the DTO rather than the create form.
   */
  minAge?: number | null;
  maxAge?: number | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

export interface AwardTypeTarget {
  id: string;
  awardTypeId: string;
  targetType: number;
  targetTypeName: string;
  targetId: string | null;
  targetName: string | null;
  isExclusion: boolean;
  reason: string | null;
  purpose: number;
  purposeName: string | null;
}

export interface UpsertAwardBudget {
  awardTypeId: string;
  year: number;
  budgetAmount: number;
  /** Optional — the service mints `AWB-{year}-{hex}` when it is blank. */
  budgetCode?: string | null;
  notes?: string | null;
}

export interface UpsertAwardCycle {
  awardTypeId: string;
  cycleCode: string;
  name: string;
  year: number;
  quarter?: number | null;
  month?: number | null;
  nominationOpensOn?: string | null;
  nominationClosesOn?: string | null;
  /** ⚠ Required iff the award is decided by `StaffVote`, and refused otherwise. */
  votingOpensOn?: string | null;
  votingClosesOn?: string | null;
}

export interface UpsertAwardCommittee {
  name: string;
  description?: string | null;
  quorumRequired: number;
  reviewDeadlineDays: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

/** A team member's share of a team nomination. Every field is correctable. */
export interface UpdateTeamNominee {
  role?: string | null;
  contributionSummary?: string | null;
  rewardPercentage?: number | null;
}

/** A contribution is one sentence, and rewording it is the whole edit. */
export interface UpdateContribution {
  description: string;
}

/**
 * A long-service award recorded by hand rather than by the sweep.
 *
 * ⚠ `employeeAwardId` links this to a conferred award and is null for one that stands alone.
 * Section E listed it as settable by no form on both DTOs; these forms are what close that.
 */
export interface CreateLongServiceAward {
  employeeId: string;
  awardTypeId: string;
  employeeAwardId?: string | null;
  yearsOfService: number;
  serviceStartDate: string;
  milestoneDate: string;
  awardDescription: string;
  monetaryAmount?: number | null;
  leaveDaysBonus?: number | null;
  otherBenefits?: string | null;
}

export interface UpdateLongServiceAward {
  employeeAwardId?: string | null;
  awardDescription: string;
  monetaryAmount?: number | null;
  leaveDaysBonus?: number | null;
  otherBenefits?: string | null;
  isProcessed: boolean;
  presentationDate?: string | null;
  presentationNotes?: string | null;
}

export interface AddCommitteeMember {
  employeeId: string;
  /** Free text, max 100 — not an enum. */
  role?: string | null;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
}

// ─── write payloads: conferral ───────────────────────────────────────────────

/** Confer from a nomination. The nominee, award and cycle all come from the nomination. */
export interface ConferFromNomination {
  awardDate: string;
  monetaryAmount?: number | null;
  awardLevelId?: string | null;
  additionalCitation?: string | null;
}

/** ⚠ Only for a `ManagementDirect` award — anything else is refused (AWD-07). */
export interface ConferDirectly {
  employeeId: string;
  awardTypeId: string;
  awardLevelId?: string | null;
  awardCycleId?: string | null;
  awardDate: string;
  reason: string;
  citation?: string | null;
  monetaryAmount?: number | null;
}

export interface EmployeeAward {
  id: string;
  tenantId: string;
  awardNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string | null;
  awardTypeId: string;
  awardTypeName: string;
  awardLevelId: string | null;
  awardLevelName: string | null;
  awardCycleId: string | null;
  awardCycleName: string | null;
  awardNominationId: string | null;
  awardDate: string;
  reason: string | null;
  citation: string | null;
  monetaryAmount: number | null;
  presentationDate: string | null;
  presentationVenue: string | null;
  paymentProcessed: boolean;
  paymentDate: string | null;
  paymentReference: string | null;
  amountPaid: number | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface RecordAwardPayment {
  awardId: string;
  paymentReference: string;
  /** Omit to pay the promised amount. What is released is the promise; what is booked is this. */
  amountPaid?: number | null;
}

export interface RecordAwardPresentation {
  awardId: string;
  presentationDate: string;
  presentationVenue?: string | null;
  presentationNotes?: string | null;
}

export interface UpdateAwardNomination {
  id: string;
  justification: string;
  proposedMonetaryAmount?: number | null;
  proposedLeaveDays?: number | null;
}

/**
 * What an award attachment can be, from `AwardAttachmentType`.
 *
 * ⚠ Read off the enum rather than inferred from the one value the nomination screen uploads.
 * Four TypeScript unions in this module's neighbours turned out to be fiction that type-checked,
 * so this is pinned to the C# definition: Photo = 1, Certificate = 2, Citation = 3,
 * SupportingDocument = 4.
 */
export const AWARD_ATTACHMENT_TYPES = [
  'Photo',
  'Certificate',
  'Citation',
  'SupportingDocument',
] as const;

export type AwardAttachmentType = (typeof AWARD_ATTACHMENT_TYPES)[number];
