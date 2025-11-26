/**
 * Business Partner Service
 * Main API service for Business Partner (Supplier/Contractor) management
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Helper function to get auth headers
const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

// ============================================================================
// BUSINESS PARTNER INTERFACES
// ============================================================================

export interface BusinessPartnerDto {
  id: string;
  partnerCode: string;
  partnerType: string; // Supplier, Contractor, Both
  companyName: string;
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  email?: string;
  phone?: string;
  website?: string;
  status: string; // Active, Inactive, Suspended, Pending
  approvalStatus: string; // Pending, Approved, Rejected
  isPreferred: boolean;
  isBlacklisted: boolean;
  performanceRating?: number;
  createdAt: string;
  updatedAt: string;
}

export interface BusinessPartnerDetailDto extends BusinessPartnerDto {
  physicalAddress?: string;
  postalAddress?: string;
  city?: string;
  country?: string;
  vatNumber?: string;
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;
  paymentTerms?: string;
  creditLimit?: number;
  notes?: string;
  contacts?: BusinessPartnerContactDto[];
  licenses?: BusinessPartnerLicenseDto[];
  documents?: BusinessPartnerDocumentDto[];
  categories?: PartnerCategoryDto[];
  specializations?: ContractorSpecializationDto[];
  financialInfo?: {
    bankName?: string;
    bankAccountNumber?: string;
    annualRevenue?: number;
  };
}

export interface BusinessPartnerContactDto {
  id: string;
  partnerId: string;
  name?: string;
  firstName?: string;
  lastName?: string;
  position?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimary: boolean;
}

export interface BusinessPartnerDocumentDto {
  id: string;
  partnerId: string;
  documentType: string;
  documentName: string;
  filePath?: string;
  uploadedAt?: string;
  isVerified: boolean;
}

export interface BusinessPartnerLicenseDto {
  id: string;
  partnerId: string;
  licenseTypeId: string;
  licenseTypeName?: string;
  licenseNumber: string;
  issueDate: string;
  expiryDate: string;
  issuingAuthority?: string;
  status: string; // Valid, Expired, Suspended
  documentPath?: string;
}

export interface PartnerCategoryDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
}

export interface ContractorSpecializationDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
}

export interface LicenseTypeDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  applicableTo: string; // Supplier, Contractor, Both
  isMandatory: boolean;
  validityPeriodMonths?: number;
  isActive: boolean;
}

export interface CreateBusinessPartnerDto {
  partnerType: string;
  companyName: string;
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  email?: string;
  phone?: string;
  website?: string;
  physicalAddress?: string;
  postalAddress?: string;
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;
  paymentTerms?: string;
  creditLimit?: number;
  notes?: string;
  categoryIds?: string[];
  specializationIds?: string[];
}

export interface UpdateBusinessPartnerDto extends CreateBusinessPartnerDto {
  status?: string;
  isPreferred?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ============================================================================
// BUSINESS PARTNER API METHODS
// ============================================================================

export const businessPartnerService = {
  // Get all partners with filtering and pagination
  async getPartners(params: {
    page?: number;
    pageSize?: number;
    search?: string;
    partnerType?: string;
    status?: string;
    approvalStatus?: string;
    categoryId?: string;
  } = {}): Promise<PagedResult<BusinessPartnerDto>> {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.search) queryParams.append('search', params.search);
    if (params.partnerType) queryParams.append('partnerType', params.partnerType);
    if (params.status) queryParams.append('status', params.status);
    if (params.approvalStatus) queryParams.append('approvalStatus', params.approvalStatus);
    if (params.categoryId) queryParams.append('categoryId', params.categoryId);

    const response = await fetch(`${API_BASE_URL}/procurement/business-partners?${queryParams}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch business partners');
    return response.json();
  },

  // Get partner by ID
  async getPartnerById(id: string): Promise<BusinessPartnerDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch business partner');
    return response.json();
  },

  // Alias for getPartnerById
  async getById(id: string): Promise<BusinessPartnerDetailDto> {
    return this.getPartnerById(id);
  },

  // Get active partners
  async getActivePartners(partnerType?: string): Promise<BusinessPartnerDto[]> {
    const queryParams = partnerType ? `?partnerType=${partnerType}` : '';
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/active${queryParams}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch active partners');
    return response.json();
  },

  // Get preferred partners
  async getPreferredPartners(partnerType?: string): Promise<BusinessPartnerDto[]> {
    const queryParams = partnerType ? `?partnerType=${partnerType}` : '';
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/preferred${queryParams}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch preferred partners');
    return response.json();
  },

  // Create new partner
  async createPartner(data: CreateBusinessPartnerDto): Promise<BusinessPartnerDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to create business partner');
    return response.json();
  },

  // Update partner
  async updatePartner(id: string, data: UpdateBusinessPartnerDto): Promise<BusinessPartnerDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to update business partner');
    return response.json();
  },

  // Delete partner
  async deletePartner(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to delete business partner');
  },

  // Approve partner
  async approvePartner(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to approve business partner');
  },

  // Reject partner
  async rejectPartner(id: string, reason: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}/reject`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ rejectionReason: reason })
    });

    if (!response.ok) throw new Error('Failed to reject business partner');
  },

  // Suspend partner
  async suspendPartner(id: string, reason?: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}/suspend`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ suspensionReason: reason })
    });

    if (!response.ok) throw new Error('Failed to suspend business partner');
  },

  // Activate partner
  async activatePartner(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${id}/activate`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to activate business partner');
  },

  // Get partner contacts
  async getPartnerContacts(partnerId: string): Promise<BusinessPartnerContactDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${partnerId}/contacts`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch partner contacts');
    return response.json();
  },

  // Get partner licenses
  async getPartnerLicenses(partnerId: string): Promise<BusinessPartnerLicenseDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${partnerId}/licenses`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch partner licenses');
    return response.json();
  }
};

