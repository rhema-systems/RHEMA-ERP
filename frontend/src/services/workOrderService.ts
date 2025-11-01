import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface WorkOrder {
  id: string;
  workOrderNumber: string;
  title: string;
  description: string;
  jobCardId?: string;
  jobCardNumber?: string;
  assetId: string;
  assetName?: string;
  workOrderTypeId: string;
  workOrderType?: string;
  maintenanceTypeId: string;
  maintenanceType?: string;
  priorityLevelId: string;
  priority?: string;
  status: string;
  assignedTechnicianId?: string;
  assignedTechnician?: string;
  assignedTeamId?: string;
  assignedTeam?: string;
  requestedStartDate?: string;
  requestedCompletionDate?: string;
  actualStartDate?: string;
  actualCompletionDate?: string;
  estimatedHours: number;
  actualHours: number;
  estimatedCost: number;
  actualCost: number;
  completionPercentage?: number;
  safetyRequirements?: string;
  requiresPermit: boolean;
  requiresLockout: boolean;
  requiresConfinedSpaceEntry: boolean;
  tasks?: Array<{
    id: string;
    taskName: string;
    description?: string;
    status: string;
    estimatedHours: number;
    actualHours: number;
    assignedTechnicianId?: string;
    assignedTechnician?: {
      id: string;
      fullName: string;
      employeeNumber: string;
    };
    completedAt?: string;
    isRequired: boolean;
  }>;
  parts?: Array<any>;
  labor?: Array<any>;
  comments?: Array<any>;
  createdAt: string;
  updatedAt?: string;
}

export interface WorkOrderListItem {
  id: string;
  workOrderNumber: string;
  title: string;
  description?: string;
  jobCardId?: string;
  jobCardNumber?: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  workOrderTypeName: string;
  maintenanceTypeName: string;
  status: string;
  priority: string;
  priorityName: string;
  priorityLevel: number;
  assignedTechnicianId?: string;
  assignedTechnicianName?: string;
  assignedTeamName?: string;
  requestedStartDate?: string;
  scheduledStartDate?: string;
  scheduledEndDate?: string;
  requestedCompletionDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  actualCompletionDate?: string;
  estimatedCost: number;
  actualCost: number;
  estimatedHours: number;
  actualHours: number;
  isOverdue: boolean;
  tasksCount: number;
  completedTasksCount: number;
  completionPercentage: number;
  createdAt: string;
  createdDate: string;
  updatedAt?: string;
  dueDate?: string;
  estimatedDuration?: number;
  type: string;
  workOrderSource?: string;
}

export interface CreateWorkOrderRequest {
  title: string;
  description?: string;
  assetId: string;
  workOrderTypeId: string;
  maintenanceTypeId: string;
  priorityLevelId: string;
  assignedTechnicianId?: string;
  assignedTeamId?: string;
  requestedStartDate?: string;
  requestedCompletionDate?: string;
  scheduledStartDate?: string;
  scheduledEndDate?: string;
  estimatedCost: number;
  estimatedHours: number;
  safetyRequirements?: string;
  requiresPermit: boolean;
  requiresLockout: boolean;
  requiresConfinedSpaceEntry: boolean;
  parentWorkOrderId?: string;
  maintenanceScheduleId?: string;
  customFieldValues?: Record<string, any>;
}

export interface UpdateWorkOrderRequest {
  id: string;
  title: string;
  description?: string;
  instructions?: string;
  notes?: string;
  status?: string;
  workOrderTypeId?: string;
  maintenanceTypeId?: string;
  priorityLevelId?: string;
  assignedTechnicianId?: string;
  assignedTeamId?: string;
  requestedStartDate?: string;
  requestedCompletionDate?: string;
  estimatedCost?: number;
  estimatedHours?: number;
  safetyRequirements?: string;
  requiresPermit?: boolean;
  requiresLockout?: boolean;
  requiresConfinedSpaceEntry?: boolean;
}

export interface UpdateWorkOrderStatusRequest {
  status: string;
  notes?: string;
}

export interface ApproveWorkOrderRequest {
  notes?: string;
}

export interface WorkOrderFilter {
  page?: number;
  pageSize?: number;
  searchTerm?: string;
  status?: string;
  priority?: string;
  assetId?: string;
  technicianId?: string;
  scheduledFrom?: string;
  scheduledTo?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface WorkOrderMetrics {
  totalWorkOrders: number;
  completedWorkOrders: number;
  inProgressWorkOrders: number;
  overdueWorkOrders: number;
  averageCompletionTime: number;
  totalCost: number;
  averageCompletionPercentage: number;
}

class WorkOrderService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Content-Type': 'application/json',
      'Authorization': token ? `Bearer ${token}` : ''
    };
  }

  // Get paginated work orders
  async getWorkOrders(filter: WorkOrderFilter = {}): Promise<PagedResult<WorkOrderListItem>> {
    const response = await axios.get(`${API_URL}/maintenance/work-orders`, {
      params: filter,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get work order by ID
  async getWorkOrderById(id: string): Promise<WorkOrder> {
    const response = await axios.get(`${API_URL}/maintenance/work-orders/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Create new work order
  async createWorkOrder(data: CreateWorkOrderRequest): Promise<WorkOrder> {
    const response = await axios.post(`${API_URL}/maintenance/work-orders`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Update work order
  async updateWorkOrder(id: string, data: UpdateWorkOrderRequest): Promise<WorkOrder> {
    // Clean up empty strings - convert to null/undefined for GUID fields
    const cleanedData = {
      ...data,
      workOrderTypeId: data.workOrderTypeId && data.workOrderTypeId.trim() !== '' ? data.workOrderTypeId : undefined,
      maintenanceTypeId: data.maintenanceTypeId && data.maintenanceTypeId.trim() !== '' ? data.maintenanceTypeId : undefined,
      priorityLevelId: data.priorityLevelId && data.priorityLevelId.trim() !== '' ? data.priorityLevelId : undefined,
      assignedTechnicianId: data.assignedTechnicianId && data.assignedTechnicianId.trim() !== '' ? data.assignedTechnicianId : undefined,
      assignedTeamId: data.assignedTeamId && data.assignedTeamId.trim() !== '' ? data.assignedTeamId : undefined,
    };
    
    const response = await axios.put(`${API_URL}/maintenance/work-orders/${id}`, cleanedData, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Update work order status
  async updateWorkOrderStatus(id: string, data: UpdateWorkOrderStatusRequest): Promise<WorkOrder> {
    const response = await axios.put(`${API_URL}/maintenance/work-orders/${id}/status`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Approve work order
  async approveWorkOrder(id: string, data?: ApproveWorkOrderRequest): Promise<WorkOrder> {
    const response = await axios.post(`${API_URL}/maintenance/work-orders/${id}/approve`, data || {}, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Delete work order
  async deleteWorkOrder(id: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/work-orders/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Get work order metrics
  async getWorkOrderMetrics(): Promise<WorkOrderMetrics> {
    const response = await axios.get(`${API_URL}/maintenance/work-orders/metrics`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }
}

export const workOrderService = new WorkOrderService();
export default workOrderService;