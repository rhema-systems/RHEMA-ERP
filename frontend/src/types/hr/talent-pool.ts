// api/talent-pool — recruitment's candidate CRM (⚠ NOT succession's api/talent-pools).
// Every union here is written from the backend enum in HREnums.cs and every interface from the
// DTOs in RecruitmentDTOs.cs, source-verified 2026-08-30 — a type guessed from an endpoint name
// is fiction that type-checks.

import type { JobCandidate } from '@/types/hr/recruitment-pipeline';
import { TALENT_POOL_STATUSES, type TalentPoolStatus } from '@/types/hr/recruitment-pipeline';

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
  createdAt: string;
  updatedAt?: string | null;
}

export interface CandidateTalentSegmentForm {
  name: string;
  description?: string | null;
  color?: string | null;
  /** Update only — the create defaults it true server-side. */
  isActive?: boolean;
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
