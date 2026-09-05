export type CivilEngineeringExtensionOfTimeLookup = { id: string; label: string };

export type CivilEngineeringExtensionOfTimeDocument = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringExtensionOfTimeLookups = {
  contracts: CivilEngineeringExtensionOfTimeLookup[];
  quantitySurveyVariations: CivilEngineeringExtensionOfTimeLookup[];
  documents: CivilEngineeringExtensionOfTimeDocument[];
  requireQuantitySurveyVariationForCostImpact: boolean;
  requireFinanceBudgetRevalidation: boolean;
  requireProcurementContractRevalidation: boolean;
};

export type CivilEngineeringExtensionOfTimeControl = {
  id: string;
  projectId: string;
  projectExtensionOfTimeId: string;
  referenceNumber: string;
  title: string;
  reason: string;
  scopeSummary: string;
  contractId: string;
  contractLabel: string;
  hasCostImpact: boolean;
  quantitySurveyVariationOrderId?: string | null;
  quantitySurveyVariationLabel?: string | null;
  daysRequested: number;
  daysApproved?: number | null;
  requestedDate: string;
  decisionDate?: string | null;
  proposedRevisedCompletionDate?: string | null;
  status: string;
  approvalStatus: string;
  rejectionReason?: string | null;
  workflowInstanceId?: string | null;
  evidenceDocumentRecordId: string;
  evidenceDocumentVersionId: string;
  evidenceDocumentReference: string;
  evidenceDocumentTitle: string;
  evidenceVersionNumber: string;
  rowVersion: string;
};

export type CreateCivilEngineeringExtensionOfTimeRequest = {
  clientRequestId: string;
  contractId: string;
  quantitySurveyVariationOrderId?: string | null;
  title: string;
  reason: string;
  scopeSummary: string;
  daysRequested: number;
  proposedRevisedCompletionDate: string;
  hasCostImpact: boolean;
  evidenceDocumentRecordId: string;
  evidenceDocumentVersionId: string;
};

export type ReviewCivilEngineeringExtensionOfTimeRequest = {
  clientRequestId: string;
  rowVersion: string;
  approve: boolean;
  comment: string;
};

export type CivilEngineeringExtensionOfTimeRevision = {
  id: string;
  action: string;
  fromStatus: string;
  toStatus: string;
  actorName: string;
  actorRoles?: string | null;
  reason?: string | null;
  correlationId: string;
  createdAt: string;
};
