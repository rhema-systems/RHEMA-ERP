export enum ProcurementExceptionalSourcingControlStatus {
  Prepared = 0,
  PendingApproval = 1,
  Approved = 2,
  Negotiated = 3,
  Recommended = 4,
  Awarded = 5,
  Contracted = 6,
  Accepted = 7,
  Filed = 8,
  Rejected = 9,
}

export interface ExceptionalEvidenceRequirement {
  evidenceRuleId: string;
  ruleCode: string;
  requirementKey: string;
  evidenceName: string;
  requiresVerification: boolean;
}

export interface ExceptionalSupplierOption {
  businessPartnerId: string;
  partnerCode: string;
  supplierName: string;
  registrationStatus: string;
}

export interface ProcurementExceptionalSourcingReadiness {
  sourceRequisitionId?: string;
  currency: string;
  estimatedValue?: number;
  quotationItems: Array<{ tenderItemId: string; description: string; quantity: number; unitOfMeasure?: string }>;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  tenderStatus: string;
  method: number;
  methodRuleCode: string;
  exceptionRuleCode: string;
  authorityRouteReference: string;
  minimumSupplierCount: number;
  boardApprovalRequired: boolean;
  managingDirectorApprovalRequired: boolean;
  ppaApprovalRequired: boolean;
  justificationRequired: boolean;
  evidenceRequired: boolean;
  postAwardFilingRequired: boolean;
  evidenceRequirements: ExceptionalEvidenceRequirement[];
  supplierOptions: ExceptionalSupplierOption[];
}

export interface ProcurementExceptionalSourcingControl {
  approvalRequired?: boolean;
  sourceRequisitionId?: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  method: number;
  methodRuleCode: string;
  exceptionRuleCode: string;
  authorityRouteReference: string;
  status: ProcurementExceptionalSourcingControlStatus;
  justification: string;
  justificationEvidenceReference: string;
  supplierSelectionEvidenceReference: string;
  preparedAtUtc: string;
  suppliersInvitedAtUtc?: string;
  boardApprovalRequired: boolean;
  managingDirectorApprovalRequired: boolean;
  ppaApprovalRequired: boolean;
  workflowInstanceId?: string;
  boardApprovalReference?: string;
  managingDirectorApprovalReference?: string;
  ppaApprovalReference?: string;
  negotiationId?: string;
  negotiationPlanReference?: string;
  negotiationMinutesEvidenceReference?: string;
  negotiationOutcomeReference?: string;
  negotiatedAmount?: number;
  recommendedBidId?: string;
  recommendationReason?: string;
  awardReference?: string;
  contractReference?: string;
  bidderAcceptanceReference?: string;
  postAwardFilingReference?: string;
  exceptionReportReference?: string;
  integrityHash: string;
  rowVersion: string;
  suppliers: Array<{ businessPartnerId: string; supplierName: string }>;
  evidenceChecklist: Array<
    ExceptionalEvidenceRequirement & {
      evidenceReference: string;
      verificationReference: string;
    }
  >;
  bids: Array<{
    bidId: string;
    bidNumber: string;
    businessPartnerId: string;
    supplierName: string;
    bidAmount: number;
    currency: string;
    status: string;
  }>;
  milestones: Array<{
    code: string;
    label: string;
    completedAtUtc?: string;
    reference?: string;
  }>;
}

export interface PrepareExceptionalSourcingRequest {
  quotation?: { reference: string; evidenceReference: string; items: Array<{ tenderItemId: string; unitPrice: number }> };
  justification: string;
  justificationEvidenceReference: string;
  supplierSelectionEvidenceReference: string;
  businessPartnerIds: string[];
  evidenceChecklist: Array<{
    requirementKey: string;
    evidenceReference: string;
    verificationReference: string;
  }>;
}
