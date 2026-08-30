export type CivilEngineeringMaintenanceCostingHandoffLookupOption = {
  id: string;
  projectId?: string | null;
  label: string;
  status: string;
};

export type CivilEngineeringMaintenanceCostingHandoffLookups = {
  approvedAssessments: CivilEngineeringMaintenanceCostingHandoffLookupOption[];
  projects: CivilEngineeringMaintenanceCostingHandoffLookupOption[];
  approvedEstimates: CivilEngineeringMaintenanceCostingHandoffLookupOption[];
  approvedProjectBudgets: CivilEngineeringMaintenanceCostingHandoffLookupOption[];
  purchaseRequisitions: CivilEngineeringMaintenanceCostingHandoffLookupOption[];
  contracts: CivilEngineeringMaintenanceCostingHandoffLookupOption[];
};

export type CivilEngineeringMaintenanceCostingHandoff = {
  id: string;
  assessmentId: string;
  intakeNumber: string;
  projectId: string;
  projectLabel: string;
  quantitySurveyEstimateVersionId: string;
  estimateLabel: string;
  estimateAmount: number;
  currencyCode: string;
  projectBudgetRevisionId?: string | null;
  projectBudgetLabel?: string | null;
  purchaseRequisitionId?: string | null;
  purchaseRequisitionLabel?: string | null;
  contractId?: string | null;
  contractLabel?: string | null;
  stage: string;
  status: string;
  approvalStatus: string;
  workflowInstanceId?: string | null;
  lastRevalidatedAt?: string | null;
  lastRevalidationSummary?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
};

export type CreateCivilEngineeringMaintenanceCostingHandoffRequest = {
  clientRequestId: string;
  assessmentId: string;
  projectId: string;
  quantitySurveyEstimateVersionId: string;
  projectBudgetRevisionId?: string | null;
  purchaseRequisitionId?: string | null;
  contractId?: string | null;
};

export type CivilEngineeringMaintenanceCostingHandoffAction = 'SubmitCosting' | 'ApproveCosting' | 'RejectCosting' | 'RefreshAuthoritativeStatus';
export type CivilEngineeringMaintenanceCostingHandoffActionRequest = {
  clientRequestId: string;
  action: CivilEngineeringMaintenanceCostingHandoffAction;
  reason?: string | null;
  rowVersion: string;
};
