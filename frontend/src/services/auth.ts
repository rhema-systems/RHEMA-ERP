import { apiService } from './api.service';
import type { LoginRequest, LoginResponse, RequestLoginOtpRequest, RequestLoginOtpResponse, VerifyLoginOtpRequest, User, Tenant } from '../types';

export class AuthService {
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await apiService.login(credentials);

    // Store tokens and user info
    if (response.token) {
      console.log('AuthService: Saving user to localStorage', response.user);
      localStorage.setItem('authToken', response.token);
      localStorage.setItem('refreshToken', response.refreshToken);
      localStorage.setItem('user', JSON.stringify(response.user));
    }

    return response;
  }

  async requestLoginOtp(request: RequestLoginOtpRequest): Promise<RequestLoginOtpResponse> {
    return apiService.requestLoginOtp(request);
  }

  async loginWithOtp(request: VerifyLoginOtpRequest): Promise<LoginResponse> {
    const response = await apiService.verifyLoginOtp(request);

    if (response.token) {
      localStorage.setItem('authToken', response.token);
      localStorage.setItem('refreshToken', response.refreshToken);
      localStorage.setItem('user', JSON.stringify(response.user));
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
    const mappedUser = {
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
      localStorage.setItem('authToken', response.token);
      localStorage.setItem('refreshToken', response.refreshToken);
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
    return localStorage.getItem('authToken');
  }

  isAuthenticated(): boolean {
    if (typeof window === 'undefined') return false;
    return !!this.getStoredToken();
  }

  clearTokens(): void {
    if (typeof window === 'undefined') return;

    localStorage.removeItem('authToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    localStorage.removeItem('currentTenant');
  }

  setCurrentTenant(tenant: Tenant): void {
    if (typeof window === 'undefined') return;

    localStorage.setItem('currentTenant', JSON.stringify(tenant));
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
    return user?.permissions?.includes(permission) ?? false;
  }

  hasAnyPermission(permissions: string[]): boolean {
    const user = this.getStoredUser();
    if (!user?.permissions) return false;

    return permissions.some(permission => user.permissions?.includes(permission));
  }

  hasAllPermissions(permissions: string[]): boolean {
    const user = this.getStoredUser();
    if (!user?.permissions) return false;

    return permissions.every(permission => user.permissions?.includes(permission));
  }
}

export const authService = new AuthService();
export default authService;
