// Real API service that connects to the .NET backend
import { getFinancePostingErrorPresentation } from '@/lib/finance/posting-error';
import { browserSessionCoordinator } from './browser-session-coordinator';
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
  mustChangePassword?: boolean;
  temporaryPasswordExpiresAtUtc?: string;
  /** The user↔employee link; null/absent = not linked. The /me portal gate reads this. */
  employeeId?: string | null;
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

export interface SaveTenantUserMappingRequest {
  userId: string;
  tenantId: string;
  expiresAt?: string | null;
  reason?: string;
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

  private isCurrentRequestSession(requestToken: string | null): boolean {
    // Other tabs share storage, but an outstanding request still belongs to
    // the token it was sent with. Never terminate a replacement session.
    this.syncTokenFromStorage();
    return !!requestToken && requestToken === this.token;
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
      const validationMessages = (() => {
        if (Array.isArray(errorData?.errors)) {
          return errorData.errors.filter((entry: unknown): entry is string =>
            typeof entry === 'string' && entry.trim().length > 0);
        }
        if (!errorData?.errors || typeof errorData.errors !== 'object') return [] as string[];

        return Object.entries(errorData.errors).flatMap(([field, entries]) => {
          const leaf = field.split('.').at(-1)?.replace(/^\$+/, '') || 'Request';
          const label = leaf
            .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
            .replace(/\bId\b/g, 'ID')
            .replace(/^./, (value) => value.toUpperCase());
          return (Array.isArray(entries) ? entries : [entries])
            .filter((entry): entry is string => typeof entry === 'string' && entry.trim().length > 0)
            .map((entry) => `${label}: ${entry.trim()}`);
        });
      })();
      const validationSummary = [...new Set(validationMessages)].join(' ');
      const apiMessage = typeof errorData?.message === 'string' ? errorData.message.trim() : '';
      const apiTitle = typeof errorData?.title === 'string' ? errorData.title.trim() : '';
      const isGenericValidationMessage = (value: string) =>
        /^one or more validation errors occurred\.?$/i.test(value);

      const sourceErrorMessage = errorData.detail ||
        (apiMessage && !isGenericValidationMessage(apiMessage) ? apiMessage : undefined) ||
        validationSummary ||
        (apiTitle && !isGenericValidationMessage(apiTitle) ? apiTitle : undefined) ||
        errorData.error ||
        (response.status === 403
          ? 'You do not have permission to perform this action. Ask an administrator to grant the required role permission.'
          : `HTTP ${response.status}: ${response.statusText}`);
      const responsePath = (() => {
        try { return new URL(response.url).pathname.toLowerCase(); }
        catch { return ''; }
      })();
      const isFinanceResponse = responsePath.includes('/api/finance/') ||
        responsePath.endsWith('/api/finance') ||
        responsePath.includes('/api/ap/') ||
        responsePath.includes('/api/ar/') ||
        responsePath.includes('/api/budget/') ||
        responsePath.includes('/api/fiscal-');
      const financePresentation = isFinanceResponse
        ? getFinancePostingErrorPresentation(errorData, sourceErrorMessage, 'Finance action failed')
        : null;
      const errorMessage = financePresentation?.description ?? sourceErrorMessage;
      const error = new Error(errorMessage);

      // Attach additional error details
      (error as any).status = response.status;
      (error as any).statusText = response.statusText;
      (error as any).response = errorData;
      if (financePresentation) (error as any).financeTitle = financePresentation.title;

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
    this.syncTokenFromStorage();
    const token = this.token;
    const refreshToken = typeof window !== 'undefined' ? localStorage.getItem('refreshToken') : null;

    if (!token || !refreshToken) {
      throw new Error('No tokens available for refresh');
    }

    const sessionVersion = browserSessionCoordinator.getSessionVersion();
    const response = await browserSessionCoordinator.coordinateRefresh(sessionVersion, async () => {
      const refreshed = await this.privateRequest<LoginResponse>('/auth/refresh', {
        method: 'POST',
        body: JSON.stringify({ token, refreshToken }),
      });

      if (!this.isCurrentRequestSession(token) ||
          (typeof window !== 'undefined' && localStorage.getItem('refreshToken') !== refreshToken)) {
        throw new Error('The session changed while refreshing authentication. Please retry.');
      }

      if (refreshed.token) {
        this.setToken(refreshed.token);
      }

      if (typeof window !== 'undefined') {
        if (refreshed.refreshToken) {
          localStorage.setItem('refreshToken', refreshed.refreshToken);
        }
        if (refreshed.expiresAt) {
          localStorage.setItem('tokenExpiry', new Date(refreshed.expiresAt).getTime().toString());
        }
      }

      browserSessionCoordinator.publish('token-refreshed');
      return refreshed;
    });

    if (response) return response;

    // Another tab completed the refresh while this tab waited for the shared lock. Tokens live
    // in the existing shared storage, so return their current view without issuing another call.
    this.syncTokenFromStorage();
    const storedToken = this.token;
    if (!storedToken || typeof window === 'undefined') {
      throw new Error('The session changed while refreshing authentication. Please retry.');
    }

    const storedUser = localStorage.getItem('user');
    const storedExpiry = Number.parseInt(localStorage.getItem('tokenExpiry') || '', 10);
    let user: UserInfo | undefined;
    if (storedUser) {
      try {
        user = JSON.parse(storedUser) as UserInfo;
      } catch {
        // The refreshed credentials are still usable. The normal /auth/me path can repopulate
        // a malformed cached user record without causing a second refresh request.
      }
    }
    return {
      token: storedToken,
      refreshToken: localStorage.getItem('refreshToken') || undefined,
      expiresAt: Number.isFinite(storedExpiry) ? new Date(storedExpiry).toISOString() : undefined,
      user,
    };
  }

  public async logout(refreshToken?: string): Promise<void> {
    this.syncTokenFromStorage();
    const token = this.token;
    const sessionRefreshToken = refreshToken ??
      (typeof window !== 'undefined' ? localStorage.getItem('refreshToken') ?? undefined : undefined);
    try {
      const body = sessionRefreshToken ? { refreshToken: sessionRefreshToken } : undefined;
      await this.privateRequest('/auth/logout', {
        method: 'POST',
        body: body ? JSON.stringify(body) : undefined,
      });
    } finally {
      // A slow logout from a stale tab must not erase a login or refresh completed elsewhere.
      if (this.isCurrentRequestSession(token) &&
          (typeof window === 'undefined' || localStorage.getItem('refreshToken') === sessionRefreshToken)) {
        this.clearToken();
      }
    }
  }

  public async getCurrentUser(options: { silent?: boolean } = {}): Promise<UserInfo> {
    return this.privateRequest<UserInfo>('/auth/me', {}, true, options.silent ?? false);
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

  public async postBlob(endpoint: string, data?: any): Promise<Blob> {
    const options: RequestInit = { method: 'POST' };
    if (data !== undefined && data !== null) {
      options.body = JSON.stringify(data);
    }

    return this.privateBlobRequest(endpoint, options);
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
    const requestToken = new Headers(config.headers).get('Authorization')?.replace(/^Bearer\s+/i, '') ?? null;

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

    const requestStartedAt = Date.now();
    try {
      const startTime = requestStartedAt;
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
      const currentRequestSession = this.isCurrentRequestSession(requestToken);
      const peerRefreshedThisRequest = !currentRequestSession &&
        browserSessionCoordinator.wasTokenRefreshedAfter(requestStartedAt);

      if (error.status === 401 && includeAuth && retryCount === 0 && (currentRequestSession || peerRefreshedThisRequest)) {
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
            if (currentRequestSession) {
              await this.refreshToken();
            }

            if (!silent && this.enableApiDebugLogging) {
              console.log('✅ Token refreshed, retrying original request...');
            }

            // Retry the original request with the new token
            return this.privateRequest<T>(endpoint, options, includeAuth, silent, retryCount + 1);
          } catch (refreshError) {
            if (!this.isCurrentRequestSession(requestToken)) {
              throw error;
            }
            if (!silent) {
              console.error('❌ Token refresh failed:', refreshError);
            }

            // Clear tokens and trigger session blacklist event
            this.clearToken();
            browserSessionCoordinator.publish('session-expired');

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
    const requestToken = new Headers(config.headers).get('Authorization')?.replace(/^Bearer\s+/i, '') ?? null;
    const method = options.method || 'GET';

    const requestStartedAt = Date.now();
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

      const currentRequestSession = this.isCurrentRequestSession(requestToken);
      const peerRefreshedThisRequest = !currentRequestSession &&
        browserSessionCoordinator.wasTokenRefreshedAfter(requestStartedAt);

      if (error?.status === 401 && retryCount === 0 && (currentRequestSession || peerRefreshedThisRequest)) {
        try {
          if (currentRequestSession) {
            await this.refreshToken();
          }
          return this.privateBlobRequest(endpoint, options, retryCount + 1);
        } catch {
          if (!this.isCurrentRequestSession(requestToken)) {
            throw error;
          }
          this.clearToken();
          browserSessionCoordinator.publish('session-expired');
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
    return this.privateRequest<TenantUserMapping[]>(
      `/administration/user-tenant-mappings/${encodeURIComponent(tenantId)}/users`
    );
  }

  public async addUserToTenant(request: SaveTenantUserMappingRequest): Promise<TenantUserMapping> {
    if (this.isServerSide()) {
      throw new Error('addUserToTenant cannot be called during server-side rendering');
    }
    return this.privateRequest<TenantUserMapping>('/administration/user-tenant-mappings', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  public async removeUserFromTenant(userId: string, tenantId: string): Promise<void> {
    if (this.isServerSide()) {
      throw new Error('removeUserFromTenant cannot be called during server-side rendering');
    }
    return this.privateRequest<void>(
      `/administration/user-tenant-mappings/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`,
      { method: 'DELETE' }
    );
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

  public async getWithSignal<T = any>(
    endpoint: string,
    query: Record<string, unknown> | undefined,
    signal: AbortSignal
  ): Promise<T> {
    return this.privateRequest<T>(this.appendQueryParams(endpoint, query), { method: 'GET', signal });
  }

  // Silent GET method - doesn't log errors to console (useful for expected 404s)
  public async silentGet<T = any>(endpoint: string, query?: Record<string, unknown>): Promise<T> {
    return this.privateRequest<T>(this.appendQueryParams(endpoint, query), { method: 'GET' }, true, true);
  }

  public async post<T = any>(endpoint: string, data?: any, signal?: AbortSignal): Promise<T> {
    const options: RequestInit = { method: 'POST', signal };
    if (data instanceof FormData) {
      options.body = data;
    } else if (data !== undefined && data !== null) {
      options.body = JSON.stringify(data);
    }
    return this.privateRequest<T>(endpoint, options);
  }

  public async put<T = any>(endpoint: string, data?: any): Promise<T> {
    const options: RequestInit = { method: 'PUT' };
    if (data instanceof FormData) {
      options.body = data;
    } else if (data !== undefined && data !== null) {
      options.body = JSON.stringify(data);
    }
    return this.privateRequest<T>(endpoint, options);
  }

  public async delete<T = any>(endpoint: string, data?: any): Promise<T> {
    const options: RequestInit = { method: 'DELETE' };
    if (data) {
      options.body = JSON.stringify(data);
    }
    return this.privateRequest<T>(endpoint, options);
  }

  public async patch<T = any>(endpoint: string, data?: any): Promise<T> {
    const options: RequestInit = { method: 'PATCH' };
    if (data instanceof FormData) {
      options.body = data;
    } else if (data !== undefined && data !== null) {
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
    return localStorage.getItem('authToken') || localStorage.getItem('token');
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
