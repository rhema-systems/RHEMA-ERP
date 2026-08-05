import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/mobile-scanning`;

export enum InventoryScanOperation {
  GoodsReceipt = 1,
  RequisitionIssue = 2,
  RequisitionReturn = 3,
  TransferShipment = 4,
  TransferReceipt = 5,
  PhysicalCount = 6,
}

export type InventoryLabelProfile = {
  id: string; name: string; description?: string; symbology: 'QR' | 'CODE128'; widthMm: number; heightMm: number; dpi: number;
  includeItemCode: boolean; includeItemName: boolean; includeUnit: boolean; includeLot: boolean; includeSerial: boolean;
  includeExpiry: boolean; isDefault: boolean; isActive: boolean; rowVersion: string;
};

export type SaveInventoryLabelProfile = Omit<InventoryLabelProfile, 'id' | 'rowVersion'> & { rowVersion?: string };

export type InventoryLabelCandidate = {
  inventoryItemId: string; itemCode: string; itemName: string; identifier: string; identifierKind: string;
  unitOfMeasureId?: string; unitCode?: string;
};

export type InventoryLabelPrint = {
  id: string; labelProfileId: string; labelProfileName: string; inventoryItemId: string; itemCode: string; itemName: string;
  identifier: string; identifierKind: string; labelCount: number; lotNumber?: string; serialNumber?: string;
  expiryDate?: string; printerName?: string; printedAtUtc: string;
};

export type InventoryScanDocumentSummary = {
  documentId: string; documentReference: string; status: string; warehouseId: string; warehouseName: string; documentDate: string;
};

export type InventoryScanDocumentLine = {
  documentLineId: string; inventoryItemId: string; itemCode: string; itemName: string; expectedQuantity: number;
  processedQuantity: number; locationId?: string; locationName?: string; lotNumber?: string; batchNumber?: string;
  serialNumber?: string; manufactureDate?: string; expiryDate?: string; rowVersion?: string;
};

export type InventoryScanDocumentContext = InventoryScanDocumentSummary & {
  operation: InventoryScanOperation; lines: InventoryScanDocumentLine[];
};

export type InventoryScanInput = {
  clientLineId: string; rawIdentifier: string; documentLineId?: string; documentLineRowVersion?: string; quantity: number; locationId?: string;
  locationIdentifier?: string; lotNumber?: string; batchNumber?: string; serialNumber?: string;
  manufactureDate?: string; expiryDate?: string; inventoryTrackingExceptionId?: string; scannedAtUtc: string;
};

export type SynchronizeInventoryScanBatch = {
  deviceId: string; idempotencyKey: string; operation: InventoryScanOperation; documentId: string; warehouseId: string;
  applyTransaction: boolean; lines: InventoryScanInput[];
};

export type InventoryScanBatch = {
  id: string; deviceId: string; idempotencyKey: string; operation: InventoryScanOperation; documentId: string;
  documentReference: string; warehouseId: string; warehouseName: string; applyTransaction: boolean;
  status: number; capturedAtUtc: string; processedAtUtc?: string; failureCode?: string; failureMessage?: string;
  reconciliation?: { documentStatus: string; scannedLineCount: number; scannedBaseQuantity: number; stockMovementCount: number; netStockMovementQuantity: number; auditedEventCount: number; reconciledAtUtc: string };
  lines: Array<InventoryScanInput & { identifierKind: string; inventoryItemId: string; itemCode: string; itemName: string; baseQuantity: number; conversionToBase: number; locationName?: string }>;
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || ''}`,
  'Content-Type': 'application/json',
});

export const inventoryScanningService = {
  async getLabelProfiles() { return (await axios.get<InventoryLabelProfile[]>(`${API_URL}/label-profiles`, { headers: headers() })).data; },
  async createLabelProfile(request: SaveInventoryLabelProfile) { return (await axios.post<InventoryLabelProfile>(`${API_URL}/label-profiles`, request, { headers: headers() })).data; },
  async updateLabelProfile(id: string, request: SaveInventoryLabelProfile) { return (await axios.put<InventoryLabelProfile>(`${API_URL}/label-profiles/${id}`, request, { headers: headers() })).data; },
  async deleteLabelProfile(id: string) { await axios.delete(`${API_URL}/label-profiles/${id}`, { headers: headers() }); },
  async searchLabelCandidates(query = '') { return (await axios.get<InventoryLabelCandidate[]>(`${API_URL}/label-candidates`, { params: { query, take: 100 }, headers: headers() })).data; },
  async recordLabelPrint(request: { labelProfileId: string; inventoryItemId: string; unitOfMeasureId?: string; identifier: string; identifierKind: string; labelCount: number; lotNumber?: string; serialNumber?: string; expiryDate?: string; printerName?: string }) {
    return (await axios.post<InventoryLabelPrint>(`${API_URL}/label-prints`, request, { headers: headers() })).data;
  },
  async getRecentPrints() { return (await axios.get<InventoryLabelPrint[]>(`${API_URL}/label-prints`, { params: { take: 100 }, headers: headers() })).data; },
  async getDocuments(operation: InventoryScanOperation) { return (await axios.get<InventoryScanDocumentSummary[]>(`${API_URL}/documents`, { params: { operation, take: 100 }, headers: headers() })).data; },
  async getDocumentContext(operation: InventoryScanOperation, documentId: string) { return (await axios.get<InventoryScanDocumentContext>(`${API_URL}/documents/${documentId}`, { params: { operation }, headers: headers() })).data; },
  async synchronize(request: SynchronizeInventoryScanBatch) {
    return (await axios.post<InventoryScanBatch>(`${API_URL}/synchronize`, request, {
      headers: { ...headers(), 'Idempotency-Key': request.idempotencyKey, 'X-Correlation-ID': request.idempotencyKey },
    })).data;
  },
  async getRecentBatches() { return (await axios.get<InventoryScanBatch[]>(`${API_URL}/batches`, { params: { take: 100 }, headers: headers() })).data; },
};

export const getInventoryScanDeviceId = () => {
  const key = 'tdcInventoryScanDeviceId';
  const existing = localStorage.getItem(key);
  if (existing) return existing;
  const created = `web-${crypto.randomUUID()}`;
  localStorage.setItem(key, created);
  return created;
};
