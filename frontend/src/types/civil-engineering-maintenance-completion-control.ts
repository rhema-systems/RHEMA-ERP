export type CivilEngineeringMaintenanceCompletionLookup = { id: string; label: string; status: string; maintenanceAssetId?: string | null };
export type CivilEngineeringMaintenanceCompletionDocument = { centralDocumentRecordId: string; centralDocumentVersionId: string; documentReference: string; title: string; versionNumber: string };

export type CivilEngineeringMaintenanceCompletionLookups = {
  completedExecutionLinks: CivilEngineeringMaintenanceCompletionLookup[];
  documents: CivilEngineeringMaintenanceCompletionDocument[];
  requireInspectionBeforeClosure: boolean;
  requireClosureEvidence: boolean;
};

export type CivilEngineeringMaintenanceCompletionControl = {
  id: string;
  executionLinkId: string;
  intakeNumber: string;
  projectId: string;
  projectLabel: string;
  maintenanceAssetId: string;
  maintenanceAssetLabel: string;
  jobCardId?: string | null;
  jobCardNumber?: string | null;
  workOrderId?: string | null;
  workOrderNumber?: string | null;
  workOrderStatus?: string | null;
  civilEngineerUserId: string;
  civilEngineerName: string;
  supervisingCivilEngineerUserId: string;
  supervisingCivilEngineerName: string;
  hodUserId: string;
  hodName: string;
  stage: string;
  status: string;
  completionSummary: string;
  completionDocumentRecordId: string;
  completionDocumentVersionId: string;
  completionEvidenceReference: string;
  completionReportedAt: string;
  inspectionStatus: string;
  paymentDirectionStatus: string;
  closedAt?: string | null;
  rowVersion: string;
};

export type CreateCivilEngineeringMaintenanceCompletionControlRequest = {
  clientRequestId: string;
  executionLinkId: string;
  completionSummary: string;
  completionDocumentRecordId: string;
  completionDocumentVersionId: string;
};

export type CivilEngineeringMaintenanceCompletionAction = 'SubmitToHod' | 'ReturnToCivilEngineer' | 'ApproveCompletion' | 'DirectInspection' | 'RecordInspectionPassed' | 'RecordInspectionFailed' | 'DirectPayment' | 'Close';

export type ProcessCivilEngineeringMaintenanceCompletionControlRequest = {
  clientRequestId: string;
  rowVersion: string;
  action: CivilEngineeringMaintenanceCompletionAction;
  note?: string | null;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
};
