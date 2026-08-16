/**
 * Employee grievances — area 9 slice 7, FR-HR-181. Backend route: `api/grievances`.
 *
 * Every union here was extracted from `HREnums.cs`, not inferred — the same discipline as
 * `types/hr/discipline.ts`, and for the same reason: a wrong member is not a compile error, it is a
 * request the server rejects with a validation message nobody can explain.
 */

/**
 * FR-HR-181's ladder. The employee is the origin, not a rung — these are the levels a grievance is
 * ANSWERED at, in the order the requirement names them.
 *
 * ⚠ These are not resolved to people. TDC has no org-authority data (no organisation unit has a head
 * recorded), so a grievance sits at a rung and HR names who should answer it. Do not build UI that
 * implies the system knows who someone's HOD is.
 */
export type GrievanceEscalationLevel =
  | 'Supervisor'
  | 'HeadOfDepartment'
  | 'HumanResources'
  | 'GeneralManagerFinanceAdmin'
  | 'ManagingDirector'
  | 'Board';

export type GrievanceStatus =
  | 'Filed' | 'UnderReview' | 'Escalated' | 'Resolved' | 'Withdrawn' | 'Closed';

export type GrievanceStepOutcome = 'AwaitingResponse' | 'Resolved' | 'Escalated';

/** The ladder in order, with the labels the requirement uses. */
export const GRIEVANCE_LADDER: { value: GrievanceEscalationLevel; label: string }[] = [
  { value: 'Supervisor', label: 'Supervisor' },
  { value: 'HeadOfDepartment', label: 'Head of department' },
  { value: 'HumanResources', label: 'Human Resources' },
  { value: 'GeneralManagerFinanceAdmin', label: 'GM Finance & Administration' },
  { value: 'ManagingDirector', label: 'Managing Director' },
  { value: 'Board', label: 'Board' },
];

export const GRIEVANCE_STATUS_OPTIONS: { value: GrievanceStatus; label: string }[] = [
  { value: 'Filed', label: 'Filed' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Escalated', label: 'Escalated' },
  { value: 'Resolved', label: 'Resolved' },
  { value: 'Withdrawn', label: 'Withdrawn' },
  { value: 'Closed', label: 'Closed' },
];

/** Statuses that mean the grievance is finished; nothing can be done to it. */
export const SETTLED_GRIEVANCE_STATUSES: GrievanceStatus[] = ['Resolved', 'Withdrawn', 'Closed'];

export interface GrievanceStep {
  id: string;
  grievanceId: string;
  level: GrievanceEscalationLevel;
  levelName: string;
  sequence: number;
  reachedDate: string;
  assignedToId?: string | null;
  assignedToName?: string | null;
  response?: string | null;
  respondedDate?: string | null;
  respondedById?: string | null;
  respondedByName?: string | null;
  outcome: GrievanceStepOutcome;
  outcomeName: string;
}

export interface GrievanceSummary {
  id: string;
  grievanceNumber: string;
  employeeId: string;
  employeeName: string;
  subject: string;
  filedDate: string;
  status: GrievanceStatus;
  statusName: string;
  currentLevel: GrievanceEscalationLevel;
  currentLevelName: string;
  /** True while the rung it sits at has not answered. */
  awaitingResponse: boolean;
}

export interface Grievance extends GrievanceSummary {
  tenantId: string;
  employeeNumber?: string | null;
  /** The employee's own words. Never amended after filing — FR-HR-181 requires it retained. */
  statement: string;
  resolvedDate?: string | null;
  resolutionSummary?: string | null;
  withdrawnDate?: string | null;
  withdrawalReason?: string | null;
  /** The full ladder in order: every rung reached, answered or not. */
  steps: GrievanceStep[];
}

/** No employee id: the griever is the caller. HR cannot raise one for somebody else. */
export interface FileGrievanceRequest {
  subject: string;
  statement: string;
}

export interface RespondToGrievanceRequest {
  response: string;
  /** True when this answer settles it; false leaves it open for the employee to judge. */
  resolvesGrievance: boolean;
}

export interface AssignGrievanceStepRequest {
  assignedToId: string;
}

export interface EscalateGrievanceRequest {
  reason?: string | null;
}

export interface WithdrawGrievanceRequest {
  reason: string;
}
