/**
 * Business Partner Service
 * Main API service for Business Partner (Supplier/Contractor/Customer) management
 */
import type { Account } from '@/types/finance';
import type { BankAccount } from '@/types/cash-management';
import type { TaxGroup } from '@/types/tax';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Helper function to get auth headers
const getAuthHeaders = () => {
  const token =
    localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
};

// ============================================================================
// BUSINESS PARTNER INTERFACES
// ============================================================================

export interface BusinessPartnerReceivablesDefaults {
  defaultArAccountId?: string | null;
  salesAccountId?: string | null;
  costOfSalesAccountId?: string | null;
  inventoryAccountId?: string | null;
  termsDiscountsTakenAccountId?: string | null;
  salesReturnsAccountId?: string | null;
  financeChargesAccountId?: string | null;
  writeoffAccountId?: string | null;
  overpaymentWriteoffAccountId?: string | null;
}

export interface BusinessPartnerPostingDefaults {
  subjectToWithholdingDeduction: boolean;
  withholdingTaxRate: number;
  defaultWithholdingTaxId?: string | null;
  defaultTaxGroupId?: string | null;
  defaultBankAccountId?: string | null;
  cashAccountSource: 'Chequebook' | 'BusinessPartner';
  defaultCashAccountId?: string | null;
  defaultApAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  defaultTermsDiscountsAvailableAccountId?: string | null;
  defaultTermsDiscountsTakenAccountId?: string | null;
  defaultFinanceChargesAccountId?: string | null;
  defaultTradeDiscountAccountId?: string | null;
  defaultMiscellaneousAccountId?: string | null;
  defaultFreightAccountId?: string | null;
  defaultTaxAccountId?: string | null;
  defaultWriteoffAccountId?: string | null;
  defaultAccruedPurchasesAccountId?: string | null;
  defaultPurchasePriceVarianceAccountId?: string | null;
}

export interface BusinessPartnerPostingOptions {
  accounts: Account[];
  bankAccounts: BankAccount[];
  taxGroups: TaxGroup[];
  withholdingTaxes?: BusinessPartnerWithholdingTaxOption[];
}

export interface BusinessPartnerWithholdingTaxOption {
  id: string;
  code: string;
  name: string;
  rate: number;
  effectiveFrom?: string;
  taxPayableAccountId?: string | null;
}

export interface BusinessPartnerDto {
  id: string;
  partnerCode: string;
  partnerType: string; // Supplier, Contractor, Both, Customer
  roleTypes?: Array<'Supplier' | 'Contractor' | 'Customer'>;
  partnerName: string;
  companyName?: string; // Alias for partnerName
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  ssnitNumber?: string;
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
  isActive?: boolean;
  isBlacklisted: boolean;
  currency?: string;
  paymentTermId?: string;
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
  isOnCreditHold?: boolean;
  categories?: string[];

  // Workflow display helpers (optional)
  currentWorkflowStepName?: string;
}

export interface BusinessPartnerDetailDto extends BusinessPartnerDto {
  receivablesDefaults?: BusinessPartnerReceivablesDefaults;
  postingDefaults?: BusinessPartnerPostingDefaults;
  parentId?: string;
  parentName?: string;
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
  paymentTermId?: string;
  currency?: string;
  creditLimit?: number;
  insuranceCoverageAmount?: number;
  registrationDate?: string;
  approvedDate?: string;
  blacklistReason?: string;
  blacklistDate?: string;
  blacklistExpiryDate?: string;
  notes?: string;
  // Customer-Specific Fields (for Debtors/Sales)
  customerAccountNumber?: string;
  defaultDiscount?: number;
  priceList?: string;
  salesRepresentativeId?: string;
  salesRepresentativeName?: string;
  salesTerritory?: string;
  isTaxExempt?: boolean;
  taxExemptionNumber?: string;
  taxExemptionExpiry?: string;
  preferredShippingMethod?: string;
  deliveryInstructions?: string;
  customerSince?: string;
  lastPurchaseDate?: string;
  totalLifetimePurchases?: number;
  averageOrderValue?: number;
  loyaltyTier?: string;
  loyaltyPoints?: number;
  creditHoldReason?: string;
  creditHoldDate?: string;
  // Related Data
  contacts?: BusinessPartnerContactDto[];
  bankAccounts?: BusinessPartnerBankAccountDto[];
  licenses?: BusinessPartnerLicenseDto[];
  documents?: BusinessPartnerDocumentDto[];
  financialRecords?: BusinessPartnerFinancialDto[];
  financialInfo?: BusinessPartnerFinancialDto[]; // Alias for financialRecords
  specializations?: ContractorSpecializationDto[];
}

export interface BusinessPartnerBankAccountDto {
  id: string;
  businessPartnerId?: string;
  bankName: string;
  branchName?: string;
  accountName?: string;
  accountNumber: string;
  swiftCode?: string;
  iban?: string;
  currency?: string;
  isPrimary: boolean;
  isActive: boolean;
}

export interface BusinessPartnerContactDto {
  isActive?: boolean;
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

export interface CreateBusinessPartnerContactDto {
  contactName: string;
  title?: string;
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
  receivablesDefaults?: BusinessPartnerReceivablesDefaults;
  postingDefaults?: BusinessPartnerPostingDefaults;
  partnerType: string;
  roleTypes?: Array<'Supplier' | 'Contractor' | 'Customer'>;
  partnerName: string;
  companyName?: string; // Alias for partnerName
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  ssnitNumber?: string;
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
  paymentTermId?: string | null;
  currency?: string;
  creditLimit?: number;
  notes?: string;
  categoryIds?: string[];
  specializationIds?: string[];
  // Parent/Hierarchy
  parentId?: string | null;
  // Customer-Specific Fields
  customerType?: string; // Retail, Wholesale, Corporate, Government
  defaultDiscount?: number;
  priceList?: string;
  salesRepresentativeId?: string | null;
  salesTerritory?: string;
  isTaxExempt?: boolean;
  taxExemptionNumber?: string;
  taxExemptionExpiry?: string;
  preferredShippingMethod?: string;
  deliveryInstructions?: string;
  customerSince?: string;
  loyaltyTier?: string;
}

export interface UpdateBusinessPartnerDto {
  partnerType?: string;
  receivablesDefaults?: BusinessPartnerReceivablesDefaults;
  postingDefaults?: BusinessPartnerPostingDefaults;
  creditLimit?: number | null;
  partnerName: string;
  tradingName?: string;
  registrationNumber?: string;
  taxNumber?: string;
  ssnitNumber?: string;
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
  currency?: string;
  paymentTerms?: string;
  paymentTermId?: string | null;
  priceList?: string;
  parentId?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const BUSINESS_PARTNER_DROPDOWN_PAGE_SIZE = 100;
const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const normalizeOptionalGuid = (
  value: string | null | undefined,
  fieldLabel: string
): string | undefined => {
  const normalized = value?.trim();
  if (!normalized) {
    return undefined;
  }

  if (!GUID_PATTERN.test(normalized)) {
    throw new Error(
      `${fieldLabel} must be selected from the available options.`
    );
  }

  return normalized;
};

const normalizePostingDefaults = (
  defaults?: BusinessPartnerPostingDefaults
): BusinessPartnerPostingDefaults | undefined => {
  if (!defaults) return undefined;
  if (
    !Number.isFinite(defaults.withholdingTaxRate) ||
    defaults.withholdingTaxRate < 0 ||
    defaults.withholdingTaxRate > 100
  ) {
    throw new Error('WHT Rate must be between 0 and 100.');
  }
  if (
    defaults.subjectToWithholdingDeduction &&
    defaults.withholdingTaxRate <= 0
  )
    throw new Error(
      'Enter a WHT Rate greater than zero when withholding deduction is selected.'
    );
  const normalized = {
    ...defaults,
    withholdingTaxRate: defaults.subjectToWithholdingDeduction
      ? defaults.withholdingTaxRate
      : 0,
  };
  for (const key of Object.keys(
    normalized
  ) as (keyof BusinessPartnerPostingDefaults)[]) {
    if (key.endsWith('Id')) {
      // Explicit null clears a saved mapping; an unavailable catalogue must never clear it implicitly.
      const value = normalizeOptionalGuid(
        normalized[key] as string | null | undefined,
        key
      );
      Object.assign(normalized, { [key]: value ?? null });
    }
  }
  return normalized;
};

const partnerSaveError = async (
  response: Response,
  fallback: string
): Promise<Error> => {
  try {
    const problem = (await response.json()) as {
      detail?: string;
      message?: string;
      title?: string;
      code?: string;
      errors?: Record<string, string[]>;
    };
    const message =
      problem.detail ||
      problem.message ||
      Object.values(problem.errors || {})
        .flat()
        .join(' ') ||
      problem.title ||
      fallback;
    return new Error(problem.code ? `${message} (${problem.code})` : message);
  } catch {
    return new Error(fallback);
  }
};

const getBusinessPartnerDropdownItems = (
  result: unknown
): BusinessPartnerDto[] => {
  if (Array.isArray(result)) {
    return result as BusinessPartnerDto[];
  }

  if (result && typeof result === 'object') {
    const pagedResult = result as {
      items?: BusinessPartnerDto[];
      Items?: BusinessPartnerDto[];
    };

    return pagedResult.items ?? pagedResult.Items ?? [];
  }

  return [];
};

const getBusinessPartnerDropdownTotalPages = (result: unknown): number => {
  if (result && typeof result === 'object' && !Array.isArray(result)) {
    const pagedResult = result as {
      totalPages?: number;
      TotalPages?: number;
    };

    return Math.max(1, pagedResult.totalPages ?? pagedResult.TotalPages ?? 1);
  }

  return 1;
};

// ============================================================================
// BUSINESS PARTNER API METHODS
// ============================================================================

export const businessPartnerService = {
  async getMyAccount(): Promise<{ id: string; partnerCode: string; partnerName: string } | null> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/my-account`, { headers: getAuthHeaders() });
    if (response.status === 404) return null;
    if (!response.ok) throw new Error('Could not load your business partner account.');
    return response.json();
  },
  async getPostingOptions(
    partnerType = 'Supplier'
  ): Promise<BusinessPartnerPostingOptions> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/posting-options?partnerType=${encodeURIComponent(partnerType || 'Supplier')}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw await partnerSaveError(
        response,
        'Failed to load business partner posting options'
      );
    return response.json();
  },
  // Get all partners with filtering and pagination
  async getPartners(
    params: {
      page?: number;
      pageSize?: number;
      search?: string;
      partnerType?: string;
      status?: string;
      approvalStatus?: string;
      categoryId?: string;
    } = {}
  ): Promise<PagedResult<BusinessPartnerDto>> {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize)
      queryParams.append('pageSize', params.pageSize.toString());
    if (params.search) queryParams.append('search', params.search);
    if (params.partnerType)
      queryParams.append('partnerType', params.partnerType);
    if (params.status) queryParams.append('status', params.status);
    if (params.approvalStatus)
      queryParams.append('approvalStatus', params.approvalStatus);
    if (params.categoryId) queryParams.append('categoryId', params.categoryId);

    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners?${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch business partners');
    return response.json();
  },

  // Get partner by ID
  async getPartnerById(id: string): Promise<BusinessPartnerDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch business partner');
    return response.json();
  },

  // Alias for getPartnerById
  async getById(id: string): Promise<BusinessPartnerDetailDto> {
    return this.getPartnerById(id);
  },

  // Get partner by user ID
  async getPartnerByUserId(userId: string): Promise<BusinessPartnerDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/user/${userId}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch business partner by user ID');
    return response.json();
  },

  // Get active partners
  async getActivePartners(partnerType?: string): Promise<BusinessPartnerDto[]> {
    const queryParams = partnerType ? `?partnerType=${partnerType}` : '';
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/active${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch active partners');
    return response.json();
  },

  // Get all partners for dropdown (simple list)
  async getAllPartnersForDropdown(): Promise<BusinessPartnerDto[]> {
    const buildUrl = (page: number) =>
      `${API_BASE_URL}/procurement/business-partners?page=${page}&pageSize=${BUSINESS_PARTNER_DROPDOWN_PAGE_SIZE}`;

    const response = await fetch(buildUrl(1), {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch partners for dropdown');
    const firstPageResult = await response.json();
    const firstPageItems = getBusinessPartnerDropdownItems(firstPageResult);
    const totalPages = getBusinessPartnerDropdownTotalPages(firstPageResult);

    if (totalPages <= 1) {
      return firstPageItems;
    }

    const remainingPages = await Promise.all(
      Array.from({ length: totalPages - 1 }, async (_, index) => {
        const pageResponse = await fetch(buildUrl(index + 2), {
          headers: getAuthHeaders(),
        });

        if (!pageResponse.ok) {
          throw new Error('Failed to fetch partners for dropdown');
        }

        return pageResponse.json();
      })
    );

    const allPartners = [
      ...firstPageItems,
      ...remainingPages.flatMap((pageResult) =>
        getBusinessPartnerDropdownItems(pageResult)
      ),
    ];

    return Array.from(
      new Map(allPartners.map((partner) => [partner.id, partner])).values()
    );
  },

  // Get preferred partners
  async getPreferredPartners(
    partnerType?: string
  ): Promise<BusinessPartnerDto[]> {
    const queryParams = partnerType ? `?partnerType=${partnerType}` : '';
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/preferred${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch preferred partners');
    return response.json();
  },

  // Create new partner
  async createPartner(
    data: CreateBusinessPartnerDto
  ): Promise<BusinessPartnerDetailDto> {
    if (
      data.creditLimit !== undefined &&
      (!Number.isFinite(data.creditLimit) || data.creditLimit < 0)
    )
      throw new Error('Credit Limit must be zero or greater.');
    // ASP.NET nullable Guid properties accept a Guid or null, but not an empty string.
    const cleanedData = {
      ...data,
      parentId: normalizeOptionalGuid(data.parentId, 'Parent business partner'),
      paymentTermId: normalizeOptionalGuid(data.paymentTermId, 'Payment term'),
      salesRepresentativeId: normalizeOptionalGuid(
        data.salesRepresentativeId,
        'Sales representative'
      ),
      postingDefaults: normalizePostingDefaults(data.postingDefaults),
    };

    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(cleanedData),
      }
    );

    if (!response.ok)
      throw await partnerSaveError(
        response,
        'Failed to create business partner'
      );
    return response.json();
  },

  // Update partner
  async updatePartner(
    id: string,
    data: UpdateBusinessPartnerDto
  ): Promise<BusinessPartnerDetailDto> {
    if (
      data.creditLimit != null &&
      (!Number.isFinite(data.creditLimit) || data.creditLimit < 0)
    )
      throw new Error('Credit Limit must be zero or greater.');
    // Keep nullable Guid fields out of JSON when the corresponding optional select is blank.
    const cleanedData = {
      ...data,
      parentId: normalizeOptionalGuid(data.parentId, 'Parent business partner'),
      paymentTermId: normalizeOptionalGuid(data.paymentTermId, 'Payment term'),
      postingDefaults: normalizePostingDefaults(data.postingDefaults),
    };

    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(cleanedData),
      }
    );

    if (!response.ok)
      throw await partnerSaveError(
        response,
        'Failed to update business partner'
      );
    return response.json();
  },

  // Delete partner
  async deletePartner(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to delete business partner');
  },

  // Approve partner
  async submitPartnerForApproval(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to submit business partner for approval'
      );
    }
  },

  // Approve partner (workflow)
  async approvePartner(id: string, notes?: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ notes: notes || undefined }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve business partner');
    }
  },

  // Reject partner
  async rejectPartner(id: string, reason: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ rejectionReason: reason }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject business partner');
    }
  },

  // Suspend partner
  async suspendPartner(id: string, reason?: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}/suspend`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ suspensionReason: reason }),
      }
    );

    if (!response.ok) throw new Error('Failed to suspend business partner');
  },

  // Activate partner
  async activatePartner(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${id}/activate`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to activate business partner');
  },

  // Blacklist partner
  async blacklistPartner(
    id: string,
    reason: string,
    blacklistUntil?: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/partner-blacklist/partners/${id}/blacklist`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({
          reason,
          blacklistUntil: blacklistUntil || null,
        }),
      }
    );

    if (!response.ok) throw new Error('Failed to blacklist business partner');
  },

  // Remove from blacklist
  async removeFromBlacklist(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/partner-blacklist/partners/${id}/remove-from-blacklist`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to remove business partner from blacklist');
  },

  // Get partner contacts
  async getPartnerContacts(
    partnerId: string
  ): Promise<BusinessPartnerContactDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${partnerId}/contacts`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch partner contacts');
    return response.json();
  },

  async createPartnerContact(
    partnerId: string,
    data: CreateBusinessPartnerContactDto
  ): Promise<BusinessPartnerContactDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${partnerId}/contacts`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(data),
      }
    );

    if (!response.ok) throw new Error('Failed to create partner contact');
    return response.json();
  },

  async updatePartnerContact(
    partnerId: string,
    contactId: string,
    data: CreateBusinessPartnerContactDto
  ): Promise<BusinessPartnerContactDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${partnerId}/contacts/${contactId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(data),
      }
    );

    if (!response.ok) throw new Error('Failed to update partner contact');
    return response.json();
  },

  async deletePartnerContact(
    partnerId: string,
    contactId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${partnerId}/contacts/${contactId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to delete partner contact');
  },

  async setPrimaryPartnerContact(
    partnerId: string,
    contactId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${partnerId}/contacts/${contactId}/set-primary`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to set primary partner contact');
  },

  // Get partner licenses
  async addPartnerLicense(partnerId: string, data: {
    licenseTypeId: string; licenseNumber: string; issuingAuthority: string;
    issueDate: string; expiryDate?: string;
  }): Promise<BusinessPartnerLicenseDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/business-partners/${partnerId}/licenses`, {
      method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data),
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.detail || result.message || 'Unable to record the licence.');
    return result;
  },

  // Get partner licenses
  async getPartnerLicenses(
    partnerId: string
  ): Promise<BusinessPartnerLicenseDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/business-partners/${partnerId}/licenses`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch partner licenses');
    return response.json();
  },
};

// Export individual methods for easier import
export const getPartnerByUserId =
  businessPartnerService.getPartnerByUserId.bind(businessPartnerService);
export const getPartnerById = businessPartnerService.getPartnerById.bind(
  businessPartnerService
);
export const getPartners = businessPartnerService.getPartners.bind(
  businessPartnerService
);
