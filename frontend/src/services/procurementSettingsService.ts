import { throwProcurementResponseError } from '@/lib/procurement-api-error';
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Helper function to get auth headers
function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { 'Authorization': `Bearer ${token}` } : {})
  };
}

export interface ProcurementSettingsDto {
  purchasePriceDifferenceHandling?: 'RevalueInventory' | 'PurchasePriceVariance';
  autoCloseTenders: boolean;
  enforceSegregationOfDuties: boolean;
  id: string;
  tenantId: string;
  autoCreateInventoryItems: boolean;
  autoCreateSupplierItems: boolean;
  allowNonInventoryItems: boolean;
  defaultItemCategoryId?: string;
  defaultUnitOfMeasureId?: string;
  defaultValuationMethod?: string;
  purchaseRequisitionNumberFormat?: string;
  purchaseOrderNumberFormat?: string;
  purchaseOrderReceiptNumberFormat?: string;
  requireApprovalForPO: boolean;
  autoApprovalThreshold?: number;
  allowBackorders: boolean;
  requireDeliveryDate: boolean;
  enforceSupplierCatalog: boolean;
  allowMultipleSuppliersPerItem: boolean;
  validateBudgetBeforePO: boolean;
  requireContractForPO: boolean;
  notes?: string;
  createdAt: string;
  createdById?: string;
  updatedAt?: string;
}

export interface UpdateProcurementSettingsDto {
  purchasePriceDifferenceHandling?: 'RevalueInventory' | 'PurchasePriceVariance';
  autoCloseTenders?: boolean;
  enforceSegregationOfDuties?: boolean;
  autoCreateInventoryItems: boolean;
  autoCreateSupplierItems: boolean;
  allowNonInventoryItems: boolean;
  defaultItemCategoryId?: string;
  defaultUnitOfMeasureId?: string;
  defaultValuationMethod?: string;
  purchaseRequisitionNumberFormat?: string;
  purchaseOrderNumberFormat?: string;
  purchaseOrderReceiptNumberFormat?: string;
  requireApprovalForPO: boolean;
  autoApprovalThreshold?: number;
  allowBackorders: boolean;
  requireDeliveryDate: boolean;
  enforceSupplierCatalog: boolean;
  allowMultipleSuppliersPerItem: boolean;
  validateBudgetBeforePO: boolean;
  requireContractForPO: boolean;
  notes?: string;
}

export const procurementSettingsService = {
  /**
   * Get procurement settings for current tenant
   */
  async getSettings(): Promise<ProcurementSettingsDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/ProcurementSettings`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch procurement settings');
    return response.json();
  },

  /**
   * Update procurement settings
   */
  async updateSettings(dto: UpdateProcurementSettingsDto): Promise<ProcurementSettingsDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/ProcurementSettings`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto)
    });

    if (!response.ok) {
      await throwProcurementResponseError(response, 'Failed to update procurement settings');
    }
    return response.json();
  },

  /**
   * Check if auto-create inventory items is enabled
   */
  async shouldAutoCreateInventoryItems(): Promise<boolean> {
    const response = await fetch(`${API_BASE_URL}/procurement/ProcurementSettings/auto-create-inventory-items`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to check auto-create inventory items setting');
    return response.json();
  },

  /**
   * Check if auto-create supplier items is enabled
   */
  async shouldAutoCreateSupplierItems(): Promise<boolean> {
    const response = await fetch(`${API_BASE_URL}/procurement/ProcurementSettings/auto-create-supplier-items`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to check auto-create supplier items setting');
    return response.json();
  },

  /**
   * Check if non-inventory items are allowed
   */
  async allowNonInventoryItems(): Promise<boolean> {
    const response = await fetch(`${API_BASE_URL}/procurement/ProcurementSettings/allow-non-inventory-items`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to check allow non-inventory items setting');
    return response.json();
  },
};

export default procurementSettingsService;
