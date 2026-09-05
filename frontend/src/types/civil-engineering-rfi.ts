export type CivilEngineeringRfiDocumentLookup = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringRfiLookups = {
  projectEngineerAssignmentId: string;
  projectEngineerName: string;
  projectManagerUserId: string;
  projectManagerName: string;
  requiresDmsEvidence: boolean;
  documents: CivilEngineeringRfiDocumentLookup[];
};

export type CivilEngineeringRfiEvidence = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  evidenceRole: string;
  documentReference: string;
  documentTitle: string;
  versionNumber: string;
};

export type CivilEngineeringRfiResponse = {
  id: string;
  sequence: number;
  responseText: string;
  respondedByUserId: string;
  respondedByName: string;
  timestamp: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};

export type CivilEngineeringRfiRouting = {
  id: string;
  projectId: string;
  projectRfiId: string;
  referenceNumber: string;
  subject: string;
  question: string;
  priority: string;
  raisedDate: string;
  responseDueDate?: string | null;
  status: string;
  approvalStatus: string;
  projectEngineerAssignmentId: string;
  projectEngineerName: string;
  projectManagerUserId: string;
  projectManagerName: string;
  externalBusinessPartnerName: string;
  workflowInstanceId?: string | null;
  rowVersion: string;
  evidence: CivilEngineeringRfiEvidence[];
  responses: CivilEngineeringRfiResponse[];
};

export type SubmitCivilEngineeringRfiResponseRequest = {
  clientRequestId: string;
  rowVersion: string;
  responseText: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};

export type ProcessCivilEngineeringRfiResponseRequest = {
  clientRequestId: string;
  rowVersion: string;
  approve: boolean;
  reason: string;
};
