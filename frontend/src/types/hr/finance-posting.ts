// HR → Finance posting (HR finish plan lane 8). Mirrors HrFinancePostingDTOs.cs.
//
// ⚠ HR never names a GL account on a claim. An administrator maps ROLES to Finance accounts once;
// each catalogued EVENT declares the roles it debits and credits; the REGISTER is one row per
// (event, source document) saying whether Finance took it. Read
// docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md before changing any of this.

export type HrFinanceAccountRole =
  | 'StaffClaimsPayable'
  | 'StaffAdvancesReceivable'
  | 'StaffPaymentsClearing'
  | 'MedicalExpense'
  | 'TravelExpense';

export type HrFinancePostingStatus = 'Posted' | 'Failed' | 'Unposted' | 'Skipped' | 'Reversed';

export interface HrFinanceAccountMapping {
  role: HrFinanceAccountRole;
  roleName: string;
  roleDescription: string;
  /** Asset / Liability / Expense — what the chosen account must be. */
  requiredAccountType: string;
  accountId: string | null;
  accountCode: string | null;
  accountName: string | null;
  /** Live from Finance at read time; false means the mapped account has since been deactivated. */
  accountIsActive: boolean | null;
  notes: string | null;
}

export interface HrFinancePostingRule {
  eventCode: string;
  name: string;
  area: string;
  sourceDocumentType: string;
  trigger: string;
  treatment: string;
  debitRoles: HrFinanceAccountRole[];
  creditRoles: HrFinanceAccountRole[];
  isEnabled: boolean;
  postOnActionDate: boolean;
  notes: string | null;
  /** Every role mapped to an active account of the right type, and Finance's book resolvable. */
  isReady: boolean;
  missingRoles: HrFinanceAccountRole[];
  /** Unposted + Failed rows for this event — the back-fill queue. */
  pendingCount: number;
  postedCount: number;
}

export interface HrFinancePostingSettings {
  functionalCurrencyCode: string;
  accountingBookCode: string | null;
  accountingBookProblem: string | null;
  mappings: HrFinanceAccountMapping[];
  rules: HrFinancePostingRule[];
}

export interface UpsertHrFinanceAccountMapping {
  role: HrFinanceAccountRole;
  accountId: string;
  notes?: string | null;
}

export interface UpsertHrFinancePostingRule {
  eventCode: string;
  isEnabled: boolean;
  postOnActionDate: boolean;
  notes?: string | null;
}

export interface HrFinancePostingLineSnapshot {
  role: HrFinanceAccountRole;
  roleName: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  description: string | null;
}

export interface HrFinancePostingRecord {
  id: string;
  eventCode: string;
  eventName: string;
  area: string;
  sourceDocumentType: string;
  sourceDocumentId: string;
  sourceReference: string;
  employeeId: string | null;
  description: string;
  /** Functional-currency debit total as sent — reconciliation evidence, not a balance. */
  amount: number;
  currencyCode: string;
  transactionCurrencyCode: string | null;
  transactionAmount: number | null;
  postingDate: string;
  status: HrFinancePostingStatus;
  statusName: string;
  statusReason: string | null;
  accountingBookCode: string | null;
  postingEventId: string | null;
  journalEntryId: string | null;
  journalEntryNumber: string | null;
  postedAt: string | null;
  attemptCount: number;
  lastAttemptAt: string | null;
  lines: HrFinancePostingLineSnapshot[];
  reversalPostingEventId: string | null;
  reversalJournalEntryId: string | null;
  reversalJournalEntryNumber: string | null;
  reversedAt: string | null;
  reversalReason: string | null;
  createdAt: string;
  canRetry: boolean;
  canReverse: boolean;
}

export interface HrFinancePostingRecordQuery {
  status?: HrFinancePostingStatus;
  eventCode?: string;
  employeeId?: string;
  from?: string;
  to?: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface HrFinancePostingSummary {
  posted: number;
  failed: number;
  unposted: number;
  skipped: number;
  reversed: number;
  postedAmount: number;
  functionalCurrencyCode: string;
}

export interface PagedHrFinancePostingRecords {
  items: HrFinancePostingRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export const HR_FINANCE_POSTING_STATUSES: HrFinancePostingStatus[] = [
  'Posted',
  'Failed',
  'Unposted',
  'Skipped',
  'Reversed',
];
