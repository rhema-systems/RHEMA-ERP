import type { ProcurementControlEvidenceReferenceKind } from './procurement-control-event';

export type ProcurementMasterDataResourceType =
  | 'SupplierProfile'
  | 'SupplierBankDetails'
  | 'SupplierTaxDetails'
  | 'InventoryItem'
  | 'InventoryCategory'
  | 'UnitOfMeasure'
  | 'Warehouse'
  | 'WarehouseLocation'
  | 'ProcurementPolicySensitive';

export type ProcurementMasterDataPolicyStatus = 'Draft' | 'Active' | 'Retired';
export type ProcurementMasterDataChangeStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Applied'
  | 'Cancelled'
  | 'RevalidationFailed';

export interface ProcurementMasterDataResourceDefinition {
  resourceType: ProcurementMasterDataResourceType;
  code: string;
  name: string;
  description: string;
  allowedFields: string[];
  allowsTenantSingletonTarget: boolean;
  sourceRequirements: string;
}

export interface ProcurementMasterDataPolicy {
  id: string;
  policyKey: string;
  resourceType: ProcurementMasterDataResourceType;
  version: number;
  status: ProcurementMasterDataPolicyStatus;
  name: string;
  description?: string;
  makerRoles: string[];
  checkerRoles: string[];
  requireIndependentApproval: boolean;
  requireRevalidation: boolean;
  requireEvidence: boolean;
  workflowDefinitionId?: string;
  workflowDefinitionName?: string;
  workflowDefinitionVersion?: number;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  supersedesPolicyId?: string;
  activatedAtUtc?: string;
  retiredAtUtc?: string;
  isEffective: boolean;
  rowVersion: string;
}

export interface SaveProcurementMasterDataPolicy {
  resourceType: ProcurementMasterDataResourceType;
  name: string;
  description?: string;
  makerRoles: string[];
  checkerRoles: string[];
  requireIndependentApproval: true;
  requireRevalidation: true;
  requireEvidence: boolean;
  workflowDefinitionId?: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  rowVersion?: string;
}

export interface ProcurementMasterDataChangeSearch {
  resourceType?: ProcurementMasterDataResourceType;
  status?: ProcurementMasterDataChangeStatus;
  targetId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ProcurementMasterDataChangePage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementMasterDataChange[];
}

export interface ProcurementMasterDataChangeSummary {
  policyCount: number;
  effectivePolicyCount: number;
  draftCount: number;
  pendingApprovalCount: number;
  approvedAwaitingEffectiveDateCount: number;
  revalidationFailedCount: number;
  appliedCount: number;
}

export interface ProcurementMasterDataChangeEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface SaveProcurementMasterDataChange {
  resourceType: ProcurementMasterDataResourceType;
  targetId: string;
  proposedChangesJson: string;
  reason: string;
  effectiveAtUtc: string;
  evidence: ProcurementMasterDataChangeEvidenceReference[];
  rowVersion?: string;
}

export interface ProcurementMasterDataChangeEvidence {
  id: string;
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference: string;
  label?: string;
  requirementKey?: string;
  fileName?: string;
  sha256?: string;
  verificationStatus?: string;
  referenceAvailable: boolean;
}

export interface ProcurementMasterDataChange {
  id: string;
  requestNumber: string;
  policyId: string;
  policyVersion: number;
  resourceType: ProcurementMasterDataResourceType;
  targetKind: string;
  targetId: string;
  targetReference: string;
  status: ProcurementMasterDataChangeStatus;
  beforeJson: string;
  beforeHash: string;
  proposedChangesJson: string;
  proposedChangesHash: string;
  appliedAfterJson?: string;
  appliedAfterHash?: string;
  reason: string;
  effectiveAtUtc: string;
  makerUserId: string;
  submittedById?: string;
  submittedAtUtc?: string;
  checkerUserId?: string;
  checkedAtUtc?: string;
  checkerComment?: string;
  revalidationPassed?: boolean;
  revalidatedAtUtc?: string;
  revalidationMessage?: string;
  revalidatedSnapshotHash?: string;
  appliedById?: string;
  appliedAtUtc?: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  correlationId: string;
  rowVersion: string;
  evidence: ProcurementMasterDataChangeEvidence[];
}
