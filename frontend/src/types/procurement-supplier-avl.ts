import type { SupplierDueDiligenceEvidenceReference } from './procurement-supplier-due-diligence';

export type SupplierAvlRegisterStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Published'
  | 'Rejected'
  | 'Retired';

export type SupplierAvlEntryStatus = 'Active' | 'Suspended' | 'Expired';
export type SupplierAvlEntryAction = 'Suspended' | 'Reinstated' | 'Expired';

export interface SupplierAvlSummary {
  totalRegisters: number;
  draftCount: number;
  pendingApprovalCount: number;
  publishedCount: number;
  currentEntryCount: number;
  suspendedEntryCount: number;
  policyAvailable: boolean;
  policyProfileCode?: string;
  policyProfileVersion?: number;
  reviewFrequencyMonths?: number;
  policyReleaseGate?: string;
}

export interface SupplierAvlListItem {
  id: string;
  registerCode: string;
  version: number;
  reviewYear: number;
  status: SupplierAvlRegisterStatus;
  effectiveFromUtc: string;
  expiresAtUtc: string;
  scheduledRetirementAtUtc?: string;
  entryCount: number;
  activeEntryCount: number;
  suspendedEntryCount: number;
  policyProfileCode: string;
  policyProfileVersion: number;
  allowedActions: string[];
  rowVersion: string;
}

export interface SupplierAvlEntryStatusHistory {
  id: string;
  action: SupplierAvlEntryAction;
  beforeStatus: SupplierAvlEntryStatus;
  afterStatus: SupplierAvlEntryStatus;
  reason: string;
  actorUserId: string;
  occurredAtUtc: string;
  correlationId: string;
  integrityHash: string;
}

export interface SupplierAvlEntry {
  id: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  status: SupplierAvlEntryStatus;
  dueDiligenceReviewId: string;
  dueDiligenceReviewReference?: string;
  registrationId?: string;
  evidencePackVersionId?: string;
  qualifiedListEntryId?: string;
  addedAtUtc: string;
  suspendedAtUtc?: string;
  suspensionReason?: string;
  reinstatedAtUtc?: string;
  expiredAtUtc?: string;
  eligibilityDecisionHash: string;
  integrityHash: string;
  rowVersion: string;
  statusHistory: SupplierAvlEntryStatusHistory[];
}

export interface SupplierAvlPublicationSnapshot {
  id: string;
  sequence: number;
  publishedAtUtc: string;
  publishedById: string;
  integrityHash: string;
}

export interface SupplierAvlRegister extends SupplierAvlListItem {
  policyDecisionId: string;
  policyProfileId: string;
  policyValueHash: string;
  policySnapshotJson: string;
  reviewFrequencyMonths: number;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  notes?: string;
  reviewComment?: string;
  submittedById?: string;
  submittedAtUtc?: string;
  approvedById?: string;
  approvedAtUtc?: string;
  publishedById?: string;
  publishedAtUtc?: string;
  integrityHash: string;
  entries: SupplierAvlEntry[];
  publicationSnapshots: SupplierAvlPublicationSnapshot[];
}

export interface SupplierAvlPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: SupplierAvlListItem[];
}

export interface SupplierAvlSearch {
  search?: string;
  status?: SupplierAvlRegisterStatus;
  page?: number;
  pageSize?: number;
}

export interface CreateSupplierAvl {
  reviewYear: number;
  effectiveFromUtc: string;
  workflowDefinitionId: string;
  notes?: string;
}

export interface UpdateSupplierAvl {
  rowVersion: string;
  effectiveFromUtc: string;
  notes?: string;
}

export interface AddSupplierAvlEntry {
  businessPartnerId: string;
  registerRowVersion: string;
}

export interface SupplierAvlLifecycleRequest {
  rowVersion: string;
  comment: string;
  evidence: SupplierDueDiligenceEvidenceReference[];
}

export interface SupplierAvlEntryLifecycleRequest {
  rowVersion: string;
  reason: string;
  evidence: SupplierDueDiligenceEvidenceReference[];
}

export interface SupplierAvlWorkflowOption {
  id: string;
  name: string;
  version: number;
}

export interface SupplierAvlSupplierOption {
  id: string;
  code: string;
  name: string;
}

export interface SupplierAvlCurrentState {
  businessPartnerId: string;
  policyAvailable: boolean;
  registerAvailable: boolean;
  isCurrent: boolean;
  code: string;
  message: string;
  registerId?: string;
  registerCode?: string;
  registerVersion?: number;
  entryId?: string;
  entryStatus?: SupplierAvlEntryStatus;
  effectiveFromUtc?: string;
  expiresAtUtc?: string;
  integrityHash?: string;
}
