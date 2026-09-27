// Contract Service - API calls for contract management

// ==================== INTERFACES ====================

export interface ContractDto {
  id: string;
  contractNumber: string;
  contractTitle: string;
  contractType: string;
  status: string;
  tenderAwardId: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  businessPartnerId: string;
  businessPartnerName: string;
  tenderBidId?: string;
  contractValue: number;
  currency: string;
  paymentTerms?: string;
  retentionPercentage: number;
  retentionClause?: string | null;
  startDate?: string;
  endDate?: string;
  durationDays?: number;
  warrantyPeriodDays?: number;
  scopeOfWork?: string;
  deliverables?: string;
  specialConditions?: string;
  penaltyClause?: string;
  signedDate?: string;
  signedByName?: string;
  contractorSignatoryName?: string;
  contractorSignedDate?: string;
  contractDocumentPath?: string;
  notes?: string;
  createdAt: string;
  createdByName?: string;
  activatedAt?: string;
  completedAt?: string;
  terminatedAt?: string;
  terminationReason?: string;
  rowVersion: string;
  milestones: ContractMilestoneDto[];
  amendments: ContractAmendmentDto[];
  documents: ContractDocumentDto[];
  totalPaidAmount: number;
  remainingAmount: number;
  completedMilestones: number;
  totalMilestones: number;
}

export interface ContractListDto {
  id: string;
  contractNumber: string;
  contractTitle: string;
  contractType: string;
  status: string;
  tenderNumber: string;
  businessPartnerName: string;
  contractValue: number;
  currency: string;
  startDate?: string;
  endDate?: string;
  createdAt: string;
}

export interface ContractMilestoneDto {
  id: string;
  contractId: string;
  milestoneName: string;
  description?: string;
  sequenceNumber: number;
  paymentPercentage: number;
  paymentAmount: number;
  plannedDate?: string;
  actualDate?: string;
  status: string;
  completedAt?: string;
  invoicedAt?: string;
  paidAt?: string;
  invoiceNumber?: string;
  notes?: string;
}

export interface ContractAmendmentDto {
  id: string;
  contractId: string;
  amendmentNumber: string;
  sequenceNumber: number;
  amendmentType: string;
  reason?: string;
  description?: string;
  previousValue?: number;
  newValue?: number;
  valueChange?: number;
  previousEndDate?: string;
  newEndDate?: string;
  daysExtended?: number;
  scopeChanges?: string;
  status: string;
  requestedDate?: string;
  requestedByName?: string;
  approvedDate?: string;
  approvedByName?: string;
  approvalNotes?: string;
  documentPath?: string;
  notes?: string;
}

export interface ContractDocumentDto {
  id: string;
  contractId: string;
  documentType: string;
  fileName: string;
  filePath: string;
  fileUploadRecordId?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  contentType?: string;
  fileSize?: number;
  description?: string;
  uploadedByName?: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateContractDto {
  tenderAwardId: string;
  contractTitle: string;
  contractType: string;
  contractValue: number;
  currency: string;
  paymentTerms?: string;
  retentionPercentage: number;
  retentionClause?: string | null;
  startDate?: string;
  endDate?: string;
  durationDays?: number;
  warrantyPeriodDays?: number;
  scopeOfWork?: string;
  deliverables?: string;
  specialConditions?: string;
  penaltyClause?: string;
  notes?: string;
}

export interface UpdateContractDto {
  contractTitle?: string;
  contractType?: string;
  contractValue?: number;
  paymentTerms?: string;
  retentionPercentage?: number;
  retentionClause?: string | null;
  startDate?: string;
  endDate?: string;
  durationDays?: number;
  warrantyPeriodDays?: number;
  scopeOfWork?: string;
  deliverables?: string;
  specialConditions?: string;
  penaltyClause?: string;
  notes?: string;
}

export interface CreateContractMilestoneDto {
  milestoneName: string;
  description?: string;
  sequenceNumber: number;
  paymentPercentage: number;
  plannedDate?: string;
  notes?: string;
}

export interface UpdateContractMilestoneDto {
  milestoneName?: string;
  description?: string;
  sequenceNumber?: number;
  paymentPercentage?: number;
  plannedDate?: string;
  notes?: string;
}

export interface UpdateMilestoneStatusDto {
  status: string;
  actualDate?: string;
  notes?: string;
}

export interface CreateContractAmendmentDto {
  amendmentType: string;
  reason?: string;
  description?: string;
  newValue?: number;
  newEndDate?: string;
  scopeChanges?: string;
  notes?: string;
}

export interface ProcessAmendmentDto {
  approved: boolean;
  notes?: string;
}

export interface UpdateContractStatusDto {
  signedByName?: string;
  contractorSignatoryName?: string;
  notes?: string;
}

export type ContractActivationCheckStatus = 'Passed' | 'Failed' | 'NotRequired' | 'Pending' | number;
export type ContractActivationStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Activated'
  | 'RevalidationFailed'
  | 'Cancelled'
  | number;

export interface ContractActivationCheck {
  key: string;
  label: string;
  status: ContractActivationCheckStatus;
  code: string;
  message: string;
  isRequired: boolean;
  referenceId?: string;
  reference?: string;
}

export interface ContractActivationEvidence {
  id: string;
  requirementKey: string;
  requirementLabel: string;
  referenceKind: 'WorkflowEvidenceDocument' | 'CentralDocumentUpload' | number;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  evidenceReference: string;
  evidenceHash: string;
}

export interface ContractActivation {
  id: string;
  contractId: string;
  sequence: number;
  status: ContractActivationStatus;
  configurationProfileId: string;
  configurationProfileVersion: number;
  policySetId: string;
  policyVersion: number;
  authorityRuleId: string;
  authorityName: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  awardReadinessDecisionId: string;
  awardReadinessSequence: number;
  ghanepsRequired: boolean;
  ghanepsCompliant: boolean;
  performanceSecurityRequired: boolean;
  performanceBondRequestId?: string;
  submittedById: string;
  submittedByName: string;
  submittedAtUtc: string;
  decidedById?: string;
  decidedByName?: string;
  decidedAtUtc?: string;
  activatedById?: string;
  activatedByName?: string;
  activatedAtUtc?: string;
  reason: string;
  decisionComment?: string;
  integrityHash: string;
  rowVersion: string;
  evidence: ContractActivationEvidence[];
}

export interface ContractActivationOverview {
  contractId: string;
  contractNumber: string;
  contractStatus: string;
  isReady: boolean;
  canSubmit: boolean;
  canDecide: boolean;
  canActivate: boolean;
  requiredEvidenceKeys: string[];
  decisionKeys: string[];
  checks: ContractActivationCheck[];
  history: ContractActivation[];
}

export interface SubmitContractActivationRequest {
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

export interface ContractOperationsPrompt {
  key: string;
  type: string;
  severity: 'Critical' | 'High' | 'Medium' | 'Low';
  title: string;
  message: string;
  recommendedAction: string;
  sourceId: string;
  sourceReference: string;
  dueAtUtc?: string;
  daysOverdue: number;
  estimatedPenaltyAmount?: number;
  calculationBasis: string;
  requiresIndependentApproval: boolean;
  amountAutoPosted: boolean;
}

export interface ContractOperationsSummary {
  contractId: string;
  contractNumber: string;
  contractTitle: string;
  contractType: string;
  status: string;
  businessPartnerId: string;
  businessPartnerName: string;
  currency: string;
  contractValue: number;
  committedSpend: number;
  invoicedAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  purchaseOrderCount: number;
  receiptCount: number;
  totalMilestones: number;
  completedMilestones: number;
  overdueMilestones: number;
  milestoneCompletionPercent: number;
  endDate?: string;
  daysToExpiry?: number;
  renewalStatus: string;
  supplierPerformanceScore?: number;
  supplierPerformanceBand?: string;
  supplierRiskScore?: number;
  supplierRiskBand?: string;
  promptCount: number;
  criticalPromptCount: number;
  overallRisk: 'Critical' | 'High' | 'Medium' | 'Low';
}

export interface ContractOperationsCurrencyTotal {
  currency: string;
  contractValue: number;
  committedSpend: number;
  invoicedAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  retentionHeldAmount: number;
}

export interface ContractOperationsPortfolio {
  generatedAtUtc: string;
  totalContracts: number;
  activeContracts: number;
  contractsWithPrompts: number;
  criticalPromptCount: number;
  currencyTotals: ContractOperationsCurrencyTotal[];
  items: ContractOperationsSummary[];
  decisionKeys: string[];
}

export interface ContractOperationsDetail {
  summary: ContractOperationsSummary;
  generatedAtUtc: string;
  penaltyClause?: string;
  paymentTerms?: string;
  retentionPercentage: number;
  milestones: Array<{
    id: string;
    name: string;
    sequence: number;
    status: string;
    paymentAmount: number;
    plannedDate?: string;
    actualDate?: string;
    daysLate: number;
  }>;
  purchaseOrders: Array<{
    id: string;
    number: string;
    status: string;
    currency: string;
    amount: number;
    orderDate: string;
    promisedDate?: string;
    receivedDate?: string;
    receiptCount: number;
  }>;
  invoices: Array<{
    id: string;
    number: string;
    status: string;
    currency: string;
    totalAmount: number;
    paidAmount: number;
    outstandingAmount: number;
    invoiceDate: string;
    dueDate?: string;
    isOverdue: boolean;
  }>;
  kpis: Array<{
    key: string;
    label: string;
    score?: number | null;
    target?: number | null;
    status: string;
    sourceReference: string;
  }>;
  prompts: ContractOperationsPrompt[];
  lineage: {
    activationId?: string;
    activationSequence?: number;
    activationStatus: string;
    configurationProfileId?: string;
    configurationProfileVersion?: number;
    policySetId?: string;
    policyVersion?: number;
    workflowDefinitionId?: string;
    workflowInstanceId?: string;
    awardReadinessDecisionId?: string;
    integrityHash?: string;
  };
  decisionKeys: string[];
}

export interface ProcessContractOperationsAlertsResult {
  processedAtUtc: string;
  evaluatedPromptCount: number;
  publishedAlertCount: number;
  alreadyPublishedCount: number;
  publishedPromptKeys: string[];
}

// ==================== API FUNCTIONS ====================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined'
    ? (localStorage.getItem('token') || localStorage.getItem('authToken'))
    : null;
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

async function apiError(response: Response, fallback: string): Promise<Error> {
  const text = await response.text();
  if (!text) return new Error(fallback);
  try {
    const payload = JSON.parse(text);
    return new Error(payload?.detail || payload?.message || payload?.title || fallback);
  } catch {
    return new Error(text || fallback);
  }
}

export const contractService = {
  async getContracts(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    status?: string,
    contractType?: string
  ): Promise<PagedResult<ContractListDto>> {
    const params = new URLSearchParams();
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    if (contractType) params.append('contractType', contractType);

    const response = await fetch(`${API_BASE_URL}/procurement/Contracts?${params.toString()}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch contracts');
    }

    return response.json();
  },

  async getContractById(id: string): Promise<ContractDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${id}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw await apiError(response, response.status === 403
        ? 'Your account does not have access to Procurement contract records.'
        : 'Failed to fetch contract');
    }

    return response.json();
  },

  async getActiveContracts(): Promise<ContractDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/active`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch active contracts');
    }

    return response.json();
  },

  async getExpiringContracts(daysAhead: number = 30): Promise<ContractDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/expiring?daysAhead=${daysAhead}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch expiring contracts');
    }

    return response.json();
  },

  async createContract(data: CreateContractDto): Promise<ContractDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create contract');
    }

    return response.json();
  },

  async getContractByAwardId(awardId: string): Promise<ContractDto | null> {
    try {
      const response = await fetch(`${API_BASE_URL}/procurement/Contracts/by-award/${awardId}`, {
        headers: getAuthHeaders(),
      });

      if (response.status === 404) {
        return null;
      }

      if (!response.ok) {
        throw new Error('Failed to fetch contract');
      }

      return response.json();
    } catch {
      return null;
    }
  },

  async updateContract(id: string, data: UpdateContractDto): Promise<ContractDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update contract');
    }

    return response.json();
  },

  async activateContract(id: string, data: UpdateContractStatusDto): Promise<ContractDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${id}/activate`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to activate contract');
    }

    return response.json();
  },

  async getActivationOverview(contractId: string): Promise<ContractActivationOverview> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-activations/contracts/${contractId}`,
      { headers: getAuthHeaders(), cache: 'no-store' }
    );
    if (!response.ok) throw await apiError(response, 'Failed to load contract activation controls');
    return response.json();
  },

  async submitActivation(
    contractId: string,
    data: SubmitContractActivationRequest
  ): Promise<ContractActivation> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-activations/contracts/${contractId}/submit`,
      {
        method: 'POST',
        headers: {
          ...getAuthHeaders(),
          'X-Correlation-ID': crypto.randomUUID(),
        },
        body: JSON.stringify(data),
      }
    );
    if (!response.ok) throw await apiError(response, 'Failed to submit contract activation');
    return response.json();
  },

  async decideActivation(
    activationId: string,
    approved: boolean,
    comment: string,
    rowVersion: string
  ): Promise<ContractActivation> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-activations/${activationId}/decision`,
      {
        method: 'POST',
        headers: {
          ...getAuthHeaders(),
          'X-Correlation-ID': crypto.randomUUID(),
        },
        body: JSON.stringify({ approved, comment, rowVersion }),
      }
    );
    if (!response.ok) throw await apiError(response, 'Failed to decide contract activation');
    return response.json();
  },

  async applyActivation(
    activationId: string,
    contractorSignatoryName: string,
    comment: string,
    rowVersion: string
  ): Promise<ContractDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-activations/${activationId}/activate`,
      {
        method: 'POST',
        headers: {
          ...getAuthHeaders(),
          'X-Correlation-ID': crypto.randomUUID(),
        },
        body: JSON.stringify({ contractorSignatoryName, comment, rowVersion }),
      }
    );
    if (!response.ok) throw await apiError(response, 'Failed to activate contract');
    return response.json();
  },

  async getContractOperations(
    search?: string,
    status?: string,
    risk?: string,
    take = 100
  ): Promise<ContractOperationsPortfolio> {
    const params = new URLSearchParams({ take: String(take) });
    if (search) params.set('search', search);
    if (status) params.set('status', status);
    if (risk) params.set('risk', risk);
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-operations?${params.toString()}`,
      { headers: getAuthHeaders(), cache: 'no-store' }
    );
    if (!response.ok) throw await apiError(response, 'Failed to load contract operations');
    return response.json();
  },

  async getContractOperationsDetail(
    contractId: string
  ): Promise<ContractOperationsDetail> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-operations/${contractId}`,
      { headers: getAuthHeaders(), cache: 'no-store' }
    );
    if (!response.ok) throw await apiError(response, 'Failed to load contract operations detail');
    return response.json();
  },

  async processContractOperationsAlerts(
    contractId?: string
  ): Promise<ProcessContractOperationsAlertsResult> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/contract-operations/process-alerts`,
      {
        method: 'POST',
        headers: {
          ...getAuthHeaders(),
          'X-Correlation-ID': crypto.randomUUID(),
        },
        body: JSON.stringify({ contractId }),
      }
    );
    if (!response.ok) throw await apiError(response, 'Failed to publish contract operations prompts');
    return response.json();
  },

  async completeContract(id: string): Promise<ContractDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${id}/complete`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to complete contract');
    }

    return response.json();
  },

  async terminateContract(id: string, reason: string): Promise<ContractDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${id}/terminate`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ reason }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to terminate contract');
    }

    return response.json();
  },

  // Milestone APIs
  async addMilestone(contractId: string, data: CreateContractMilestoneDto): Promise<ContractMilestoneDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${contractId}/milestones`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add milestone');
    }

    return response.json();
  },

  async updateMilestone(milestoneId: string, data: UpdateContractMilestoneDto): Promise<ContractMilestoneDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/milestones/${milestoneId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update milestone');
    }

    return response.json();
  },

  async updateMilestoneStatus(milestoneId: string, data: UpdateMilestoneStatusDto): Promise<ContractMilestoneDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/milestones/${milestoneId}/status`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update milestone status');
    }

    return response.json();
  },

  async deleteMilestone(milestoneId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/milestones/${milestoneId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete milestone');
    }
  },

  // Amendment APIs
  async createAmendment(contractId: string, data: CreateContractAmendmentDto): Promise<ContractAmendmentDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${contractId}/amendments`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create amendment');
    }

    return response.json();
  },

  async processAmendment(amendmentId: string, data: ProcessAmendmentDto): Promise<ContractAmendmentDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/amendments/${amendmentId}/process`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to process amendment');
    }

    return response.json();
  },

  async deleteAmendment(amendmentId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/amendments/${amendmentId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete amendment');
    }
  },

  // Document APIs
  async uploadDocument(contractId: string, file: File, documentType: string, description?: string): Promise<ContractDocumentDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);
    if (description) {
      formData.append('description', description);
    }

    const { ['Content-Type']: _contentType, ...headers } = getAuthHeaders() as Record<string, string>;

    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/${contractId}/documents`, {
      method: 'POST',
      headers,
      body: formData,
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to upload document');
    }

    return response.json();
  },

  async deleteDocument(documentId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Contracts/documents/${documentId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete document');
    }
  },
};
