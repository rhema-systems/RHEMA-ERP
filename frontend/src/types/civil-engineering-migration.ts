export type CivilEngineeringMigrationSource = 'Spreadsheet' | 'DocumentRegister' | 'SharedDrive' | 'PhysicalFileRegister' | 'LegacyDatabase';
export type CivilEngineeringMigrationRecordType = 'Drawing' | 'Specification' | 'SiteReport' | 'Permit' | 'MaintenanceScope' | 'Complaint' | 'TestReport' | 'PhysicalFileReference';
export type CivilEngineeringMigrationBatchStatus = 'ValidationFailed' | 'AwaitingReconciliation' | 'Reconciled' | 'ReadyForOwnerPosting';

export type CivilEngineeringMigrationDocument = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringMigrationLookups = {
  sourceTypes: CivilEngineeringMigrationSource[];
  recordTypes: CivilEngineeringMigrationRecordType[];
  documents: CivilEngineeringMigrationDocument[];
  reconciliationDocuments: CivilEngineeringMigrationDocument[];
  preservePhysicalFileReference: boolean;
  requireReconciliation: boolean;
  requireSignedAcceptance: boolean;
};

export type CivilEngineeringMigrationRecordInput = {
  recordType: CivilEngineeringMigrationRecordType;
  sourceReference: string;
  title: string;
  recordDate?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  physicalFileReference?: string;
};

export type StageCivilEngineeringMigrationBatchRequest = {
  clientRequestId: string;
  sourceType: CivilEngineeringMigrationSource;
  sourceRegisterReference: string;
  records: CivilEngineeringMigrationRecordInput[];
};

export type ReconcileCivilEngineeringMigrationBatchRequest = {
  rowVersion: string;
  reconciliationDocumentRecordId: string;
  reconciliationDocumentVersionId: string;
  declaration: string;
};

export type SignOffCivilEngineeringMigrationBatchRequest = {
  rowVersion: string;
  declaration: string;
};

export type CivilEngineeringMigrationIssue = { sequence?: number | null; code: string; severity: string; message: string };
export type CivilEngineeringMigrationRecord = {
  sequence: number;
  recordType: CivilEngineeringMigrationRecordType;
  sourceReference: string;
  title: string;
  recordDate?: string | null;
  documentReference?: string | null;
  physicalFileReference?: string | null;
  validationStatus: string;
  validationMessage?: string | null;
};
export type CivilEngineeringMigrationBatch = {
  id: string;
  projectId: string;
  sourceType: CivilEngineeringMigrationSource;
  sourceRegisterReference: string;
  status: CivilEngineeringMigrationBatchStatus;
  recordCount: number;
  errorCount: number;
  createdAt: string;
  reconciledAt?: string | null;
  signedOffAt?: string | null;
  isReadyForOwnerPosting: boolean;
  postingBoundary: string;
  rowVersion: string;
  records: CivilEngineeringMigrationRecord[];
  issues: CivilEngineeringMigrationIssue[];
};
export type CivilEngineeringMigrationRevision = { action: string; actorName: string; actorRoles?: string | null; reason?: string | null; correlationId: string; createdAt: string };
