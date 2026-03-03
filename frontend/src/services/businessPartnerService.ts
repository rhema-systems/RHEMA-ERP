/**
 * Business Partner Service
 * Main API service for Business Partner (Supplier/Contractor/Customer) management
 *
 * @module services/businessPartnerService
 *
 * CHANGE LOG:
 * -----------
 * 2026-02-19 — REFACTORED to use centralized `apiService` (from api.service.ts)
 *
 * WHAT CHANGED:
 *   - Replaced standalone `fetch()` calls with `apiService.get/post/put/delete()`
 *   - Removed local `API_BASE_URL` constant and `getAuthHeaders()` helper
 *   - Added `import { apiService } from './api.service'`
 *
 * WHAT DID NOT CHANGE:
 *   - All interfaces (BusinessPartnerDto, CreateBusinessPartnerDto, etc.)
 *   - All method signatures and return types
 *   - All exported names (businessPartnerService, getPartners, getPartnerById, etc.)
 *   - Business logic (data cleaning, query param construction)
 *
 * WHY:
 *   The previous implementation used raw `fetch()` with a hardcoded fallback URL:
 *     `const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api'`
 *
 *   This caused "Failed to fetch business partners" errors because:
 *   1. Port 5000 is occupied by a Windows system process (HTTP.sys/PID 4), NOT our API
 *   2. Our API actually runs on port 53485 (configured in launchSettings.json)
 *   3. When NEXT_PUBLIC_API_URL wasn't resolved, requests hit the wrong port → 404
 *
 *   The centralized `apiService` (api.service.ts) is already used by ~30 other services
 *   across Finance, Auth, Admin, EHC, Maintenance, Analytics, and more. It provides:
 *   - Single source of truth for the API base URL
 *   - Automatic JWT token management (no manual localStorage reads)
 *   - Automatic 401 → token refresh → retry cycle
 *   - Session blacklist event dispatch on auth failure
 *   - Consistent request/response logging
 *
 * PATTERN REFERENCE: See finance.service.ts for the canonical example of this pattern.
 */

// Centralized API client — handles base URL, auth tokens, and error handling.
// See api.service.ts for implementation details.
import { apiService } from './api.service';

// ============================================================================
// BUSINESS PARTNER INTERFACES
// ============================================================================

export interface BusinessPartnerDto {
  id: string;
  partnerCode: string;
  partnerType: string; // Supplier, Contractor, Both, Customer
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
  // Parent/Hierarchy
  parentId?: string;
  parentName?: string;
  // Customer-specific fields for list view
  customerType?: string;
  creditLimit?: number;
  outstandingBalance?: number;
  paymentTerms?: string;
  salesRepresentativeId?: string;
  salesRepresentativeName?: string;
  // Additional common fields
  contactPerson?: string;
  postalAddress?: string;
  province?: string;
  postalCode?: string;
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;
  paymentMethod?: string;
  notes?: string;
}

export interface BusinessPartnerContactDto {
  id: string;
  partnerId: string;
  contactName: string;
  contactTitle?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimary: boolean;
  department?: string;
  notes?: string;
}

export interface BusinessPartnerLicenseDto {
  id: string;
  partnerId: string;
  licenseType: string;
  licenseNumber: string;
  issuingAuthority?: string;
  issueDate: string;
  expiryDate?: string;
  status: string;
  notes?: string;
}

export interface BusinessPartnerDetailDto extends BusinessPartnerDto {
  contacts?: BusinessPartnerContactDto[];
  licenses?: BusinessPartnerLicenseDto[];
  // Extended customer fields
  customerSince?: string;
  lastOrderDate?: string;
  totalOrders?: number;
  totalRevenue?: number;
  loyaltyTier?: string;
  loyaltyPoints?: number;
  preferredDeliveryMethod?: string;
  preferredPaymentMethod?: string;
  taxExempt?: boolean;
  taxExemptionNumber?: string;
  // Extended supplier fields
  supplierSince?: string;
  lastDeliveryDate?: string;
  averageLeadTime?: number;
  qualityRating?: number;
  deliveryRating?: number;
  priceCompetitiveness?: number;
}

export interface CreateBusinessPartnerDto {
  partnerType: string;
  partnerName: string;
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
  status?: string;
  parentId?: string;
  contactPerson?: string;
  postalAddress?: string;
  province?: string;
  postalCode?: string;
  bankName?: string;
  bankAccountNumber?: string;
  bankBranchCode?: string;
  paymentMethod?: string;
  paymentTerms?: string;
  notes?: string;
  // Customer-specific
  customerType?: string;
  creditLimit?: number;
  salesRepresentativeId?: string;
  preferredDeliveryMethod?: string;
  preferredPaymentMethod?: string;
  taxExempt?: boolean;
  taxExemptionNumber?: string;
}

export interface UpdateBusinessPartnerDto extends Partial<CreateBusinessPartnerDto> {
  id?: string;
  parentId?: string;
}

// ============================================================================
// RESPONSE INTERFACES
// ============================================================================

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

// Base path is relative — apiService prepends the full API base URL automatically.
// e.g. this becomes: http://localhost:53485/api/procurement/business-partners
const BASE_PATH = '/procurement/business-partners';

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

    const query = queryParams.toString();
    return apiService.get<PagedResult<BusinessPartnerDto>>(`${BASE_PATH}${query ? `?${query}` : ''}`);
  },

  // Get partner by ID
  async getPartnerById(id: string): Promise<BusinessPartnerDetailDto> {
    return apiService.get<BusinessPartnerDetailDto>(`${BASE_PATH}/${id}`);
  },

  // Alias for getPartnerById
  async getById(id: string): Promise<BusinessPartnerDetailDto> {
    return this.getPartnerById(id);
  },

  // Get partner by user ID
  async getPartnerByUserId(userId: string): Promise<BusinessPartnerDetailDto> {
    return apiService.get<BusinessPartnerDetailDto>(`${BASE_PATH}/user/${userId}`);
  },

  // Get active partners
  async getActivePartners(partnerType?: string): Promise<BusinessPartnerDto[]> {
    const queryParams = partnerType ? `?partnerType=${partnerType}` : '';
    return apiService.get<BusinessPartnerDto[]>(`${BASE_PATH}/active${queryParams}`);
  },

  // Get all partners for dropdown (simple list)
  async getAllPartnersForDropdown(): Promise<BusinessPartnerDto[]> {
    const result = await apiService.get<PagedResult<BusinessPartnerDto>>(`${BASE_PATH}?pageSize=1000`);
    return result.items || (result as any);
  },

  // Get preferred partners
  async getPreferredPartners(partnerType?: string): Promise<BusinessPartnerDto[]> {
    const queryParams = partnerType ? `?partnerType=${partnerType}` : '';
    return apiService.get<BusinessPartnerDto[]>(`${BASE_PATH}/preferred${queryParams}`);
  },

  // Create new partner
  async createPartner(data: CreateBusinessPartnerDto): Promise<BusinessPartnerDetailDto> {
    // Clean up the data - convert empty strings to undefined for nullable Guid fields
    const cleanedData = {
      ...data,
      parentId: data.parentId && data.parentId !== '' ? data.parentId : undefined,
    };

    return apiService.post<BusinessPartnerDetailDto>(BASE_PATH, cleanedData);
  },

  // Update partner
  async updatePartner(id: string, data: UpdateBusinessPartnerDto): Promise<BusinessPartnerDetailDto> {
    // Clean up the data - convert empty strings to undefined for nullable Guid fields
    const cleanedData = {
      ...data,
      parentId: data.parentId && data.parentId !== '' ? data.parentId : undefined,
    };

    return apiService.put<BusinessPartnerDetailDto>(`${BASE_PATH}/${id}`, cleanedData);
  },

  // Delete partner
  async deletePartner(id: string): Promise<void> {
    return apiService.delete(`${BASE_PATH}/${id}`);
  },

  // Submit partner for approval
  async submitPartnerForApproval(id: string): Promise<void> {
    return apiService.post(`${BASE_PATH}/${id}/submit`);
  },

  // Approve partner (workflow)
  async approvePartner(id: string, notes?: string): Promise<void> {
    return apiService.post(`${BASE_PATH}/${id}/approve`, { notes: notes || undefined });
  },

  // Reject partner
  async rejectPartner(id: string, reason: string): Promise<void> {
    return apiService.post(`${BASE_PATH}/${id}/reject`, { rejectionReason: reason });
  },

  // Suspend partner
  async suspendPartner(id: string, reason?: string): Promise<void> {
    return apiService.post(`${BASE_PATH}/${id}/suspend`, { suspensionReason: reason });
  },

  // Activate partner
  async activatePartner(id: string): Promise<void> {
    return apiService.post(`${BASE_PATH}/${id}/activate`);
  },

  // Blacklist partner
  async blacklistPartner(id: string, reason: string, blacklistUntil?: string): Promise<void> {
    return apiService.post(`/procurement/partner-blacklist/partners/${id}/blacklist`, {
      reason,
      blacklistUntil: blacklistUntil || null
    });
  },

  // Remove from blacklist
  async removeFromBlacklist(id: string): Promise<void> {
    return apiService.post(`/procurement/partner-blacklist/partners/${id}/remove-from-blacklist`);
  },

  // Get partner contacts
  async getPartnerContacts(partnerId: string): Promise<BusinessPartnerContactDto[]> {
    return apiService.get<BusinessPartnerContactDto[]>(`${BASE_PATH}/${partnerId}/contacts`);
  },

  // Get partner licenses
  async getPartnerLicenses(partnerId: string): Promise<BusinessPartnerLicenseDto[]> {
    return apiService.get<BusinessPartnerLicenseDto[]>(`${BASE_PATH}/${partnerId}/licenses`);
  }
};

// Export individual methods for easier import
export const getPartnerByUserId = businessPartnerService.getPartnerByUserId.bind(businessPartnerService);
export const getPartnerById = businessPartnerService.getPartnerById.bind(businessPartnerService);
export const getPartners = businessPartnerService.getPartners.bind(businessPartnerService);
