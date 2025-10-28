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
}

export interface WorkOrderType {
  id: string;
  name: string;
  code: string;
  description?: string;
  color?: string;
  icon?: string;
  isActive: boolean;
}

export interface PriorityLevel {
  id: string;
  name: string;
  level: number;
  description?: string;
  color?: string;
  responseTimeHours: number;
  isActive: boolean;
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
  estimatedCost: number;
  actualCost?: number;
  completionNotes?: string;
  tasks?: WorkOrderTask[];
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
        { id: '1', name: 'Low', level: 1, description: 'Low priority maintenance', color: '#22c55e', responseTimeHours: 168, isActive: true },
        { id: '2', name: 'Medium', level: 2, description: 'Medium priority maintenance', color: '#3b82f6', responseTimeHours: 48, isActive: true },
        { id: '3', name: 'High', level: 3, description: 'High priority maintenance', color: '#f59e0b', responseTimeHours: 12, isActive: true },
        { id: '4', name: 'Critical', level: 4, description: 'Critical emergency maintenance', color: '#ef4444', responseTimeHours: 2, isActive: true }
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
}

export const maintenanceApiService = new MaintenanceApiService();
export default maintenanceApiService;