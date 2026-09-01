export type CivilEngineeringInspectionLookupOption = { id: string; label: string };
export type CivilEngineeringInspectionPlanningLookup = { id: string; label: string; spatialReference: string };
export type CivilEngineeringInspectionDocument = { centralDocumentRecordId: string; centralDocumentVersionId: string; documentReference: string; title: string; versionNumber: string };

export type CivilEngineeringInspectionLookups = {
  planningGisValidations: CivilEngineeringInspectionPlanningLookup[];
  inspectors: CivilEngineeringInspectionLookupOption[];
  documents: CivilEngineeringInspectionDocument[];
  requireIndependentReinspection: boolean;
  blockCompletionOnFailure: boolean;
};

export type CivilEngineeringInspectionControl = {
  id: string; projectId: string; qualityCheckpointId: string; planningGisValidationId: string; planningGisValidationLabel: string;
  inspectorUserId: string; inspectorName: string; reinspectionInspectorUserId?: string | null; reinspectionInspectorName?: string | null;
  nonConformanceId?: string | null; purpose: string; scheduledAt: string; spatialReferenceSnapshot: string; boundaryCoordinatesSnapshot?: string | null;
  stage: string; status: string; findings?: string | null; correctiveAction?: string | null; inspectedAt?: string | null; correctiveActionRecordedAt?: string | null; reinspectedAt?: string | null; closedAt?: string | null;
  planDocumentRecordId: string; planDocumentVersionId: string; inspectionDocumentRecordId?: string | null; inspectionDocumentVersionId?: string | null;
  correctiveActionDocumentRecordId?: string | null; correctiveActionDocumentVersionId?: string | null; reinspectionDocumentRecordId?: string | null; reinspectionDocumentVersionId?: string | null;
  closureDocumentRecordId?: string | null; closureDocumentVersionId?: string | null;
  workflowDefinitionId?: string | null; workflowInstanceId?: string | null; planApprovalStatus: string;
  planApprovedById?: string | null; planApprovedAt?: string | null; planRejectionReason?: string | null; rowVersion: string;
};

export type CreateCivilEngineeringInspectionControlRequest = {
  clientRequestId: string; planningGisValidationId: string; inspectorUserId: string; scheduledAt: string; purpose: string;
  planDocumentRecordId: string; planDocumentVersionId: string;
};

export enum CivilEngineeringInspectionAction {
  RecordPassed = 1,
  RecordFailed = 2,
  RecordCorrectiveAction = 3,
  RecordReinspectionPassed = 4,
  RecordReinspectionFailed = 5,
  Close = 6,
  ApprovePlan = 7,
  RejectPlan = 8,
}

export type ProcessCivilEngineeringInspectionControlRequest = {
  clientRequestId: string; rowVersion: string; action: CivilEngineeringInspectionAction; findings?: string | null; correctiveAction?: string | null;
  reviewComment?: string | null; reinspectionInspectorUserId?: string | null; centralDocumentRecordId?: string | null; centralDocumentVersionId?: string | null;
};

export type CivilEngineeringInspectionRevision = { id: string; action: string; fromStage: string; toStage: string; actorName: string; actorRoles?: string | null; reason?: string | null; correlationId: string; createdAt: string };
