const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { 'Authorization': `Bearer ${token}` } : {})
  };
}

export interface MaintenanceSettingsDto {
  id: string;
  tenantId: string;
  fleetComplianceDueSoonDays: number;
  blockFleetDispatchWhenComplianceDueSoon: boolean;
  createdAt: string;
  createdById?: string;
  updatedAt?: string;
}

export interface UpdateMaintenanceSettingsDto {
  fleetComplianceDueSoonDays: number;
  blockFleetDispatchWhenComplianceDueSoon: boolean;
}

export const maintenanceSettingsService = {
  async getSettings(): Promise<MaintenanceSettingsDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/MaintenanceSettings`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error('Failed to fetch maintenance settings');
    return response.json();
  },

  async updateSettings(dto: UpdateMaintenanceSettingsDto): Promise<MaintenanceSettingsDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/MaintenanceSettings`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto)
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update maintenance settings');
    }

    return response.json();
  }
};

export default maintenanceSettingsService;

