export type ProcurementCalendarProfileStatus =
  | 'Draft'
  | 'Published'
  | 'Retired';
export type ProcurementCalendarEventType =
  | 'AppPreparation'
  | 'AppSubmission'
  | 'MidYearReview'
  | 'CycleCount'
  | 'YearEndClose'
  | 'Renewal'
  | 'GhanepsDeadline';
export type ProcurementCalendarOccurrenceStatus =
  | 'Upcoming'
  | 'Due'
  | 'Acknowledged'
  | 'Escalated'
  | 'Completed'
  | 'Cancelled'
  | 'Failed';
export type ProcurementCalendarRunStatus =
  | 'Running'
  | 'Completed'
  | 'CompletedWithWarnings'
  | 'Failed';

export interface ProcurementCalendarRuleRequest {
  ruleKey?: string;
  eventType: ProcurementCalendarEventType;
  title: string;
  description?: string;
  dueMonth: number;
  dueDay: number;
  dueLocalTime: string;
  reminderLeadDays: number;
  escalationAfterDays: number;
  ownerRoleName: string;
  escalationRoleName: string;
  statutoryReference: string;
  isEnabled: boolean;
}

export interface SaveProcurementCalendarProfile {
  profileCode: string;
  name: string;
  description?: string;
  timeZoneId: string;
  generationHorizonDays: number;
  catchUpDays: number;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  changeSummary: string;
  rowVersion?: string;
  rules: ProcurementCalendarRuleRequest[];
}

export interface ProcurementCalendarRule
  extends ProcurementCalendarRuleRequest {
  id: string;
  ruleKey: string;
  ruleCode: string;
}

export interface ProcurementCalendarValidationIssue {
  code: string;
  field: string;
  message: string;
}

export interface ProcurementCalendarProfile {
  id: string;
  profileKey: string;
  profileCode: string;
  name: string;
  description?: string;
  version: number;
  status: ProcurementCalendarProfileStatus;
  timeZoneId: string;
  generationHorizonDays: number;
  catchUpDays: number;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  isEffective: boolean;
  changeSummary?: string;
  approvalReference?: string;
  supersedesProfileId?: string;
  publishedAtUtc?: string;
  publishedByName?: string;
  retiredAtUtc?: string;
  retiredByName?: string;
  createdAtUtc: string;
  rowVersion: string;
  rules: ProcurementCalendarRule[];
  validationIssues: ProcurementCalendarValidationIssue[];
}

export interface ProcurementCalendarSummary {
  profileFamilies: number;
  draftProfiles: number;
  publishedProfiles: number;
  openTasks: number;
  dueTasks: number;
  escalatedTasks: number;
  completedTasks: number;
  nextDueAtUtc?: string;
  lastRunAtUtc?: string;
}

export interface ProcurementCalendarOccurrence {
  id: string;
  occurrenceKey: string;
  profileId: string;
  profileVersion: number;
  profileCode: string;
  ruleCode: string;
  eventType: ProcurementCalendarEventType;
  calendarYear: number;
  title: string;
  description?: string;
  dueAtUtc: string;
  dueLocal: string;
  timeZoneId: string;
  ownerUserId: string;
  ownerName: string;
  ownerRoleName?: string;
  escalationUserId: string;
  escalationOwnerName: string;
  escalationRoleName?: string;
  statutoryReference: string;
  status: ProcurementCalendarOccurrenceStatus;
  generatedAtUtc: string;
  reminderSentAtUtc?: string;
  dueNotificationSentAtUtc?: string;
  escalatedAtUtc?: string;
  acknowledgedAtUtc?: string;
  completedAtUtc?: string;
  cancelledAtUtc?: string;
  actionReason?: string;
  rowVersion: string;
}

export interface ProcurementCalendarOccurrencePage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementCalendarOccurrence[];
}

export interface ProcurementCalendarRun {
  id: string;
  runKey: string;
  trigger: 'Scheduled' | 'Manual' | 'Publication';
  status: ProcurementCalendarRunStatus;
  attemptCount: number;
  evaluationAtUtc: string;
  windowStartUtc: string;
  windowEndUtc: string;
  startedAtUtc: string;
  completedAtUtc?: string;
  requestedByName: string;
  profilesEvaluated: number;
  rulesEvaluated: number;
  createdCount: number;
  rescheduledCount: number;
  reminderCount: number;
  dueCount: number;
  escalationCount: number;
  failedCount: number;
  errorSummary?: string;
}
