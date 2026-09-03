/**
 * Employee bulk import — the wire types of `api/hr/employees/import-sessions`.
 * Backend: `EmployeeImportDtos.cs`; design: `docs/HR/HR-EMPLOYEE-IMPORT-DESIGN.md`.
 */

export type EmployeeImportSessionStatus =
  | 'Validated'
  | 'CommitRequested'
  | 'Committing'
  | 'Committed'
  | 'CommittedWithErrors'
  | 'Cancelled'
  | 'Failed';

export type EmployeeImportRowOutcome =
  | 'Ready'
  | 'Warning'
  | 'Error'
  | 'Committed'
  | 'CommittedWithIssues'
  | 'Failed'
  | 'Skipped';

export type EmployeeImportCommitPolicy = 'ValidRowsOnly' | 'AllOrNothing';

export type EmployeeImportFindingSeverity = 'Error' | 'Warning';

export interface EmployeeImportFinding {
  severity: EmployeeImportFindingSeverity;
  column?: string | null;
  /** Excel address, e.g. `Employees!L7`. */
  cell?: string | null;
  message: string;
  suggestions: string[];
}

export interface EmployeeImportCounterReconcile {
  formatId: string;
  register: string;
  counterBefore: number;
  counterAfter: number;
  message?: string | null;
}

export interface EmployeeImportSessionSummary {
  id: string;
  reference: string;
  fileName: string;
  fileSizeBytes: number;
  templateVersion: number;
  uploadedByName?: string | null;
  uploadedOn: string;
  status: EmployeeImportSessionStatus;
  commitPolicy?: EmployeeImportCommitPolicy | null;
  commitRequestedOn?: string | null;
  commitStartedOn?: string | null;
  commitCompletedOn?: string | null;
  totalRows: number;
  readyCount: number;
  warningCount: number;
  errorCount: number;
  skippedCount: number;
  committedCount: number;
  failedCount: number;
  failureMessage?: string | null;
  fileFindings: EmployeeImportFinding[];
  counterReconciliation: EmployeeImportCounterReconcile[];
  sourceDocumentRecordId?: string | null;
}

export interface EmployeeImportRow {
  id: string;
  rowNumber: number;
  staffNumber?: string | null;
  displayName?: string | null;
  employmentType?: string | null;
  outcome: EmployeeImportRowOutcome;
  skip: boolean;
  errorCount: number;
  warningCount: number;
  findings: EmployeeImportFinding[];
  /** The cells as read, keyed by column key. */
  values: Record<string, string | null>;
  createdEmployeeId?: string | null;
  commitMessage?: string | null;
  committedOn?: string | null;
}

export interface EmployeeImportRowPage {
  items: EmployeeImportRow[];
  total: number;
  page: number;
  pageSize: number;
}

export interface EmployeeImportProgress {
  status: EmployeeImportSessionStatus;
  totalRows: number;
  toCommit: number;
  committedCount: number;
  failedCount: number;
  remaining: number;
  commitStartedOn?: string | null;
  commitCompletedOn?: string | null;
  failureMessage?: string | null;
}

export interface EmployeeImportFollowUp {
  employeeId: string;
  staffNumber: string;
  displayName: string;
  rowNumber: number;
  items: string[];
}

export interface EmployeeImportColumnGuide {
  key: string;
  header: string;
  required: boolean;
  kind: string;
  help: string;
}

/** The 400 body when the workbook itself is unusable (wrong template, missing columns, empty). */
export interface EmployeeImportFileRejection {
  message: string;
  findings: EmployeeImportFinding[];
}

export const SESSION_STATUS_LABEL: Record<EmployeeImportSessionStatus, string> = {
  Validated: 'Checked — awaiting commit',
  CommitRequested: 'Queued for commit',
  Committing: 'Committing',
  Committed: 'Committed',
  CommittedWithErrors: 'Committed with errors',
  Cancelled: 'Cancelled',
  Failed: 'Failed',
};

export const ROW_OUTCOME_LABEL: Record<EmployeeImportRowOutcome, string> = {
  Ready: 'Ready',
  Warning: 'Warning',
  Error: 'Error',
  Committed: 'Created',
  CommittedWithIssues: 'Created with issues',
  Failed: 'Failed',
  Skipped: 'Skipped',
};
