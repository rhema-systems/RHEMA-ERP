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

/** What a session may do to the register. Chosen at upload. */
export type EmployeeImportMode = 'CreateOnly' | 'UpdateOnly' | 'CreateOrUpdate';

export type EmployeeImportRowAction = 'Create' | 'Update';

/** One field an Update row would change. */
export interface EmployeeImportChange {
  field: string;
  from?: string | null;
  to?: string | null;
}

export const IMPORT_MODE_LABEL: Record<EmployeeImportMode, string> = {
  CreateOnly: 'Create new employees',
  UpdateOnly: 'Update existing employees',
  CreateOrUpdate: 'Create or update',
};

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
  mode: EmployeeImportMode;
  /** Rows the checker marked as creates / updates, errors excluded. */
  createCount: number;
  updateCount: number;
  /** Of committedCount, how many were creates and how many updates. */
  createdCount: number;
  updatedCount: number;
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
  action: EmployeeImportRowAction;
  targetEmployeeId?: string | null;
  skip: boolean;
  errorCount: number;
  warningCount: number;
  findings: EmployeeImportFinding[];
  /** For an Update row: what would change. Empty for a Create row. */
  changes: EmployeeImportChange[];
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
  createdCount: number;
  updatedCount: number;
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
  Committed: 'Written',
  CommittedWithIssues: 'Written with issues',
  Failed: 'Failed',
  Skipped: 'Skipped',
};
