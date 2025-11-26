/**
 * Partner Configuration Service
 * API service for managing Business Partner configuration (Categories, Specializations, License Types)
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
// INTERFACES
// ============================================================================

export interface PartnerCategoryDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  partnerCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreatePartnerCategoryDto {
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
  contractorCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateContractorSpecializationDto {
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
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateLicenseTypeDto {
  name: string;
  code: string;
  description?: string;
  applicableTo: string;
  isMandatory: boolean;
  validityPeriodMonths?: number;
  isActive: boolean;
}

// ============================================================================
// PARTNER CATEGORY API METHODS
// ============================================================================

export const partnerCategoryService = {
  // Get all categories
  async getAll(): Promise<PartnerCategoryDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-categories`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch partner categories');
    return response.json();
  },

  // Get active categories
  async getActive(): Promise<PartnerCategoryDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-categories/active`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch active categories');
    return response.json();
  },

  // Get category by ID
  async getById(id: string): Promise<PartnerCategoryDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-categories/${id}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch category');
    return response.json();
  },

  // Create category
  async create(data: CreatePartnerCategoryDto): Promise<PartnerCategoryDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-categories`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to create category');
    return response.json();
  },

  // Update category
  async update(id: string, data: CreatePartnerCategoryDto): Promise<PartnerCategoryDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-categories/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to update category');
    return response.json();
  },

  // Delete category
  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/partner-categories/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to delete category');
  }
};

// ============================================================================
// CONTRACTOR SPECIALIZATION API METHODS
// ============================================================================

export const contractorSpecializationService = {
  // Get all specializations
  async getAll(): Promise<ContractorSpecializationDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/contractor-specializations`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch specializations');
    return response.json();
  },

  // Get active specializations
  async getActive(): Promise<ContractorSpecializationDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/contractor-specializations/active`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch active specializations');
    return response.json();
  },

  // Get specialization by ID
  async getById(id: string): Promise<ContractorSpecializationDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/contractor-specializations/${id}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch specialization');
    return response.json();
  },

  // Create specialization
  async create(data: CreateContractorSpecializationDto): Promise<ContractorSpecializationDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/contractor-specializations`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to create specialization');
    return response.json();
  },

  // Update specialization
  async update(id: string, data: CreateContractorSpecializationDto): Promise<ContractorSpecializationDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/contractor-specializations/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to update specialization');
    return response.json();
  },

  // Delete specialization
  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/contractor-specializations/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to delete specialization');
  }
};

// ============================================================================
// LICENSE TYPE API METHODS
// ============================================================================

export const licenseTypeService = {
  // Get all license types
  async getAll(): Promise<LicenseTypeDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch license types');
    return response.json();
  },

  // Get active license types
  async getActive(): Promise<LicenseTypeDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types/active`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch active license types');
    return response.json();
  },

  // Get mandatory license types
  async getMandatory(applicableTo: string = 'Both'): Promise<LicenseTypeDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types/mandatory?applicableTo=${applicableTo}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch mandatory license types');
    return response.json();
  },

  // Get license type by ID
  async getById(id: string): Promise<LicenseTypeDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types/${id}`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch license type');
    return response.json();
  },

  // Create license type
  async create(data: CreateLicenseTypeDto): Promise<LicenseTypeDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to create license type');
    return response.json();
  },

  // Update license type
  async update(id: string, data: CreateLicenseTypeDto): Promise<LicenseTypeDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data)
    });

    if (!response.ok) throw new Error('Failed to update license type');
    return response.json();
  },

  // Delete license type
  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/license-types/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to delete license type');
  }
};

