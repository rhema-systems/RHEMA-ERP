import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/replenishment`;

export type InventoryReplenishmentStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7;
export type InventoryReplenishmentActionType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;

export type InventoryReplenishmentAction = {
  sequence: number; actionType: InventoryReplenishmentActionType;
  previousStatus?: InventoryReplenishmentStatus; newStatus: InventoryReplenishmentStatus;
  actorUserId: string; actorName: string; occurredAtUtc: string; reason?: string; integrityHash: string;
};

export type InventoryReplenishmentRecommendation = {
  id: string; recommendationNumber: string; warehouseId: string; warehouseCode: string; warehouseName: string;
  inventoryItemId: string; itemCode: string; itemName: string; unitOfMeasure: string;
  preferredSupplierId?: string; preferredSupplierName?: string; status: InventoryReplenishmentStatus;
  demandWindowDays: number; demandFromUtc: string; demandToUtc: string; demandQuantity: number;
  averageDailyDemand: number; leadTimeDays: number; safetyLeadTimeDays: number;
  currentStock: number; availableStock: number; allocatedStock: number; onOrderQuantity: number;
  openRecommendationQuantity: number; minimumLevel: number; maximumLevel: number; reorderLevel: number;
  reorderQuantity: number; safetyStock: number; minimumOrderQuantity: number; orderMultiple: number;
  leadTimeDemand: number; projectedAvailableAtReceipt: number; recommendedQuantity: number;
  estimatedUnitCost: number; requiredDateUtc: string; validUntilUtc: string; explanation: string;
  calculationHash: string; generatedById: string; generatedByName: string; generatedAtUtc: string;
  workflowInstanceId?: string; purchaseRequisitionId?: string; purchaseRequisitionNumber?: string;
  rowVersion: string; actions: InventoryReplenishmentAction[];
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
});
const mutationHeaders = () => ({ ...headers(), 'X-Correlation-ID': crypto.randomUUID() });

export const inventoryReplenishmentService = {
  async getAll(filters?: { warehouseId?: string; status?: number; take?: number }) {
    return (await axios.get<InventoryReplenishmentRecommendation[]>(API_URL,
      { params: filters, headers: headers() })).data;
  },
  async getById(id: string) {
    return (await axios.get<InventoryReplenishmentRecommendation>(`${API_URL}/${id}`,
      { headers: headers() })).data;
  },
  async generate(warehouseId: string, demandWindowDays: number) {
    return (await axios.post<InventoryReplenishmentRecommendation[]>(`${API_URL}/generate`,
      { warehouseId, demandWindowDays, idempotencyKey: `generate:${crypto.randomUUID()}` },
      { headers: mutationHeaders() })).data;
  },
  async submit(value: InventoryReplenishmentRecommendation, reason: string) {
    return (await axios.post<InventoryReplenishmentRecommendation>(`${API_URL}/${value.id}/submit`,
      { reason, rowVersion: value.rowVersion, idempotencyKey: `submit:${crypto.randomUUID()}` },
      { headers: mutationHeaders() })).data;
  },
  async decide(value: InventoryReplenishmentRecommendation, approved: boolean, comment: string) {
    return (await axios.post<InventoryReplenishmentRecommendation>(`${API_URL}/${value.id}/decision`,
      { approved, comment, rowVersion: value.rowVersion, idempotencyKey: `decision:${crypto.randomUUID()}` },
      { headers: mutationHeaders() })).data;
  },
  async createPurchaseRequisition(value: InventoryReplenishmentRecommendation, request: {
    department: string; costCenter?: string; justification: string;
  }) {
    return (await axios.post<InventoryReplenishmentRecommendation>(
      `${API_URL}/${value.id}/purchase-requisition`,
      { ...request, rowVersion: value.rowVersion, idempotencyKey: `pr:${crypto.randomUUID()}` },
      { headers: mutationHeaders() })).data;
  },
};
