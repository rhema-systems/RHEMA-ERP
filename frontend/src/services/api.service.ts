// Real API service that connects to the .NET backend
export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  message?: string;
  errors?: string[];
}

export interface LoginRequest {
  username: string;
  password: string;
  tenantCode?: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfo;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  currentTenantId?: string;
  currentTenantCode?: string;
  currentTenantName?: string;
  accessibleTenants: UserTenantInfo[];
  isActive: boolean;
  roles: string[];
}

export interface UserTenantInfo {
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  isDefault: boolean;
  accessLevel: string;
}

export interface GetUserTenantsRequest {
  username: string;
}

export interface GetUserTenantsResponse {
  tenants: UserTenantInfo[];
  defaultTenant?: UserTenantInfo;
}

export interface SelectTenantRequest {
  tenantCode: string;
  setAsDefault?: boolean;
}

export interface SelectTenantResponse {
  token: string;
  expiresAt: string;
  user: UserInfo;
}

export interface TenantUserMapping {
  userId: string;
  tenantId: string;
  isActive: boolean;
  expiresAt: string | null;
  accessLevel: string;
  isDefault: boolean;
  grantedAt: string;
  user: TenantUserInfo;
}

export interface TenantUserInfo {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  fullName?: string;
  isActive: boolean;
}

export interface TenantDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  status: 'Active' | 'Inactive' | 'Suspended';
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  
  // Branding
  logoUrl?: string;
  primaryColor?: string;
  secondaryColor?: string;
  faviconUrl?: string;
  coverImageUrl?: string;
  
  // Contact Information
  domain?: string;
  contactEmail?: string;
  contactPhone?: string;
  address?: string;
  
  // Subscription
  subscriptionStartDate?: string;
  subscriptionEndDate?: string;
  
  // LDAP Configuration
  ldapServer?: string;
  ldapPort?: number;
  ldapBaseDn?: string;
  ldapBindDn?: string;
  ldapBindPassword?: string;
  ldapEnabled?: boolean;
  
  // Default tenant settings
  isDefaultForPublicUsers?: boolean;
  isDefaultForInternalUsers?: boolean;
  
  // Feature flags
  allowSelfRegistration?: boolean;
  publicRegistrationDomains?: string;
  requireEmailVerification?: boolean;
  userAudience?: number;
  welcomeMessage?: string;
  defaultPriority?: number;
  enableAutoSelection?: boolean;
}

export interface RefreshTokenRequest {
  token: string;
  refreshToken: string;
}

class ApiService {
  private baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
  private token: string | null = null;

  constructor() {
    // Load token from localStorage if available
    if (typeof window !== 'undefined') {
      this.token = localStorage.getItem('authToken');
    }
  }

  private isServerSide(): boolean {
    return typeof window === 'undefined';
  }

  private getHeaders(isFormData: boolean = false, includeAuth: boolean = true): HeadersInit {
    const headers: HeadersInit = {};

    // Don't set Content-Type for FormData - browser will set it with boundary
    if (!isFormData) {
      headers['Content-Type'] = 'application/json';
    }

    if (this.token && includeAuth) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    return headers;
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      
      // Check if this is a 401 (Unauthorized) response - likely a blacklisted token
      if (response.status === 401) {
        // Don't trigger session blacklist event if we're on login/public endpoints
        const url = new URL(response.url);
        const isPublicEndpoint = url.pathname.includes('/auth/login') || 
                                url.pathname.includes('/auth/security-settings') || 
                                url.pathname.includes('/auth/refresh') ||
                                (url.pathname.includes('/tenant') && !this.token);
        
        if (!isPublicEndpoint && typeof window !== 'undefined') {
          // Clear the token immediately to prevent further API calls with blacklisted token
          this.clearToken();
          
          // Emit a custom event to notify about session termination
          window.dispatchEvent(new CustomEvent('session-blacklisted', {
            detail: {
              status: response.status,
              message: errorData.message || 'Session terminated'
            }
          }));
        }
      }
      
      throw new Error(errorData.message || `HTTP ${response.status}: ${response.statusText}`);
    }

    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      return response.json();
    }

    return response.text() as unknown as T;
  }


  public setToken(token: string): void {
    this.token = token;
    if (typeof window !== 'undefined') {
      localStorage.setItem('authToken', token);
    }
  }

  public clearToken(): void {
    this.token = null;
    if (typeof window !== 'undefined') {
      localStorage.removeItem('authToken');
      localStorage.removeItem('refreshToken');
    }
  }

  // Auth endpoints
  public async login(request: LoginRequest): Promise<LoginResponse> {
    const response = await this.privateRequest<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(request),
    });

    // Store tokens
    this.setToken(response.token);
    if (typeof window !== 'undefined') {
      localStorage.setItem('refreshToken', response.refreshToken);
    }

    return response;
  }

  public async refreshToken(): Promise<LoginResponse> {
    const token = this.token;
    const refreshToken = typeof window !== 'undefined' ? localStorage.getItem('refreshToken') : null;

    if (!token || !refreshToken) {
      throw new Error('No tokens available for refresh');
    }

    const response = await this.privateRequest<LoginResponse>('/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ token, refreshToken }),
    });

    this.setToken(response.token);
    if (typeof window !== 'undefined') {
      localStorage.setItem('refreshToken', response.refreshToken);
    }

    return response;
  }

  public async logout(refreshToken?: string): Promise<void> {
    try {
      const body = refreshToken ? { refreshToken } : undefined;
      await this.privateRequest('/auth/logout', {
        method: 'POST',
        body: body ? JSON.stringify(body) : undefined,
      });
    } finally {
      this.clearToken();
    }
  }

  public async getCurrentUser(): Promise<UserInfo> {
    return this.privateRequest<UserInfo>('/auth/me');
  }

  // Public request method for admin service
  public async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    return this.privateRequest<T>(endpoint, options, true);
  }

  // Public request method without authentication
  public async publicRequest<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    return this.privateRequest<T>(endpoint, options, false);
  }

  // Rename private request method
  private async privateRequest<T>(endpoint: string, options: RequestInit = {}, includeAuth: boolean = true): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;
    
    // Check if the body is FormData
    const isFormData = options.body instanceof FormData;
    
    const config: RequestInit = {
      headers: this.getHeaders(isFormData, includeAuth),
      ...options,
    };

    const method = options.method || 'GET';
    const timestamp = new Date().toISOString();
    
    console.log(`🚀 API ${method} ${endpoint} - ${timestamp}`);
    if (method !== 'GET' && config.headers) {
      console.log('📤 Request headers:', config.headers);
      if (options.body) {
        try {
          const bodyData = JSON.parse(options.body as string);
          console.log('📦 Request body:', bodyData);
        } catch {
          console.log('📦 Request body (non-JSON):', options.body);
        }
      }
    }

    try {
      const startTime = Date.now();
      const response = await fetch(url, config);
      const endTime = Date.now();
      const duration = endTime - startTime;
      
      const status = response.status;
      const statusText = response.statusText;
      
      if (status >= 200 && status < 300) {
        console.log(`✅ API ${method} ${endpoint} - ${status} ${statusText} (${duration}ms)`);
      } else {
        console.log(`❌ API ${method} ${endpoint} - ${status} ${statusText} (${duration}ms)`);
      }
      
      const result = await this.handleResponse<T>(response);
      
      // Log response data for non-GET operations and errors
      if (method !== 'GET' || status >= 400) {
        console.log('📥 Response data:', result);
      }
      
      return result;
    } catch (error) {
      console.error(`💥 API ${method} ${endpoint} failed:`, error);
      throw error;
    }
  }

  // Tenant endpoints
  public async getTenants(): Promise<TenantDto[]> {
    // Return empty array during SSR to avoid errors
    if (this.isServerSide()) {
      return [];
    }
    return this.privateRequest<TenantDto[]>('/tenant');
  }

  public async getTenantByCode(code: string): Promise<TenantDto> {
    if (this.isServerSide()) {
      throw new Error('getTenantByCode cannot be called during server-side rendering');
    }
    return this.privateRequest<TenantDto>(`/tenant/by-code/${encodeURIComponent(code)}`);
  }

  public async getTenantById(id: string): Promise<TenantDto> {
    if (this.isServerSide()) {
      throw new Error('getTenantById cannot be called during server-side rendering');
    }
    return this.privateRequest<TenantDto>(`/tenant/${encodeURIComponent(id)}`);
  }

  // User-Tenant endpoints
  public async getUserTenants(request: GetUserTenantsRequest): Promise<GetUserTenantsResponse> {
    if (this.isServerSide()) {
      throw new Error('getUserTenants cannot be called during server-side rendering');
    }
    return this.privateRequest<GetUserTenantsResponse>('/auth/user-tenants', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  public async selectTenant(request: SelectTenantRequest): Promise<SelectTenantResponse> {
    if (this.isServerSide()) {
      throw new Error('selectTenant cannot be called during server-side rendering');
    }
    const response = await this.privateRequest<SelectTenantResponse>('/auth/select-tenant', {
      method: 'POST',
      body: JSON.stringify(request),
    });

    // Update stored token with new tenant context
    this.setToken(response.token);
    
    return response;
  }

  public async getTenantUsers(tenantId: string): Promise<TenantUserMapping[]> {
    if (this.isServerSide()) {
      throw new Error('getTenantUsers cannot be called during server-side rendering');
    }
    return this.privateRequest<TenantUserMapping[]>(`/auth/tenant/${encodeURIComponent(tenantId)}/users`);
  }
}

export const apiService = new ApiService();

// Helper function to get stored token for SignalR
export function getStoredToken(): string | null {
  if (typeof window !== 'undefined') {
    return localStorage.getItem('authToken');
  }
  return null;
}

// Backward compatibility wrapper for old apiRequest calls
export async function apiRequest<T>(options: {
  url: string;
  method?: string;
  data?: any;
}): Promise<T> {
  const { url, method = 'GET', data } = options;
  const requestOptions: RequestInit = {
    method,
  };
  
  if (data) {
    requestOptions.body = JSON.stringify(data);
  }
  
  return apiService.request<T>(url, requestOptions);
}
