// Types for HR Training & Learning — Slice 8 (Analytics & Dashboards).
//
// Mirrors TrainingDashboardDto / TrainingAnalyticsDto / EmployeeTrainingSummaryDto.
//
// ⚠ Rates travel with their denominator on purpose. A compliance rate of 0 means "nobody complies"
// OR "nobody has been assigned anything", and only `complianceRecordsCount` separates them — the
// second is a programme that has not started, not a crisis. Never render a rate without checking it.

import type { TrainingNominationSummary } from './training-delivery';
import type { TrainingCertificateSummary } from './training-certificates';
import type { EmployeeComplianceRecordSummary } from './training-compliance';

export interface TrainingCategoryBreakdown {
  categoryOptionId?: string | null;
  categoryName: string;
  categoryColor?: string | null;
  programsCount: number;
  completionsCount: number;
  totalSpent: number;
}

export interface TrainingDashboard {
  totalProgramsActive: number;
  totalSchedulesThisYear: number;
  upcomingSchedulesCount: number;
  totalNominationsThisYear: number;
  pendingNominationsCount: number;
  /** Every recorded completion this year, passed or not. */
  completedTrainingsThisYear: number;
  overallPassRate: number;
  employeesCertifiedThisYear: number;
  expiringCertificatesIn30Days: number;
  overallComplianceRate: number;
  /** The denominator behind the rate — 0 means nothing has been assigned. */
  complianceRecordsCount: number;
  nonCompliantEmployeesCount: number;
  activeMentoringPairsCount: number;
  activeLearningPathEnrollmentsCount: number;
  budgetUtilizationRate: number;
  budgetSpent: number;
  budgetAllocated: number;
  categoryBreakdown: TrainingCategoryBreakdown[];
  recentNominations: TrainingNominationSummary[];
  upcomingSchedules: any[];
}

export interface TrainerUtilization {
  trainerId: string;
  name: string;
  sessionsDelivered: number;
  hoursDelivered: number;
  averageRating?: number | null;
  ratingsCount: number;
}

export interface MonthlyCompletionPoint {
  month: number;
  monthName: string;
  completed: number;
  passed: number;
}

/** Kirkpatrick funnel counts, widest to narrowest. A stage that exceeds the one above it is a bug. */
export interface KirkpatrickFunnel {
  nominated: number;
  attended: number;
  completed: number;
  feedbackL1: number;
  followUpL23: number;
}

export interface TrainingAnalytics {
  year: number;
  currency: string;
  activeProgramsCount: number;
  completionsYtd: number;
  passedYtd: number;
  passRate: number;
  complianceRate: number;
  complianceRecordsCount: number;
  certificatesIssuedYtd: number;
  budgetAllocated: number;
  budgetSpent: number;
  budgetUtilizationRate: number;
  categoryBreakdown: TrainingCategoryBreakdown[];
  topTrainers: TrainerUtilization[];
  monthlyCompletions: MonthlyCompletionPoint[];
  funnel: KirkpatrickFunnel;
  feedbackResponses: number;
  /** 1–5. Null when nobody has answered — not zero. */
  avgSatisfaction?: number | null;
  wouldRecommendRate: number;
  avgLikelihoodToApply?: number | null;
  avgPreScore?: number | null;
  avgPostScore?: number | null;
  avgScoreGain?: number | null;
  followUpsCompleted: number;
  /** Null rather than a divide-by-zero when there were no completions. */
  costPerCompletion?: number | null;
}

export interface EmployeeTrainingSummary {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  totalTrainingsCompleted: number;
  trainingsPassed: number;
  totalTrainingHours: number;
  activeCertificatesCount: number;
  expiringCertificatesCount: number;
  complianceRequirementsCount: number;
  compliantRequirementsCount: number;
  /** Server-computed; 100 when nothing is assigned, so check the count before showing it. */
  complianceRate: number;
  learningPathsEnrolledCount: number;
  learningPathsCompletedCount: number;
  hasActiveMentoringPair: boolean;
  recentTrainings: TrainingNominationSummary[];
  certificates: TrainingCertificateSummary[];
  complianceRecords: EmployeeComplianceRecordSummary[];
}
