import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/directed-operations`;

export type DirectedTaskType = 'PutAway' | 'Picking' | 'Replenishment';
export type DirectedTaskStatus = 'Assigned' | 'InProgress' | 'AwaitingStockMove' | 'Completed' | 'Cancelled';
export type DirectedAssignee = { userId: string; username: string; displayName: string };

export type LocationCapacity = {
  locationId: string; locationCode: string; locationName: string; isActive: boolean;
  isPickingLocation: boolean; isReceivingLocation: boolean; isQuarantineLocation: boolean;
  usedWeight: number; maxWeight?: number; usedVolume: number; maxVolume?: number;
  usedItemSlots: number; maxItemSlots?: number; incomingWeight: number; incomingVolume: number;
  incomingItemSlots: number; hasCapacity: boolean; capacityIssues: string[];
};

export type DirectedSuggestion = {
  suggestionKey: string; taskType: DirectedTaskType; warehouseId: string; warehouseCode: string;
  inventoryItemId: string; itemCode: string; itemName: string; sourceLocationId?: string;
  sourceLocationCode?: string; destinationLocationId?: string; destinationLocationCode?: string;
  quantity: number; sourceDocumentType: string; sourceDocumentId: string; sourceLineId: string;
  sourceReference: string; isQuarantine: boolean; explanation: string; destinationCapacity?: LocationCapacity;
};

export type DirectedTaskAction = {
  id: string; sequence: number; actionType: string; statusAfter: DirectedTaskStatus; actorUserId: string;
  actorName: string; occurredAtUtc: string; comment: string; correlationId: string; integrityHash: string;
};

export type DirectedTask = {
  id: string; taskNumber: string; taskType: DirectedTaskType; status: DirectedTaskStatus;
  warehouseId: string; warehouseCode: string; inventoryItemId: string; itemCode: string; itemName: string;
  sourceLocationId?: string; sourceLocationCode?: string; destinationLocationId?: string;
  destinationLocationCode?: string; quantity: number; sourceDocumentType: string; sourceDocumentId: string;
  sourceLineId: string; sourceReference: string; isQuarantine: boolean; assignedToUserId: string;
  assignedToName: string; linkedInventoryTransferId?: string; assignedAtUtc: string; dueAtUtc?: string;
  startedAtUtc?: string; completedAtUtc?: string; reason: string; notes?: string; rowVersion: string;
  actions: DirectedTaskAction[];
};

export type ConfirmDirectedTask = {
  rowVersion: string; comment: string; lotNumber?: string; batchNumber?: string; serialNumber?: string;
  manufactureDate?: string; expiryDate?: string; inventoryTrackingExceptionId?: string;
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
  'X-Correlation-ID': crypto.randomUUID(),
});

export const inventoryDirectedOperationService = {
  async assignees(warehouseId?: string, taskType?: DirectedTaskType) {
    return (await axios.get<DirectedAssignee[]>(`${API_URL}/assignees`, {
      params: { warehouseId: warehouseId || undefined, taskType }, headers: headers(),
    })).data;
  },
  async suggestions(warehouseId: string, taskType?: DirectedTaskType, take = 250) {
    return (await axios.get<DirectedSuggestion[]>(`${API_URL}/suggestions`, {
      params: { warehouseId, taskType, take }, headers: headers(),
    })).data;
  },
  async tasks(warehouseId?: string, status?: DirectedTaskStatus, take = 250) {
    return (await axios.get<DirectedTask[]>(`${API_URL}/tasks`, {
      params: { warehouseId: warehouseId || undefined, status, take }, headers: headers(),
    })).data;
  },
  async create(suggestion: DirectedSuggestion, assignedToUserId?: string) {
    return (await axios.post<DirectedTask>(`${API_URL}/tasks`, {
      warehouseId: suggestion.warehouseId,
      suggestionKey: suggestion.suggestionKey,
      assignedToUserId: assignedToUserId || undefined,
      reason: suggestion.explanation,
      idempotencyKey: crypto.randomUUID(),
    }, { headers: headers() })).data;
  },
  async start(task: DirectedTask, comment: string) {
    return (await axios.post<DirectedTask>(`${API_URL}/tasks/${task.id}/start`,
      { rowVersion: task.rowVersion, comment }, { headers: headers() })).data;
  },
  async confirm(task: DirectedTask, request: Omit<ConfirmDirectedTask, 'rowVersion'>) {
    return (await axios.post<DirectedTask>(`${API_URL}/tasks/${task.id}/confirm`,
      { ...request, rowVersion: task.rowVersion }, { headers: headers() })).data;
  },
  async reconcile(task: DirectedTask) {
    return (await axios.post<DirectedTask>(`${API_URL}/tasks/${task.id}/reconcile`,
      { rowVersion: task.rowVersion }, { headers: headers() })).data;
  },
  async cancel(task: DirectedTask, reason: string) {
    return (await axios.post<DirectedTask>(`${API_URL}/tasks/${task.id}/cancel`,
      { rowVersion: task.rowVersion, reason }, { headers: headers() })).data;
  },
};
