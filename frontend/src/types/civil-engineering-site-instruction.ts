export type CivilEngineeringSiteInstructionDocumentLookup = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringSiteInstructionLookups = {
  projectEngineerAssignmentId: string;
  projectEngineerName: string;
  projectManagerUserId: string;
  projectManagerName: string;
  contractorBusinessPartnerId: string;
  contractorName: string;
  requiresDmsEvidence: boolean;
  documents: CivilEngineeringSiteInstructionDocumentLookup[];
};

export type CivilEngineeringSiteInstructionEvidence = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  evidenceRole: string;
  documentReference: string;
  documentTitle: string;
  versionNumber: string;
};

export type CivilEngineeringSiteInstructionResponse = {
  id: string;
  sequence: number;
  action:
    | 'ContractorAcknowledged'
    | 'ContractorResponded'
    | 'EngineeringResponseAccepted'
    | 'EngineeringResponseReturned'
    | 'EngineeringFollowUp'
    | 'InstructionClosed';
  message: string;
  actorUserId: string;
  actorName: string;
  timestamp: string;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
};

export type CivilEngineeringSiteInstructionRouting = {
  id: string;
  projectId: string;
  projectSiteInstructionId: string;
  referenceNumber: string;
  title: string;
  description: string;
  status: string;
  approvalStatus: string;
  projectEngineerAssignmentId: string;
  projectEngineerName: string;
  projectManagerUserId: string;
  projectManagerName: string;
  contractorBusinessPartnerId: string;
  contractorName: string;
  contractId?: string | null;
  instructionVersion: number;
  supersedesRoutingId?: string | null;
  contractorResponseReviewedById?: string | null;
  contractorResponseReviewedAt?: string | null;
  contractorResponseReviewReason?: string | null;
  closedById?: string | null;
  closedAt?: string | null;
  workflowInstanceId?: string | null;
  createdAt: string;
  rowVersion: string;
  evidence: CivilEngineeringSiteInstructionEvidence[];
  responses: CivilEngineeringSiteInstructionResponse[];
};

export type CreateCivilEngineeringSiteInstructionRequest = {
  clientRequestId: string;
  projectEngineerAssignmentId: string;
  referenceNumber: string;
  title: string;
  description: string;
  effectiveDate: string;
  supersedesRoutingId?: string;
  evidence: Array<{
    centralDocumentRecordId: string;
    centralDocumentVersionId: string;
  }>;
};

export type ProcessCivilEngineeringSiteInstructionRoutingRequest = {
  clientRequestId: string;
  rowVersion: string;
  approve: boolean;
  reason: string;
};

export type RespondToCivilEngineeringSiteInstructionRequest = {
  clientRequestId: string;
  rowVersion: string;
  action: 'ContractorAcknowledged' | 'ContractorResponded';
  message: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
};

export type ReviewCivilEngineeringSiteInstructionResponseRequest = {
  clientRequestId: string;
  rowVersion: string;
  approve: boolean;
  reason: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};

export type FollowUpCivilEngineeringSiteInstructionRequest = {
  clientRequestId: string;
  rowVersion: string;
  action: 'EngineeringFollowUp' | 'InstructionClosed';
  reason: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};
