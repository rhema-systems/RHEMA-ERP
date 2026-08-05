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
  requestedById: string;
  submittedById?: string;
  submittedAtUtc?: string;
  postedById?: string;
  postedAtUtc?: string;
  reversedById?: string;
  reversedAtUtc?: string;
  reversalReason?: string;
  financePostingEventId?: string;
  financeJournalEntryId?: string;
  reversalFinancePostingEventId?: string;
  reversalFinanceJournalEntryId?: string;
  rowVersion: string;
}

export interface InventoryControlEvidenceRequest { centralDocumentVersionId: string; evidenceReference: string; }
export interface InventoryControlEvidenceDto extends InventoryControlEvidenceRequest { id: string; fileUploadRecordId: string; documentReference: string; versionNumber: string; }
export interface StockAdjustmentActionDto { sequence: number; actionType: string; actorUserId: string; occurredAtUtc: string; comment?: string; }

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
  batchNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
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
  evidence: InventoryControlEvidenceDto[];
  actions: StockAdjustmentActionDto[];
}

export interface CreateStockAdjustmentItemDto {
  inventoryItemId: string;
  locationId?: string;
  serialNumber?: string;
  lotNumber?: string;
  batchNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
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
  relatedIssueVoucherId?: string;
  idempotencyKey?: string;
  correlationId?: string;
  evidence?: InventoryControlEvidenceRequest[];
}

export interface UpdateStockAdjustmentDto {
  reasonCode: string;
  description?: string;
  reference?: string;
  adjustmentDate?: string;
  items?: CreateStockAdjustmentItemDto[];
  rowVersion: string;
  evidence?: InventoryControlEvidenceRequest[];
}

// Receipt Reason codes (for Inventory Receipt - always positive adjustments)
export const StockAdjustmentReasonCodes = {
  CYCLE_COUNT: 'Cycle Count Adjustment',
  PHYSICAL_COUNT: 'Physical Count Adjustment',
  DAMAGE: 'Damaged Goods',
  LOSS: 'Lost Inventory',
  FOUND: 'Found Inventory',
  THEFT: 'Theft / Shrinkage',
  EXPIRED: 'Expired Inventory',
  QUALITY_ISSUE: 'Quality Issue',
  DONATION: 'Donation',
  WRITE_OFF: 'Write-off',
  POSITIVE_ADJUSTMENT: 'Positive Adjustment',
  NEGATIVE_ADJUSTMENT: 'Negative Adjustment',
  INITIAL_STOCK: 'Initial Stock Entry',
  OTHER: 'Other Controlled Adjustment',
};

// Status colors
export const StockAdjustmentStatusColors: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-800',
  PendingApproval: 'bg-amber-100 text-amber-800',
  Approved: 'bg-blue-100 text-blue-800',
  Rejected: 'bg-red-100 text-red-800',
  Posted: 'bg-green-100 text-green-800',
  Reversed: 'bg-purple-100 text-purple-800',
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
      { ...dto, idempotencyKey: dto.idempotencyKey || crypto.randomUUID(), correlationId: dto.correlationId || `adjustment-ui:${crypto.randomUUID()}`, evidence: dto.evidence || [] },
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

  async submit(id: string): Promise<StockAdjustmentDetailDto> {
    const current = await this.getById(id);
    const response = await axios.post<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}/submit`,
      { rowVersion: current.rowVersion, idempotencyKey: crypto.randomUUID(), comment: 'Submitted for independent stock-adjustment approval.' },
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  async decide(id: string, approved: boolean, comment: string): Promise<StockAdjustmentDetailDto> {
    const current = await this.getById(id);
    const response = await axios.post<StockAdjustmentDetailDto>(`${API_URL}/inventory/adjustments/${id}/decision`,
      { approved, comment, rowVersion: current.rowVersion, idempotencyKey: crypto.randomUUID() }, { headers: getAuthHeaders() });
    return response.data;
  },

  // Post adjustment (apply to inventory)
  async post(id: string): Promise<StockAdjustmentDetailDto> {
    const current = await this.getById(id);
    const response = await axios.post<StockAdjustmentDetailDto>(
      `${API_URL}/inventory/adjustments/${id}/post`,
      { rowVersion: current.rowVersion, idempotencyKey: crypto.randomUUID(), comment: 'Posted atomically to stock and Finance.' },
      { headers: getAuthHeaders() }
    );
    return response.data;
  },

  async reverse(id: string, reason: string): Promise<StockAdjustmentDetailDto> {
    const current = await this.getById(id);
    const response = await axios.post<StockAdjustmentDetailDto>(`${API_URL}/inventory/adjustments/${id}/reverse`,
      { rowVersion: current.rowVersion, idempotencyKey: crypto.randomUUID(), reason, comment: reason }, { headers: getAuthHeaders() });
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
