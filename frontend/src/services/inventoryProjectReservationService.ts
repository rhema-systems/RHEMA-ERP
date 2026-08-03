import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/project-reservations`;

export type InventoryProjectReservationStatus = 1 | 2 | 3 | 4 | 5 | 6;
export type InventoryProjectReservationActionType = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export type InventoryProjectReservationAction = {
  id: string; sequence: number; actionType: InventoryProjectReservationActionType;
  previousStatus?: InventoryProjectReservationStatus; newStatus: InventoryProjectReservationStatus;
  quantity: number; previousInventoryItemId?: string; newInventoryItemId?: string;
  notificationId?: string; actorUserId: string; actorName: string; occurredAtUtc: string;
  reason?: string; integrityHash: string;
};

export type InventoryProjectReservationNotification = {
  id: string; recipientId: string; recipientName: string; notificationType: string;
  title: string; message: string; status: string; isRead: boolean; scheduledFor: string;
};

export type InventoryProjectReservation = {
  id: string; inventoryRequisitionId: string; inventoryRequisitionItemId: string; requisitionNumber: string;
  projectId: string; projectCode: string; projectTitle: string; departmentId: string; departmentName: string;
  warehouseId: string; warehouseName: string; locationId: string; locationCode: string;
  inventoryItemId: string; itemCode: string; itemName: string; reservedQuantity: number;
  fulfilledQuantity: number; releasedQuantity: number; remainingQuantity: number;
  status: InventoryProjectReservationStatus; reservedAtUtc: string; expiresAtUtc: string;
  fulfilledAtUtc?: string; releasedAtUtc?: string; reservedById: string; reservedByName: string;
  substitutedFromReservationId?: string; substitutedByReservationId?: string; notes?: string;
  rowVersion: string; actions: InventoryProjectReservationAction[];
  notifications: InventoryProjectReservationNotification[];
};

export type CreateInventoryProjectReservation = {
  inventoryRequisitionItemId: string; locationId: string; quantity: number;
  expiresAtUtc: string; idempotencyKey: string; correlationId?: string; notes?: string;
};

export type ReleaseInventoryProjectReservation = {
  quantity: number; reason: string; idempotencyKey: string; correlationId?: string; rowVersion: string;
};

export type SubstituteInventoryProjectReservation = {
  replacementInventoryItemId: string; expiresAtUtc?: string; reason: string;
  idempotencyKey: string; correlationId?: string; rowVersion: string;
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
});
const correlationHeaders = () => ({ ...headers(), 'X-Correlation-ID': crypto.randomUUID() });

export const inventoryProjectReservationService = {
  async getAll(filters?: { projectId?: string; departmentId?: string; status?: number; take?: number }) {
    return (await axios.get<InventoryProjectReservation[]>(API_URL, { params: filters, headers: headers() })).data;
  },
  async getById(id: string) {
    return (await axios.get<InventoryProjectReservation>(`${API_URL}/${id}`, { headers: headers() })).data;
  },
  async reserve(request: CreateInventoryProjectReservation) {
    return (await axios.post<InventoryProjectReservation>(API_URL, request, { headers: correlationHeaders() })).data;
  },
  async release(id: string, request: ReleaseInventoryProjectReservation) {
    return (await axios.post<InventoryProjectReservation>(`${API_URL}/${id}/release`, request,
      { headers: correlationHeaders() })).data;
  },
  async substitute(id: string, request: SubstituteInventoryProjectReservation) {
    return (await axios.post<InventoryProjectReservation>(`${API_URL}/${id}/substitute`, request,
      { headers: correlationHeaders() })).data;
  },
};
