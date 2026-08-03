import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/tracking-controls`;

export type InventoryTrackingRequirements = {
  inventoryItemId: string; itemCode: string; itemName: string; categoryId: string; categoryName: string;
  requiresLot: boolean; requiresBatch: boolean; requiresSerial: boolean; requiresManufactureDate: boolean;
  requiresExpiryDate: boolean; enforcesFifoIssue: boolean; minimumShelfLifeDays: number; categoryLineage: string[];
};

export type InventoryTrackingException = {
  id: string; inventoryItemId: string; itemCode: string; itemName: string; warehouseId: string; warehouseName: string;
  locationId?: string; referenceId: string; referenceType: string; referenceNumber: string; exceptionCodes: string[];
  reason: string; lotNumber?: string; batchNumber?: string; serialNumber?: string; workflowInstanceId: string;
  workflowEvidenceDocumentId: string; approvedById: string; approvedAtUtc: string; expiresAtUtc: string;
  consumedAtUtc?: string; isAvailable: boolean;
};

export type InventoryTraceabilityEvent = {
  id: string; inventoryItemId: string; itemCode: string; itemName: string; warehouseId: string; warehouseName: string;
  direction: string; quantity: number; referenceType: string; referenceNumber: string; lotNumber?: string;
  batchNumber?: string; serialNumber?: string; manufactureDate?: string; expiryDate?: string;
  trackingExceptionId?: string; occurredAtUtc: string;
};

export type RegisterInventoryTrackingException = {
  inventoryItemId: string; warehouseId: string; locationId?: string; referenceId: string; referenceLineId?: string;
  referenceType: string; referenceNumber: string; exceptionCodes: string[]; reason: string; lotNumber?: string;
  batchNumber?: string; serialNumber?: string; workflowInstanceId: string; workflowEvidenceDocumentId: string;
  expiresAtUtc: string;
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
});

export const inventoryTrackingControlService = {
  async getRequirements(inventoryItemId: string) {
    return (await axios.get<InventoryTrackingRequirements>(`${API_URL}/requirements/${inventoryItemId}`, { headers: headers() })).data;
  },
  async getExceptions(take = 200) {
    return (await axios.get<InventoryTrackingException[]>(`${API_URL}/exceptions`, { params: { take }, headers: headers() })).data;
  },
  async registerException(request: RegisterInventoryTrackingException) {
    return (await axios.post<InventoryTrackingException>(`${API_URL}/exceptions`, request, {
      headers: { ...headers(), 'X-Correlation-ID': crypto.randomUUID() },
    })).data;
  },
  async getEvents(inventoryItemId?: string, warehouseId?: string, take = 500) {
    return (await axios.get<InventoryTraceabilityEvent[]>(`${API_URL}/events`, {
      params: { inventoryItemId: inventoryItemId || undefined, warehouseId: warehouseId || undefined, take },
      headers: headers(),
    })).data;
  },
};
