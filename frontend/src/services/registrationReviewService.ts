/**
 * Service for admin registration review operations
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
}

export interface RegistrationReviewDto {
  id: string;
  applicationNumber: string;
  companyName: string;
  tradingName?: string;
  partnerType: string;
  email: string;
  phone: string;
  status: string;
  submittedAt?: string;
  reviewedAt?: string;
  reviewedBy?: string;
  reviewNotes?: string;
  completionPercentage: number;
}

export interface RegistrationDetailDto extends RegistrationReviewDto {
  registrationNumber?: string;
  taxNumber?: string;
  vatNumber?: string;
  website?: string;
  physicalAddress?: string;
  city?: string;
  country?: string;
  postalCode?: string;
  contactPersonName?: string;
  contactPersonEmail?: string;
  contactPersonPhone?: string;
  industryType?: string;
  yearsInBusiness?: number;
  numberOfEmployees?: number;
  annualRevenue?: number;
  bankName?: string;
  bankAccountNumber?: string;
  categoryIds?: string[];
  specializationIds?: string[];
  documents?: Array<{
    id: string;
    documentType: string;
    documentName: string;
    filePath: string;
    uploadedAt: string;
    isVerified: boolean;
  }>;
  licenses?: Array<{
    licenseTypeId: string;
    licenseNumber: string;
    issueDate: string;
    expiryDate?: string;
    issuingAuthority: string;
  }>;
  statusHistory?: Array<{
    id: string;
    status: string;
    changedAt: string;
    changedBy?: string;
    notes?: string;
  }>;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const registrationReviewService = {
  /**
   * Get all registrations with filtering and pagination
   */
  async getRegistrations(params: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    partnerType?: string;
  } = {}): Promise<PagedResult<RegistrationReviewDto>> {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.search) queryParams.append('search', params.search);
    if (params.status) queryParams.append('status', params.status);
    if (params.partnerType) queryParams.append('partnerType', params.partnerType);

    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations?${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return await response.json();
  },

  /**
   * Get pending review registrations
   */
  async getPendingReview(): Promise<RegistrationReviewDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/pending-review`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return await response.json();
  },

  /**
   * Get registration details by ID
   */
  async getById(id: string): Promise<RegistrationDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return await response.json();
  },

  /**
   * Approve a registration
   */
  async approve(id: string, notes?: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ approvedById: 'current-user-id', notes }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  /**
   * Reject a registration
   */
  async reject(id: string, reason: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ rejectedById: 'current-user-id', reason }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  /**
   * Request more information
   */
  async requestMoreInfo(id: string, notes: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}/request-more-info`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ requestedById: 'current-user-id', notes }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  /**
   * Verify a document
   */
  async verifyDocument(registrationId: string, documentId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents/${documentId}/verify`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ verifiedById: 'current-user-id' }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },
};

