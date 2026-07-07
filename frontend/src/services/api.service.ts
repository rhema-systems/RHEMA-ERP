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
  twoFactorCode?: string;
  recaptchaToken?: string;
}

export interface LoginResponse {
  token?: string;
  refreshToken?: string;
  expiresAt?: string;
  user?: UserInfo;
  requiresTwoFactor?: boolean;
  twoFactorToken?: string;
}

export type OtpChannel = 'Email' | 'Sms';

export interface RequestLoginOtpRequest {
  identifier: string;
  channel: OtpChannel;
  tenantCode?: string;
  recaptchaToken?: string;
}

export interface RequestLoginOtpResponse {
  success: boolean;
  message: string;
}

export interface VerifyLoginOtpRequest {
  identifier: string;
  channel: OtpChannel;
  otpCode: string;
  tenantCode?: string;
  rememberMe?: boolean;
  twoFactorCode?: string;
  recaptchaToken?: string;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  currentTenantId?: string;
  currentTenantCode?: string;
  currentTenantName?: string;
  accessibleTenants: UserTenantInfo[];
  isActive: boolean;
  roles: string[];
  permissions: string[];
  createdAt?: string;
  lastLoginAt?: string;
  tenantId?: string;
  authenticationProvider?: 'Local' | 'LDAP';
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
  baseCurrency?: string;
  baseCurrencyName?: string;
  currencySymbol?: string;
  currencyDecimalPlaces?: number;
}

export interface RefreshTokenRequest {
  token: string;
  refreshToken: string;
}

class ApiService {
  private baseUrl = process.env.NEXT_PUBLIC_API_URL || '/api';
  private token: string | null = null;
  private readonly enableApiDebugLogging = process.env.NEXT_PUBLIC_DEBUG_API === 'true';
  private readonly requestTimeoutMs = Number.parseInt(
    process.env.NEXT_PUBLIC_API_TIMEOUT_MS || '',
    10
  ) || 20000;

  constructor() {
    // Load token from localStorage if available
    if (typeof window !== 'undefined') {
      this.token = localStorage.getItem('authToken') || localStorage.getItem('token');
    }
  }

  private isServerSide(): boolean {
    return typeof window === 'undefined';
  }

  private getHeaders(isFormData: boolean = false, includeAuth: boolean = true): HeadersInit {
    const headers: HeadersInit = {};

    this.syncTokenFromStorage();

    // Don't set Content-Type for FormData - browser will set it with boundary
    if (!isFormData) {
      headers['Content-Type'] = 'application/json';
    }

    if (this.token && includeAuth) {
      // Validate token format before sending
      if (this.isValidJwtFormat(this.token)) {
        headers['Authorization'] = `Bearer ${this.token}`;
      } else {
        console.warn('Invalid JWT token format detected, clearing token:',
          this.token.length > 50 ? this.token.substring(0, 50) + '...' : this.token);
        this.clearToken();
        // Don't include Authorization header with invalid token
      }
    }

    return headers;
  }

  private syncTokenFromStorage(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const storedToken = localStorage.getItem('authToken') || localStorage.getItem('token');
    if (storedToken !== this.token) {
      this.token = storedToken;
    }
  }

  private appendQueryParams(endpoint: string, query?: Record<string, unknown>): string {
    if (!query) {
      return endpoint;
    }

    const params = new URLSearchParams();

    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') {
        continue;
      }

      if (Array.isArray(value)) {
        value.forEach((entry) => params.append(key, String(entry)));
        continue;
      }

      params.append(key, String(value));
    }

    const queryString = params.toString();
    if (!queryString) {
      return endpoint;
    }

    return endpoint.includes('?')
      ? `${endpoint}&${queryString}`
      : `${endpoint}?${queryString}`;
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      let errorData: any = {};

      try {
        // Try to parse JSON error response
        const text = await response.text();
        if (text) {
          try {
            errorData = JSON.parse(text);
          } catch {
            // If JSON parsing fails, but we have text, use it as the message
            errorData = { message: text };
          }
        }
      } catch {
        // If we can't get any text at all, use status info
        errorData = {};
      }

      // Check if this is a 401 (Unauthorized) response - likely a blacklisted token
      if (response.status === 401) {
        // Don't trigger session blacklist event if we're on login/public endpoints
        const url = new URL(response.url);
        const isPublicEndpoint = url.pathname.includes('/auth/login') ||
          url.pathname.includes('/auth/security-settings') ||
          url.pathname.includes('/auth/refresh') ||
          (url.pathname.includes('/tenant') && !this.token);

        // Only trigger session blacklist if it's not a public endpoint
        // The privateRequest method will handle token refresh attempts first
        if (!isPublicEndpoint && typeof window !== 'undefined') {
          // Don't immediately clear token here - let the retry logic handle it first
          // The session blacklist event will be handled by the privateRequest method after retry attempts fail
        }
      }

      // Create a proper error with the message from the API response
      // Only fall back to generic HTTP status message if no other message is available
      const errorMessage = errorData.message ||
        errorData.title ||
        errorData.error ||
        `HTTP ${response.status}: ${response.statusText}`;
      const error = new Error(errorMessage);

      // Attach additional error details
      (error as any).status = response.status;
      (error as any).statusText = response.statusText;
      (error as any).response = errorData;

      throw error;
    }

    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      return response.json();
    }

    return response.text() as unknown as T;
  }


  public setToken(token: string | null | undefined): void {
    // Handle null/undefined tokens
    if (!token) {
      this.clearToken();
      return;
    }

    // Validate token format before storing
    if (!this.isValidJwtFormat(token)) {
      console.error('Attempting to store invalid JWT token format:',
        token.length > 50 ? token.substring(0, 50) + '...' : token);
      return; // Don't store invalid tokens
    }

    this.token = token;
    if (typeof window !== 'undefined') {
      localStorage.setItem('authToken', token);
      localStorage.setItem('token', token);
    }
  }

  public clearToken(): void {
    this.token = null;
    if (typeof window !== 'undefined') {
      localStorage.removeItem('authToken');
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
    }
  }

  // Auth endpoints
  public async login(request: LoginRequest): Promise<LoginResponse> {
    const response = await this.privateRequest<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(request),
    });

    // Store tokens only if they exist
    if (response.token) {
      this.setToken(response.token);
    }

    if (response.refreshToken && typeof window !== 'undefined') {
      localStorage.setItem('refreshToken', response.refreshToken);
    }

    return response;
  }

  public async requestLoginOtp(request: RequestLoginOtpRequest): Promise<RequestLoginOtpResponse> {
    return this.publicRequest<RequestLoginOtpResponse>('/auth/otp/request', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  public async verifyLoginOtp(request: VerifyLoginOtpRequest): Promise<LoginResponse> {
    const response = await this.publicRequest<LoginResponse>('/auth/otp/verify', {
      method: 'POST',
      body: JSON.stringify(request),
    });

    // Store tokens only if they exist
    if (response.token) {
      this.setToken(response.token);
    }

    if (response.refreshToken && typeof window !== 'undefined') {
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

    if (response.token) {
      this.setToken(response.token);
    }

    if (response.refreshToken && typeof window !== 'undefined') {
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
  public async request<T = any>(endpoint: string, options: RequestInit = {}): Promise<T> {
    return this.privateRequest<T>(endpoint, options, true, false);
  }

  // Public request method without authentication
  public async publicRequest<T = any>(endpoint: string, options: RequestInit = {}): Promise<T> {
    return this.privateRequest<T>(endpoint, options, false, false);
  }

  // Silent request method that doesn't log errors to console
  public async silentRequest<T = any>(endpoint: string, options: RequestInit = {}): Promise<T> {
    return this.privateRequest<T>(endpoint, options, true, true);
  }

  private createRequestTimeout(signal?: AbortSignal | null): {
    signal?: AbortSignal;
    clear: () => void;
  } {
    if (signal || this.requestTimeoutMs <= 0 || typeof AbortController === 'undefined') {
      return { signal: signal ?? undefined, clear: () => undefined };
    }

    const controller = new AbortController();
    const timeoutId: ReturnType<typeof setTimeout> = setTimeout(() => {
      controller.abort();
    }, this.requestTimeoutMs);

    return {
      signal: controller.signal,
      clear: () => clearTimeout(timeoutId),
    };
  }

  private normalizeFetchError(error: any, method: string, endpoint: string): any {
    if (error?.name !== 'AbortError') {
      return error;
    }

    const timeoutError = new Error(
      `API request timed out after ${this.requestTimeoutMs}ms: ${method} ${endpoint}`
    );
    (timeoutError as any).status = 0;
    (timeoutError as any).statusText = 'Request Timeout';
    (timeoutError as any).originalError = error;
    return timeoutError;
  }

  public async downloadBlob(endpoint: string, query?: Record<string, unknown>): Promise<Blob> {
    return this.privateBlobRequest(this.appendQueryParams(endpoint, query), { method: 'GET' });
  }

  // Rename private request method
  private async privateRequest<T>(endpoint: string, options: RequestInit = {}, includeAuth: boolean = true, silent: boolean = false, retryCount: number = 0): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;

    // Check if the body is FormData
    const isFormData = options.body instanceof FormData;

    const timeout = this.createRequestTimeout(options.signal);
    const config: RequestInit = {
      headers: this.getHeaders(isFormData, includeAuth),
      ...options,
      signal: timeout.signal,
    };

    const method = options.method || 'GET';
    const timestamp = new Date().toISOString();

    if (!silent && this.enableApiDebugLogging) {
      console.log(`🚀 API ${method} ${endpoint} - ${timestamp}${retryCount > 0 ? ` (retry ${retryCount})` : ''}`);
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
    }

    try {
      const startTime = Date.now();
      const response = await fetch(url, config);
      const endTime = Date.now();
      const duration = endTime - startTime;

      const status = response.status;
      const statusText = response.statusText;

      if (!silent && this.enableApiDebugLogging) {
        if (status >= 200 && status < 300) {
          console.log(`✅ API ${method} ${endpoint} - ${status} ${statusText} (${duration}ms)`);
        } else {
          console.log(`❌ API ${method} ${endpoint} - ${status} ${statusText} (${duration}ms)`);
        }
      }

      const result = await this.handleResponse<T>(response);

      // Log response data for non-GET operations and errors (only if not silent)
      if (!silent && this.enableApiDebugLogging && (method !== 'GET' || status >= 400)) {
        console.log('📥 Response data:', result);
      }

      return result;
    } catch (caught: any) {
      const error = this.normalizeFetchError(caught, method, endpoint);

      // Handle 401 errors with token refresh attempt (only once)
      if (error.status === 401 && includeAuth && retryCount === 0 && this.token) {
        // Don't retry for auth endpoints to avoid infinite loops
        const isAuthEndpoint = endpoint.includes('/auth/login') ||
          endpoint.includes('/auth/refresh') ||
          endpoint.includes('/auth/logout');

        if (!isAuthEndpoint) {
          if (!silent && this.enableApiDebugLogging) {
            console.log('🔄 401 error detected, attempting token refresh...');
          }

          try {
            // Attempt to refresh token
            await this.refreshToken();

            if (!silent && this.enableApiDebugLogging) {
              console.log('✅ Token refreshed, retrying original request...');
            }

            // Retry the original request with the new token
            return this.privateRequest<T>(endpoint, options, includeAuth, silent, retryCount + 1);
          } catch (refreshError) {
            if (!silent) {
              console.error('❌ Token refresh failed:', refreshError);
            }

            // Clear tokens and trigger session blacklist event
            this.clearToken();

            // Trigger session blacklist event since token refresh failed
            if (typeof window !== 'undefined') {
              window.dispatchEvent(new CustomEvent('session-blacklisted', {
                detail: {
                  status: 401,
                  message: 'Session terminated - token refresh failed'
                }
              }));
            }

            // Still throw the original 401 error
            throw error;
          }
        }
      }

      if (!silent) {
        console.error(`💥 API ${method} ${endpoint} failed:`, error);
      }
      throw error;
    } finally {
      timeout.clear();
    }
  }

  private async privateBlobRequest(endpoint: string, options: RequestInit = {}, retryCount: number = 0): Promise<Blob> {
    const url = `${this.baseUrl}${endpoint}`;
    const isFormData = options.body instanceof FormData;
    const timeout = this.createRequestTimeout(options.signal);
    const config: RequestInit = {
      headers: this.getHeaders(isFormData, true),
      ...options,
      signal: timeout.signal,
    };
    const method = options.method || 'GET';

    try {
      const response = await fetch(url, config);

      if (response.ok) {
        return await response.blob();
      }

      let error: any;
      try {
        await this.handleResponse<never>(response);
      } catch (e) {
        error = e;
      }

      if (error?.status === 401 && retryCount === 0 && this.token) {
        try {
          await this.refreshToken();
          return this.privateBlobRequest(endpoint, options, retryCount + 1);
        } catch {
          this.clearToken();
          if (typeof window !== 'undefined') {
            window.dispatchEvent(new CustomEvent('session-blacklisted', {
              detail: {
                status: 401,
                message: 'Session terminated - token refresh failed'
              }
            }));
          }
        }
      }

      throw error || new Error(`HTTP ${response.status}: ${response.statusText}`);
    } catch (caught) {
      const error = this.normalizeFetchError(caught, method, endpoint);
      console.error(`Blob download ${endpoint} failed:`, error);
      throw error;
    } finally {
      timeout.clear();
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
    if (response.token) {
      this.setToken(response.token);
    }

    return response;
  }

  public async getTenantUsers(tenantId: string): Promise<TenantUserMapping[]> {
    if (this.isServerSide()) {
      throw new Error('getTenantUsers cannot be called during server-side rendering');
    }
    return this.privateRequest<TenantUserMapping[]>(`/auth/tenant/${encodeURIComponent(tenantId)}/users`);
  }

  /**
   * Validates that a JWT token has the correct format (3 parts separated by dots)
   * @param token The JWT token to validate
   * @returns True if the token has valid format, false otherwise
   */
  private isValidJwtFormat(token: string): boolean {
    if (!token || typeof token !== 'string') {
      return false;
    }

    // Remove any whitespace
    const trimmedToken = token.trim();

    if (!trimmedToken) {
      return false;
    }

    const parts = trimmedToken.split('.');

    // JWT should have exactly 3 parts: header.payload.signature
    if (parts.length !== 3) {
      return false;
    }

    // Each part should not be empty
    for (const part of parts) {
      if (!part || part.trim() === '') {
        return false;
      }
    }

    return true;
  }

  // Standard HTTP methods
  public async get<T = any>(endpoint: string, query?: Record<string, unknown>): Promise<T> {
    return this.privateRequest<T>(this.appendQueryParams(endpoint, query), { method: 'GET' });
  }

  // Silent GET method - doesn't log errors to console (useful for expected 404s)
  public async silentGet<T = any>(endpoint: string, query?: Record<string, unknown>): Promise<T> {
    return this.privateRequest<T>(this.appendQueryParams(endpoint, query), { method: 'GET' }, true, true);
  }

  public async post<T = any>(endpoint: string, data?: any): Promise<T> {
    const options: RequestInit = { method: 'POST' };
    if (data) {
      options.body = JSON.stringify(data);
    }
    return this.privateRequest<T>(endpoint, options);
  }

  public async put<T = any>(endpoint: string, data?: any): Promise<T> {
    const options: RequestInit = { method: 'PUT' };
    if (data) {
      options.body = JSON.stringify(data);
    }
    return this.privateRequest<T>(endpoint, options);
  }

  public async delete<T = any>(endpoint: string): Promise<T> {
    return this.privateRequest<T>(endpoint, { method: 'DELETE' });
  }

  public async patch<T = any>(endpoint: string, data?: any): Promise<T> {
    const options: RequestInit = { method: 'PATCH' };
    if (data) {
      options.body = JSON.stringify(data);
    }
    return this.privateRequest<T>(endpoint, options);
  }
}

export const apiService = new ApiService();
// Default export added for backward compatibility — some files use `import apiService from '...'`
// instead of `import { apiService } from '...'`. Both patterns now work.
export default apiService;

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
