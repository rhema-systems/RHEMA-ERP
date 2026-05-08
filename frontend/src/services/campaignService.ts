// Campaign & Product Catalog API Service

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

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ── Campaign DTOs ──
export interface CampaignSummaryDto {
  id: string;
  name: string;
  campaignType: string;
  campaignStatus: string;
  startDate: string;
  endDate?: string;
  budget: number;
  actualCost: number;
  leadsGenerated: number;
  responseRate: number;
  managerName?: string;
  createdAt: string;
}

export interface CampaignDetailDto extends CampaignSummaryDto {
  managerId?: string;
  description?: string;
  targetAudience?: string;
  channel?: string;
  expectedRevenue: number;
  memberCount: number;
}

export interface CreateCampaignDto {
  name: string;
  campaignType?: string;
  description?: string;
  targetAudience?: string;
  channel?: string;
  startDate: string;
  endDate?: string;
  budget?: number;
  expectedRevenue?: number;
  managerId?: string;
}

// ── Product DTOs ──
export interface ProductSummaryDto {
  id: string;
  name: string;
  sku?: string;
  category?: string;
  unitPrice: number;
  cost: number;
  margin: number;
  isActive: boolean;
  createdAt: string;
}

export interface CreateProductDto {
  name: string;
  sku?: string;
  category?: string;
  description?: string;
  unitPrice: number;
  cost?: number;
  isActive?: boolean;
}

export const campaignService = {
  async getCampaigns(page = 1, pageSize = 20, search?: string, status?: string): Promise<PagedResult<CampaignSummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/campaigns?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch campaigns');
    return res.json();
  },

  async getCampaignById(id: string): Promise<CampaignDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/campaigns/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch campaign');
    return res.json();
  },

  async createCampaign(data: CreateCampaignDto): Promise<CampaignDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/campaigns`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create campaign');
    return res.json();
  },

  async launchCampaign(id: string): Promise<CampaignDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/campaigns/${id}/launch`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to launch campaign');
    return res.json();
  },

  async completeCampaign(id: string): Promise<CampaignDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/campaigns/${id}/complete`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to complete campaign');
    return res.json();
  },

  async cancelCampaign(id: string): Promise<CampaignDetailDto> {
    const res = await fetch(`${API_BASE_URL}/sales/campaigns/${id}/cancel`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to cancel campaign');
    return res.json();
  },

  // ── Products ──
  async getProducts(page = 1, pageSize = 20, search?: string, category?: string): Promise<PagedResult<ProductSummaryDto>> {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (category) params.append('category', category);
    const res = await fetch(`${API_BASE_URL}/sales/products?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch products');
    return res.json();
  },

  async createProduct(data: CreateProductDto): Promise<ProductSummaryDto> {
    const res = await fetch(`${API_BASE_URL}/sales/products`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create product');
    return res.json();
  },
};
