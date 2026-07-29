import type { ProcurementControlEvidenceReferenceKind } from './procurement-control-event';

export type FrameworkAgreementStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Published'
  | 'Rejected'
  | 'Superseded'
  | 'Expired'
  | 'Terminated';

export type FrameworkSourceType =
  | 'RequestForQuotation'
  | 'Tender'
  | 'ExceptionalSourcing';

export type FrameworkAuthorityKind =
  | 'User'
  | 'Role'
  | 'OrganizationalUnit'
  | 'Permission';

export type FrameworkExtensionStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected';

export interface FrameworkAgreementSearch {
  search?: string;
  businessPartnerId?: string;
  status?: FrameworkAgreementStatus;
  sourceType?: FrameworkSourceType;
  effectiveOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface FrameworkAgreementSummary {
  totalVersions: number;
  draftCount: number;
  pendingApprovalCount: number;
  publishedCount: number;
  effectiveCount: number;
  expiringWithin90DaysCount: number;
  pendingExtensionCount: number;
  totalEffectiveCeiling: number;
  effectiveCeilingByCurrency: Record<string, number>;
}

export interface FrameworkAgreementListItem {
  id: string;
  agreementKey: string;
  agreementNumber: string;
  title: string;
  version: number;
  status: FrameworkAgreementStatus;
  businessPartnerId: string;
  supplierCode: string;
  supplierName: string;
  sourceType: FrameworkSourceType;
  sourceId: string;
  sourceReference: string;
  priceListReference: string;
  priceListVersion: number;
  ceilingAmount: number;
  availableCeiling: number;
  currencyCode: string;
  effectiveFromUtc: string;
  effectiveToUtc: string;
  effectiveEndUtc: string;
  isEffective: boolean;
  categoryCount: number;
  priceLineCount: number;
  authorityCount: number;
  documentCount: number;
  allowedActions: string[];
  rowVersion: string;
}

export interface FrameworkAgreementPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: FrameworkAgreementListItem[];
}

export interface FrameworkAgreementCategory {
  id: string;
  partnerCategoryId: string;
  categoryCode: string;
  categoryName: string;
  integrityHash: string;
}

export interface FrameworkPriceListLine {
  id: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  unitPrice: number;
  minimumQuantity: number;
  maximumQuantity?: number;
  leadTimeDays: number;
  specifications?: string;
  integrityHash: string;
}

export interface FrameworkCallOffAuthority {
  id: string;
  authorityKind: FrameworkAuthorityKind;
  authorityUserId?: string;
  authorityValue: string;
  displayName: string;
  maximumCallOffAmount?: number;
  validFromUtc: string;
  validToUtc: string;
  isActive: boolean;
  integrityHash: string;
}

export interface FrameworkAgreementDocument {
  id: string;
  documentType: string;
  title: string;
  fileUploadRecordId: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  dmsReference: string;
  isRequired: boolean;
  isCurrent: boolean;
  retiredAtUtc?: string;
  integrityHash: string;
}

export interface FrameworkAgreementExtension {
  id: string;
  sequenceNumber: number;
  status: FrameworkExtensionStatus;
  previousEndUtc: string;
  proposedEndUtc: string;
  reason: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  submittedById: string;
  submittedByName: string;
  submittedAtUtc: string;
  decidedById?: string;
  decidedByName?: string;
  decidedAtUtc?: string;
  decisionComment?: string;
  integrityHash: string;
  rowVersion: string;
}

export interface FrameworkAgreement extends FrameworkAgreementListItem {
  awardReadinessDecisionId: string;
  sourceIntegrityHash: string;
  supplierEligibilityDecisionHash: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  supersedesAgreementId?: string;
  supersededByAgreementId?: string;
  description?: string;
  termsSummary?: string;
  reviewComment?: string;
  submittedById?: string;
  submittedByName?: string;
  submittedAtUtc?: string;
  publishedById?: string;
  publishedByName?: string;
  publishedAtUtc?: string;
  rejectedById?: string;
  rejectedByName?: string;
  rejectedAtUtc?: string;
  terminatedById?: string;
  terminatedAtUtc?: string;
  terminationReason?: string;
  integrityHash: string;
  categories: FrameworkAgreementCategory[];
  priceLines: FrameworkPriceListLine[];
  callOffAuthorities: FrameworkCallOffAuthority[];
  documents: FrameworkAgreementDocument[];
  extensions: FrameworkAgreementExtension[];
}

export interface SaveFrameworkPriceListLine {
  inventoryItemId: string;
  unitPrice: number;
  minimumQuantity: number;
  maximumQuantity?: number;
  leadTimeDays: number;
  specifications?: string;
}

export interface SaveFrameworkCallOffAuthority {
  authorityKind: FrameworkAuthorityKind;
  authorityUserId?: string;
  authorityValue: string;
  displayName: string;
  maximumCallOffAmount?: number;
  validFromUtc: string;
  validToUtc: string;
  isActive: boolean;
}

export interface CreateFrameworkAgreement {
  sourceType: FrameworkSourceType;
  sourceId: string;
  businessPartnerId: string;
  title: string;
  ceilingAmount: number;
  currencyCode: string;
  effectiveFromUtc: string;
  effectiveToUtc: string;
  workflowDefinitionId: string;
  description?: string;
  termsSummary?: string;
  categoryIds: string[];
  priceLines: SaveFrameworkPriceListLine[];
  callOffAuthorities: SaveFrameworkCallOffAuthority[];
}

export interface UpdateFrameworkAgreement
  extends Omit<
    CreateFrameworkAgreement,
    'sourceType' | 'sourceId' | 'businessPartnerId'
  > {
  rowVersion: string;
}

export interface FrameworkEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface FrameworkLifecycleRequest {
  rowVersion: string;
  comment: string;
  evidence: FrameworkEvidenceReference[];
}

export interface CloneFrameworkAgreement {
  rowVersion: string;
  effectiveFromUtc: string;
  effectiveToUtc: string;
  workflowDefinitionId: string;
  changeSummary: string;
}

export interface RequestFrameworkExtension {
  agreementRowVersion: string;
  proposedEndUtc: string;
  workflowDefinitionId: string;
  reason: string;
  evidence: FrameworkEvidenceReference[];
}

export interface DecideFrameworkExtension {
  rowVersion: string;
  approve: boolean;
  comment: string;
  evidence: FrameworkEvidenceReference[];
}

export interface FrameworkWorkflowOption {
  id: string;
  name: string;
  version: number;
}

export interface FrameworkCategoryOption {
  id: string;
  code: string;
  name: string;
}

export interface FrameworkItemOption {
  id: string;
  code: string;
  name: string;
  unitOfMeasure: string;
}

export interface FrameworkSourceOption {
  sourceType: FrameworkSourceType;
  sourceId: string;
  sourceReference: string;
  businessPartnerId: string;
  supplierCode: string;
  supplierName: string;
  awardAmount: number;
  currencyCode: string;
  awardedAtUtc: string;
  awardReadinessDecisionId: string;
}
