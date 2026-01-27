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

export interface MaintenanceToolDto {
  id: string;
  toolCode: string;
  name: string;
  description: string;
  category: string;
  status: string;
  currentLocation: string | null;
  homeLocation: string | null;
  requiresCertification: boolean;
  requiresTraining: boolean;
  dailyRentalRate: number;
  lastUsedDate: string | null;
  totalUsageDays: number;
}

export interface CheckoutToolDto {
  workOrderId?: string;
  jobCardId?: string;
  expectedReturnDate?: string;
  checkoutNotes?: string;
  conditionOnCheckout: string;
}

export interface ReturnToolDto {
  conditionOnReturn: string;
  returnNotes?: string;
  damageReported: boolean;
  damageDescription?: string;
  damageCost?: number;
}

export interface ToolDamageDto {
  damageDescription: string;
  estimatedCost?: number;
  requiresRepair: boolean;
}

export interface ToolCheckoutResult {
  checkoutId: string;
  toolId: string;
  toolName: string;
  toolCode: string;
  checkoutDate: string;
  expectedReturnDate: string | null;
  checkedOutBy: string;
  status: string;
}

export interface ToolReturnResult {
  checkoutId: string;
  returnDate: string;
  daysCheckedOut: number;
  isOverdue: boolean;
  overdueDays: number | null;
  damageReported: boolean;
}

export interface ToolCheckoutDto {
  id: string;
  toolId: string;
  toolCode: string;
  toolName: string;
  checkedOutById: string;
  checkedOutByName: string;
  checkoutDate: string;
  expectedReturnDate: string | null;
  actualReturnDate: string | null;
  status: string;
  daysOut: number;
  isOverdue: boolean;
  workOrderId: string | null;
  workOrderNumber: string | null;
  conditionOnCheckout: string | null;
  checkoutNotes: string | null;
}

export interface ToolAvailabilityDto {
  toolId: string;
  toolName: string;
  toolCode: string;
  isAvailable: boolean;
  currentStatus: string;
  availableFrom: string | null;
  upcomingCheckouts: ToolCheckoutDto[];
}

export interface ToolCheckoutHistoryDto {
  toolId: string;
  toolName: string;
  toolCode: string;
  totalCheckouts: number;
  totalUsageDays: number;
  totalRentalCost: number;
  recentCheckouts: ToolCheckoutDto[];
}

export const toolCheckoutService = {
  // Tool Management
  async getAvailableTools(): Promise<MaintenanceToolDto[]> {
    const response = await axios.get(`${API_BASE_URL}/maintenance/tools/available?itemType=4`, {
      headers: getHeaders(),
    });
    return response.data;
  },

  async getAllTools(): Promise<MaintenanceToolDto[]> {
    const response = await axios.get(`${API_BASE_URL}/maintenance/tools/all?itemType=4`, {
      headers: getHeaders(),
    });
    return response.data;
  },

  async getToolById(toolId: string): Promise<MaintenanceToolDto> {
    const response = await axios.get(`${API_BASE_URL}/maintenance/tools/${toolId}`, {
      headers: getHeaders(),
    });
    return response.data;
  },

  async checkToolAvailability(toolId: string, startDate?: string, endDate?: string): Promise<ToolAvailabilityDto> {
    const params = new URLSearchParams();
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/tools/${toolId}/availability?${params.toString()}`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  async getToolHistory(toolId: string, limit: number = 50): Promise<ToolCheckoutHistoryDto> {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/tools/${toolId}/history?limit=${limit}`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  // Checkout Operations
  async checkoutTool(toolId: string, employeeId: string, dto: CheckoutToolDto): Promise<ToolCheckoutResult> {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/tools/checkout?toolId=${toolId}&employeeId=${employeeId}`,
      dto,
      { headers: getHeaders() }
    );
    return response.data;
  },

  async returnTool(checkoutId: string, dto: ReturnToolDto): Promise<ToolReturnResult> {
    const response = await axios.post(
      `${API_BASE_URL}/maintenance/tools/return/${checkoutId}`,
      dto,
      { headers: getHeaders() }
    );
    return response.data;
  },

  async reportDamage(checkoutId: string, dto: ToolDamageDto): Promise<void> {
    await axios.post(
      `${API_BASE_URL}/maintenance/tools/checkouts/${checkoutId}/damage`,
      dto,
      { headers: getHeaders() }
    );
  },

  // Query Operations
  async getActiveCheckouts(employeeId?: string): Promise<ToolCheckoutDto[]> {
    const params = employeeId ? `?employeeId=${employeeId}` : '';
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/tools/checkouts/active${params}`,
      { headers: getHeaders() }
    );
    return response.data;
  },

  async getOverdueCheckouts(): Promise<ToolCheckoutDto[]> {
    const response = await axios.get(`${API_BASE_URL}/maintenance/tools/checkouts/overdue`, {
      headers: getHeaders(),
    });
    return response.data;
  },

  async getEmployeeCheckoutHistory(employeeId: string, limit: number = 50): Promise<ToolCheckoutDto[]> {
    const response = await axios.get(
      `${API_BASE_URL}/maintenance/tools/employees/${employeeId}/checkouts?limit=${limit}`,
      { headers: getHeaders() }
    );
    return response.data;
  },
};
