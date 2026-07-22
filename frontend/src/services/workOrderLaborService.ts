import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

export interface WorkOrderLaborDto {
  id: string;
  workOrderId: string;
  technicianId: string;
  startTime: string;
  endTime?: string;
  hours: number;
  hourlyRate: number;
  totalCost: number;
  notes?: string;
  laborType: string;
  technician?: {
    id: string;
    fullName: string;
    employeeNumber: string;
  };
  createdAt: string;
  updatedAt?: string;
}

export interface CreateWorkOrderLaborDto {
  workOrderId: string;
  technicianId: string;
  startTime: string;
  hourlyRate: number;
  notes?: string;
  laborType: string;
}

export interface UpdateWorkOrderLaborDto {
  startTime: string;
  endTime?: string;
  hours: number;
  hourlyRate: number;
  notes?: string;
  laborType: string;
}

export interface EndLaborDto {
  endTime: string;
  notes?: string;
}

class WorkOrderLaborService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Content-Type': 'application/json',
      'Authorization': token ? `Bearer ${token}` : ''
    };
  }

  async getLaborByWorkOrder(workOrderId: string): Promise<WorkOrderLaborDto[]> {
    const response = await axios.get(
      `${API_URL}/maintenance/work-orders/${workOrderId}/labor`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  async startLabor(data: CreateWorkOrderLaborDto): Promise<WorkOrderLaborDto> {
    const response = await axios.post(
      `${API_URL}/maintenance/work-orders/labor`,
      data,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  async endLabor(id: string, data: EndLaborDto): Promise<WorkOrderLaborDto> {
    const response = await axios.post(
      `${API_URL}/maintenance/work-orders/labor/${id}/end`,
      data,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  async updateLabor(id: string, data: UpdateWorkOrderLaborDto): Promise<WorkOrderLaborDto> {
    const response = await axios.put(
      `${API_URL}/maintenance/work-orders/labor/${id}`,
      data,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  async deleteLabor(id: string): Promise<void> {
    await axios.delete(
      `${API_URL}/maintenance/work-orders/labor/${id}`,
      { headers: this.getAuthHeaders() }
    );
  }

  async getTotalLaborCost(workOrderId: string): Promise<number> {
    const response = await axios.get(
      `${API_URL}/maintenance/work-orders/${workOrderId}/labor/total-cost`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }
}

export const workOrderLaborService = new WorkOrderLaborService();
export default workOrderLaborService;
