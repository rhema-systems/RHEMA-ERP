import { apiService } from './api.service';
import type { LoginRequest, LoginResponse, RequestLoginOtpRequest, RequestLoginOtpResponse, VerifyLoginOtpRequest, User, Tenant } from '../types';
import { hasAllPermissionsAccess, hasAnyPermissionAccess, hasPermissionAccess } from '../lib/permissions';

const SESSION_ACTIVITY_STORAGE_KEY = 'erp-session-last-activity';

export class AuthService {
  private markSessionActivityNow(): void {
    if (typeof window === 'undefined') return;

    localStorage.setItem(SESSION_ACTIVITY_STORAGE_KEY, Date.now().toString());
  }

  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await apiService.login(credentials);
    
    // Store tokens and user info
    if (response.token) {
      apiService.setToken(response.token);
      if (response.refreshToken) {
        localStorage.setItem('refreshToken', response.refreshToken);
      }
      if (response.expiresAt) {
        localStorage.setItem('tokenExpiry', new Date(response.expiresAt).getTime().toString());
      }
      localStorage.setItem('user', JSON.stringify(response.user));
      this.markSessionActivityNow();
    }
    
    return response;
  }

  async requestLoginOtp(request: RequestLoginOtpRequest): Promise<RequestLoginOtpResponse> {
    return apiService.requestLoginOtp(request);
  }

  async loginWithOtp(request: VerifyLoginOtpRequest): Promise<LoginResponse> {
    const response = await apiService.verifyLoginOtp(request);

    if (response.token) {
      apiService.setToken(response.token);
      if (response.refreshToken) {
        localStorage.setItem('refreshToken', response.refreshToken);
      }
      if (response.expiresAt) {
        localStorage.setItem('tokenExpiry', new Date(response.expiresAt).getTime().toString());
      }
      localStorage.setItem('user', JSON.stringify(response.user));
      this.markSessionActivityNow();
    }

    return response;
  }

  logout = async (): Promise<void> => {
    try {
      // Get refresh token before clearing
      const refreshToken = localStorage.getItem('refreshToken');
      
      // Call logout endpoint with refresh token if available
      await apiService.logout(refreshToken || undefined);
    } catch (error) {
      // Continue with logout even if API call fails
      console.warn('Logout API call failed:', error);
    } finally {
      // Always clear local storage
      this.clearTokens();
    }
  }

  async getCurrentUser(): Promise<User> {
    const userInfo = await apiService.getCurrentUser();
    // Keep everything the server sent. The explicit fields below only rename/shape what the
    // desk shell reads; before the spread was here, every field not on that list (employeeId,
    // the current-tenant trio, accessibleTenants, mustChangePassword) was dropped on refetch,
    // so a linked user turned "unlinked" five minutes after login or on any page reload.
    const mappedUser = {
      ...userInfo,
      id: userInfo.id,
      username: userInfo.username,
      email: userInfo.email,
      firstName: userInfo.firstName,
      lastName: userInfo.lastName,
      roles: userInfo.roles,
      permissions: userInfo.permissions,
      isActive: userInfo.isActive,
      lastLoginAt: userInfo.lastLoginAt,
      createdAt: userInfo.createdAt,
      phoneNumber: userInfo.phoneNumber,
      tenantId: userInfo.tenantId,
    } as User;

    if (typeof window !== 'undefined') {
      localStorage.setItem('user', JSON.stringify(mappedUser));
    }

    return mappedUser;
  }

  async refreshToken(): Promise<LoginResponse> {
    const response = await apiService.refreshToken();
    
    if (response.token) {
      apiService.setToken(response.token);
      if (response.refreshToken) {
        localStorage.setItem('refreshToken', response.refreshToken);
      }
      if (response.expiresAt) {
        localStorage.setItem('tokenExpiry', new Date(response.expiresAt).getTime().toString());
      }
      localStorage.setItem('user', JSON.stringify(response.user));
    }

    return response;
  }

  forgotPassword = async (request: { email: string; tenantCode?: string; captchaToken?: string | null }): Promise<{ success: boolean; message: string }> => {
    return apiService.publicRequest('/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  getStoredUser(): User | null {
    if (typeof window === 'undefined') return null;
    
    const userStr = localStorage.getItem('user');
    if (!userStr) return null;
    
    try {
      return JSON.parse(userStr);
    } catch {
      return null;
    }
  }

  getStoredToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem('authToken') || localStorage.getItem('token');
  }

  isAuthenticated(): boolean {
    if (typeof window === 'undefined') return false;
    return !!this.getStoredToken();
  }

  clearTokens(): void {
    if (typeof window === 'undefined') return;
    
    apiService.clearToken();
    localStorage.removeItem('token');
    localStorage.removeItem('tokenExpiry');
    localStorage.removeItem('user');
    localStorage.removeItem('currentTenant');
    localStorage.removeItem('currentTenantCode');
    localStorage.removeItem(SESSION_ACTIVITY_STORAGE_KEY);
  }

  setCurrentTenant(tenant: Tenant): void {
    if (typeof window === 'undefined') return;
    
    localStorage.setItem('currentTenant', JSON.stringify(tenant));
    localStorage.setItem('currentTenantCode', tenant.code);
    window.dispatchEvent(new CustomEvent('tenant-changed', { detail: tenant }));
  }

  getCurrentTenant(): Tenant | null {
    if (typeof window === 'undefined') return null;
    
    const tenantStr = localStorage.getItem('currentTenant');
    if (!tenantStr) return null;
    
    try {
      return JSON.parse(tenantStr);
    } catch {
      return null;
    }
  }

  hasTenantSelected(): boolean {
    return !!this.getCurrentTenant();
  }

  hasRole(role: string): boolean {
    const user = this.getStoredUser();
    return user?.roles?.includes(role) ?? false;
  }

  hasAnyRole(roles: string[]): boolean {
    const user = this.getStoredUser();
    if (!user?.roles) return false;
    
    return roles.some(role => user.roles.includes(role));
  }

  hasAllRoles(roles: string[]): boolean {
    const user = this.getStoredUser();
    if (!user?.roles) return false;
    
    return roles.every(role => user.roles.includes(role));
  }

  hasPermission(permission: string): boolean {
    const user = this.getStoredUser();
    return hasPermissionAccess(user, permission);
  }

  hasAnyPermission(permissions: string[]): boolean {
    const user = this.getStoredUser();
    return hasAnyPermissionAccess(user, permissions);
  }

  hasAllPermissions(permissions: string[]): boolean {
    const user = this.getStoredUser();
    return hasAllPermissionsAccess(user, permissions);
  }
}

export const authService = new AuthService();
export default authService;
