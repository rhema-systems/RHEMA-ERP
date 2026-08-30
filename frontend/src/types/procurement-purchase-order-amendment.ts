import type { ProcurementPurchaseOrderSourceType } from '@/services/purchasingService';

export type PurchaseOrderAmendmentStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Applied'
  | 'Rejected'
  | 'Cancelled'
  | 'Dispatched'
  | 'Acknowledged';

export type PurchaseOrderDispatchChannel =
  | 'SupplierPortal'
  | 'Email'
  | 'Courier'
  | 'HandDelivery'
  | 'Other';

export type PurchaseOrderAcknowledgementOutcome =
  | 'Received'
  | 'Accepted'
  | 'Disputed';

export interface PurchaseOrderAmendmentItemRequest {
  purchaseOrderItemId?: string;
  inventoryItemId?: string;
  supplierItemCode?: string;
  itemDescription: string;
  orderedQuantity: number;
  unitOfMeasure: string;
  itemUnitOfMeasureId?: string;
  warehouseId?: string;
  unitPrice: number;
  priceListLineId?: string;
  expectedDeliveryDate?: string;
  notes?: string;
}

export interface CreatePurchaseOrderAmendmentRequest {
  reason: string;
  changeScope: string;
  sourceType?: ProcurementPurchaseOrderSourceType;
  sourceId?: string;
  businessPartnerId?: string;
  requiredDate?: string;
  promisedDate?: string;
  paymentTerms?: string;
  shippingTerms?: string;
  terms?: string;
  notes?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  deliveryInstructions?: string;
  taxAmount: number;
  shippingCost: number;
  miscellaneousCost: number;
  discountAmount: number;
  items: PurchaseOrderAmendmentItemRequest[];
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  idempotencyKey: string;
}

export interface PurchaseOrderAmendmentLifecycleRequest {
  comment: string;
  rowVersion: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
}

export interface DecidePurchaseOrderAmendmentRequest
  extends PurchaseOrderAmendmentLifecycleRequest {
  approved: boolean;
}

export interface DispatchPurchaseOrderAmendmentRequest {
  channel: PurchaseOrderDispatchChannel;
  destination: string;
  dispatchReference: string;
  documentReference: string;
  organizationSignatureEvidenceReference: string;
  dispatchEvidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  idempotencyKey: string;
}

export interface AcknowledgePurchaseOrderAmendmentRequest {
  outcome: PurchaseOrderAcknowledgementOutcome;
  acknowledgementChannel: string;
  acknowledgementReference: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  comments?: string;
  idempotencyKey: string;
}

export interface PurchaseOrderAmendmentDiff {
  path: string;
  before?: string;
  after?: string;
}

export interface PurchaseOrderCommitmentAdjustment {
  id: string;
  sequence: number;
  purchaseOrderAmountBefore: number;
  purchaseOrderAmountAfter: number;
  requisitionExposureBefore: number;
  requisitionExposureAfter: number;
  commitmentAmountBefore: number;
  commitmentAmountAfter: number;
  deltaAmount: number;
  budgetCommittedBefore: number;
  budgetCommittedAfter: number;
  budgetAvailableBefore: number;
  budgetAvailableAfter: number;
  currency: string;
  appliedAtUtc: string;
  appliedByName: string;
  integrityHash: string;
}

export interface PurchaseOrderAmendmentAcknowledgement {
  id: string;
  sequence: number;
  outcome: PurchaseOrderAcknowledgementOutcome;
  acknowledgedAtUtc: string;
  acknowledgedByUserId: string;
  acknowledgedByBusinessPartnerId?: string;
  acknowledgementChannel: string;
  acknowledgementReference: string;
  evidenceReference: string;
  comments?: string;
  integrityHash: string;
}

export interface PurchaseOrderAmendmentDispatch {
  id: string;
  revisionNumber: number;
  sequence: number;
  channel: PurchaseOrderDispatchChannel;
  destination: string;
  dispatchReference: string;
  documentReference: string;
  organizationSignatureEvidenceReference: string;
  dispatchEvidenceReference: string;
  dispatchedAtUtc: string;
  dispatchedByName: string;
  integrityHash: string;
  acknowledgements: PurchaseOrderAmendmentAcknowledgement[];
}

export interface PurchaseOrderAmendment {
  id: string;
  purchaseOrderId: string;
  purchaseOrderNumber: string;
  supplierName: string;
  amendmentNumber: string;
  amendmentSequence: number;
  baseRevisionNumber: number;
  proposedRevisionNumber: number;
  status: PurchaseOrderAmendmentStatus;
  reason: string;
  changeScope: string;
  beforeTotalAmount: number;
  proposedTotalAmount: number;
  commitmentDelta: number;
  currency: string;
  proposedSourceType: ProcurementPurchaseOrderSourceType;
  proposedSourceId: string;
  proposedSourceReference: string;
  proposedBusinessPartnerId: string;
  proposedBusinessPartnerName: string;
  beforeIntegrityHash: string;
  proposedIntegrityHash: string;
  diffIntegrityHash: string;
  diffs: PurchaseOrderAmendmentDiff[];
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  submittedAtUtc?: string;
  submittedByName?: string;
  decidedAtUtc?: string;
  decidedByName?: string;
  decisionComment?: string;
  appliedAtUtc?: string;
  appliedByName?: string;
  requestEvidenceReference: string;
  approvalEvidenceReference?: string;
  rowVersion: string;
  commitmentAdjustments: PurchaseOrderCommitmentAdjustment[];
  dispatches: PurchaseOrderAmendmentDispatch[];
}

export interface PurchaseOrderAmendmentOverview {
  purchaseOrderId: string;
  purchaseOrderNumber: string;
  currentRevisionNumber: number;
  isFrameworkCallOff: boolean;
  canCreateAmendment: boolean;
  blockedReason?: string;
  decisionKeys: string[];
  amendments: PurchaseOrderAmendment[];
}
