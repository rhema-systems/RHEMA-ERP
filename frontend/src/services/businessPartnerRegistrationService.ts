// Business Partner Registration Service
// Handles external registration portal operations

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// ============================================================================
// INTERFACES
// ============================================================================

export interface BusinessPartnerRegistrationDto {
  id: string;
  applicationNumber: string;
  partnerType: string; // Supplier, Contractor, Both
  registrationCategory?: 'Goods' | 'Works' | 'Services';
  status: string; // Draft, Submitted, UnderReview, Approved, Rejected
  companyName: string;
  registrationNumber?: string;
  email?: string;
  phone?: string;
  submittedDate?: string;
  reviewedDate?: string;
  approvedDate?: string;
  reviewedBy?: string;
  approvedBy?: string;
  completionPercentage: number;
  createdAt: string;
}

export interface BusinessPartnerRegistrationDetailDto
  extends BusinessPartnerRegistrationDto {
  registrationData?: string; // JSON data containing full registration form
  reviewNotes?: string;
  rejectionReason?: string;
  documents?: BusinessPartnerRegistrationDocumentDto[];
  statusHistory?: BusinessPartnerRegistrationStatusHistoryDto[];
}

export interface BusinessPartnerRegistrationDocumentDto {
  id: string;
  registrationId: string;
  fileUploadRecordId?: string;
  virusScanStatus?: number;
  documentType: string;
  documentName: string;
  filePath: string;
  fileSize: number;
  mimeType?: string;
  evidenceRequirementCode?: string;
  classificationCode?: string;
  issuedAtUtc?: string;
  expiresAtUtc?: string;
  checksumSha256?: string;
  isVerified: boolean;
  isRejected: boolean;
  rejectionReason?: string;
  rejectedDate?: string;
  uploadedAt: string;
}

export interface BusinessPartnerRegistrationStatusHistoryDto {
  id: string;
  registrationId: string;
  fromStatus: string;
  toStatus: string;
  changedBy?: string;
  changedAt: string;
  notes?: string;
}

export interface CreateBusinessPartnerRegistrationDto {
  partnerType: string;
  registrationCategory?: 'Goods' | 'Works' | 'Services';
  companyName: string;
  registrationNumber?: string;
  email?: string;
  phone?: string;
  registrationData?: string; // JSON data
}

export interface UpdateBusinessPartnerRegistrationDto {
  companyName: string;
  registrationCategory?: 'Goods' | 'Works' | 'Services';
  registrationNumber?: string;
  email?: string;
  phone?: string;
  registrationData?: string; // JSON data
  completionPercentage: number;
}

export interface RegistrationFormData {
  // Company Information
  companyName: string;
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  vatNumber?: string;
  partnerType: string;
  registrationCategory?: 'Goods' | 'Works' | 'Services';

  // Contact Information
  email: string;
  phone: string;
  alternatePhone?: string;
  website?: string;
  physicalAddress?: string;
  city?: string;
  country?: string;
  postalCode?: string;

  // Primary Contact Person
  contactPersonName?: string;
  contactPersonTitle?: string;
  contactPersonEmail?: string;
  contactPersonPhone?: string;

  // Business Details
  industryType?: string;
  yearsInBusiness?: number;
  numberOfEmployees?: number;
  annualRevenue?: number;

  // Banking Information
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;

  // Categories and Specializations
  categoryIds?: string[];
  specializationIds?: string[];

  // License Information (for contractors)
  licenses?: Array<{
    licenseTypeId: string;
    licenseNumber: string;
    issueDate: string;
    expiryDate?: string;
    issuingAuthority: string;
  }>;

  // Documents
  documents?: Array<{
    documentType: string;
    documentName: string;
    file?: File;
    fileSize?: number;
  }>;
}

// ============================================================================
// API HELPER FUNCTIONS
// ============================================================================

const getAuthHeaders = (): HeadersInit => {
  const token =
    typeof window !== 'undefined'
      ? localStorage.getItem('token') || localStorage.getItem('authToken')
      : null;

  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
};

const handleResponse = async <T>(response: Response): Promise<T> => {
  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `HTTP error! status: ${response.status}`);
  }
  return response.json();
};

// ============================================================================
// BUSINESS PARTNER REGISTRATION SERVICE
// ============================================================================

export const businessPartnerRegistrationService = {
  /**
   * Get all registrations for the current user
   */
  async getMyRegistrations(): Promise<BusinessPartnerRegistrationDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/my-registrations`,
      {
        method: 'GET',
        headers: getAuthHeaders(),
      }
    );
    return handleResponse<BusinessPartnerRegistrationDto[]>(response);
  },

  /**
   * Get a registration by ID
   */
  async getById(id: string): Promise<BusinessPartnerRegistrationDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}`,
      {
        method: 'GET',
        headers: getAuthHeaders(),
      }
    );
    return handleResponse<BusinessPartnerRegistrationDetailDto>(response);
  },

  /**
   * Get a registration by application number (public - no auth required)
   */
  async getByApplicationNumber(
    applicationNumber: string
  ): Promise<BusinessPartnerRegistrationDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/by-application-number/${applicationNumber}`,
      {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' },
      }
    );
    return handleResponse<BusinessPartnerRegistrationDto>(response);
  },

  /**
   * Create a new registration (draft)
   */
  async create(
    data: CreateBusinessPartnerRegistrationDto
  ): Promise<BusinessPartnerRegistrationDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(data),
      }
    );
    return handleResponse<BusinessPartnerRegistrationDetailDto>(response);
  },

  /**
   * Update an existing registration (draft only)
   */
  async update(
    id: string,
    data: UpdateBusinessPartnerRegistrationDto
  ): Promise<BusinessPartnerRegistrationDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(data),
      }
    );
    return handleResponse<BusinessPartnerRegistrationDetailDto>(response);
  },

  /**
   * Delete a registration (draft only)
   */
  async delete(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP error! status: ${response.status}`);
    }
  },

  /**
   * Submit a registration for review
   */
  async submit(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${id}/submit`,
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
   * Upload a document for a registration
   */
  async uploadDocument(
    registrationId: string,
    file: File,
    documentType: string,
    evidence?: {
      requirementCode?: string;
      classificationCode?: string;
      issueDate?: string;
      expiryDate?: string;
    }
  ): Promise<BusinessPartnerRegistrationDocumentDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);
    formData.append('registrationId', registrationId);
    if (evidence?.requirementCode)
      formData.append('evidenceRequirementCode', evidence.requirementCode);
    if (evidence?.classificationCode)
      formData.append('classificationCode', evidence.classificationCode);
    if (evidence?.issueDate) formData.append('issueDate', evidence.issueDate);
    if (evidence?.expiryDate)
      formData.append('expiryDate', evidence.expiryDate);

    const token =
      typeof window !== 'undefined'
        ? localStorage.getItem('token') || localStorage.getItem('authToken')
        : null;

    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents`,
      {
        method: 'POST',
        headers: {
          ...(token && { Authorization: `Bearer ${token}` }),
        },
        body: formData,
      }
    );
    return handleResponse<BusinessPartnerRegistrationDocumentDto>(response);
  },

  /**
   * Track document download
   */
  async trackDocumentDownload(
    registrationId: string,
    documentId: string
  ): Promise<void> {
    const token =
      typeof window !== 'undefined'
        ? localStorage.getItem('token') || localStorage.getItem('authToken')
        : null;

    try {
      const response = await fetch(
        `${API_BASE_URL}/procurement/business-partner-registrations/${registrationId}/documents/${documentId}/track-download`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            ...(token && { Authorization: `Bearer ${token}` }),
          },
        }
      );

      // Don't throw error if tracking fails - it's not critical
      if (!response.ok) {
        console.warn('Failed to track document download:', response.statusText);
      }
    } catch (error) {
      // Silently fail - tracking shouldn't block document download
      console.warn('Error tracking document download:', error);
    }
  },

  /**
   * Download a document and track the download
   */
  async downloadDocument(
    registrationId: string,
    documentId: string,
    documentName: string,
    filePath: string
  ): Promise<void> {
    // Track the download first
    await this.trackDocumentDownload(registrationId, documentId);

    // Then download the file
    const token =
      typeof window !== 'undefined'
        ? localStorage.getItem('token') || localStorage.getItem('authToken')
        : null;

    const response = await fetch(filePath, {
      headers: {
        ...(token && { Authorization: `Bearer ${token}` }),
      },
    });

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

  /**
   * Helper: Convert RegistrationFormData to CreateBusinessPartnerRegistrationDto
   */
  convertFormDataToCreateDto(
    formData: RegistrationFormData
  ): CreateBusinessPartnerRegistrationDto {
    return {
      partnerType: formData.partnerType,
      registrationCategory: formData.registrationCategory,
      companyName: formData.companyName,
      registrationNumber: formData.registrationNumber || undefined,
      email: formData.email || undefined,
      phone: formData.phone || undefined,
      registrationData: JSON.stringify(formData),
    };
  },

  /**
   * Helper: Convert RegistrationFormData to UpdateBusinessPartnerRegistrationDto
   */
  convertFormDataToUpdateDto(
    formData: RegistrationFormData,
    completionPercentage: number
  ): UpdateBusinessPartnerRegistrationDto {
    return {
      companyName: formData.companyName,
      registrationCategory: formData.registrationCategory,
      registrationNumber: formData.registrationNumber || undefined,
      email: formData.email || undefined,
      phone: formData.phone || undefined,
      registrationData: JSON.stringify(formData),
      completionPercentage,
    };
  },

  /**
   * Helper: Parse registration data from JSON string
   */
  parseRegistrationData(
    registrationData?: string
  ): RegistrationFormData | null {
    if (!registrationData) return null;
    try {
      const topLevel: any = JSON.parse(registrationData);
      let raw: any = topLevel;

      // Backwards compatibility:
      // Older registrations stored the **entire C# DTO** as JSON with a nested
      // RegistrationData/registrationData string that contains the real
      // RegistrationFormData. Newer ones may store just the form JSON.
      if (raw && typeof raw === 'object') {
        const nested = raw.RegistrationData ?? raw.registrationData;
        if (typeof nested === 'string' && nested.trim().startsWith('{')) {
          try {
            raw = JSON.parse(nested);
          } catch {
            // If nested JSON is invalid, fall back to the outer object
            raw = topLevel;
          }
        }
      }

      // Normalise casing and ensure required fields always exist
      const result: RegistrationFormData = {
        ...(raw || {}),
        companyName: (raw?.companyName ?? raw?.CompanyName ?? '').toString(),
        partnerType: (
          raw?.partnerType ??
          raw?.PartnerType ??
          'Supplier'
        ).toString(),
        registrationCategory:
          raw?.registrationCategory ?? raw?.RegistrationCategory ?? undefined,
        email: (raw?.email ?? raw?.Email ?? '').toString(),
        phone: (raw?.phone ?? raw?.Phone ?? '').toString(),
      };

      return result;
    } catch {
      return null;
    }
  },

  /**
   * Helper: Calculate completion percentage based on form data
   */
  calculateCompletionPercentage(formData: RegistrationFormData): number {
    const requiredFields = [
      formData.companyName,
      formData.partnerType,
      formData.email,
      formData.phone,
      formData.physicalAddress,
      formData.city,
      formData.country,
    ];

    const optionalFields = [
      formData.tradingName,
      formData.registrationNumber,
      formData.taxNumber,
      formData.website,
      formData.contactPersonName,
      formData.contactPersonEmail,
      formData.bankName,
      formData.bankAccountNumber,
    ];

    const requiredFilled = requiredFields.filter(
      (f) => f && f.trim() !== ''
    ).length;
    const optionalFilled = optionalFields.filter(
      (f) => f && f.trim() !== ''
    ).length;

    const requiredWeight = 70; // 70% weight for required fields
    const optionalWeight = 30; // 30% weight for optional fields

    const requiredPercentage =
      (requiredFilled / requiredFields.length) * requiredWeight;
    const optionalPercentage =
      (optionalFilled / optionalFields.length) * optionalWeight;

    return Math.round(requiredPercentage + optionalPercentage);
  },
};
