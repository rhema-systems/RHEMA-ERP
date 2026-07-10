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

// ==================== API FUNCTIONS ====================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(): HeadersInit {
  const token = typeof window !== 'undefined'
    ? (localStorage.getItem('token') || localStorage.getItem('authToken'))
    : null;
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
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
      throw new Error('Failed to fetch contract');
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
