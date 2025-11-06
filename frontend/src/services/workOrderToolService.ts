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

export interface WorkOrderToolDto {
  id: string;
  workOrderId: string;
  toolId: string;
  toolCode: string;
  toolName: string;
  description?: string;
  category: string;
  currentLocation?: string;
  isRequired: boolean;
  isAllocated: boolean;
  allocationDate?: string;
  checkoutId?: string;
  isCheckedOut: boolean;
  checkoutDate?: string;
  expectedReturnDate?: string;
  actualReturnDate?: string;
  checkoutStatus?: string;
  checkedOutById?: string;
  checkedOutByName?: string;
  dailyRentalRate: number;
  requiresCertification: boolean;
  requiresTraining: boolean;
  safetyNotes?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AllocateWorkOrderToolDto {
  workOrderId: string;
  toolId: string;
  isRequired: boolean;
  notes?: string;
}

export interface CheckoutWorkOrderToolDto {
  workOrderId: string;
  toolId: string;
  expectedReturnDate?: string;
  conditionOnCheckout?: string;
  checkoutNotes?: string;
}

export interface ReturnWorkOrderToolDto {
  conditionOnReturn?: string;
  returnNotes?: string;
  damageReported: boolean;
  damageDescription?: string;
  damageCost?: number;
}

export interface WorkOrderToolSummaryDto {
  totalTools: number;
  requiredTools: number;
  allocatedTools: number;
  checkedOutTools: number;
  returnedTools: number;
  overdueTools: number;
  totalRentalCost: number;
}

const workOrderToolService = {
  /**
   * Get all tools allocated to a work order
   */
  getWorkOrderTools: async (workOrderId: string): Promise<WorkOrderToolDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Get tool summary statistics for a work order
   */
  getToolSummary: async (workOrderId: string): Promise<WorkOrderToolSummaryDto> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/summary`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Get checked out tools for a work order
   */
  getCheckedOutTools: async (workOrderId: string): Promise<WorkOrderToolDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/checked-out`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Get overdue tools for a work order
   */
  getOverdueTools: async (workOrderId: string): Promise<WorkOrderToolDto[]> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/overdue`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Get total tool rental cost for a work order
   */
  getTotalCost: async (workOrderId: string): Promise<number> => {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/cost`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Allocate a tool to a work order
   */
  allocateTool: async (data: AllocateWorkOrderToolDto): Promise<WorkOrderToolDto> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/workorders/${data.workOrderId}/tools`,
      data,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Allocate multiple tools to a work order in bulk
   */
  allocateToolsBulk: async (workOrderId: string, tools: AllocateWorkOrderToolDto[]): Promise<WorkOrderToolDto[]> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/bulk`,
      tools,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Remove tool allocation from a work order
   */
  removeToolAllocation: async (workOrderId: string, toolId: string): Promise<void> => {
    await axios.delete(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/${toolId}`,
      { headers: getHeaders() }
    );
  },

  /**
   * Checkout an allocated tool for the work order
   */
  checkoutTool: async (data: CheckoutWorkOrderToolDto): Promise<WorkOrderToolDto> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/workorders/${data.workOrderId}/tools/${data.toolId}/checkout`,
      data,
      { headers: getHeaders() }
    );
    return response.data;
  },

  /**
   * Return a checked out tool
   */
  returnTool: async (
    workOrderId: string,
    toolId: string,
    data: ReturnWorkOrderToolDto
  ): Promise<WorkOrderToolDto> => {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/workorders/${workOrderId}/tools/${toolId}/return`,
      data,
      { headers: getHeaders() }
    );
    return response.data;
  },
};

export default workOrderToolService;
