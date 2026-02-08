/**
 * Price List Service
 * API service for price list management
 */

import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// ============================================================================
// ENUMS
// ============================================================================

export enum PriceListType {
  Sales = 0,
  Purchase = 1,
  Transfer = 2,
  Contract = 3
}

export enum PriceListStatus {
  Draft = 0,
  Active = 1,
  Expired = 2,
  Superseded = 3,
  Cancelled = 4
}

export enum PriceListApprovalStatus {
  Draft = 0,
  PendingApproval = 1,
  Approved = 2,
  Rejected = 3
}

export enum PriceRoundingRule {
  None = 0,
  RoundUp = 1,
  RoundDown = 2,
  RoundNearest = 3
}

// ============================================================================
// INTERFACES
// ============================================================================

export interface PriceListDto {
  id: string;
  priceListCode: string;
  name: string;
  description?: string;
  type: PriceListType;
  currency: string;
  effectiveDate: string;
  expirationDate?: string;
  status: PriceListStatus;
  approvalStatus: PriceListApprovalStatus;
  isDefault: boolean;
  priority: number;
  customerGroupId?: string;
  customerGroupName?: string;
  supplierGroupId?: string;
  supplierGroupName?: string;
  supersededPriceListId?: string;
  approvedById?: string;
  approvedAt?: string;
  approvalComments?: string;
  notes?: string;
  lineCount: number;
  isEffective: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreatePriceListDto {
  priceListCode: string;
  name: string;
  description?: string;
  type: PriceListType;
  currency: string;
  effectiveDate: string;
  expirationDate?: string;
  isDefault?: boolean;
  priority?: number;
  customerGroupId?: string;
  supplierGroupId?: string;
  notes?: string;
}

export interface UpdatePriceListDto {
  name: string;
  description?: string;
  currency: string;
  effectiveDate: string;
  expirationDate?: string;
  isDefault?: boolean;
  priority?: number;
  customerGroupId?: string;
  supplierGroupId?: string;
  notes?: string;
}

export interface PriceListLineDto {
  id: string;
  priceListId: string;
  inventoryItemId: string;
  itemCode?: string;
  itemName?: string;
  unitOfMeasure: string;
  basePrice: number;
  discountPercent: number;
  netPrice: number;
  minQuantity: number;
  maxQuantity?: number;
  supplierItemCode?: string;
  minimumOrderQuantity?: number;
  orderMultiple?: number;
  leadTimeDays?: number;
  previousPrice?: number;
  priceChangePercent?: number;
  notes?: string;
  isActive: boolean;
}

export interface CreatePriceListLineDto {
  inventoryItemId: string;
  unitOfMeasure?: string;
  basePrice: number;
  discountPercent?: number;
  minQuantity?: number;
  maxQuantity?: number;
  supplierItemCode?: string;
  minimumOrderQuantity?: number;
  orderMultiple?: number;
  leadTimeDays?: number;
  notes?: string;
}

export interface UpdatePriceListLineDto extends CreatePriceListLineDto {
  isActive: boolean;
}

// ============================================================================
// Helper Functions
// ============================================================================

export const getPriceListTypeLabel = (type: PriceListType): string => {
  const labels: Record<PriceListType, string> = {
    [PriceListType.Sales]: 'Sales',
    [PriceListType.Purchase]: 'Purchase',
    [PriceListType.Transfer]: 'Transfer',
    [PriceListType.Contract]: 'Contract'
  };
  return labels[type] || 'Unknown';
};

export const getPriceListStatusLabel = (status: PriceListStatus): string => {
  const labels: Record<PriceListStatus, string> = {
    [PriceListStatus.Draft]: 'Draft',
    [PriceListStatus.Active]: 'Active',
    [PriceListStatus.Expired]: 'Expired',
    [PriceListStatus.Superseded]: 'Superseded',
    [PriceListStatus.Cancelled]: 'Cancelled'
  };
  return labels[status] || 'Unknown';
};

export const getPriceListApprovalStatusLabel = (status: PriceListApprovalStatus): string => {
  const labels: Record<PriceListApprovalStatus, string> = {
    [PriceListApprovalStatus.Draft]: 'Draft',
    [PriceListApprovalStatus.PendingApproval]: 'Pending Approval',
    [PriceListApprovalStatus.Approved]: 'Approved',
    [PriceListApprovalStatus.Rejected]: 'Rejected'
  };
  return labels[status] || 'Unknown';
};

// ============================================================================
// GROUP INTERFACES
// ============================================================================

export interface CustomerGroupDto {
  id: string;
  groupCode: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface SupplierGroupDto {
  id: string;
  groupCode: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface ItemPriceListLineDto {
  id: string;
  priceListId: string;
  priceListCode: string;
  priceListName: string;
  priceListType: PriceListType;
  priceListStatus: PriceListStatus;
  currency: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  itemDescription?: string;
  unitOfMeasure: string;
  basePrice: number;
  discountPercent: number;
  netPrice: number;
  minQuantity: number;
  maxQuantity?: number;
  supplierItemCode?: string;
  lastPriceUpdate?: string;
  isActive: boolean;
}

// ============================================================================
// API SERVICE
// ============================================================================

const getAuthHeaders = () => {
  const token = typeof window !== 'undefined'
    ? (localStorage.getItem('token') || localStorage.getItem('authToken'))
    : null;
  return token ? { Authorization: `Bearer ${token}` } : {};
};

export const priceListService = {
  // Customer Groups
  async getCustomerGroups(): Promise<CustomerGroupDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/customergroups`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching customer groups:', error);
      throw error;
    }
  },

  async getActiveCustomerGroups(): Promise<CustomerGroupDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/customergroups/active`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching active customer groups:', error);
      throw error;
    }
  },

  // Supplier Groups
  async getSupplierGroups(): Promise<SupplierGroupDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/suppliergroups`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching supplier groups:', error);
      throw error;
    }
  },

  async getActiveSupplierGroups(): Promise<SupplierGroupDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/suppliergroups/active`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching active supplier groups:', error);
      throw error;
    }
  },

  // Price List Lines by Inventory Item
  async getLinesByInventoryItem(inventoryItemId: string): Promise<ItemPriceListLineDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/pricelists/by-item/${inventoryItemId}`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching price list lines by inventory item:', error);
      throw error;
    }
  },

  // Price List CRUD
  async getAllPriceLists(): Promise<PriceListDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/pricelists`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching price lists:', error);
      throw error;
    }
  },

  async getPriceListById(id: string): Promise<PriceListDto> {
    try {
      const response = await axios.get(`${API_URL}/pricing/pricelists/${id}`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching price list:', error);
      throw error;
    }
  },

  async createPriceList(dto: CreatePriceListDto): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists`, dto, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error creating price list:', error);
      throw error;
    }
  },

  async updatePriceList(id: string, dto: UpdatePriceListDto): Promise<PriceListDto> {
    try {
      const response = await axios.put(`${API_URL}/pricing/pricelists/${id}`, dto, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error updating price list:', error);
      throw error;
    }
  },

  async deletePriceList(id: string): Promise<void> {
    try {
      await axios.delete(`${API_URL}/pricing/pricelists/${id}`, { headers: getAuthHeaders() });
    } catch (error) {
      console.error('Error deleting price list:', error);
      throw error;
    }
  },

  // Filtering
  async getPriceListsByType(type: PriceListType): Promise<PriceListDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/pricelists/by-type/${type}`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching price lists by type:', error);
      throw error;
    }
  },

  async getActivePriceLists(): Promise<PriceListDto[]> {
    try {
      const url = `${API_URL}/pricing/pricelists/active`;
      console.log('Fetching active price lists from:', url);
      console.log('Auth headers:', getAuthHeaders());
      const response = await axios.get(url, { headers: getAuthHeaders() });
      console.log('Price lists response:', response.data);
      return response.data;
    } catch (error) {
      console.error('Error fetching active price lists:', error);
      throw error;
    }
  },

  // Approval Workflow
  async submitForApproval(id: string): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${id}/submit-for-approval`, {}, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error submitting price list for approval:', error);
      throw error;
    }
  },

  async approvePriceList(id: string, comments?: string): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${id}/approve`, { comments }, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error approving price list:', error);
      throw error;
    }
  },

  async rejectPriceList(id: string, reason: string): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${id}/reject`, { reason }, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error rejecting price list:', error);
      throw error;
    }
  },

  // Price List Operations
  async activatePriceList(id: string): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${id}/activate`, {}, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error activating price list:', error);
      throw error;
    }
  },

  async deactivatePriceList(id: string): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${id}/deactivate`, {}, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error deactivating price list:', error);
      throw error;
    }
  },

  async copyPriceList(id: string, newCode: string, newName: string): Promise<PriceListDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${id}/copy`, { newCode, newName }, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error copying price list:', error);
      throw error;
    }
  },

  // Price List Lines
  async getPriceListLines(priceListId: string): Promise<PriceListLineDto[]> {
    try {
      const response = await axios.get(`${API_URL}/pricing/pricelists/${priceListId}/lines`, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error fetching price list lines:', error);
      throw error;
    }
  },

  async createPriceListLine(priceListId: string, dto: CreatePriceListLineDto): Promise<PriceListLineDto> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${priceListId}/lines`, dto, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error creating price list line:', error);
      throw error;
    }
  },

  async updatePriceListLine(priceListId: string, lineId: string, dto: UpdatePriceListLineDto): Promise<PriceListLineDto> {
    try {
      const response = await axios.put(`${API_URL}/pricing/pricelists/${priceListId}/lines/${lineId}`, dto, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error updating price list line:', error);
      throw error;
    }
  },

  async deletePriceListLine(priceListId: string, lineId: string): Promise<void> {
    try {
      await axios.delete(`${API_URL}/pricing/pricelists/${priceListId}/lines/${lineId}`, { headers: getAuthHeaders() });
    } catch (error) {
      console.error('Error deleting price list line:', error);
      throw error;
    }
  },

  // Bulk Operations
  async bulkUpdatePrices(priceListId: string, percentageChange: number): Promise<number> {
    try {
      const response = await axios.post(`${API_URL}/pricing/pricelists/${priceListId}/bulk-update-prices`, { percentageChange }, { headers: getAuthHeaders() });
      return response.data;
    } catch (error) {
      console.error('Error bulk updating prices:', error);
      throw error;
    }
  },

  async exportToCsv(priceListId: string): Promise<Blob> {
    try {
      const response = await axios.get(`${API_URL}/pricing/pricelists/${priceListId}/export`, {
        headers: getAuthHeaders(),
        responseType: 'blob'
      });
      return response.data;
    } catch (error) {
      console.error('Error exporting price list:', error);
      throw error;
    }
  }
};

export default priceListService;
