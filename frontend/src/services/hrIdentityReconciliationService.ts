import { apiService } from './api.service';

export interface HrIdentityReconciliationState {
  userId: string;
  userDisplayName: string;
  userName?: string;
  employeeId: string;
  employeeNumber: string;
  userIsActive: boolean;
  hrAccessEligible: boolean;
  accessSuspendedByReconciliation: boolean;
  reactivationReviewRequired: boolean;
  staffStatus: string;
  departmentId?: string;
  departmentName?: string;
  managerEmployeeId?: string;
  managerName?: string;
  managerUserId?: string;
  roles: string[];
  lastObservedAtUtc: string;
  lastReconciledAtUtc: string;
  reviewReason?: string;
}

export interface HrIdentityWorkflowIssue {
  id: string;
  userId: string;
  userDisplayName: string;
  employeeId: string;
  employeeNumber: string;
  workflowInstanceId: string;
  workflowStepInstanceId?: string;
  workflowApprovalId?: string;
  issueType: string;
  status: string;
  staleAssigneeId?: string;
  staleAssigneeName?: string;
  suggestedReplacementUserId?: string;
  suggestedReplacementName?: string;
  reason: string;
  detectedAtUtc: string;
  replacementUserId?: string;
  replacementUserName?: string;
  resolvedAtUtc?: string;
  resolutionNote?: string;
}

export interface HrIdentityReconciliationRun {
  id: string;
  idempotencyKey: string;
  trigger: string;
  status: string;
  requestedById?: string;
  startedAtUtc: string;
  completedAtUtc?: string;
  candidateCount: number;
  reconciledCount: number;
  reviewRequiredCount: number;
  failedCount: number;
  error?: string;
}

export interface HrIdentityReconciliationDashboard {
  linkedUsers: number;
  hrIneligibleUsers: number;
  suspendedUsers: number;
  reactivationReviews: number;
  openWorkflowIssues: number;
  states: HrIdentityReconciliationState[];
  issues: HrIdentityWorkflowIssue[];
  recentRuns: HrIdentityReconciliationRun[];
}

export interface HrIdentityUserOption {
  userId: string;
  employeeId: string;
  displayName: string;
  userName?: string;
  employeeNumber?: string;
  departmentId?: string;
  departmentName?: string;
  isManager: boolean;
}

const baseUrl = '/administration/hr-identity-reconciliation';

export const hrIdentityReconciliationService = {
  getDashboard: () =>
    apiService.get<HrIdentityReconciliationDashboard>(baseUrl),
  getReplacementUsers: () =>
    apiService.get<HrIdentityUserOption[]>(`${baseUrl}/replacement-users`),
  run: () =>
    apiService.post<HrIdentityReconciliationRun>(`${baseUrl}/runs`, {}),
  retry: (runId: string) =>
    apiService.post<HrIdentityReconciliationRun>(
      `${baseUrl}/runs/${runId}/retry`,
      {}
    ),
  resolveIssue: (
    issueId: string,
    replacementUserId: string,
    resolutionNote: string
  ) =>
    apiService.post<HrIdentityWorkflowIssue>(
      `${baseUrl}/issues/${issueId}/resolve`,
      {
        replacementUserId,
        resolutionNote,
      }
    ),
  reactivate: (userId: string, reviewNote: string) =>
    apiService.post<HrIdentityReconciliationState>(
      `${baseUrl}/users/${userId}/reactivate`,
      {
        reviewNote,
      }
    ),
};
