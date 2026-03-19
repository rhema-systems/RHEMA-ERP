// Sales Agreement API Service

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined'
    ? (localStorage.getItem('token') || localStorage.getItem('authToken'))
    : null;
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// ==================== INTERFACES ====================

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SalesAgreementSummaryDto {
  id: string;
  documentNumber: string;
  agreementTitle: string;
  customerName: string;
  businessPartnerId: string;
  agreementType: string;
  agreementStatus: string;
  startDate: string;
  endDate?: string;
  agreedValue: number;
  utilizedValue: number;
  currency: string;
  propertyReference?: string;
  salesRepName?: string;
  autoRenew: boolean;
  completedMilestones: number;
  totalMilestones: number;
  createdAt: string;
}

export interface SalesAgreementDetailDto {
  id: string;
  documentNumber: string;
  agreementTitle: string;
  businessPartnerId: string;
  customerName: string;
  agreementType: string;
  agreementStatus: string;
  startDate: string;
  endDate?: string;
  expiryWarningDays: number;
  autoRenew: boolean;
  renewalPeriodMonths?: number;
  agreedValue: number;
  minimumCommitment: number;
  maximumCommitment: number;
  utilizedValue: number;
  currency: string;
  discountPercentage?: number;
  pricingTerms?: string;
  paymentSchedule?: string;
  propertyReference?: string;
  propertyType?: string;
  propertyDescription?: string;
  propertyLocation?: string;
  salesRepId?: string;
  salesRepName?: string;
  approvedByName?: string;
  approvedDate?: string;
  approvalComments?: string;
  terminatedDate?: string;
  terminationReason?: string;
  terminatedByName?: string;
  notes?: string;
  internalNotes?: string;
  termsAndConditions?: string;
  lines: SalesAgreementLineDto[];
  milestones: SalesAgreementMilestoneDto[];
  renewals: SalesAgreementRenewalDto[];
  documents: SalesAgreementDocumentDto[];
  createdAt: string;
  createdByName?: string;
  modifiedAt?: string;
}

export interface SalesAgreementLineDto {
  id: string;
  lineNumber: number;
  productId?: string;
  description: string;
  productCode?: string;
  agreedPrice: number;
  minimumQuantity: number;
  maximumQuantity: number;
  utilizedQuantity: number;
  unit?: string;
  discountPercentage: number;
  discountTiersJson?: string;
  notes?: string;
}

export interface SalesAgreementMilestoneDto {
  id: string;
  sequenceNumber: number;
  milestoneName: string;
  description?: string;
  paymentPercentage: number;
  paymentAmount: number;
  dueDate?: string;
  completedDate?: string;
  paidDate?: string;
  status: string;
  invoiceNumber?: string;
  notes?: string;
}

export interface SalesAgreementRenewalDto {
  id: string;
  renewalNumber: number;
  previousStartDate: string;
  previousEndDate: string;
  newStartDate: string;
  newEndDate: string;
  previousValue: number;
  newValue: number;
  priceChangePercentage?: number;
  renewalTerms?: string;
  notes?: string;
  renewedByName?: string;
  renewedDate: string;
}

export interface SalesAgreementDocumentDto {
  id: string;
  fileName: string;
  filePath: string;
  contentType?: string;
  fileSize?: number;
  documentType: string;
  description?: string;
  uploadedByName?: string;
  createdAt: string;
}

export interface CreateSalesAgreementDto {
  businessPartnerId: string;
  agreementTitle: string;
  agreementType?: string;
  startDate: string;
  endDate?: string;
  expiryWarningDays?: number;
  autoRenew?: boolean;
  renewalPeriodMonths?: number;
  agreedValue: number;
  minimumCommitment?: number;
  maximumCommitment?: number;
  currency?: string;
  discountPercentage?: number;
  pricingTerms?: string;
  paymentSchedule?: string;
  propertyReference?: string;
  propertyType?: string;
  propertyDescription?: string;
  propertyLocation?: string;
  salesRepId?: string;
  notes?: string;
  internalNotes?: string;
  termsAndConditions?: string;
  lines?: CreateSalesAgreementLineDto[];
  milestones?: CreateSalesAgreementMilestoneDto[];
}

export interface CreateSalesAgreementLineDto {
  productId?: string;
  description: string;
  productCode?: string;
  agreedPrice: number;
  minimumQuantity?: number;
  maximumQuantity?: number;
  unit?: string;
  discountPercentage?: number;
  discountTiersJson?: string;
  notes?: string;
}

export interface CreateSalesAgreementMilestoneDto {
  milestoneName: string;
  description?: string;
  paymentPercentage: number;
  paymentAmount: number;
  dueDate?: string;
  notes?: string;
}

export interface RenewAgreementDto {
  newEndDate: string;
  newValue?: number;
  renewalTerms?: string;
  notes?: string;
}

// ==================== API FUNCTIONS ====================

export const salesAgreementService = {
  async getAgreements(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    status?: string,
    agreementType?: string,
    customerId?: string,
  ): Promise<PagedResult<SalesAgreementSummaryDto>> {
    const params = new URLSearchParams();
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    if (agreementType) params.append('agreementType', agreementType);
    if (customerId) params.append('customerId', customerId);

    const response = await fetch(`${API_BASE_URL}/sales/agreements?${params.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch agreements');
    return response.json();
  },

  async getAgreementById(id: string): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch agreement');
    return response.json();
  },

  async createAgreement(data: CreateSalesAgreementDto): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create agreement');
    }
    return response.json();
  },

  async submitForApproval(id: string): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}/submit`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async processApproval(id: string, data: { isApproved: boolean; comments?: string }): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async suspend(id: string, reason?: string): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}/suspend?reason=${encodeURIComponent(reason || '')}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async resume(id: string): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}/resume`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async terminate(id: string, reason: string): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}/terminate`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ reason }),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async renew(id: string, data: RenewAgreementDto): Promise<SalesAgreementDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/${id}/renew`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async updateMilestone(milestoneId: string, data: { status: string; actualDate?: string; invoiceNumber?: string; notes?: string }): Promise<SalesAgreementMilestoneDto> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/milestones/${milestoneId}`, {
      method: 'PATCH',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) { const e = await response.text(); throw new Error(e || 'Failed'); }
    return response.json();
  },

  async getExpiringAgreements(daysAhead: number = 30): Promise<SalesAgreementSummaryDto[]> {
    const response = await fetch(`${API_BASE_URL}/sales/agreements/expiring?daysAhead=${daysAhead}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Failed to fetch expiring agreements');
    return response.json();
  },
};
