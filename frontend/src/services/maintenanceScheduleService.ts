import { apiService } from './api.service';

export interface MaintenanceSchedule {
  id: string;
  name: string;
  code: string;
  description?: string;
  assetId: string;
  assetName?: string;
  maintenanceTypeId: string;
  maintenanceType?: string;
  maintenanceTypeName?: string;
  frequency: string;
  nextDueDate: string;
  lastCompletedDate?: string;
  priority: string;
  estimatedHours: number;
  estimatedCost: number;
  assignedTechnicianId?: string;
  assignedTechnicianName?: string;
  assignedTeamId?: string;
  assignedTeam?: string;
  isActive: boolean;
  autoGenerateWorkOrders: boolean;
  advanceNotificationDays?: number;
  notificationRecipients?: string;
  // Trigger fields
  primaryTriggerType?: string;
  secondaryTriggerType?: string;
  triggerLogic?: string;
  mileageTrigger?: number;
  operatingHoursTrigger?: number;
  cycleTrigger?: number;
  conditionCriteria?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateMaintenanceScheduleDto {
  name: string;
  code: string;
  description?: string;
  assetId: string;
  maintenanceTypeId: string;
  maintenanceType: string;
  priority: string;
  frequency: string;
  frequencyValue?: number;
  frequencyUnit?: string;
  frequencyInterval?: number;
  startDate?: string;
  nextDueDate: string;
  estimatedDuration?: number;
  estimatedHours: number;
  estimatedCost: number;
  assignedTechnicianId?: string;
  assignedTeamId?: string;
  assignedTeam?: string;
  assetCategory?: string;
  instructions?: string;
  safetyNotes?: string;
  requiredSkills?: string[];
  requiredTools?: string[];
  requiredParts?: string[];
  isActive?: boolean;
  autoCreate?: boolean;
  autoGenerateWorkOrders?: boolean;
  leadTime?: number;
  advanceNotificationDays?: number;
  notificationRecipients?: string;
  maxDelayDays?: number;
  notes?: string;
  // Trigger fields
  primaryTriggerType?: string;
  secondaryTriggerType?: string;
  triggerLogic?: string;
  mileageTrigger?: number;
  operatingHoursTrigger?: number;
  cycleTrigger?: number;
  conditionCriteria?: string;
}

export interface UpdateMaintenanceScheduleDto extends CreateMaintenanceScheduleDto {
}

export interface MaintenanceScheduleHistory {
  id: string;
  scheduleId: string;
  changeType: string;
  previousValues?: string;
  newValues?: string;
  changeReason?: string;
  changedById: string;
  changedByName?: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  data?: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

class MaintenanceScheduleService {
  // Get all maintenance schedules
  async getSchedules(params?: {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    priority?: string;
    frequency?: string;
    isActive?: boolean;
  }): Promise<PagedResult<MaintenanceSchedule>> {
    try {
      const response = await apiService.get('/maintenance/schedules', params);
      return response;
    } catch (error) {
      console.error('Error fetching maintenance schedules:', error);
      throw error;
    }
  }

  // Get schedule by ID
  async getScheduleById(id: string): Promise<MaintenanceSchedule> {
    try {
      const response = await apiService.get(`/maintenance/schedules/${id}`);
      return response;
    } catch (error) {
      console.error(`Error fetching schedule ${id}:`, error);
      throw error;
    }
  }

  // Create new schedule
  async createSchedule(data: CreateMaintenanceScheduleDto): Promise<MaintenanceSchedule> {
    try {
      const response = await apiService.post('/maintenance/schedules', data);
      return response;
    } catch (error) {
      console.error('Error creating maintenance schedule:', error);
      throw error;
    }
  }

  // Update existing schedule
  async updateSchedule(id: string, data: UpdateMaintenanceScheduleDto): Promise<MaintenanceSchedule> {
    try {
      const response = await apiService.put(`/maintenance/schedules/${id}`, data);
      return response;
    } catch (error) {
      console.error(`Error updating schedule ${id}:`, error);
      throw error;
    }
  }

  // Delete schedule
  async deleteSchedule(id: string): Promise<void> {
    try {
      await apiService.delete(`/maintenance/schedules/${id}`);
    } catch (error) {
      console.error(`Error deleting schedule ${id}:`, error);
      throw error;
    }
  }

  // Generate work order from schedule
  async generateWorkOrder(id: string): Promise<any> {
    try {
      const response = await apiService.post(`/maintenance/schedules/${id}/generate-work-order`, {});
      return response;
    } catch (error) {
      console.error(`Error generating work order for schedule ${id}:`, error);
      throw error;
    }
  }

  // Send reminder for schedule
  async sendReminder(id: string, force: boolean = false): Promise<void> {
    try {
      await apiService.post(`/maintenance/schedules/${id}/send-reminder`, { force });
    } catch (error) {
      console.error(`Error sending reminder for schedule ${id}:`, error);
      throw error;
    }
  }

  // Evaluate usage triggers
  async evaluateUsageTriggers(id: string): Promise<boolean> {
    try {
      const response = await apiService.post(`/maintenance/schedules/${id}/evaluate-usage-triggers`, {});
      return response;
    } catch (error) {
      console.error(`Error evaluating usage triggers for schedule ${id}:`, error);
      throw error;
    }
  }

  // Evaluate condition triggers
  async evaluateConditionTriggers(id: string): Promise<boolean> {
    try {
      const response = await apiService.post(`/maintenance/schedules/${id}/evaluate-condition-triggers`, {});
      return response;
    } catch (error) {
      console.error(`Error evaluating condition triggers for schedule ${id}:`, error);
      throw error;
    }
  }

  // Get schedule history
  async getScheduleHistory(id: string): Promise<MaintenanceScheduleHistory[]> {
    try {
      const response = await apiService.get(`/maintenance/schedules/${id}/history`);
      return response;
    } catch (error) {
      console.error(`Error fetching history for schedule ${id}:`, error);
      throw error;
    }
  }
}

export const maintenanceScheduleService = new MaintenanceScheduleService();
export default maintenanceScheduleService;
