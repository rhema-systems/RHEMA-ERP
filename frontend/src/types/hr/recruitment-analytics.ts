/**
 * Recruitment analytics — `GET api/recruitment-dashboard/analytics?year=`.
 *
 * Mirrors `RecruitmentAnalyticsDto`. Same controller and the same HR gate as
 * `./recruitment-dashboard`, but a different question: the dashboard is "what needs doing today",
 * this is "how did the year go". Kept in its own file the way training splits
 * `training-analytics` off, because the shapes have nothing in common.
 *
 * ⚠ Two reading rules, both of which the screen has to honour:
 *
 * 1. **A `null` is not a zero.** Every speed figure and `costPerHire` is nullable and means "no
 *    sample", not "instant" or "free". `timedHiresSampleSize` is the denominator behind all four
 *    speed figures — only hires with a confirmed or actual start date count, so it is normally
 *    smaller than `hiresYtd`. Render `—`, never `0`.
 * 2. **A rate can be 0 because nothing has happened.** `acceptanceRate` is measured against offers
 *    the candidate actually *responded* to (`accepted + declined`), so an entirely un-responded set
 *    of offers reports `0` — which is not a rejection rate of zero, it is no answer yet. Same trap
 *    as training's compliance rate: check the denominator before rendering the rate.
 */

/** Applications received in the year, counted independently at each stage they reached. */
export interface RecruitmentFunnel {
  applied: number;
  shortlisted: number;
  interviewed: number;
  offered: number;
  hired: number;
}

export interface RecruitmentSourceEffectiveness {
  /** `ApplicationSource` enum value. */
  source: number;
  sourceName: string;
  applications: number;
  shortlisted: number;
  hires: number;
  /** Already a percentage (0–100, 1dp) of this source's own applications — do not multiply by 100. */
  hireRate: number;
}

export interface RecruitmentOfferOutcome {
  totalOffers: number;
  accepted: number;
  declined: number;
  expired: number;
  withdrawn: number;
  /** Sent or negotiating — still with the candidate. */
  pending: number;
  /**
   * Percentages (0–100, 1dp) of `accepted + declined`, NOT of `totalOffers`. The five counts above
   * do not have to add up to `totalOffers` either: a draft, pending-approval, on-hold or rejected
   * offer is in the total and in none of them.
   */
  acceptanceRate: number;
  declineRate: number;
}

export interface VacancyAgeingBucket {
  label: string;
  count: number;
}

export interface AgeingVacancy {
  vacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  recruiterName?: string | null;
  statusName: string;
  applicationCount: number;
  ageDays: number;
}

export interface RecruiterLoad {
  recruiterId: string;
  recruiterName: string;
  openVacancies: number;
  applications: number;
  hiresYtd: number;
}

export interface RecruitmentCostCategory {
  /** `RecruitmentCostCategory` enum value. */
  category: number;
  categoryName: string;
  /** Base currency — the server has already applied each cost row's exchange rate. */
  amount: number;
}

export interface MonthlyRecruitmentPoint {
  month: number;
  monthName: string;
  applications: number;
  offers: number;
  hires: number;
}

export interface RecruitmentAnalytics {
  year: number;
  /** The tenant's base currency — every money field on this payload is already converted into it. */
  currency: string;

  // Headline — point-in-time for vacancies, year-to-date for the rest.
  openVacanciesCount: number;
  applicationsYtd: number;
  offersYtd: number;
  hiresYtd: number;

  // Speed. All four are null when nothing in the year had a confirmed start.
  /** Requisition raised → the hire's start. */
  avgTimeToFillDays?: number | null;
  /** Median of the same series — one pathological vacancy does not move it. */
  medianTimeToFillDays?: number | null;
  /** Application received → that candidate's start. */
  avgTimeToHireDays?: number | null;
  /** Vacancy published → shortlist completed. */
  avgTimeToShortlistDays?: number | null;
  /** The denominator behind all four figures above. */
  timedHiresSampleSize: number;

  // Cost.
  totalRecruitmentCost: number;
  costPerHire?: number | null;
  costByCategory: RecruitmentCostCategory[];

  funnel: RecruitmentFunnel;
  sourceEffectiveness: RecruitmentSourceEffectiveness[];
  offerOutcomes: RecruitmentOfferOutcome;

  // Ageing — point-in-time, deliberately not filtered by the selected year.
  vacancyAgeing: VacancyAgeingBucket[];
  oldestOpenVacancies: AgeingVacancy[];
  /**
   * Open `PositionVacancy` rows: an authorised seat standing empty, whether or not anyone has
   * raised a requisition for it. A different population from `openVacanciesCount`, which counts
   * live recruitment campaigns — the two are not comparable and must not be added.
   */
  openPositionVacanciesCount: number;
  avgPositionVacancyAgeDays?: number | null;

  recruiterLoad: RecruiterLoad[];
  monthlyTrend: MonthlyRecruitmentPoint[];
}
