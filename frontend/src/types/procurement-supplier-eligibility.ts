export type SupplierEligibilityBoundary =
  | 'StatusReview'
  | 'Invitation'
  | 'Award'
  | 'Contract'
  | 'ManualPurchaseOrder'
  | 'FrameworkCallOff';

export interface SupplierEligibilityEvaluationRequest {
  businessPartnerId: string;
  boundary: SupplierEligibilityBoundary;
  categoryIds?: string[];
  requiresPrequalification?: boolean;
  requiresLicenses?: boolean;
  includeFinancialWarnings?: boolean;
  minimumPerformanceRating?: number;
}

export interface SupplierEligibilityFinding {
  code: string;
  message: string;
  blocking: boolean;
}

export interface SupplierEligibilityQualifiedListLineage {
  entryId: string;
  exerciseId: string;
  exerciseReference: string;
  applicationId: string;
  applicationNumber: string;
  categoryId: string;
  status: string;
  validFromUtc: string;
  expiresAtUtc: string;
  isCurrent: boolean;
  approvalReference: string;
  approvalEvidenceReference: string;
  policySetId: string;
  policySetCode: string;
  policySetVersion: number;
  sourceConfigurationProfileId: string;
}

export interface SupplierEligibilityResult {
  isValid: boolean;
  validationCode: string;
  errors: string[];
  warnings: string[];
  businessPartnerId: string;
  tenantId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  boundary: SupplierEligibilityBoundary;
  evaluatedAtUtc: string;
  approvalStatus?: string;
  registrationStatus: string;
  isActive: boolean;
  isBlacklisted: boolean;
  blacklistReason?: string;
  blacklistDate?: string;
  blacklistExpiryDate?: string;
  performanceRating?: number;
  riskLevel?: string;
  creditRating?: string;
  categoryIds: string[];
  categories: Array<{
    categoryId: string;
    categoryCode?: string;
    categoryName?: string;
    isPrimary: boolean;
  }>;
  requiredCategoryIds: string[];
  registrationId?: string;
  registrationNumber?: string;
  registrationApplicationStatus?: string;
  evidencePackVersionId?: string;
  evidencePackCode?: string;
  evidencePackVersion?: number;
  evidenceReady?: boolean;
  evidencePackBoundAtUtc?: string;
  evidencePackSnapshotHash?: string;
  evidencePackBindingIntegrityHash?: string;
  evidenceBlockingReasons: string[];
  qualifiedListEntries: SupplierEligibilityQualifiedListLineage[];
  avlPolicyAvailable: boolean;
  avlDecisionId?: string;
  avlProfileId?: string;
  avlProfileCode?: string;
  avlProfileVersion?: number;
  avlApprovalReference?: string;
  avlSourceLineage?: string;
  avlPolicyValueHash?: string;
  avlEvidenceReferences: string[];
  avlRegisterAvailable: boolean;
  avlRegisterId?: string;
  avlRegisterCode?: string;
  avlRegisterVersion?: number;
  avlRegisterEffectiveFromUtc?: string;
  avlRegisterExpiresAtUtc?: string;
  avlRegisterIntegrityHash?: string;
  avlEntryId?: string;
  avlEntryStatus?: 'Active' | 'Suspended' | 'Expired';
  avlEntryIntegrityHash?: string;
  avlEntryEligibilityDecisionHash?: string;
  formalAvlCurrent: boolean;
  dueDiligencePolicyAvailable: boolean;
  dueDiligenceReviewId?: string;
  dueDiligenceReviewReference?: string;
  dueDiligenceCycleNumber?: number;
  dueDiligenceReviewType?: SupplierDueDiligenceReviewType;
  dueDiligenceStatus?: SupplierDueDiligenceStatus;
  dueDiligenceOutcome?: SupplierDueDiligenceOutcome;
  dueDiligenceReviewPeriodStartUtc?: string;
  dueDiligenceReviewPeriodEndUtc?: string;
  dueDiligenceNextReviewDueAtUtc?: string;
  dueDiligencePolicyDecisionId?: string;
  dueDiligencePolicyValueHash?: string;
  dueDiligenceWorkflowInstanceId?: string;
  dueDiligenceIntegrityHash?: string;
  dueDiligenceCurrent: boolean;
  dueDiligenceChecks: Array<{
    checkId: string;
    checkType: SupplierDueDiligenceCheckType;
    status: SupplierDueDiligenceCheckStatus;
    sourceName: string;
    sourceReference: string;
    checkedAtUtc?: string;
    validUntilUtc?: string;
    integrityHash: string;
    evidenceCount: number;
    evidenceIntegrityHashes: string[];
  }>;
  performanceScorecardPolicyAvailable: boolean;
  performanceScorecardId?: string;
  performanceScorecardReference?: string;
  performanceCalculatedAtUtc?: string;
  performanceNextReviewDueAtUtc?: string;
  performanceOverallScore?: number;
  performanceBand?: string;
  performanceDataCoveragePercent?: number;
  performanceMinimumScore?: number;
  performanceEligibilityAction?: 'AlertOnly' | 'EscalationRequired' | 'AwardHardStop';
  performanceMinimumScoreBreached: boolean;
  performanceScorecardPolicyValueHash?: string;
  performanceScorecardIntegrityHash?: string;
  performanceScorecardCurrent: boolean;
  performanceAwardBlocked: boolean;
  riskPolicyAvailable: boolean;
  riskAssessmentId?: string;
  riskAssessmentReference?: string;
  riskAssessedAtUtc?: string;
  riskNextReviewDueAtUtc?: string;
  riskScore?: number;
  riskBand?: string;
  riskEligibilityAction?: 'AlertOnly' | 'EscalationRequired' | 'AwardHardStop';
  maximumSpendSharePercent?: number;
  concentrationLimitPercent?: number;
  singleSourceCategoryCount: number;
  openRiskAlertCount: number;
  escalatedRiskAlertCount: number;
  riskAssessmentPolicyValueHash?: string;
  riskAssessmentIntegrityHash?: string;
  riskAssessmentCurrent: boolean;
  riskAwardBlocked: boolean;
  decisionKeys: string[];
  findings: SupplierEligibilityFinding[];
  decisionHash: string;
}

export interface SupplierEligibilityBoundaryOption {
  value: SupplierEligibilityBoundary;
  label: string;
}
import type {
  SupplierDueDiligenceCheckStatus,
  SupplierDueDiligenceCheckType,
  SupplierDueDiligenceOutcome,
  SupplierDueDiligenceReviewType,
  SupplierDueDiligenceStatus,
} from './procurement-supplier-due-diligence';
