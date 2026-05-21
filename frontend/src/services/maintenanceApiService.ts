import { apiService } from './api.service';

export interface Asset {
  id: string;
  name: string;
  assetNumber: string;
  description?: string;
  location?: string;
  status: 'Active' | 'Inactive' | 'Maintenance' | 'OutOfService' | 'Retired' | 'Disposed';
  criticality: 'Low' | 'Medium' | 'High' | 'Critical';
  assetCategoryId: string;
  categoryName?: string;
  assetType?: string;
  manufacturer?: string;
  model?: string;
  serialNumber?: string;
  purchaseDate?: string;
  currentValue?: number;
  activeWorkOrdersCount?: number;
  lastMaintenanceDate?: string;
  nextMaintenanceDate?: string;
  warrantyEndDate?: string;
  warrantyStartDate?: string;
  isFleetAsset?: boolean;
}

export interface WorkOrderType {
  id: string;
  name: string;
  code: string;
  description?: string;
  color?: string;
  icon?: string;
  isActive: boolean;
  requiresApproval?: boolean;
  defaultPriority?: number;
}

export interface PriorityLevel {
  id: string;
  name: string;
  code: string;
  description?: string;
  level: number;
  isActive: boolean;
  color?: string;
  icon?: string;
  responseTime: number;
  escalationTime: number;
  requiresApproval?: boolean;
  notificationRules?: string;
  slaHours: number;
  autoAssign: boolean;
}

export interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  department: string;
  position: string;
  isActive: boolean;
}

export interface WorkOrderTask {
  id: string;
  taskName: string;
  description?: string;
  status: string;
  assignedTechnicianId?: string;
  assignedTechnician?: {
    id: string;
    fullName: string;
    employeeNumber?: string;
  };
  estimatedHours: number;
  actualHours: number;
  completedAt?: string;
  isRequired: boolean;
  photoPath?: string;
}

export interface WorkOrderPart {
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
  serialNumber?: string;
  lotNumber?: string;
  status: string;
  allocationId?: string;
  allocatedAt?: string;
  pickedAt?: string;
  usedAt?: string;
  notes?: string;
  isExcludedFromBilling?: boolean;
  billingExclusionReason?: string;
  billingExcludedAt?: string;
  billingExcludedBy?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface WorkOrderLabor {
  id: string;
  workOrderId: string;
  technicianId: string;
  startTime?: string;
  endTime?: string;
  hours: number;
  hourlyRate: number;
  totalCost: number;
  notes?: string;
  laborType?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface WorkOrderTool {
  id: string;
  workOrderId: string;
  toolId: string;
  toolCode?: string;
  toolName?: string;
  description?: string;
  category?: string;
  currentLocation?: string;
  isRequired: boolean;
  isAllocated: boolean;
  allocationDate?: string;
  checkoutId?: string;
  isCheckedOut?: boolean;
  checkoutDate?: string;
  expectedReturnDate?: string;
  actualReturnDate?: string;
  checkoutStatus?: string;
  checkedOutById?: string;
  checkedOutByName?: string;
  dailyRentalRate?: number;
  requiresCertification?: boolean;
  requiresTraining?: boolean;
  safetyNotes?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface WorkOrder {
  id: string;
  workOrderNumber: string;
  title: string;
  description?: string;
  assetId: string;
  assetName?: string;
  workOrderTypeId: string;
  maintenanceTypeId: string;
  priorityLevelId: string;
  assignedTechnicianId?: string;
  status: 'Open' | 'InProgress' | 'OnHold' | 'Completed' | 'Cancelled';
  priority: string;
  createdAt: string;
  requestedStartDate?: string;
  requestedCompletionDate?: string;
  actualStartDate?: string;
  actualCompletionDate?: string;
  estimatedHours: number;
  actualHours?: number;
  billingType?: 'Maintenance' | 'Repairs' | string;
  fixedAmount?: number;
  estimatedCost: number;
  actualCost?: number;
  completionNotes?: string;
  tasks?: WorkOrderTask[];
  parts?: WorkOrderPart[];
  labor?: WorkOrderLabor[];
  tools?: WorkOrderTool[];
}

export interface JobCard {
  id: string;
  jobCardNumber: string;
  title: string;
  description?: string;
  assetId: string;
  assetName?: string;
  status: string;
  requestedDate: string;
  approvalStatus: string;
}

export interface MaintenanceStaffSchedule {
  id?: string;
  technicianId: string;
  technicianName?: string;
  technicianFullName?: string;
  startDateTime: string;
  endDateTime: string;
  scheduleType: string;
  status: string;
  workOrderId?: string;
  workOrderNumber?: string;
  jobCardId?: string;
  jobCardNumber?: string;
  teamId?: string;
  teamName?: string;
  workLocation?: string;
  address?: string;
  latitude?: number;
  longitude?: number;
  requiresTravel: boolean;
  departureTime?: string;
  arrivalTime?: string;
  estimatedTravelMinutes?: number;
  actualTravelMinutes?: number;
  assignedVehicleId?: string;
  vehicleName?: string;
  transportationType?: string;
  notes?: string;
  actualStartTime?: string;
  actualEndTime?: string;
  createdAt?: string;
  updatedAt?: string;
  assignedVehicleName?: string;
}

export interface MaintenanceExpense {
  id?: string;
  workOrderId: string;
  workOrderNumber?: string;
  scheduleId?: string;
  technicianId?: string;
  technicianName?: string;
  expenseType: string;
  description: string;
  amount: number;
  expenseDate: string;
  mileageDriven?: number;
  mileageRate?: number;
  // Vehicle tracking - backend uses vehicleId/vehicleName
  vehicleId?: string;
  vehicleName?: string;
  vehicleUsed?: string; // Kept for backward compatibility, maps to vehicleId
  // Receipt and documentation - backend uses referenceNumber
  referenceNumber?: string;
  receiptNumber?: string; // Kept for backward compatibility, maps to referenceNumber
  receiptPath?: string;
  // Vendor - backend uses vendorName
  vendorName?: string;
  vendor?: string; // Kept for backward compatibility, maps to vendorName
  paymentMethod?: string;
  category?: string;
  status: string;
  approvedBy?: string;
  approvedById?: string;
  approvedDate?: string;
  approvalNotes?: string;
  // Location - backend uses location field
  location?: string;
  notes?: string; // Kept for backward compatibility, maps to location
  isApproved?: boolean;
  approvedByName?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

class MaintenanceApiService {
  // Assets
  async getAssets(page: number = 1, pageSize: number = 25, searchTerm?: string): Promise<PagedResult<Asset>> {
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      
      if (searchTerm) {
        params.append('searchTerm', searchTerm);
      }

      return await apiService.request<PagedResult<Asset>>(`/maintenance/assets?${params}`);
    } catch (error) {
      console.warn('Failed to load assets from API, using fallback data:', error);
      // Fallback mock data for testing
      const mockAssets: Asset[] = [
        {
          id: '1',
          name: 'HVAC Unit 1',
          assetNumber: 'HVAC-001',
          description: 'Main building HVAC system',
          location: 'Building A - Roof',
          status: 'Active',
          criticality: 'High',
          assetCategoryId: '1',
          categoryName: 'HVAC Systems',
          manufacturer: 'Carrier',
          model: 'WeatherExpert 50HC'
        },
        {
          id: '2', 
          name: 'Elevator Unit 1',
          assetNumber: 'ELEV-001',
          description: 'Main passenger elevator',
          location: 'Building A - Lobby',
          status: 'Active',
          criticality: 'Critical',
          assetCategoryId: '2',
          categoryName: 'Transportation',
          manufacturer: 'Otis',
          model: 'Gen2 Premier'
        },
        {
          id: '3',
          name: 'Generator Unit 1', 
          assetNumber: 'GEN-001',
          description: 'Emergency backup generator',
          location: 'Building A - Basement',
          status: 'Active',
          criticality: 'High',
          assetCategoryId: '3',
          categoryName: 'Power Systems',
          manufacturer: 'Caterpillar',
          model: 'C32 ACERT'
        },
        {
          id: '4',
          name: 'Fire Pump System',
          assetNumber: 'FP-001', 
          description: 'Fire suppression pump system',
          location: 'Building A - Mechanical Room',
          status: 'Active',
          criticality: 'Critical',
          assetCategoryId: '4',
          categoryName: 'Safety Systems',
          manufacturer: 'Aurora',
          model: 'Series 4200'
        }
      ];
      
      return {
        items: mockAssets,
        totalCount: mockAssets.length,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        hasNext: false,
        hasPrevious: false
      };
    }
  }

  async getAssetById(id: string): Promise<Asset> {
    return await apiService.request<Asset>(`/maintenance/assets/${id}`);
  }

  async getAvailableVehicles(): Promise<Asset[]> {
    try {
      // Use the proper backend endpoint that filters by AssetType="Vehicle" in the database
      return await apiService.request<Asset[]>('/maintenance/assets/available-vehicles');
    } catch (error) {
      console.error('Error fetching available vehicles:', error);
      return [];
    }
  }

  // Work Order Types
  async getWorkOrderTypes(): Promise<WorkOrderType[]> {
    return await apiService.request<WorkOrderType[]>('/maintenance/work-order-types/active');
  }

  // Priority Levels
  async getPriorityLevels(): Promise<PriorityLevel[]> {
    try {
      return await apiService.request<PriorityLevel[]>('/maintenance/priority-levels/active');
    } catch (error) {
      console.warn('Failed to load priority levels, using fallback data:', error);
      // Fallback mock data for testing
      return [
        { id: '1', name: 'Low', code: 'LOW', level: 1, description: 'Low priority maintenance', color: '#22c55e', responseTime: 168, escalationTime: 192, slaHours: 168, autoAssign: false, isActive: true },
        { id: '2', name: 'Medium', code: 'MED', level: 2, description: 'Medium priority maintenance', color: '#3b82f6', responseTime: 48, escalationTime: 72, slaHours: 48, autoAssign: false, isActive: true },
        { id: '3', name: 'High', code: 'HIGH', level: 3, description: 'High priority maintenance', color: '#f59e0b', responseTime: 12, escalationTime: 24, slaHours: 12, autoAssign: true, isActive: true },
        { id: '4', name: 'Critical', code: 'CRIT', level: 4, description: 'Critical emergency maintenance', color: '#ef4444', responseTime: 2, escalationTime: 4, slaHours: 2, autoAssign: true, isActive: true }
      ];
    }
  }

  // Maintenance Types
  async getMaintenanceTypes(): Promise<any[]> {
    try {
      return await apiService.request<any[]>('/maintenance/maintenance-types/active');
    } catch (error) {
      console.warn('Failed to load maintenance types, using fallback data:', error);
      // Fallback mock data for testing
      return [
        { id: '1', name: 'Preventive', description: 'Scheduled preventive maintenance' },
        { id: '2', name: 'Corrective', description: 'Fix broken or faulty equipment' },
        { id: '3', name: 'Emergency', description: 'Urgent repairs' },
        { id: '4', name: 'Inspection', description: 'Regular inspections' }
      ];
    }
  }

  // Technicians
  async getTechnicians(): Promise<Employee[]> {
    try {
      // Check if user is authenticated
      const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;
      if (!token) {
        console.warn('No authentication token found. User must log in to access employees.');
        return [];
      }

      // Try the specific maintenance technician endpoint first
      try {
        const response = await apiService.request<any>('/employees/maintenance-available');
        
        // The endpoint returns an array directly
        const technicians = Array.isArray(response) ? response : [];
        
        return technicians.map((emp: any) => ({
          id: emp.id,
          firstName: emp.firstName,
          lastName: emp.lastName,
          email: emp.emailAddress,
          department: emp.departmentName,
          position: emp.positionTitle,
          isActive: emp.isActive
        }));
      } catch (maintenanceError) {
        console.warn('Maintenance-available endpoint failed, trying all employees:', maintenanceError);
        
        // Fallback to all employees
        const response = await apiService.request<any>('/employees?pageSize=1000&isActive=true');
        const employees = Array.isArray(response) ? response : [];
        
        return employees
          .filter((emp: any) => emp.isActive)
          .map((emp: any) => ({
            id: emp.id,
            firstName: emp.firstName,
            lastName: emp.lastName,
            email: emp.emailAddress,
            department: emp.departmentName,
            position: emp.positionTitle,
            isActive: emp.isActive
          }));
      }
    } catch (error) {
      console.error('Error fetching technicians from all endpoints:', error);
      return [];
    }
  }

  // Work Orders
  async getWorkOrders(
    page: number = 1, 
    pageSize: number = 25, 
    searchTerm?: string, 
    status?: string, 
    priority?: string,
    assetId?: string,
    technicianId?: string
  ): Promise<PagedResult<WorkOrder>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    
    if (searchTerm) params.append('searchTerm', searchTerm);
    if (status) params.append('status', status);
    if (priority) params.append('priority', priority);
    if (assetId) params.append('assetId', assetId);
    if (technicianId) params.append('technicianId', technicianId);

    return await apiService.request<PagedResult<WorkOrder>>(`/maintenance/work-orders?${params}`);
  }

  async getWorkOrderById(id: string): Promise<WorkOrder> {
    return await apiService.request<WorkOrder>(`/maintenance/work-orders/${id}`);
  }

  async createWorkOrder(workOrder: Partial<WorkOrder>): Promise<WorkOrder> {
    return await apiService.request<WorkOrder>('/maintenance/work-orders', {
      method: 'POST',
      body: JSON.stringify(workOrder)
    });
  }

  async updateWorkOrder(id: string, workOrder: Partial<WorkOrder>): Promise<WorkOrder> {
    return await apiService.request<WorkOrder>(`/maintenance/work-orders/${id}`, {
      method: 'PUT',
      body: JSON.stringify(workOrder)
    });
  }

  async deleteWorkOrder(id: string): Promise<void> {
    await apiService.request<void>(`/maintenance/work-orders/${id}`, {
      method: 'DELETE'
    });
  }

  async updateWorkOrderStatus(id: string, status: string, notes?: string): Promise<WorkOrder> {
    return await apiService.request<WorkOrder>(`/maintenance/work-orders/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status, notes })
    });
  }

  // Job Cards
  async getJobCards(): Promise<JobCard[]> {
    return await apiService.request<JobCard[]>('/maintenance/job-cards');
  }

  async getApprovedJobCards(): Promise<JobCard[]> {
    return await apiService.request<JobCard[]>('/maintenance/job-cards?status=Approved');
  }

  async createJobCard(jobCard: Partial<JobCard>): Promise<JobCard> {
    return await apiService.request<JobCard>('/maintenance/job-cards', {
      method: 'POST',
      body: JSON.stringify(jobCard)
    });
  }

  async updateJobCardStatus(id: string, status: string, notes?: string): Promise<JobCard> {
    return await apiService.request<JobCard>(`/maintenance/job-cards/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status, notes })
    });
  }

  // Dashboard/Analytics
  async getWorkOrderMetrics(): Promise<any> {
    return await apiService.request<any>('/maintenance/work-orders/metrics');
  }

  async getAssetMetrics(): Promise<any> {
    return await apiService.request<any>('/maintenance/assets/metrics');
  }

  async getOverdueWorkOrders(): Promise<WorkOrder[]> {
    return await apiService.request<WorkOrder[]>('/maintenance/work-orders/overdue');
  }

  async getWorkOrdersDueSoon(days: number = 7): Promise<WorkOrder[]> {
    return await apiService.request<WorkOrder[]>(`/maintenance/work-orders/due-soon?days=${days}`);
  }

  // Staff Schedules
  async getStaffSchedulesByWorkOrder(workOrderId: string): Promise<MaintenanceStaffSchedule[]> {
    return await apiService.request<MaintenanceStaffSchedule[]>(
      `/maintenance/staff-schedules/by-workorder/${workOrderId}`
    );
  }

  async getStaffSchedulesByTechnician(technicianId: string): Promise<MaintenanceStaffSchedule[]> {
    return await apiService.request<MaintenanceStaffSchedule[]>(
      `/maintenance/staff-schedules/by-technician/${technicianId}`
    );
  }

  async getStaffSchedulesByDateRange(startDate: string, endDate: string): Promise<MaintenanceStaffSchedule[]> {
    return await apiService.request<MaintenanceStaffSchedule[]>(
      `/maintenance/staff-schedules/by-date-range?startDate=${startDate}&endDate=${endDate}`
    );
  }

  async getStaffScheduleById(id: string): Promise<MaintenanceStaffSchedule> {
    return await apiService.request<MaintenanceStaffSchedule>(
      `/maintenance/staff-schedules/${id}`
    );
  }

  async createStaffSchedule(schedule: Partial<MaintenanceStaffSchedule>): Promise<MaintenanceStaffSchedule> {
    return await apiService.request<MaintenanceStaffSchedule>(
      '/maintenance/staff-schedules',
      {
        method: 'POST',
        body: JSON.stringify(schedule)
      }
    );
  }

  async updateStaffSchedule(id: string, schedule: Partial<MaintenanceStaffSchedule>): Promise<MaintenanceStaffSchedule> {
    return await apiService.request<MaintenanceStaffSchedule>(
      `/maintenance/staff-schedules/${id}`,
      {
        method: 'PUT',
        body: JSON.stringify(schedule)
      }
    );
  }

  async deleteStaffSchedule(id: string): Promise<void> {
    await apiService.request<void>(
      `/maintenance/staff-schedules/${id}`,
      {
        method: 'DELETE'
      }
    );
  }

  async startStaffSchedule(id: string): Promise<MaintenanceStaffSchedule> {
    return await apiService.request<MaintenanceStaffSchedule>(
      `/maintenance/staff-schedules/${id}/start`,
      {
        method: 'POST'
      }
    );
  }

  async completeStaffSchedule(id: string): Promise<MaintenanceStaffSchedule> {
    return await apiService.request<MaintenanceStaffSchedule>(
      `/maintenance/staff-schedules/${id}/complete`,
      {
        method: 'POST'
      }
    );
  }

  // Expenses
  async getExpensesByWorkOrder(workOrderId: string): Promise<MaintenanceExpense[]> {
    return await apiService.request<MaintenanceExpense[]>(
      `/maintenance/expenses/by-workorder/${workOrderId}`
    );
  }

  async getExpensesByTechnician(technicianId: string): Promise<MaintenanceExpense[]> {
    return await apiService.request<MaintenanceExpense[]>(
      `/maintenance/expenses/by-technician/${technicianId}`
    );
  }

  async getPendingExpenses(): Promise<MaintenanceExpense[]> {
    return await apiService.request<MaintenanceExpense[]>(
      '/maintenance/expenses/pending'
    );
  }

  async getTotalExpensesByWorkOrder(workOrderId: string): Promise<number> {
    const response = await apiService.request<{ workOrderId: string; totalAmount: number }>(
      `/maintenance/expenses/total-by-workorder/${workOrderId}`
    );
    return response.totalAmount ?? 0;
  }

  async getExpenseById(id: string): Promise<MaintenanceExpense> {
    return await apiService.request<MaintenanceExpense>(
      `/maintenance/expenses/${id}`
    );
  }

  async createExpense(expense: Partial<MaintenanceExpense>): Promise<MaintenanceExpense> {
    return await apiService.request<MaintenanceExpense>(
      '/maintenance/expenses',
      {
        method: 'POST',
        body: JSON.stringify(expense)
      }
    );
  }

  async updateExpense(id: string, expense: Partial<MaintenanceExpense>): Promise<MaintenanceExpense> {
    return await apiService.request<MaintenanceExpense>(
      `/maintenance/expenses/${id}`,
      {
        method: 'PUT',
        body: JSON.stringify(expense)
      }
    );
  }

  async deleteExpense(id: string): Promise<void> {
    await apiService.request<void>(
      `/maintenance/expenses/${id}`,
      {
        method: 'DELETE'
      }
    );
  }

  async approveExpense(id: string, approvalNotes?: string): Promise<MaintenanceExpense> {
    return await apiService.request<MaintenanceExpense>(
      `/maintenance/expenses/${id}/approve`,
      {
        method: 'POST',
        body: JSON.stringify({ approvalNotes })
      }
    );
  }

  async uploadExpenseReceipt(file: File): Promise<{ filePath: string; fileName: string }> {
    const formData = new FormData();
    formData.append('file', file);

    const token = localStorage.getItem('authToken');
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
    const response = await fetch(`${baseUrl}/maintenance/expenses/upload-receipt`, {
      method: 'POST',
      headers: {
        'Authorization': token ? `Bearer ${token}` : '',
      },
      body: formData
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Upload failed' }));
      throw new Error(error.message || 'Failed to upload receipt');
    }

    return await response.json();
  }

  async uploadTaskPhoto(taskId: string, file: File): Promise<{ filePath: string; fileName: string; task: WorkOrderTask }> {
    const formData = new FormData();
    formData.append('file', file);

    const token = localStorage.getItem('authToken');
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
    const response = await fetch(`${baseUrl}/maintenance/work-orders/tasks/${taskId}/photo`, {
      method: 'POST',
      headers: {
        'Authorization': token ? `Bearer ${token}` : '',
      },
      body: formData
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Upload failed' }));
      throw new Error(error.message || 'Failed to upload task photo');
    }

    return await response.json();
  }

  async deleteTaskPhoto(taskId: string): Promise<void> {
    await apiService.request<void>(`/maintenance/work-orders/tasks/${taskId}/photo`, {
      method: 'DELETE'
    });
  }
}

export const maintenanceApiService = new MaintenanceApiService();
export default maintenanceApiService;
