/**
 * Recruitment — area 6 closeout: the aggregated dashboard.
 *
 * One controller, two endpoints (`api/recruitment-dashboard` and `.../analytics`), both HR-gated —
 * the payload carries candidate names on recent applications, upcoming interviews, offers and
 * hires starting soon, the same PII shape `api/job-applications` restricts to HR.
 */

export interface DashboardSummary {
  totalVacancies: number;
  activeApplications: number;
  interviewsScheduled: number;
  offersMade: number;
}

export interface DashboardPipelineStage {
  stageName: string;
  count: number;
}

export interface DashboardApplication {
  applicationId: string;
  candidateName: string;
  vacancyTitle: string;
  stageName: string;
  stageCssClass: string;
  dateApplied: string;
}

export interface DashboardInterview {
  interviewId: string;
  candidateName: string;
  vacancyTitle: string;
  interviewDateTime: string;
  interviewType: string;
  typeCssClass: string;
  candidateCount: number;
  round: number;
}

export interface DashboardSlaAlert {
  vacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  applicationDeadline?: string | null;
}

export interface DashboardHire {
  hireId: string;
  hireNumber: string;
  candidateName: string;
  positionTitle: string;
  expectedStartDate: string;
  statusName: string;
}

export interface DashboardExpiringOffer {
  offerId: string;
  offerNumber: string;
  candidateName: string;
  positionTitle: string;
  expiryDate?: string | null;
}

export interface DashboardDeadlineVacancy {
  vacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  recruiterName?: string | null;
  applicationCount: number;
  applicationDeadline?: string | null;
  daysRemaining: number;
}

export interface RecruitmentDashboard {
  summary: DashboardSummary;
  pipelineStages: DashboardPipelineStage[];
  recentApplications: DashboardApplication[];
  upcomingInterviews: DashboardInterview[];
  slaAlerts: DashboardSlaAlert[];
  hiresStartingSoon: DashboardHire[];
  expiringOffers: DashboardExpiringOffer[];
  deadlineApproachingVacancies: DashboardDeadlineVacancy[];
}
