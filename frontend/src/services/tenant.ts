import { apiService, type UserTenantInfo, type GetUserTenantsRequest, type SelectTenantRequest } from './api.service';
import type { Tenant } from '../types';

export class TenantService {
  /**
   * Get all tenants (for general listing, not user-specific)
   */
  async getAllTenants(): Promise<Tenant[]> {
    try {
      const response = await apiService.getTenants();
      return response.map(dto => ({
        id: dto.id,
        name: dto.name,
        code: dto.code,
        description: dto.description,
        isActive: dto.isActive,
        createdAt: dto.createdAt,
        logoUrl: dto.logoUrl
      }));
    } catch (error) {
      console.error('Error fetching tenants:', error);
      // Return empty array instead of default tenant
      return [];
    }
  }

  /**
   * Get accessible tenants for a specific user (used during login flow)
   */
  async getUserTenants(username: string): Promise<UserTenantInfo[]> {
    try {
      const request: GetUserTenantsRequest = { username };
      const response = await apiService.getUserTenants(request);
      return response.tenants;
    } catch (error) {
      console.error('Error fetching user tenants:', error);
      return [];
    }
  }

  /**
   * Select a tenant for the current user session
   */
  async selectTenant(tenantCode: string, setAsDefault: boolean = false): Promise<void> {
    try {
      const request: SelectTenantRequest = { tenantCode, setAsDefault };
      const response = await apiService.selectTenant(request);

      // Persist updated user context (token is already updated by apiService.selectTenant)
      if (typeof window !== 'undefined' && response?.user) {
        try {
          localStorage.setItem('user', JSON.stringify(response.user));
          localStorage.setItem('currentTenantCode', response.user.currentTenantCode || tenantCode);

          if (response.user.currentTenantId && response.user.currentTenantCode) {
            localStorage.setItem('currentTenant', JSON.stringify({
              id: response.user.currentTenantId,
              code: response.user.currentTenantCode,
              name: response.user.currentTenantName || response.user.currentTenantCode,
              isActive: true,
              createdAt: new Date().toISOString(),
            }));
          }
        } catch {
          // ignore storage errors
        }
      }
      
      // The API service already updates the token, but we can also dispatch events
      if (typeof window !== 'undefined') {
        const detail = { tenantCode, user: response.user };
        // Dispatch both legacy and canonical events (different parts of the app listen to different names)
        window.dispatchEvent(new CustomEvent('tenant-selected', { detail }));
        window.dispatchEvent(new CustomEvent('tenant-changed', { detail }));
      }
    } catch (error) {
      console.error('Error selecting tenant:', error);
      throw error;
    }
  }

  async getTenantById(id: string): Promise<Tenant | null> {
    try {
      const response = await apiService.getTenantById(id);
      return {
        id: response.id,
        name: response.name,
        code: response.code,
        description: response.description,
        isActive: response.isActive,
        createdAt: response.createdAt,
        logoUrl: response.logoUrl
      };
    } catch (error) {
      console.error('Error fetching tenant by ID:', error);
      return null;
    }
  }

  async getTenantByCode(code: string): Promise<Tenant | null> {
    try {
      const response = await apiService.getTenantByCode(code);
      return {
        id: response.id,
        name: response.name,
        code: response.code,
        description: response.description,
        isActive: response.isActive,
        createdAt: response.createdAt,
        logoUrl: response.logoUrl
      };
    } catch (error) {
      console.error('Error fetching tenant by code:', error);
      return null;
    }
  }

  async getTenantUsers(tenantId: string) {
    try {
      console.log('Getting users for tenant:', tenantId);
      const userMappings = await apiService.getTenantUsers(tenantId);
      return userMappings;
    } catch (error) {
      console.error('Error fetching tenant users:', error);
      return [];
    }
  }

  async addUserToTenant(data: { userId: string; tenantId: string; expiresAt?: string | null }) {
    // Mock implementation - this endpoint doesn't exist yet
    console.log('Adding user to tenant:', data);
    return Promise.resolve();
  }

  async removeUserFromTenant(userId: string, tenantId: string) {
    // Mock implementation - this endpoint doesn't exist yet
    console.log('Removing user from tenant:', userId, tenantId);
    return Promise.resolve();
  }
}

export const tenantService = new TenantService();
export default tenantService;
