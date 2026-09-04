/**
 * Service for admin registration review operations
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
}

async function readError(response: Response): Promise<string> {
  const body = await response.text();
  if (!body) return `HTTP error! status: ${response.status}`;

  try {
    const problem = JSON.parse(body) as {
      detail?: string;
      title?: string;
      message?: string;
    };
    return (
      problem.detail ||
      problem.message ||
      problem.title ||
      `HTTP error! status: ${response.status}`
    );
  } catch {
    return body;
  }
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
  submittedDate?: string;  // Changed from submittedAt to match backend
  reviewedDate?: string;   // Changed from reviewedAt to match backend
  reviewedBy?: string;
  reviewNotes?: string;
  completionPercentage: number;
  createdAt?: string;      // Added to match backend
}

export interface RegistrationDetailDto extends RegistrationReviewDto {
  registrationData?: string;
  registrationNumber?: string;
  taxNumber?: string;
  vatNumber?: string;
  website?: string;
  physicalAddress?: string;
  city?: string;
  country?: string;
  postalCode?: string;
  alternatePhone?: string;
  contactPersonName?: string;
  contactPersonTitle?: string;
  contactPersonEmail?: string;
  contactPersonPhone?: string;
  industryType?: string;
  yearsInBusiness?: number;
  numberOfEmployees?: number;
  annualRevenue?: number;
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;
  categoryIds?: string[];
  specializationIds?: string[];
  documents?: Array<{
    id: string;
    documentType: string;
    documentName: string;
    filePath: string;
    uploadedAt: string;
    isVerified: boolean;
    isRejected: boolean;
    rejectionReason?: string;
    rejectedDate?: string;
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
        body: JSON.stringify({ notes }),
      }
    );

    if (!response.ok) {
      throw new Error(await readError(response));
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
        body: JSON.stringify({ reason }),
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
        body: JSON.stringify({ notes }),
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
        body: JSON.stringify({}),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  /**
   * Reject a document
   */
  async rejectDocument(registrationId: string, documentId: string, reason: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents/${documentId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ reason }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  async revertDocumentRejection(registrationId: string, documentId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents/${documentId}/revert-rejection`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  /**
   * View a document (opens in new tab)
   */
  async viewDocument(registrationId: string, documentId: string, documentName: string): Promise<void> {
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');

    // Open the document download endpoint in a new tab
    const url = `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents/${documentId}/download`;

    // Create a temporary link and click it
    const link = document.createElement('a');
    link.href = url;
    link.target = '_blank';
    link.rel = 'noopener noreferrer';

    // Add authorization header by using fetch and creating blob URL
    const response = await fetch(url, {
      headers: {
        ...(token && { Authorization: `Bearer ${token}` }),
      },
    });

    if (!response.ok) {
      throw new Error('Failed to load document');
    }

    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    window.open(blobUrl, '_blank');

    // Clean up after a delay
    setTimeout(() => window.URL.revokeObjectURL(blobUrl), 100);
  },

  /**
   * Download a document
   */
  async downloadDocument(registrationId: string, documentId: string, documentName: string): Promise<void> {
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');

    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents/${documentId}/download`,
      {
        headers: {
          ...(token && { Authorization: `Bearer ${token}` }),
        },
      }
    );

    if (!response.ok) {
      throw new Error('Failed to download document');
    }

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = documentName;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  },
};
