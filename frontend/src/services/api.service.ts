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
  tenantId?: string;
  isActive: boolean;
  roles: string[];
}

export interface TenantDto {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  logoUrl?: string;
  domain?: string;
  contactEmail?: string;
  contactPhone?: string;
  address?: string;
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

  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    return headers;
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
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

  public async logout(): Promise<void> {
    try {
      await this.privateRequest('/auth/logout', {
        method: 'POST',
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
    return this.privateRequest<T>(endpoint, options);
  }

  // Rename private request method
  private async privateRequest<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;
    const config: RequestInit = {
      headers: this.getHeaders(),
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
}

export const apiService = new ApiService();