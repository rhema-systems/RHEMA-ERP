/**
 * Inventory Requisition Service
 * API service for inventory requisitions (department issue requests)
 */

import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

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
  requestedById?: string;
  approvedById?: string;
  approvedByName?: string;
  notes?: string;
  purpose?: string;
  createdAtFormatted: string;
  currentWorkflowStepName?: string;
  rowVersion: string;
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
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
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
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
  notes?: string;
}

export interface AddRequisitionItemDto {
  inventoryItemId: string;
  requestedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
  notes?: string;
}

export interface UpdateRequisitionItemDto {
  requestedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
  notes?: string;
}

export interface IssueRequisitionItemDto {
  itemId: string;
  issuedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
}

export interface IssueRequisitionDto {
  idempotencyKey: string;
  rowVersion: string;
  receiverUserId: string;
  movementReasonCode: string;
  items: IssueRequisitionItemDto[];
  notes?: string;
}

export interface InventoryIssueReceiverDto {
  userId: string;
  username: string;
  displayName: string;
}

export interface InventoryIssueVoucherLineDto {
  id: string;
  inventoryRequisitionItemId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  warehouseId: string;
  locationId?: string;
  locationCode?: string;
  quantity: number;
  unitCost: number;
  totalValue: number;
  unitOfMeasure?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
}

export interface InventoryIssueVoucherActionDto {
  sequence: number;
  actionType: number;
  statusAfter: number;
  actorUserId: string;
  actorName: string;
  occurredAtUtc: string;
  comment: string;
}

export interface InventoryIssueVoucherDto {
  id: string;
  voucherNumber: string;
  inventoryRequisitionId: string;
  requisitionNumber: string;
  status: number;
  warehouseId: string;
  warehouseName: string;
  locationId?: string;
  locationCode?: string;
  departmentId: string;
  departmentName?: string;
  costCenter?: string;
  projectId?: string;
  projectCode?: string;
  requestedById: string;
  requestedByName: string;
  approvedById: string;
  approvedByName: string;
  issuedById: string;
  issuedByName: string;
  receiverUserId: string;
  receiverName: string;
  movementReasonCode: string;
  financePostingEventId?: string;
  financeJournalEntryId?: string;
  acknowledgedById?: string;
  issuedAtUtc: string;
  acknowledgedAtUtc?: string;
  notes?: string;
  receiverComment?: string;
  rowVersion: string;
  lines: InventoryIssueVoucherLineDto[];
  actions: InventoryIssueVoucherActionDto[];
}

export interface InventoryIssueAccountingRuleDto {
  id: string;
  inventoryCategoryId: string;
  inventoryCategoryCode: string;
  inventoryCategoryName: string;
  itemType: number;
  movementReasonCode: string;
  movementReasonName: string;
  treatment: number;
  expenseAccountId?: string;
  expenseAccount?: string;
  fixedAssetCategoryId?: string;
  fixedAssetCategory?: string;
  isActive: boolean;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  rowVersion: string;
}

export interface InventoryIssueAccountingRuleRequest {
  inventoryCategoryId: string;
  itemType: number;
  movementReasonCode: string;
  treatment: number;
  expenseAccountId?: string;
  fixedAssetCategoryId?: string;
  isActive: boolean;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  rowVersion?: string;
}

type IssueAccountingRuleResponse = Omit<InventoryIssueAccountingRuleDto, 'itemType' | 'treatment'> & {
  itemType: number | string;
  treatment: number | string;
};

function normalizeIssueAccountingRule(rule: IssueAccountingRuleResponse): InventoryIssueAccountingRuleDto {
  const enumNumber = (value: number | string, names: Record<string, number>): number => {
    const result = typeof value === 'number' ? value : names[value] ?? Number(value);
    if (!Object.values(names).includes(result)) throw new Error(`Unsupported issue-accounting value: ${value}`);
    return result;
  };
  return {
    ...rule,
    itemType: enumNumber(rule.itemType, { StockItem: 1, Service: 2, NonStockItem: 3, FixedAsset: 4 }),
    treatment: enumNumber(rule.treatment, { Expense: 1, FixedAsset: 2 }),
  };
}

export interface InventoryIssueAccountingOptionDto {
  id: string;
  code: string;
  name: string;
  type?: string;
}

export interface InventoryIssueAccountingOptionsDto {
  inventoryCategories: InventoryIssueAccountingOptionDto[];
  expenseAccounts: InventoryIssueAccountingOptionDto[];
  fixedAssetCategories: InventoryIssueAccountingOptionDto[];
  movementReasons: Record<string, string>;
  applicableMovementReasonCodes: string[];
  applicableMovementReasonCodesByRequisitionItem: Record<string, string[]>;
}

export interface InventoryIssueFinanceLineageDto {
  id: string;
  inventoryIssueVoucherLineId: string;
  treatment: number;
  movementReasonCode: string;
  issuedQuantity: number;
  returnedQuantity: number;
  issuedValue: number;
  postingEventId: string;
  journalEntryId: string;
  fixedAssetId?: string;
  status: number;
}

export interface ReturnRequisitionItemDto {
  itemId: string;
  returnedQuantity: number;
  locationId?: string;
  lotNumber?: string;
  batchNumber?: string;
  serialNumber?: string;
  manufactureDate?: string;
  expiryDate?: string;
  inventoryTrackingExceptionId?: string;
}

export interface ReturnRequisitionDto {
  items: ReturnRequisitionItemDto[];
  reasonCode: string;
  reason: string;
  notes?: string;
  idempotencyKey: string;
  correlationId?: string;
  rowVersion: string;
  evidence: InventoryControlEvidenceRequest[];
}

export interface InventoryControlEvidenceRequest { centralDocumentVersionId: string; evidenceReference: string; }
export interface InventoryControlEvidenceDto extends InventoryControlEvidenceRequest { id: string; fileUploadRecordId: string; documentReference: string; versionNumber: string; }
export interface InventoryReturnVoucherLineDto { id: string; requisitionItemId: string; inventoryItemId: string; itemCode: string; itemName: string; locationId?: string; quantity: number; unitCost: number; totalValue: number; lotNumber?: string; batchNumber?: string; serialNumber?: string; expiryDate?: string; }
export interface InventoryReturnVoucherActionDto { sequence: number; actionType: string; actorUserId: string; actorName: string; occurredAtUtc: string; comment?: string; }
export interface InventoryReturnVoucherDto {
  id: string; voucherNumber: string; inventoryRequisitionId: string; requisitionNumber: string;
  warehouseId: string; warehouseName: string; status: string; reasonCode: string; reason: string; notes?: string;
  requestedById: string; requestedByName: string; approvedById?: string; approvedAtUtc?: string;
  postedById?: string; postedAtUtc?: string; reversedById?: string; reversedAtUtc?: string; reversalReason?: string;
  totalValue: number; rowVersion: string; lines: InventoryReturnVoucherLineDto[];
  evidence: InventoryControlEvidenceDto[]; actions: InventoryReturnVoucherActionDto[];
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
  issue: async (id: string, dto: IssueRequisitionDto): Promise<InventoryIssueVoucherDto | undefined> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/${id}/issue`, dto, { headers: getAuthHeaders() });
    return response.data?.voucher;
  },

  getIssueReceivers: async (): Promise<InventoryIssueReceiverDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/issue-receivers`, { headers: getAuthHeaders() });
    return response.data;
  },

  getIssueVouchers: async (requisitionId: string): Promise<InventoryIssueVoucherDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/${requisitionId}/issue-vouchers`, { headers: getAuthHeaders() });
    return response.data;
  },

  getIssueAccountingOptions: async (requisitionId?: string): Promise<InventoryIssueAccountingOptionsDto> => {
    const response = await axios.get(`${API_URL}/inventory/issue-accounting/options`, {
      headers: getAuthHeaders(),
      params: requisitionId ? { requisitionId } : undefined,
    });
    return response.data;
  },

  getIssueAccountingRules: async (): Promise<InventoryIssueAccountingRuleDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/issue-accounting/rules`, { headers: getAuthHeaders() });
    return (response.data as IssueAccountingRuleResponse[]).map(normalizeIssueAccountingRule);
  },

  createIssueAccountingRule: async (dto: InventoryIssueAccountingRuleRequest): Promise<InventoryIssueAccountingRuleDto> => {
    const response = await axios.post(`${API_URL}/inventory/issue-accounting/rules`, dto, { headers: getAuthHeaders() });
    return normalizeIssueAccountingRule(response.data);
  },

  updateIssueAccountingRule: async (id: string, dto: InventoryIssueAccountingRuleRequest): Promise<InventoryIssueAccountingRuleDto> => {
    const response = await axios.put(`${API_URL}/inventory/issue-accounting/rules/${id}`, dto, { headers: getAuthHeaders() });
    return normalizeIssueAccountingRule(response.data);
  },

  deleteIssueAccountingRule: async (id: string, rowVersion: string): Promise<void> => {
    await axios.delete(`${API_URL}/inventory/issue-accounting/rules/${id}`, {
      headers: getAuthHeaders(),
      params: { rowVersion },
    });
  },

  getIssueFinanceLineage: async (issueVoucherId: string): Promise<InventoryIssueFinanceLineageDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/issue-accounting/issue-vouchers/${issueVoucherId}/lineage`, {
      headers: getAuthHeaders(),
    });
    return response.data;
  },

  acknowledgeIssueVoucher: async (voucherId: string, rowVersion: string, comment: string): Promise<InventoryIssueVoucherDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/issue-vouchers/${voucherId}/acknowledge`, {
      rowVersion,
      comment,
      idempotencyKey: crypto.randomUUID(),
    }, { headers: getAuthHeaders() });
    return response.data;
  },

  downloadIssueVoucher: async (voucherId: string): Promise<Blob> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/issue-vouchers/${voucherId}/download`, {
      headers: getAuthHeaders(),
      responseType: 'blob',
    });
    return response.data;
  },

  // Return requisition items to stock
  returnItems: async (id: string, dto: ReturnRequisitionDto): Promise<InventoryReturnVoucherDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/${id}/return`, dto, { headers: getAuthHeaders() });
    return response.data;
  },

  getReturnReasons: async (): Promise<Record<string, string>> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/return-reasons`, { headers: getAuthHeaders() });
    return response.data;
  },

  getReturnVouchers: async (requisitionId?: string): Promise<InventoryReturnVoucherDto[]> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/return-vouchers`, { headers: getAuthHeaders(), params: requisitionId ? { requisitionId } : undefined });
    return response.data;
  },

  downloadReturnVoucher: async (voucherId: string): Promise<Blob> => {
    const response = await axios.get(`${API_URL}/inventory/requisitions/return-vouchers/${voucherId}/download`, {
      headers: getAuthHeaders(), responseType: 'blob',
    });
    return response.data;
  },

  decideReturnVoucher: async (voucher: InventoryReturnVoucherDto, approved: boolean, comment: string): Promise<InventoryReturnVoucherDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/return-vouchers/${voucher.id}/decision`, { approved, comment, rowVersion: voucher.rowVersion, idempotencyKey: crypto.randomUUID() }, { headers: getAuthHeaders() });
    return response.data;
  },

  postReturnVoucher: async (voucher: InventoryReturnVoucherDto): Promise<InventoryReturnVoucherDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/return-vouchers/${voucher.id}/post`, { rowVersion: voucher.rowVersion, idempotencyKey: crypto.randomUUID() }, { headers: getAuthHeaders() });
    return response.data;
  },

  reverseReturnVoucher: async (voucher: InventoryReturnVoucherDto, reason: string): Promise<InventoryReturnVoucherDto> => {
    const response = await axios.post(`${API_URL}/inventory/requisitions/return-vouchers/${voucher.id}/reverse`, { rowVersion: voucher.rowVersion, reason, idempotencyKey: crypto.randomUUID() }, { headers: getAuthHeaders() });
    return response.data;
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
