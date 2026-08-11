import { apiService } from '@/services/api.service';

export type MaterialLineType =
  'materialOnSite' | 'materialOffSite' | 'tdcSuppliedMaterial';

export interface MaterialValuationLookup {
  worksheetId: string;
  interimValuationId: string;
  contractId: string;
  label: string;
  contractNumber: string;
  contractorName: string;
  currency: string;
  status: string;
}

export interface MaterialSourceLookup {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  approvedRateId?: string | null;
  approvedUnitRate?: number | null;
  currency: string;
}

export interface MaterialIssueLookup {
  issueVoucherLineId: string;
  inventoryItemId: string;
  voucherNumber: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  quantity: number;
  unitCost: number;
  totalValue: number;
  integrityHash: string;
}

export interface MaterialEvidenceLookup {
  evidenceId: string;
  worksheetId: string;
  evidenceType: 'materialOnSite' | 'materialOffSite';
  label: string;
  fileName: string;
  checksumSha256: string;
}

export interface MaterialReconciliationLine {
  id: string;
  sequence: number;
  lineType: MaterialLineType;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  quantity: number;
  deliveredUnitCost?: number | null;
  approvedRateId?: string | null;
  approvedUnitRate?: number | null;
  appliedUnitRate: number;
  totalValue: number;
  inventoryIssueVoucherLineId?: string | null;
  issueVoucherNumber?: string | null;
  valuationEvidenceId?: string | null;
  sourceHash: string;
}

export interface MaterialReconciliation {
  id: string;
  projectId: string;
  contractId: string;
  valuationWorksheetId: string;
  reconciliationNumber: string;
  status: string;
  approvalStatus: string;
  contractNumber: string;
  contractorName: string;
  currency: string;
  valuationBasis: string;
  materialOnSiteAmount: number;
  materialOffSiteAmount: number;
  tdcSuppliedDeductionAmount: number;
  contractorConfirmedAt?: string | null;
  workflowInstanceId?: string | null;
  notes?: string | null;
  rowVersion: string;
  lines: MaterialReconciliationLine[];
}

export interface MaterialReconciliationWorkspace {
  valuations: MaterialValuationLookup[];
  materialSources: MaterialSourceLookup[];
  inventoryIssues: MaterialIssueLookup[];
  evidence: MaterialEvidenceLookup[];
  reconciliations: MaterialReconciliation[];
}

export interface MaterialReconciliationRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson: string;
  createdAt: string;
}

export interface SaveMaterialReconciliationLine {
  lineType: MaterialLineType;
  inventoryItemId: string;
  quantity: number;
  deliveredUnitCost?: number | null;
  approvedRateId?: string | null;
  inventoryIssueVoucherLineId?: string | null;
  valuationEvidenceId?: string | null;
}

export interface MaterialActionRequest {
  clientRequestId: string;
  rowVersion: string;
  reason: string;
}

const root = '/quantity-survey/material-reconciliations';
const externalRoot = (projectId: string) =>
  `/projects/external/my-projects/${projectId}/material-reconciliations`;

export const quantitySurveyMaterialReconciliationService = {
  workspace: (projectId: string) =>
    apiService.get<MaterialReconciliationWorkspace>(root, { projectId }),
  externalWorkspace: (projectId: string) =>
    apiService.get<MaterialReconciliationWorkspace>(externalRoot(projectId)),
  save: (
    projectId: string,
    request: {
      clientRequestId: string;
      valuationWorksheetId: string;
      rowVersion?: string | null;
      reason: string;
      notes?: string | null;
      lines: SaveMaterialReconciliationLine[];
    }
  ) =>
    apiService.put<MaterialReconciliation>(
      `${root}?projectId=${encodeURIComponent(projectId)}`,
      request
    ),
  submit: (id: string, request: MaterialActionRequest) =>
    apiService.post<MaterialReconciliation>(`${root}/${id}/submit`, request),
  approve: (id: string, request: MaterialActionRequest) =>
    apiService.post<MaterialReconciliation>(`${root}/${id}/approve`, request),
  reject: (id: string, request: MaterialActionRequest) =>
    apiService.post<MaterialReconciliation>(`${root}/${id}/reject`, request),
  confirm: (
    projectId: string,
    id: string,
    request: MaterialActionRequest & { attestation: string }
  ) =>
    apiService.post<MaterialReconciliation>(
      `${externalRoot(projectId)}/${id}/confirm`,
      request
    ),
  history: (id: string) =>
    apiService.get<MaterialReconciliationRevision[]>(`${root}/${id}/history`),
};
