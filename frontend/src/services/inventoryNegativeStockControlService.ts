import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/negative-stock-controls`;

export type InventoryNegativeStockPolicy = {
  configurationProfileId: string; configurationProfileVersion: number; defaultPolicy: string | number;
  emergencyOverrideEligible: boolean; overridePermission: string; workflowDefinitionId?: string;
  evidenceRequirements: string[]; overrideDurationHours: number; auditRequired: boolean;
};

export type InventoryNegativeStockOverride = {
  id: string; inventoryItemId: string; itemCode: string; itemName: string; warehouseId: string; warehouseName: string;
  locationId?: string; referenceId: string; referenceLineId?: string; referenceType: string; referenceNumber: string;
  authorizedQuantity: number; reason: string; configurationProfileVersion: number; workflowInstanceId: string;
  centralDocumentVersionId: string; evidenceReference: string; requestedById: string; approvedById: string;
  approvedAtUtc: string; expiresAtUtc: string; consumedAtUtc?: string; consumedByReferenceId?: string;
  isAvailable: boolean; rowVersion: string;
};

export type RegisterInventoryNegativeStockOverride = {
  inventoryItemId: string; warehouseId: string; locationId?: string; referenceId: string; referenceLineId?: string;
  referenceType: string; referenceNumber: string; authorizedQuantity: number; reason: string;
  workflowInstanceId: string; centralDocumentVersionId: string; evidenceReference: string; expiresAtUtc: string;
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
});

export const inventoryNegativeStockControlService = {
  async getPolicy() {
    return (await axios.get<InventoryNegativeStockPolicy>(`${API_URL}/policy`, { headers: headers() })).data;
  },
  async getOverrides(take = 200) {
    return (await axios.get<InventoryNegativeStockOverride[]>(`${API_URL}/overrides`, { params: { take }, headers: headers() })).data;
  },
  async registerOverride(request: RegisterInventoryNegativeStockOverride) {
    return (await axios.post<InventoryNegativeStockOverride>(`${API_URL}/overrides`, request, {
      headers: { ...headers(), 'X-Correlation-ID': crypto.randomUUID() },
    })).data;
  },
};
