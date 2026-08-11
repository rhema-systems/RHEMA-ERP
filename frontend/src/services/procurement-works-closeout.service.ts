export type WorksCloseoutActionType =
  | 'InitialTakeover'
  | 'DefectRectification'
  | 'FinalTakeover'
  | 'WarrantyRelease'
  | 'PerformanceSecurityRelease'
  | 'RetentionRelease'
  | 'DisputeOpen'
  | 'DisputeResolve'
  | 'Termination'
  | 'FinalAccount'
  | 'Closeout';

export type WorksCloseoutActionStatus =
  'PendingApproval' | 'Approved' | 'Rejected' | 'RevalidationFailed' | number;

export type RetentionReleaseStage =
  | 'PracticalCompletion'
  | 'SectionalTakeover'
  | 'DefectsLiability'
  | 'FinalRelease'
  | number;

export type WorksCloseoutCheckStatus =
  'Passed' | 'Failed' | 'NotRequired' | 'Pending' | number;

export interface WorksCloseoutCheck {
  key: string;
  label: string;
  status: WorksCloseoutCheckStatus;
  code: string;
  message: string;
  isRequired: boolean;
  referenceId?: string;
  reference?: string;
}

export interface WorksCloseoutEvidence {
  id: string;
  requirementKey: string;
  requirementLabel: string;
  referenceKind: 'WorkflowEvidenceDocument' | 'CentralDocumentUpload' | number;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  evidenceReference: string;
  evidenceHash: string;
}

export interface WorksCloseoutAction {
  id: string;
  contractId: string;
  projectId: string;
  sequence: number;
  actionType: WorksCloseoutActionType | number;
  status: WorksCloseoutActionStatus;
  configurationProfileId: string;
  configurationProfileVersion: number;
  policySetId: string;
  policyVersion: number;
  authorityRuleId: string;
  authorityName: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  projectHandoverItemId?: string;
  projectDefectLiabilityCaseId?: string;
  projectFinalAccountId?: string;
  projectPaymentCertificateId?: string;
  performanceBondRequestId?: string;
  retentionReleaseStage?: RetentionReleaseStage;
  quantitySurveyConfigurationProfileId?: string;
  quantitySurveyConfigurationProfileVersion?: number;
  quantitySurveyRetentionDecisionId?: string;
  quantitySurveyRetentionPolicyHash?: string;
  retentionHeldSnapshot?: number;
  retentionReleasedBefore?: number;
  retentionStageLimitAmount?: number;
  retentionReleasedAfter?: number;
  usesRetentionBond: boolean;
  effectiveAtUtc?: string;
  defectsLiabilityEndsAtUtc?: string;
  amount?: number;
  currency?: string;
  requiresIndependentFinanceApproval: boolean;
  amountAutoPosted: boolean;
  submittedById: string;
  submittedByName: string;
  submittedAtUtc: string;
  decidedById?: string;
  decidedByName?: string;
  decidedAtUtc?: string;
  reason: string;
  decisionComment?: string;
  integrityHash: string;
  rowVersion: string;
  evidence: WorksCloseoutEvidence[];
}

export interface WorksCloseoutProjectSummary {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  completedPracticalTakeovers: number;
  completedFinalTakeovers: number;
  openDefects: number;
  closedDefects: number;
  retentionHeld: number;
  retentionReleased: number;
  finalAccountId?: string;
  finalAccountStatus?: string;
  finalAccountValue?: number;
  currency?: string;
  projectClosureStatus?: string;
}

export interface WorksHandoverSource {
  id: string;
  projectUnitId?: string;
  handoverType: string;
  title: string;
  status: string;
  referenceNumber?: string;
  completedDate?: string;
}

export interface WorksDefectSource {
  id: string;
  title: string;
  status: string;
  isWarrantyRelated: boolean;
  warrantyExpiryDate?: string;
  rectificationCost?: number;
  currency: string;
}

export interface WorksCertificateSource {
  id: string;
  title: string;
  certificateNumber?: string;
  status: string;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  currency: string;
}

export interface WorksCloseoutOverview {
  contractId: string;
  contractNumber: string;
  contractStatus: string;
  contractRowVersion: string;
  isWorksContract: boolean;
  hasActiveDispute: boolean;
  isClosed: boolean;
  decisionKeys: string[];
  requiredEvidence: Record<WorksCloseoutActionType, string[]>;
  project?: WorksCloseoutProjectSummary;
  handoverItems: WorksHandoverSource[];
  defects: WorksDefectSource[];
  paymentCertificates: WorksCertificateSource[];
  performanceSecurity?: { id: string; status: string };
  retentionPolicy?: RetentionPolicy;
  retentionLedger: RetentionLedgerEntry[];
  checks: WorksCloseoutCheck[];
  history: WorksCloseoutAction[];
}

export interface RetentionPolicy {
  profileId: string;
  profileVersion: number;
  decisionId: string;
  maximumRetentionPercent: number;
  practicalCompletionReleasePercent: number;
  sectionalTakeoverReleasePercent: number;
  defectsReleasePercent: number;
  defectsLiabilityDays: number;
  approvalWorkflowDefinitionId: string;
  allowRetentionBond: boolean;
  policyHash: string;
}

export interface RetentionLedgerEntry {
  sourceType: string;
  sourceId: string;
  sourceReference: string;
  effectiveAtUtc: string;
  releaseStage?: RetentionReleaseStage;
  heldAmount: number;
  releasedAmount: number;
  runningHeldAmount: number;
  runningReleasedAmount: number;
  outstandingAmount: number;
  currency: string;
  status: string;
}

export interface SubmitWorksCloseoutAction {
  actionType: WorksCloseoutActionType;
  projectHandoverItemId?: string;
  projectDefectLiabilityCaseId?: string;
  projectFinalAccountId?: string;
  projectPaymentCertificateId?: string;
  performanceBondRequestId?: string;
  retentionReleaseStage?: RetentionReleaseStage;
  usesRetentionBond?: boolean;
  effectiveAtUtc?: string;
  amount?: number;
  currency?: string;
  reason: string;
  idempotencyKey: string;
  contractRowVersion: string;
  evidence: Array<{
    requirementKey: string;
    referenceKind: 'CentralDocumentUpload';
    fileUploadRecordId: string;
    evidenceReference: string;
  }>;
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function headers(): Record<string, string> {
  const token =
    typeof window !== 'undefined'
      ? localStorage.getItem('token') || localStorage.getItem('authToken')
      : null;
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

async function responseError(
  response: Response,
  fallback: string
): Promise<Error> {
  const text = await response.text();
  if (!text) return new Error(fallback);
  try {
    const payload = JSON.parse(text);
    return new Error(
      payload?.detail || payload?.message || payload?.title || fallback
    );
  } catch {
    return new Error(text || fallback);
  }
}

export const procurementWorksCloseoutService = {
  async getOverview(contractId: string): Promise<WorksCloseoutOverview> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/works-closeout/contracts/${contractId}`,
      { headers: headers(), cache: 'no-store' }
    );
    if (!response.ok)
      throw await responseError(
        response,
        'Failed to load Works closeout controls'
      );
    return response.json();
  },

  async submit(
    contractId: string,
    request: SubmitWorksCloseoutAction
  ): Promise<WorksCloseoutAction> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/works-closeout/contracts/${contractId}/actions`,
      {
        method: 'POST',
        headers: { ...headers(), 'X-Correlation-ID': crypto.randomUUID() },
        body: JSON.stringify(request),
      }
    );
    if (!response.ok)
      throw await responseError(
        response,
        'Failed to submit Works closeout action'
      );
    return response.json();
  },

  async decide(
    actionId: string,
    approved: boolean,
    comment: string,
    rowVersion: string
  ): Promise<WorksCloseoutAction> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/works-closeout/actions/${actionId}/decision`,
      {
        method: 'POST',
        headers: { ...headers(), 'X-Correlation-ID': crypto.randomUUID() },
        body: JSON.stringify({ approved, comment, rowVersion }),
      }
    );
    if (!response.ok)
      throw await responseError(
        response,
        'Failed to decide Works closeout action'
      );
    return response.json();
  },
};
