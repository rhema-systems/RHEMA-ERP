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
  partnerName: string;
  companyName?: string; // Alias for partnerName
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  vatNumber?: string;
  email?: string;
  phone?: string;
  alternatePhone?: string;
  website?: string;
  physicalAddress?: string;
  city?: string;
  country?: string;
  status: string; // Active, Inactive, Suspended, Pending
  approvalStatus?: string; // Pending, Approved, Rejected
  isPreferred: boolean;
  isBlacklisted: boolean;
  performanceRating?: number;
  createdAt: string;
  updatedAt?: string;
}

export interface BusinessPartnerDetailDto extends BusinessPartnerDto {
  legalName?: string;
  postalAddress?: string;
  physicalState?: string;
  physicalPostalCode?: string;
  mailingAddress?: string;
  mailingCity?: string;
  mailingState?: string;
  mailingCountry?: string;
  mailingPostalCode?: string;
  // Banking Information
  bankName?: string;
  bankBranch?: string;
  accountNumber?: string;
  accountName?: string;
  swiftCode?: string;
  iban?: string;
  // Contact Person
  contactPerson?: string;
  contactTitle?: string;
  contactEmail?: string;
  contactPhone?: string;
  // Classification
  industryType?: string;
  companySize?: string;
  annualRevenue?: number;
  geographicCoverage?: string;
  // Other
  paymentTerms?: string;
  creditLimit?: number;
  insuranceCoverageAmount?: number;
  registrationDate?: string;
  approvedDate?: string;
  blacklistReason?: string;
  blacklistDate?: string;
  blacklistExpiryDate?: string;
  notes?: string;
  // Related Data
  contacts?: BusinessPartnerContactDto[];
  licenses?: BusinessPartnerLicenseDto[];
  documents?: BusinessPartnerDocumentDto[];
  financialRecords?: BusinessPartnerFinancialDto[];
  financialInfo?: BusinessPartnerFinancialDto[]; // Alias for financialRecords
  categories?: PartnerCategoryDto[];
  specializations?: ContractorSpecializationDto[];
}

export interface BusinessPartnerContactDto {
  id: string;
  businessPartnerId?: string;
  partnerId?: string; // Alias
  contactName?: string;
  name?: string; // Alias for contactName
  firstName?: string;
  lastName?: string;
  contactTitle?: string;
  title?: string; // Alias for contactTitle
  position?: string; // Alias for contactTitle
  department?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimary: boolean;
}

export interface BusinessPartnerDocumentDto {
  id: string;
  businessPartnerId?: string;
  partnerId?: string; // Alias
  documentType: string;
  documentName: string;
  filePath?: string;
  documentPath?: string; // Alias
  fileSize?: number;
  mimeType?: string;
  issueDate?: string;
  expiryDate?: string;
  uploadedAt?: string;
  isVerified: boolean;
  verifiedBy?: string;
  verifiedDate?: string;
  verificationNotes?: string;
}

export interface BusinessPartnerLicenseDto {
  id: string;
  businessPartnerId?: string;
  partnerId?: string; // Alias
  licenseTypeId: string;
  licenseTypeName?: string;
  licenseNumber: string;
  issueDate?: string;
  expiryDate?: string;
  issuingAuthority?: string;
  status: string; // Valid, Expired, Suspended, Active
  filePath?: string;
  documentPath?: string; // Alias
  verificationNotes?: string;
  isExpired?: boolean;
  daysUntilExpiry?: number;
}

export interface BusinessPartnerFinancialDto {
  id: string;
  businessPartnerId?: string;
  partnerId?: string; // Alias
  fiscalYear?: number;
  financialYear?: number; // Alias
  annualRevenue?: number;
  revenue?: number; // Alias
  netProfit?: number;
  profit?: number; // Alias
  totalAssets?: number;
  assets?: number; // Alias
  totalLiabilities?: number;
  liabilities?: number; // Alias
  creditRating?: string;
  financialStatementPath?: string;
  isAudited?: boolean;
  auditorName?: string;
  auditDate?: string;
  // Banking info (for compatibility)
  bankName?: string;
  bankAccountNumber?: string;
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
  city?: string;
  country?: string;
  postalCode?: string;
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;
  paymentTerms?: string;
  creditLimit?: number;
  notes?: string;
  categoryIds?: string[];
  specializationIds?: string[];
}

export interface UpdateBusinessPartnerDto {
  partnerName: string;
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  email?: string;
  phone?: string;
  physicalAddress?: string;
  city?: string;
  country?: string;
  postalCode?: string;
  website?: string;
  status?: string;
  isPreferred?: boolean;
  notes?: string;
  categoryIds?: string[];
  specializationIds?: string[];
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

  // Get partner by user ID
  async getPartnerByUserId(userId: string): Promise<BusinessPartnerDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/user/${userId}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch business partner by user ID');
    return response.json();
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

  // Blacklist partner
  async blacklistPartner(id: string, reason: string, blacklistUntil?: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-blacklist/partners/${id}/blacklist`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        reason,
        blacklistUntil: blacklistUntil || null
      })
    });

    if (!response.ok) throw new Error('Failed to blacklist business partner');
  },

  // Remove from blacklist
  async removeFromBlacklist(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-blacklist/partners/${id}/remove-from-blacklist`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to remove business partner from blacklist');
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

// Export individual methods for easier import
export const getPartnerByUserId = businessPartnerService.getPartnerByUserId.bind(businessPartnerService);
export const getPartnerById = businessPartnerService.getPartnerById.bind(businessPartnerService);
export const getPartners = businessPartnerService.getPartners.bind(businessPartnerService);

