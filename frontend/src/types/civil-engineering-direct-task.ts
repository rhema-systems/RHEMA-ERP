export type CivilEngineeringUrgency = 'Routine' | 'Priority' | 'Urgent' | 'Emergency';

export type CivilEngineeringDirectTaskAssignee = {
  userId: string;
  roleId: string;
  displayName: string;
  roleName: string;
};

export type CivilEngineeringDirectTaskDocument = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringDirectTaskMeasurementUnit = {
  id: string;
  code: string;
  name: string;
  symbol?: string | null;
  category: string;
};

export type CivilEngineeringDirectTaskLookups = {
  assignees: CivilEngineeringDirectTaskAssignee[];
  urgencies: CivilEngineeringUrgency[];
  documents: CivilEngineeringDirectTaskDocument[];
  requireDueDate: boolean;
  urgentResponseHours: number;
};

export type CivilEngineeringDirectTaskFeedbackAction = 'Acknowledge' | 'UpdateProgress' | 'Complete' | 'Accept' | 'Return';

export type CivilEngineeringDirectTaskFeedbackLookups = {
  documents: CivilEngineeringDirectTaskDocument[];
  availableActions: CivilEngineeringDirectTaskFeedbackAction[];
  measurementUnits: CivilEngineeringDirectTaskMeasurementUnit[];
  requireFeedbackEvidence: boolean;
  requireClosureAcceptance: boolean;
};

export type ProcessCivilEngineeringDirectTaskFeedbackRequest = {
  clientRequestId: string;
  action: CivilEngineeringDirectTaskFeedbackAction;
  message?: string;
  progressPercent?: number;
  measurementValue?: number;
  measurementUnitId?: string;
  capturedOfflineAtUtc?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  rowVersion: string;
};

export type CivilEngineeringDirectTaskFeedback = {
  id: string;
  sequence: number;
  action: CivilEngineeringDirectTaskFeedbackAction;
  progressPercent?: number | null;
  measurementValue?: number | null;
  measurementUnitLabel?: string | null;
  capturedOfflineAtUtc?: string | null;
  message?: string | null;
  actorName: string;
  documentReference?: string | null;
  workflowOutcome?: string | null;
  correlationId: string;
  createdAt: string;
};

export type CreateCivilEngineeringDirectTaskRequest = {
  clientRequestId: string;
  title: string;
  instructions: string;
  assignedToUserId: string;
  assignedRoleId: string;
  urgency: CivilEngineeringUrgency;
  urgencyReason?: string;
  dueDate?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
};

export type EscalateCivilEngineeringUrgentTasksRequest = {
  clientRequestId: string;
};

export type CivilEngineeringDirectTask = {
  id: string;
  projectId: string;
  workItemId: string;
  title: string;
  instructions: string;
  assignedToUserId: string;
  assignedToName: string;
  assignedRoleId: string;
  assignedRoleName: string;
  urgency: CivilEngineeringUrgency;
  dueDate: string;
  status: string;
  approvalStatus: string;
  isUrgentPath: boolean;
  urgencyReason?: string | null;
  urgentResponseDueAt?: string | null;
  urgentEscalatedAt?: string | null;
  progressPercent: number;
  acknowledgedAt?: string | null;
  completedAt?: string | null;
  acceptedAt?: string | null;
  documentReference?: string | null;
  workflowInstanceId?: string | null;
  createdAt: string;
  rowVersion: string;
};
