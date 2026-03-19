/**
 * Inventory Requisition Service
 * API service for inventory requisitions (department issue requests)
 */

import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// ============================================================================
// INTERFACES
// ============================================================================

export type RequisitionStatus = 'Draft' | 'Submitted' | 'Approved' | 'InProgress' | 'PartiallyIssued' | 'Issued' | 'Completed' | 'Cancelled' | 'Rejected';
export type RequisitionType = 'DepartmentRequisition' | 'ProjectRequisition' | 'MaintenanceRequisition' | 'ProductionRequisition' | 'EmergencyRequisition' | 'ReturnToStock';

// Map numeric enum values to string types
export const RequisitionStatusMap: Record<number, RequisitionStatus> = {
  1: 'Draft', 2: 'Submitted', 3: 'Approved', 4: 'InProgress',
  5: 'PartiallyIssued', 6: 'Issued', 7: 'Completed', 8: 'Cancelled', 9: 'Rejected'
};

export const RequisitionTypeMap: Record<number, RequisitionType> = {
  1: 'DepartmentRequisition', 2: 'ProjectRequisition', 3: 'MaintenanceRequisition',
  4: 'ProductionRequisition', 5: 'EmergencyRequisition', 6: 'ReturnToStock'
};

export interface InventoryRequisitionDto {
  id: string;
  requisitionNumber: string;
  description?: string;
  departmentId: string;
  departmentName?: string;
  costCenter?: string;
  warehouseId: string;
  warehouseName: string;
  locationId?: string;
  locationName?: string;
  projectId?: string;
  projectCode?: string;
  status: number;
  requisitionType: number;
  priority: string;
  requestDate: string;
  requestDateFormatted: string;
  requiredDate?: string;
  requiredDateFormatted?: string;
  issuedDate?: string;
  totalItems: number;
  totalQuantity: number;
  totalValue: number;
  requestedByName?: string;
  approvedByName?: string;
  notes?: string;
  purpose?: string;
  createdAtFormatted: string;
  currentWorkflowStepName?: string;
}

export interface InventoryRequisitionDetailDto extends InventoryRequisitionDto {
  approvalDate?: string;
  completedDate?: string;
  issuedByName?: string;
  rejectionReason?: string;
  cancellationReason?: string;
  locationId?: string;
  locationName?: string;
  items: InventoryRequisitionItemDto[];
}

export interface InventoryRequisitionItemDto {
  id: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  requestedQuantity: number;
  approvedQuantity: number;
  issuedQuantity: number;
  unitOfMeasure: string;
  unitCost: number;
  totalCost: number;
  lotNumber?: string;
  serialNumber?: string;
  locationId?: string;
  locationName?: string;
  notes?: string;
}

export interface CreateInventoryRequisitionDto {
  departmentId: string;
  departmentName?: string;
  costCenter?: string;
  warehouseId: string;
  locationId?: string;
  projectId?: string;
  projectCode?: string;
  requisitionType: number;
  priority: string;
  requiredDate?: string;
  purpose?: string;
  notes?: string;
  items: CreateInventoryRequisitionItemDto[];
}

export interface UpdateInventoryRequisitionDto {
  departmentId?: string;
  departmentName?: string;
  costCenter?: string;
  warehouseId?: string;
  locationId?: string;
  projectId?: string;
  projectCode?: string;
  requiredDate?: string;
  purpose?: string;
  notes?: string;
}

export interface CreateInventoryRequisitionItemDto {
  inventoryItemId: string;
  requestedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface AddRequisitionItemDto {
  inventoryItemId: string;
  requestedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface UpdateRequisitionItemDto {
  requestedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  serialNumber?: string;
  notes?: string;
}

export interface IssueRequisitionItemDto {
  itemId: string;
  issuedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  serialNumber?: string;
}

export interface IssueRequisitionDto {
  items: IssueRequisitionItemDto[];
  notes?: string;
}

export interface ReturnRequisitionItemDto {
  itemId: string;
  returnedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  serialNumber?: string;
}

export interface ReturnRequisitionDto {
  items: ReturnRequisitionItemDto[];
  notes?: string;
}

export interface DepartmentDto {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

const getAuthHeaders = () => {
  const token = typeof window !== 'undefined' 
    ? (localStorage.getItem('token') || localStorage.getItem('authToken')) 
    : null;
  return { Authorization: token ? `Bearer ${token}` : '', 'Content-Type': 'application/json' };
};

// ============================================================================
// SERVICE
// ============================================================================

export const inventoryRequisitionService = {
  // Get all requisitions
  getAll: async (fromDate?: string, toDate?: string): Promise<InventoryRequisitionDto[]> => {
    const params = new URLSearchParams();
    if (fromDate) params.append('fromDate', fromDate);
    if (toDate) params.append('toDate', toDate);
    const url = `${API_URL}/inventory/requisitions${params.toString() ? '?' + params.toString() : ''}`;
    const response = await axios.get(url, { headers: getAuthHeaders() });
    return response.data;
  },

  // Get requisition by ID
  getById: async (id: string): Promise<InventoryRequisitionDetailDto> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/${id}`, { headers: getAuthHeaders() });
    return response.data;
  },

  // Get requisitions by warehouse
  getByWarehouse: async (warehouseId: string): Promise<InventoryRequisitionDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/by-warehouse/${warehouseId}`, { headers: getAuthHeaders() });
    return response.data;
  },

  // Get requisitions by project
  getByProject: async (projectId: string): Promise<InventoryRequisitionDto[]> => {
    const response = await axios.get(`${API_URL}/projects/${projectId}/materials/requisitions`, { headers: getAuthHeaders() });
    return response.data;
  },

  // Get requisitions by department
  getByDepartment: async (departmentId: string): Promise<InventoryRequisitionDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/by-department/${departmentId}`, { headers: getAuthHeaders() });
    return response.data;
  },

  // Get pending approval requisitions
  getPendingApproval: async (): Promise<InventoryRequisitionDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/pending-approval`, { headers: getAuthHeaders() });
    return response.data;
  },

  // Get pending issue requisitions
  getPendingIssue: async (): Promise<InventoryRequisitionDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/pending-issue`, { headers: getAuthHeaders() });
    return response.data;
  },

  // Create requisition
  create: async (dto: CreateInventoryRequisitionDto): Promise<InventoryRequisitionDetailDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions`, dto, { headers: getAuthHeaders() });
    return response.data;
  },

  // Update requisition
  update: async (id: string, dto: UpdateInventoryRequisitionDto): Promise<InventoryRequisitionDetailDto> => {
    const response = await axios.put(`${API_URL}/inventory/requisitions/${id}`, dto, { headers: getAuthHeaders() });
    return response.data;
  },

  // Submit requisition
  submit: async (id: string): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/submit`, {}, { headers: getAuthHeaders() });
  },

  // Approve requisition
  approve: async (id: string, notes?: string): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/approve`, { notes }, { headers: getAuthHeaders() });
  },

  // Reject requisition
  reject: async (id: string, reason: string): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/reject`, { reason }, { headers: getAuthHeaders() });
  },

  // Issue requisition
  issue: async (id: string, dto: IssueRequisitionDto): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/issue`, dto, { headers: getAuthHeaders() });
  },

  // Return requisition items to stock
  returnItems: async (id: string, dto: ReturnRequisitionDto): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/return`, dto, { headers: getAuthHeaders() });
  },

  // Complete requisition
  complete: async (id: string): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/complete`, {}, { headers: getAuthHeaders() });
  },

  // Cancel requisition
  cancel: async (id: string, reason: string): Promise<void> => {
    await axios.post(`${API_URL}/inventory/requisitions/${id}/cancel`, { reason }, { headers: getAuthHeaders() });
  },

  // Add item to requisition
  addItem: async (requisitionId: string, dto: AddRequisitionItemDto): Promise<InventoryRequisitionItemDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/${requisitionId}/items`, dto, { headers: getAuthHeaders() });
    return response.data;
  },

  // Update item in requisition
  updateItem: async (requisitionId: string, itemId: string, dto: UpdateRequisitionItemDto): Promise<InventoryRequisitionItemDto> => {
    const response = await axios.put(`${API_URL}/inventory/requisitions/${requisitionId}/items/${itemId}`, dto, { headers: getAuthHeaders() });
    return response.data;
  },

  // Remove item from requisition
  removeItem: async (requisitionId: string, itemId: string): Promise<void> => {
    await axios.delete(`${API_URL}/inventory/requisitions/${requisitionId}/items/${itemId}`, { headers: getAuthHeaders() });
  },

  // Get departments (for dropdown)
  getDepartments: async (): Promise<DepartmentDto[]> => {
    const response = await axios.get(`${API_URL}/departments/active`, { headers: getAuthHeaders() });
    return response.data;
  }
};
