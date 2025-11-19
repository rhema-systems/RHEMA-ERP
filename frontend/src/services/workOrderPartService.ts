import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:7095/api';

const getAuthToken = () => {
  if (typeof window !== 'undefined') {
    return localStorage.getItem('authToken');
  }
  return null;
};

const getHeaders = () => ({
  'Content-Type': 'application/json',
  Authorization: `Bearer ${getAuthToken()}`,
});

export interface WorkOrderPartDto {
  id: string;
  workOrderId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  description?: string;
  quantityRequired: number;
  quantityAllocated: number;
  quantityUsed: number;
  quantityReturned: number;
  unitCost: number;
  totalCost: number;
  warehouseId?: string;
  warehouseName?: string;
  serialNumber?: string;
  lotNumber?: string;
  warehouseLocationCode?: string;
  warehouseLocationName?: string;
  status: string;
  allocationId?: string;
  allocatedAt?: string;
  pickedAt?: string;
  usedAt?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateWorkOrderPartDto {
  workOrderId: string;
  inventoryItemId: string;
  warehouseId: string;
  quantityRequired: number;
  unitCost: number;
  warehouseLocationId?: string;
  serialNumber?: string;
  lotNumber?: string;
  notes?: string;
}

export interface UpdateWorkOrderPartDto {
  quantityRequired: number;
  quantityUsed: number;
  quantityReturned: number;
  unitCost: number;
  warehouseLocationId?: string;
  serialNumber?: string;
  lotNumber?: string;
  status: string;
  notes?: string;
}

export interface InventoryItemDto {
  id: string;
  itemCode: string;
  name: string; // Backend property is 'name', not 'itemName'
  description?: string;
  unitOfMeasure: string;
  unitCost: number;
  currentStock: number;
  availableStock: number;
  standardCost: number;
  averageCost: number;
  isSerialTracked: boolean;
  isLotTracked: boolean;
  itemType: number;
  status: number;
  categoryName: string;
}

export interface WarehouseDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  warehouseType: string;
  isActive: boolean;
  isDefault: boolean;
}

export interface WarehouseInventoryDto {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  itemType: number;
  description?: string;
  unitOfMeasure?: string;
  currentStock: number;
  availableStock: number;
  allocatedStock: number;
  unitCost: number;
  categoryName?: string;
}

export interface WarehouseLocationDto {
  id: string;
  warehouseId: string;
  locationCode: string;
  name?: string;
  description?: string;
  locationType: string;
  parentLocationId?: string;
  isActive: boolean;
  isPickingLocation: boolean;
  isReceivingLocation: boolean;
}

const workOrderPartService = {
  /**
   * Get all parts/consumables for a work order
   */
  getPartsByWorkOrder: async (workOrderId: string): Promise<WorkOrderPartDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/work-orders/${workOrderId}/parts`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Add a part/consumable to a work order
   */
  addPart: async (data: CreateWorkOrderPartDto): Promise<WorkOrderPartDto> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/work-orders/parts`,
      data,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Add multiple parts/consumables to a work order in bulk
   */
  addPartsBulk: async (workOrderId: string, parts: CreateWorkOrderPartDto[]): Promise<WorkOrderPartDto[]> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/work-orders/${workOrderId}/parts/bulk`,
      parts,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Update a work order part/consumable
   */
  updatePart: async (id: string, data: UpdateWorkOrderPartDto): Promise<WorkOrderPartDto> => {
    const response = await axios.put(
      `${API_BASE_URL}/maintenance/work-orders/parts/${id}`,
      data,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Delete a work order part/consumable
   */
  deletePart: async (id: string): Promise<void> => {
    await axios.delete(
      `${API_BASE_URL}/maintenance/work-orders/parts/${id}`,
      { headers: getHeaders() }
    );
  },

  /**
   * Delete multiple work order parts/consumables in bulk
   */
  deletePartsBulk: async (ids: string[]): Promise<void> => {
    await axios.post(
      `${API_BASE_URL}/maintenance/work-orders/parts/bulk-delete`,
      ids,
      { headers: getHeaders() }
    );
  },

  /**
   * Get total parts cost for a work order
   */
  getTotalPartsCost: async (workOrderId: string): Promise<number> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/work-orders/${workOrderId}/parts/total-cost`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Get inventory items filtered by itemType=1 (StockItem/Consumables)
   */
  getInventoryItems: async (): Promise<InventoryItemDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/InventoryItems?itemType=1`,
      { headers: getHeaders() }
    );
    return response.data || [];
  },

  /**
   * Get inventory items filtered by itemType=4 (Tools)
   */
  getToolInventoryItems: async (): Promise<InventoryItemDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/InventoryItems?itemType=4`,
      { headers: getHeaders() }
    );
    return response.data || [];
  },

  /**
   * Get all active warehouses
   */
  getWarehouses: async (): Promise<WarehouseDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/InventoryItems/warehouses`,
      { headers: getHeaders() }
    );
    return response.data || [];
  },

  /**
   * Get inventory items by warehouse and item type
   */
  getInventoryByWarehouse: async (
    warehouseId: string,
    itemType: number
  ): Promise<WarehouseInventoryDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/InventoryItems/by-warehouse/${warehouseId}?itemType=${itemType}`,
      { headers: getHeaders() }
    );
    return response.data || [];
  },

  /**
   * Get warehouse locations
   */
  getWarehouseLocations: async (): Promise<WarehouseLocationDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/InventoryItems/warehouse-locations`,
      { headers: getHeaders() }
    );
    return response.data || [];
  },

  /**
   * Get work order parts for a specific work order
   */
  getWorkOrderParts: async (workOrderId: string): Promise<WorkOrderPartDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/work-orders/${workOrderId}/parts`,
      { headers: getHeaders() }
    );
    return response.data || [];
  },

  /**
   * Return unused parts back to warehouse stock
   */
  returnUnusedParts: async (partId: string): Promise<WorkOrderPartDto> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/work-orders/parts/${partId}/return`,
      {},
      { headers: getHeaders() }
    );
    return response.data;
  },
};

export default workOrderPartService;
