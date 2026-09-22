// api/talent-pool — recruitment's candidate CRM (⚠ NOT succession's api/talent-pools).
// Every union here is written from the backend enum in HREnums.cs and every interface from the
// DTOs in RecruitmentDTOs.cs, source-verified 2026-08-30 — a type guessed from an endpoint name
// is fiction that type-checks.

import type { CriterionScore, JobCandidate } from '@/types/hr/recruitment-pipeline';
import { TALENT_POOL_STATUSES, type TalentPoolStatus } from '@/types/hr/recruitment-pipeline';
import type {
  MandatoryMatchMode,
  ShortlistingComparisonOperator,
  ShortlistingCriteriaType,
  ValueMatchStrategy,
} from '@/types/hr/recruitment';

export { TALENT_POOL_STATUSES };
export type { TalentPoolStatus };

// TalentPoolEntrySource — HREnums.cs (NotSpecified=0 … CandidatePortal=8)
export const TALENT_POOL_SOURCES = [
  'NotSpecified',
  'AppliedAndRetained',
  'RecruiterAdded',
  'Referral',
  'CareerFair',
  'LinkedIn',
  'UnsolicitedCv',
  'InternalTransfer',
  'CandidatePortal',
] as const;
export type TalentPoolSource = (typeof TALENT_POOL_SOURCES)[number];

// CandidateEngagementEventType — HREnums.cs (Email=1 … InternalNote=10)
export const ENGAGEMENT_EVENT_TYPES = [
  'Email',
  'PhoneCall',
  'InPersonMeeting',
  'InvitedToApply',
  'ProfileReview',
  'StatusUpdate',
  'LinkedInMessage',
  'Sms',
  'InterviewAssessment',
  'InternalNote',
] as const;
export type EngagementEventType = (typeof ENGAGEMENT_EVENT_TYPES)[number];

// BulkTalentPoolOperation — HREnums.cs (AssignSegment=1 … RemoveFromPool=4)
export const BULK_POOL_OPERATIONS = [
  'AssignSegment',
  'RemoveSegment',
  'SetStatus',
  'RemoveFromPool',
] as const;
export type BulkPoolOperation = (typeof BULK_POOL_OPERATIONS)[number];

export interface CandidateTalentSegment {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  color?: string | null;
  isActive: boolean;
  memberCount: number;
  /** Round 3 lane V (D-6) - who works the segment, why it exists, and the role it feeds. */
  ownerEmployeeId?: string | null;
  ownerEmployeeName?: string | null;
  purpose?: string | null;
  targetPositionId?: string | null;
  targetPositionTitle?: string | null;
  jobFamilyId?: string | null;
  jobFamilyName?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CandidateTalentSegmentForm {
  name: string;
  description?: string | null;
  color?: string | null;
  /** Update only — the create defaults it true server-side. */
  isActive?: boolean;
  /** Lane V. Sent every save: null CLEARS the field, it does not mean "leave it alone". */
  ownerEmployeeId?: string | null;
  purpose?: string | null;
  targetPositionId?: string | null;
  jobFamilyId?: string | null;
}

export interface CandidateSegmentMembership {
  id: string;
  tenantId: string;
  jobCandidateId: string;
  segmentId: string;
  segmentName: string;
  segmentColor?: string | null;
  addedDate: string;
  notes?: string | null;
}

export interface CandidateEngagementEvent {
  id: string;
  tenantId: string;
  jobCandidateId: string;
  candidateName: string;
  eventType: EngagementEventType;
  eventTypeName: string;
  eventDate: string;
  subject?: string | null;
  notes?: string | null;
  recordedByName?: string | null;
  isInternal: boolean;
  createdAt: string;
}

export interface LogEngagementEventForm {
  eventType: EngagementEventType;
  eventDate: string;
  subject?: string | null;
  notes?: string | null;
  isInternal: boolean;
}

/** TalentPoolCandidateDto : JobCandidateDto — the pool fields ride on the full candidate. */
export interface TalentPoolCandidate extends JobCandidate {
  talentPoolSource: TalentPoolSource;
  talentPoolSourceName: string;
  talentPoolStatus: TalentPoolStatus;
  talentPoolStatusName: string;
  talentPoolNotes?: string | null;
  talentPoolReviewDate?: string | null;
  /** Why they last left the pool — populated only after a removal. */
  talentPoolRemovalReason?: string | null;
  lastEngagedDate?: string | null;
  daysInPool: number;
  engagementCount: number;
  isOverdueForReview: boolean;
  segments: CandidateSegmentMembership[];
}

/**
 * ⚠ `sortBy` accepts only these keys server-side — the DTO's own default `"LastName"` falls
 * through to the fallback arm, so never send it.
 */
export const TALENT_POOL_SORT_KEYS = [
  'fullname',
  'dateadded',
  'lastengaged',
  'reviewdate',
  'experience',
] as const;
export type TalentPoolSortKey = (typeof TALENT_POOL_SORT_KEYS)[number];

export interface TalentPoolFilter {
  search?: string;
  segmentIds?: string[];
  status?: TalentPoolStatus;
  source?: TalentPoolSource;
  workArrangement?: JobCandidate['preferredWorkArrangement'];
  minExperienceYears?: number;
  maxExperienceYears?: number;
  availableBefore?: string;
  overdueForReview?: boolean;
  dormantMoreThanDays?: number;
  /**
   * An area on the geography tree. Matches candidates recorded in it AND anywhere beneath it
   * (round 4, lane B2) - pick Greater Accra and the person recorded in Tema comes back.
   *
   * WARNING: a candidate with only a typed city is NOT matched; the server has no name to compare
   * against here. Use `search` for a typed city.
   */
  geoAreaId?: string;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: TalentPoolSortKey;
  sortDescending?: boolean;
}

export interface TalentPoolPagedResult {
  items: TalentPoolCandidate[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AddToTalentPoolPayload {
  source: TalentPoolSource;
  segmentIds: string[];
  notes?: string | null;
  reviewDate?: string | null;
}

export interface RemoveFromTalentPoolPayload {
  reason?: string | null;
  notes?: string | null;
}

export interface BulkTalentPoolPayload {
  candidateIds: string[];
  operation: BulkPoolOperation;
  segmentId?: string | null;
  status?: TalentPoolStatus | null;
  notes?: string | null;
}

export interface TalentPoolBulkResultItem {
  applicationId: string;
  candidateId?: string | null;
  success: boolean;
  message?: string | null;
}

export interface TalentPoolBulkResult {
  succeeded: number;
  skipped: number;
  results: TalentPoolBulkResultItem[];
}

export interface TalentPoolSourceBreakdown {
  sourceName: string;
  count: number;
}

export interface TalentPoolSegmentBreakdown {
  segmentId: string;
  segmentName: string;
  segmentColor?: string | null;
  count: number;
}

export interface TalentPoolAnalytics {
  totalInPool: number;
  active: number;
  passive: number;
  dormant: number;
  overdueForReview: number;
  convertedThisYear: number;
  addedThisMonth: number;
  addedThisYear: number;
  avgDaysInPool: number;
  bySource: TalentPoolSourceBreakdown[];
  bySegment: TalentPoolSegmentBreakdown[];
}

export interface TalentPoolVacancyMatch {
  candidateId: string;
  candidateName: string;
  candidateNumber: string;
  headline?: string | null;
  totalYearsExperience?: number | null;
  preferredWorkArrangementName?: string | null;
  availableFrom?: string | null;
  matchScore: number;
  /**
   * The highest the 40/30/20 fit rubric can award - 90, not 100. Sent by the server since G-13.2
   * and simply missing from this type until round 4 lane B4, so every screen rendered the score
   * bare and nobody could tell whether 65 was good.
   */
  matchScoreMax: number;
  matchReasons: string[];
}

export interface CandidateVacancyMatch {
  vacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  vacancyStatus: string;
  vacancyStatusName: string;
  applicationDeadline?: string | null;
  hiringManagerName?: string | null;
  numberOfPositions: number;
  matchScore: number;
  matchReasons: string[];
}


// -- Screening the pool by real criteria (round 4, lane B) ------------------------------------
//
// WARNING: distinct from `TalentPoolVacancyMatch` above, and shown BESIDE it rather than merged
// into it (decision D-7). The match score is a blind 40/30/20 rubric over experience, work mode and
// availability, out of 90. The criteria score is the vacancy's OWN shortlisting criteria run
// through the same engine that scores applications, out of 100. They answer different questions and
// averaging them would answer neither.

/** One criterion in an ad-hoc screen - mirrors `AdHocScreeningCriterionDto`. */
export interface AdHocScreeningCriterion {
  criteriaName: string;
  type: ShortlistingCriteriaType;
  requiredValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  isMandatory: boolean;
  matchMode?: MandatoryMatchMode;
  matchStrategy?: ValueMatchStrategy;
  weight: number;
  comparisonOperator?: ShortlistingComparisonOperator | null;
  values?: { referenceId?: string | null; label?: string | null }[] | null;
}

export interface TalentPoolScreenRequest {
  filter?: TalentPoolFilter;
  /** How many rows come back. The whole filtered set is scored; this trims the answer. */
  topN?: number;
  /** Default true - a near miss the recruiter might waive is worth seeing. */
  includeNonMatching?: boolean;
  /** Ad-hoc door only; the by-vacancy door reads the vacancy's own criteria. */
  criteria?: AdHocScreeningCriterion[];
}

export interface ScreeningCriterionSummary {
  criteriaId?: string | null;
  criteriaName: string;
  type: ShortlistingCriteriaType;
  typeName: string;
  isMandatory: boolean;
  weight: number;
  acceptedValues?: string | null;
}

export interface TalentPoolScreenRow {
  candidateId: string;
  candidateName: string;
  candidateNumber: string;
  email: string;
  headline?: string | null;
  city?: string | null;
  geoAreaId?: string | null;
  totalYearsExperience?: number | null;
  preferredWorkArrangementName?: string | null;
  availableFrom?: string | null;
  hasPhoto: boolean;
  isInTalentPool: boolean;
  /**
   * WARNING: `null` is NOT zero. Zero means "measured, and missed everything"; null means the
   * criteria asked questions this record cannot answer. Render them differently - a `?? 0` here
   * puts a candidate nobody knows anything about beside one who was checked and genuinely does
   * not fit.
   */
  criteriaScore: number | null;
  criteriaScoreMax: number;
  allMandatoryPassed: boolean;
  totalWeight: number;
  alreadyApplied: boolean;
  breakdown: CriterionScore[];
}

export interface TalentPoolScreenResult {
  vacancyId?: string | null;
  vacancyNumber?: string | null;
  jobTitle?: string | null;
  screenedCount: number;
  scoredCount: number;
  qualifiedCount: number;
  criteria: ScreeningCriterionSummary[];
  rows: TalentPoolScreenRow[];
}

export interface TalentPoolInviteToApplyPayload {
  jobVacancyId: string;
  candidateIds: string[];
  notes?: string | null;
  sendEmail?: boolean;
}

export interface TalentPoolBookInterviewPayload {
  jobInterviewId: string;
  candidateIds: string[];
  notes?: string | null;
}
