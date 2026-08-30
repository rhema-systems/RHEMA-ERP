export type CivilEngineeringWeeklySupervisionLookup = { id: string; label: string };

export type CivilEngineeringWeeklySupervisionDocument = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringWeeklySupervisionLookups = {
  activityCategories: CivilEngineeringWeeklySupervisionLookup[];
  contractors: CivilEngineeringWeeklySupervisionLookup[];
  milestones: CivilEngineeringWeeklySupervisionLookup[];
  risks: CivilEngineeringWeeklySupervisionLookup[];
  issues: CivilEngineeringWeeklySupervisionLookup[];
  dependencies: CivilEngineeringWeeklySupervisionLookup[];
  recoveryOwners: CivilEngineeringWeeklySupervisionLookup[];
  documents: CivilEngineeringWeeklySupervisionDocument[];
  actorTypes: string[];
  evidenceRoles: string[];
  siteStatuses: string[];
  minimumPhotoCount: number;
  dueDay: string;
};

export type CivilEngineeringWeeklySupervisionActivity = {
  sequence: number;
  activityCategoryId: string;
  activityCategoryLabel: string;
  actorType: string;
  contractorBusinessPartnerId?: string | null;
  contractorName?: string | null;
  description: string;
  progressPercent?: number | null;
};

export type CivilEngineeringWeeklySupervisionEvidence = {
  evidenceRole: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  documentTitle: string;
  versionNumber: string;
};

export type CivilEngineeringWeeklySupervisionReview = {
  sequence: number;
  action: string;
  outcome: string;
  comment: string;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  documentReference?: string | null;
  documentTitle?: string | null;
  versionNumber?: string | null;
  actorUserId: string;
  actorName: string;
  createdAt: string;
};

export type CivilEngineeringWeeklySupervisionReport = {
  id: string;
  projectId: string;
  projectEngineerAssignmentId: string;
  projectEngineerName: string;
  projectMilestoneId?: string | null;
  projectMilestoneTitle: string;
  milestoneTargetDateSnapshot?: string | null;
  milestoneActualDateSnapshot?: string | null;
  projectRiskId?: string | null;
  projectRiskTitle?: string | null;
  projectIssueId?: string | null;
  projectIssueTitle?: string | null;
  projectTaskDependencyId?: string | null;
  projectTaskDependencyLabel?: string | null;
  weekStart: string;
  weekEnd: string;
  overallProgressPercent?: number | null;
  siteStatus: string;
  delayReason?: string | null;
  recoveryActionItemId?: string | null;
  recoveryActionTitle?: string | null;
  isProgressCorrection: boolean;
  progressCorrectionDecisionId?: string | null;
  progressCorrectionDecisionTitle?: string | null;
  materialUsageSummary?: string | null;
  safetyNotes?: string | null;
  testSummary?: string | null;
  dueAt: string;
  isOverdue: boolean;
  escalatedAt?: string | null;
  status: string;
  approvalStatus: string;
  rejectionReason?: string | null;
  workflowInstanceId?: string | null;
  rowVersion: string;
  activities: CivilEngineeringWeeklySupervisionActivity[];
  evidence: CivilEngineeringWeeklySupervisionEvidence[];
  reviews: CivilEngineeringWeeklySupervisionReview[];
};

export type CreateCivilEngineeringWeeklySupervisionReportRequest = {
  clientRequestId: string;
  weekStart: string;
  projectMilestoneId: string;
  projectRiskId?: string | null;
  projectIssueId?: string | null;
  projectTaskDependencyId?: string | null;
  overallProgressPercent?: number | null;
  siteStatus: string;
  delayReason?: string | null;
  recoveryAction?: string | null;
  recoveryOwnerUserId?: string | null;
  recoveryDueDate?: string | null;
  materialUsageSummary?: string | null;
  safetyNotes?: string | null;
  testSummary?: string | null;
  activities: Array<{
    activityCategoryId: string;
    actorType: string;
    contractorBusinessPartnerId?: string | null;
    description: string;
    progressPercent?: number | null;
  }>;
  evidence: Array<{
    centralDocumentRecordId: string;
    centralDocumentVersionId: string;
    evidenceRole: string;
  }>;
};

export type ProcessCivilEngineeringWeeklySupervisionReportRequest = {
  clientRequestId: string;
  rowVersion: string;
  approve: boolean;
  comment: string;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
};
