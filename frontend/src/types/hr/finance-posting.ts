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
  | 'TravelExpense'
  | 'LeaveEncashmentExpense'
  | 'AwardsExpense'
  | 'BenefitsExpense'
  | 'SeparationExpense'
  | 'EmployeeRecoveriesIncome'
  | 'StatutoryDeductionsPayable'
  | 'StaffReceivables'
  | 'StaffReceivableWriteOff'
  | 'RecruitmentExpense'
  | 'InsuranceRecoveriesIncome'
  | 'ConsultingRevenue';

/** What the event produces in Finance: a GL journal, an AP vendor invoice Finance approves and pays, or an AR customer invoice Finance issues and collects. */
export type HrFinancePostingKind = 'Journal' | 'VendorInvoice' | 'CustomerInvoice';

export type HrFinancePostingStatus = 'Posted' | 'Failed' | 'Unposted' | 'Skipped' | 'Reversed';

/** For events whose document names no payment method: HR settles directly, or payroll clears the payable. */
export type HrFinanceSettlementRoute = 'Direct' | 'Payroll';

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
  /** True when the rule (not the document) decides payroll vs direct settlement. */
  supportsSettlementRoute: boolean;
  kind: HrFinancePostingKind;
  kindName: string;
  settlementRoute: HrFinanceSettlementRoute | null;
  defaultSettlementRoute: HrFinanceSettlementRoute | null;
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
  settlementRoute?: HrFinanceSettlementRoute | null;
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
  kind: HrFinancePostingKind;
  kindName: string;
  /** AP hand-off rows: the Finance vendor invoice and its status as last pulled. */
  vendorInvoiceId: string | null;
  vendorInvoiceNumber: string | null;
  /** AR hand-off rows: the Finance customer invoice. */
  customerInvoiceId: string | null;
  customerInvoiceNumber: string | null;
  externalStatus: string | null;
  externalStatusAt: string | null;
  /** True for a posted AP row: Finance's status (and the voucher, once paid) can be pulled. */
  canRefresh: boolean;
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

/** What Finance says was spent against an HR budget (manpower or training): a read of its book balances. */
export interface HrBudgetFinanceActualsPeriod {
  fiscalPeriodId: string;
  periodName: string;
  startDate: string;
  endDate: string;
  /** The fiscal period starts before or ends after the budget's window; the whole period is counted. */
  partlyOutsideBudget: boolean;
  debits: number;
  credits: number;
  netMovement: number;
  transactionCount: number;
}

export interface HrBudgetFinanceActuals {
  budgetId: string;
  budgetNumber: string;
  budgetKind: 'Manpower' | 'Training' | string;
  unitName: string | null;
  periodStart: string;
  periodEnd: string;
  /** True when an account and Finance's book were resolved and the periods were read. */
  linked: boolean;
  /** Why the actuals could not be read, when they could not — a sentence for the screen. */
  problem: string | null;
  accountId: string | null;
  accountCode: string | null;
  accountName: string | null;
  accountingBookCode: string | null;
  functionalCurrencyCode: string;
  budget: number;
  actual: number;
  variance: number;
  variancePercentage: number;
  periods: HrBudgetFinanceActualsPeriod[];
}
