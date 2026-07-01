import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Helper to get auth headers
const getAuthHeaders = () => {
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') || localStorage.getItem('authToken') : null;
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
};

// Types
export interface StockAdjustmentDto {
  id: string;
  adjustmentNumber: string;
  adjustmentDate: string;
  warehouseId?: string;
  warehouseName?: string;
  reasonCode: string;
  description?: string;
  reference?: string;
  status: string;
  totalAdjustmentValue: number;
  itemCount: number;
  approvedByName?: string;
  approvedAt?: string;
  createdAt: string;
  createdByName?: string;
}

export interface StockAdjustmentItemDto {
  id: string;
  adjustmentId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  categoryName?: string;
  unitOfMeasure: string;
  locationId?: string;
  locationCode?: string;
  warehouseName?: string;
  serialNumber?: string;
  lotNumber?: string;
  systemQuantity: number;
  physicalQuantity: number;
  adjustmentQuantity: number;
  unitCost: number;
  adjustmentValue: number;
  totalValue: number;
  previousQuantity: number;
  newQuantity: number;
  reason?: string;
  notes?: string;
}

export interface StockAdjustmentDetailDto extends StockAdjustmentDto {
  items: StockAdjustmentItemDto[];
}

export interface CreateStockAdjustmentItemDto {
  inventoryItemId: string;
  locationId?: string;
  serialNumber?: string;
  lotNumber?: string;
  adjustmentQuantity: number;
  unitCost?: number;
  reason?: string;
  notes?: string;
}

export interface CreateStockAdjustmentDto {
  warehouseId: string;
  reasonCode: string;
  description?: string;
  reference?: string;
  adjustmentDate?: string;
  items: CreateStockAdjustmentItemDto[];
}

export interface UpdateStockAdjustmentDto {
  reasonCode: string;
  description?: string;
  reference?: string;
  adjustmentDate?: string;
  items?: CreateStockAdjustmentItemDto[];
}

// Receipt Reason codes (for Inventory Receipt - always positive adjustments)
export const StockAdjustmentReasonCodes = {
  PURCHASE_RECEIPT: 'Purchase Receipt',
  RETURN_FROM_CUSTOMER: 'Return from Customer',
  TRANSFER_IN: 'Transfer In',
  PRODUCTION_OUTPUT: 'Production Output',
  FOUND_INVENTORY: 'Found Inventory',
  CYCLE_COUNT: 'Cycle Count Correction',
  PHYSICAL_COUNT: 'Physical Count Correction',
  INITIAL_STOCK: 'Initial Stock Entry',
  DONATION_RECEIVED: 'Donation Received',
  SAMPLE_RECEIVED: 'Sample Received',
  WARRANTY_REPLACEMENT: 'Warranty Replacement',
  OTHER: 'Other Receipt',
};

// Status colors
export const StockAdjustmentStatusColors: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-800',
  Approved: 'bg-blue-100 text-blue-800',
  Posted: 'bg-green-100 text-green-800',
  Cancelled: 'bg-red-100 text-red-800',
};

// Service functions
export const stockAdjustmentService = {
  // Get all adjustments with optional filtering
  async getAll(params?: {
    status?: string;
    reasonCode?: string;
    startDate?: string;
    endDate?: string;
  }): Promise<StockAdjustmentDto[]> {
    const queryParams = new URLSearchParams();
    if (params?.status) queryParams.append('status', params.status);
    if (params?.reasonCode) queryParams.append('reasonCode', params.reasonCode);
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);

    const url = `${API_URL}/inventory/adjustments${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;
    const response = await axios.get<StockAdjustmentDto[]>(url, {
      headers: getAuthHeaders(),
    });
    return response.data;
  },

  // Get adjustment by ID
  async getById(id: string): Promise<StockAdjustmentDetailDto> {
    const response = await axios.get<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}`,
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Get adjustment by number
  async getByNumber(adjustmentNumber: string): Promise<StockAdjustmentDetailDto> {
    const response = await axios.get<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/by-number/${adjustmentNumber}`,
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Get pending adjustments
  async getPending(): Promise<StockAdjustmentDto[]> {
    const response = await axios.get<StockAdjustmentDto[]>(
      `${API_URL}/inventory/adjustments/pending`,
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Get reason codes
  async getReasonCodes(): Promise<Record<string, string>> {
    const response = await axios.get<Record<string, string>>(
      `${API_URL}/inventory/adjustments/reason-codes`,
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Create adjustment
  async create(dto: CreateStockAdjustmentDto): Promise<StockAdjustmentDetailDto> {
    const response = await axios.post<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments`,
      dto,
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Update adjustment
  async update(id: string, dto: UpdateStockAdjustmentDto): Promise<StockAdjustmentDetailDto> {
    const response = await axios.put<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}`,
      dto,
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Delete adjustment
  async delete(id: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/adjustments/${id}`, {
      headers: getAuthHeaders(),
    });
  },

  // Approve adjustment
  async approve(id: string): Promise<StockAdjustmentDetailDto> {
    const response = await axios.post<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}/approve`,
      {},
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Post adjustment (apply to inventory)
  async post(id: string): Promise<StockAdjustmentDetailDto> {
    const response = await axios.post<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}/post`,
      {},
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Cancel adjustment
  async cancel(id: string): Promise<StockAdjustmentDetailDto> {
    const response = await axios.post<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}/cancel`,
      {},
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  // Delete adjustment item
  async deleteItem(adjustmentId: string, itemId: string): Promise<void> {
    await axios.delete(`${API_URL}/inventory/adjustments/${adjustmentId}/items/${itemId}`, {
      headers: getAuthHeaders(),
    });
  },
};

export default stockAdjustmentService;
