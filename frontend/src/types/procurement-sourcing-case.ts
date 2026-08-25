import type {
  ProcurementCategoryClass,
  ProcurementMethodType,
} from '@/types/procurement-policy';
import type {
  ProcurementComplianceFinding,
  ProcurementComplianceMethodCandidate,
} from '@/types/procurement-compliance';
import type { PurchaseRequisitionSourcingReleaseDto } from '@/services/purchasingService';

export type ProcurementSourcingCaseStatus =
  'Ready' | 'InProgress' | 'Closed' | 'Cancelled';

export type ProcurementSourcingCaseSourceRequestStatus =
  'Planned' | 'Created' | 'Cancelled';

export type ProcurementSourcingMethodSelectionBasis =
  'AutomaticRecommendation' | 'ApprovedOverride';

export interface ProcurementSourcingMethodOverrideReadiness {
  isRequested: boolean;
  isEligible: boolean;
  decisionCode: string;
  message: string;
  ruleId?: string;
  ruleCode?: string;
  ruleName?: string;
  exceptionType?: string;
  approverRole?: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  approvalReference?: string;
  evidenceReference?: string;
  approvedAtUtc?: string;
  approvalActorUserIds: string[];
}

export interface ProcurementSourcingCaseSummary {
  totalCount: number;
  readyCount: number;
  inProgressCount: number;
  closedCount: number;
  cancelledCount: number;
  staleCount: number;
}

export interface ProcurementSourcingCaseSourceOption {
  requisitionId: string;
  requisitionNumber: string;
  requisitionStatus: string;
  sourcingReleaseId: string;
  releaseReference: string;
  sourcePlanId?: string;
  sourcePlanItemId?: string;
  sourcePlanNumber?: string;
  sourcePlanItemDescription?: string;
  category: ProcurementCategoryClass;
  estimatedValue: number;
  currencyCode: string;
  currentCaseId?: string;
  currentCaseNumber?: string;
}

export interface ProcurementSourcingCaseLineOption {
  id: string;
  lineNumber: number;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
  lineTotal: number;
}

export interface ProcurementSourcingCaseLotItem {
  requisitionItemId: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
  lineTotal: number;
}

export interface ProcurementSourcingCaseLot {
  id: string;
  lotNumber: number;
  lotCode: string;
  title: string;
  description?: string;
  estimatedValue: number;
  currencyCode: string;
  items: ProcurementSourcingCaseLotItem[];
}

export interface ProcurementSourcingCaseSourceRequest {
  id: string;
  requestSequence: number;
  requestReference: string;
  sourceType: string;
  method: ProcurementMethodType;
  status: ProcurementSourcingCaseSourceRequestStatus;
  lotIds: string[];
  plannedAtUtc: string;
  sourceEntityId?: string;
  sourceEntityReference?: string;
  registeredAtUtc?: string;
  registeredById?: string;
  registeredByName?: string;
}

export interface ProcurementSourcingCase {
  id: string;
  caseNumber: string;
  caseSequence: number;
  requisitionId: string;
  requisitionNumber: string;
  sourcingReleaseId: string;
  sourcingReleaseReference: string;
  sourcePlanId: string;
  sourcePlanItemId: string;
  sourcePlanNumber?: string;
  sourcePlanItemDescription?: string;
  category: ProcurementCategoryClass;
  recommendedMethod: ProcurementMethodType;
  selectedMethod: ProcurementMethodType;
  methodSelectionBasis: ProcurementSourcingMethodSelectionBasis;
  estimatedValue: number;
  currencyCode: string;
  policySetId: string;
  policyCode: string;
  policyVersion: number;
  methodRuleId: string;
  methodRuleCode: string;
  thresholdRuleId: string;
  thresholdRuleCode: string;
  authorityRouteId?: string;
  authorityRouteReference?: string;
  approvedExceptionRuleId?: string;
  methodOverrideWorkflowInstanceId?: string;
  methodOverrideReason?: string;
  methodOverrideApprovalActorUserIds: string[];
  methodOverrideApprovedAtUtc?: string;
  exceptionApprovalReference?: string;
  exceptionEvidenceReference?: string;
  justification: string;
  status: ProcurementSourcingCaseStatus;
  isSourceCurrent: boolean;
  sourceStateCode: string;
  sourceStateMessage: string;
  createdAtUtc: string;
  createdById?: string;
  createdByName: string;
  startedAtUtc?: string;
  startedByName?: string;
  closedAtUtc?: string;
  closedByName?: string;
  closureReason?: string;
  sourceControlFingerprint: string;
  caseFingerprint: string;
  integrityHash: string;
  rowVersion: string;
  lots: ProcurementSourcingCaseLot[];
  sourceRequests: ProcurementSourcingCaseSourceRequest[];
}

export interface ProcurementSourcingCasePage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementSourcingCase[];
}

export interface ProcurementSourcingCaseReadiness {
  requisitionId: string;
  requisitionNumber: string;
  isReleaseCurrent: boolean;
  isMethodCompliant: boolean;
  canCreate: boolean;
  decisionCode: string;
  message: string;
  requestedMethod?: ProcurementMethodType;
  recommendedMethod?: ProcurementMethodType;
  selectedMethod?: ProcurementMethodType;
  suggestedMethod?: ProcurementMethodType;
  methodSelectionBasis: ProcurementSourcingMethodSelectionBasis;
  override?: ProcurementSourcingMethodOverrideReadiness;
  currentRelease?: PurchaseRequisitionSourcingReleaseDto;
  currentCase?: ProcurementSourcingCase;
  methodCandidates: ProcurementComplianceMethodCandidate[];
  hardStops: ProcurementComplianceFinding[];
  reviewRequirements: ProcurementComplianceFinding[];
  lines: ProcurementSourcingCaseLineOption[];
}

export interface CreateProcurementSourcingCaseLot {
  lotCode?: string;
  title: string;
  description?: string;
  purchaseRequisitionItemIds: string[];
}

export interface CreateProcurementSourcingCase {
  requisitionId: string;
  selectedMethod?: ProcurementMethodType;
  justification: string;
  methodOverrideReason?: string;
  lots: CreateProcurementSourcingCaseLot[];
}
