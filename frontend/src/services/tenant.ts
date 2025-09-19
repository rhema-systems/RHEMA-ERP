import { apiService } from './api';
import { API_ENDPOINTS } from '../config/api';
import type { Tenant } from '../types';

export class TenantService {
  async getAllTenants(): Promise<Tenant[]> {
    try {
      const response = await apiService.get<Tenant[]>(API_ENDPOINTS.TENANTS.LIST);
      return response; // The apiService.get already extracts response.data
    } catch (error) {
      console.error('Error fetching tenants:', error);
      // Return default tenant as fallback
      return [
        {
          id: '1',
          name: 'Default Organization',
          code: 'DEFAULT',
          description: 'Default system tenant',
          isActive: true,
          createdAt: new Date().toISOString(),
          logoUrl: undefined
        }
      ];
    }
  }

  async getTenantById(id: string): Promise<Tenant | null> {
    try {
      const response = await apiService.get<Tenant>(API_ENDPOINTS.TENANTS.BY_ID(id));
      return response; // The apiService.get already extracts response.data
    } catch (error) {
      console.error('Error fetching tenant by ID:', error);
      return null;
    }
  }

  async getTenantByCode(code: string): Promise<Tenant | null> {
    try {
      const response = await apiService.get<Tenant>(API_ENDPOINTS.TENANTS.BY_CODE(code));
      return response; // The apiService.get already extracts response.data
    } catch (error) {
      console.error('Error fetching tenant by code:', error);
      return null;
    }
  }
}

export const tenantService = new TenantService();
export default tenantService;