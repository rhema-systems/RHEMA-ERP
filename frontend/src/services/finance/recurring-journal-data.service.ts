import { apiService } from '@/services/api.service';

export type RecurringJournalStatus =
  | 'Draft' | 'PendingApproval' | 'Active' | 'Rejected' | 'Paused' | 'Completed' | 'Cancelled';
export type RecurringJournalOccurrenceStatus =
  | 'Due' | 'Generating' | 'SubmissionFailed' | 'PendingApproval' | 'Approved'
  | 'Posted' | 'Failed' | 'WaiverPending' | 'Waived' | 'Superseded';
export type RecurrenceFrequency = 'Daily' | 'Weekly' | 'SemiMonthly' | 'Monthly' | 'Quarterly' | 'Annually' | 'Custom';
export type BusinessDayConvention = 'NoAdjustment' | 'NextBusinessDay' | 'PreviousBusinessDay';
export type RecurringJournalReversalRule = 'None' | 'NextCalendarDay' | 'FirstDayOfNextFiscalPeriod' | 'DayOffset';
export type RecurringJournalReversalStatus = 'NotApplicable' | 'PendingAuthorization' | 'Scheduled' | 'Processing' | 'Failed' | 'Posted';

export interface RecurringJournalLineInput {
  id?: string;
  accountId: string;
  isDebit: boolean;
  fixedAmount: number;
  description?: string;
  dimensionValuesJson: string;
}

export interface CreateRecurringJournalTemplate {
  name: string;
  description?: string;
  journalType: string;
  bookClassification: string;
  currencyCode: string;
  referencePattern?: string;
  notes?: string;
  ownerUserId?: string;
  effectiveFrom: string;
  endDate?: string;
  maximumOccurrences?: number;
  timeZoneId: string;
  frequency: RecurrenceFrequency;
  interval: number;
  recurrenceRuleJson: string;
  businessDayConvention: BusinessDayConvention;
  autoReverse: boolean;
  reversalRule: RecurringJournalReversalRule;
  reversalDayOffset?: number;
  lines: RecurringJournalLineInput[];
}

export interface RecurringJournalTemplateLine extends RecurringJournalLineInput {
  id: string;
  lineNumber: number;
  accountCode: string;
  accountName: string;
}

export interface RecurringJournalOccurrence {
  id: string;
  templateVersion: number;
  sequenceNumber: number;
  scheduledDate: string;
  effectiveDate: string;
  status: RecurringJournalOccurrenceStatus;
  journalEntryId?: string;
  reversalJournalEntryId?: string;
  reversalDueDate?: string;
  reversalStatus: RecurringJournalReversalStatus;
  reversalAuthorizedAt?: string;
  reversalAuthorizedByUserId?: string;
  reversalAttemptCount: number;
  reversalLastAttemptAt?: string;
  reversalError?: string;
  reversalPostingEventId?: string;
  reversalProcessedBy?: string;
  attemptCount: number;
  generatedAt?: string;
  reviewedAt?: string;
  reviewedByUserId?: string;
  reviewComment?: string;
  postedAt?: string;
  postedByUserId?: string;
  reversedAt?: string;
  errorMessage?: string;
  adjustmentExplanation?: string;
  waivedAt?: string;
  waivedByUserId?: string;
  waiverReason?: string;
  workflowInstanceId?: string;
  canApprove: boolean;
  canReject: boolean;
  canPost: boolean;
  canRequestWaiver: boolean;
  actionDisabledReason?: string;
}

export interface RecurringJournalTemplate extends CreateRecurringJournalTemplate {
  id: string;
  templateNumber: string;
  version: number;
  status: RecurringJournalStatus;
  nextDueDate?: string;
  lastGeneratedDueDate?: string;
  generatedOccurrenceCount: number;
  submittedAt?: string;
  submittedByUserId?: string;
  reviewedAt?: string;
  reviewComment?: string;
  activatedAt?: string;
  lastFailure?: string;
  exceptionCount: number;
  totalDebit: number;
  totalCredit: number;
  rowVersion: string;
  canApprove: boolean;
  canReject: boolean;
  actionDisabledReason?: string;
  lines: RecurringJournalTemplateLine[];
  occurrences: RecurringJournalOccurrence[];
}

export interface RecurringJournalGenerationResult {
  templateCount: number;
  generatedCount: number;
  existingCount: number;
  failedCount: number;
}

export interface RecurringJournalReversalProcessingResult {
  candidateCount: number;
  postedCount: number;
  existingCount: number;
  failedCount: number;
}

class RecurringJournalDataService {
  // Keep route literals visible to the repository's frontend/backend contract
  // test; hiding the base path in a property would make route drift invisible.
  getAll = () => apiService.get<RecurringJournalTemplate[]>('/finance/recurring-journals');
  get = (id: string) => apiService.get<RecurringJournalTemplate>(`/finance/recurring-journals/${id}`);
  create = (request: CreateRecurringJournalTemplate) =>
    apiService.post<RecurringJournalTemplate>('/finance/recurring-journals', request);
  update = (id: string, request: CreateRecurringJournalTemplate & { rowVersion: string }) =>
    apiService.put<RecurringJournalTemplate>(`/finance/recurring-journals/${id}`, request);

  // Reasons are part of the durable Finance audit trail, not presentation-only notes.
  submit = (id: string, comment: string) => this.decide(id, 'submit', comment);
  approve = (id: string, comment: string) => this.decide(id, 'approve', comment);
  reject = (id: string, comment: string) => this.decide(id, 'reject', comment);
  pause = (id: string, comment: string) => this.decide(id, 'pause', comment);
  resume = (id: string, comment: string) => this.decide(id, 'resume', comment);
  cancel = (id: string, comment: string) => this.decide(id, 'cancel', comment);
  createNewVersion = (id: string, comment: string) => this.decide(id, 'new-version', comment);

  processDue = (asOfDate?: string) => {
    // Keep the query marker in the literal route so the route-contract test can
    // normalize it and still verify this browser call against the controller.
    if (asOfDate) {
      return apiService.post<RecurringJournalGenerationResult>(
        `/finance/recurring-journals/process-due?asOfDate=${encodeURIComponent(asOfDate)}`, {});
    }
    return apiService.post<RecurringJournalGenerationResult>('/finance/recurring-journals/process-due', {});
  };

  approveOccurrence = (id: string, comment: string) =>
    apiService.post<RecurringJournalOccurrence>(`/finance/recurring-journals/occurrences/${id}/approve`, { comment });
  rejectOccurrence = (id: string, comment: string) =>
    apiService.post<RecurringJournalOccurrence>(`/finance/recurring-journals/occurrences/${id}/reject`, { comment });
  requestOccurrenceWaiver = (id: string, comment: string) =>
    apiService.post<RecurringJournalOccurrence>(`/finance/recurring-journals/occurrences/${id}/request-waiver`, { comment });
  postOccurrence = (id: string) =>
    apiService.post<RecurringJournalOccurrence>(`/finance/recurring-journals/occurrences/${id}/post`, {});
  retryReversal = (id: string) =>
    apiService.post<RecurringJournalReversalProcessingResult>(`/finance/recurring-journals/occurrences/${id}/retry-reversal`, {});

  private decide(id: string, action: string, comment: string) {
    return apiService.post<RecurringJournalTemplate>(`/finance/recurring-journals/${id}/${action}`, { comment });
  }
}

export const recurringJournalDataService = new RecurringJournalDataService();
