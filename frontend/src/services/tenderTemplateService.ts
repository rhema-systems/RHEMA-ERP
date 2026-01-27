const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface TenderTemplateDto {
  id: string;
  templateName: string;
  description?: string;
  tenderType: string;
  category: string;
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;
  evaluationCriteriaJson?: string;
  defaultValidityDays?: number;
  requiredDocuments?: string;
  termsAndConditions?: string;
  requiresPrequalification: boolean;
  allowPartialBids: boolean;
  isActive: boolean;
  usageCount: number;
  createdAt: string;
  createdByName?: string;
}

export interface CreateTenderTemplateDto {
  templateName: string;
  description?: string;
  tenderType: string;
  category: string;
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;
  evaluationCriteriaJson?: string;
  defaultValidityDays?: number;
  requiredDocuments?: string;
  termsAndConditions?: string;
  requiresPrequalification: boolean;
  allowPartialBids: boolean;
  isActive: boolean;
}

export interface UpdateTenderTemplateDto {
  templateName: string;
  description?: string;
  tenderType: string;
  category: string;
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;
  evaluationCriteriaJson?: string;
  defaultValidityDays?: number;
  requiredDocuments?: string;
  termsAndConditions?: string;
  requiresPrequalification: boolean;
  allowPartialBids: boolean;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

export const tenderTemplateService = {
  async getAll(): Promise<TenderTemplateDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderTemplates`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch tender templates');
    }
    return response.json();
  },

  async getActive(): Promise<TenderTemplateDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderTemplates/active`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch active tender templates');
    }
    return response.json();
  },

  async getById(id: string): Promise<TenderTemplateDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderTemplates/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch tender template');
    }
    return response.json();
  },

  async create(data: CreateTenderTemplateDto): Promise<TenderTemplateDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderTemplates`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to create tender template');
    }
    return response.json();
  },

  async update(id: string, data: UpdateTenderTemplateDto): Promise<TenderTemplateDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderTemplates/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to update tender template');
    }
    return response.json();
  },

  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderTemplates/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to delete tender template');
    }
  },
};

