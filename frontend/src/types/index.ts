// API Response Types
export interface ApiResponse<T = unknown> {
  data?: T;
  message?: string;
  success: boolean;
  errors?: string[];
}

// User Types
export interface User {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  tenantId?: string;
  currentTenantId?: string;
  currentTenantCode?: string;
  currentTenantName?: string;
  isActive: boolean;
  roles: string[];
  phoneNumber?: string;
  createdAt?: string;
  lastLoginAt?: string;
  profilePictureUrl?: string;
  twoFactorEnabled?: boolean;
  authenticationProvider?: 'Local' | 'LDAP';
}

// Authentication Types
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
  user?: User;
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

export interface RefreshTokenRequest {
  token: string;
  refreshToken: string;
}

// Navigation Types
export interface NavItem {
  title: string;
  href: string;
  icon?: string;
  disabled?: boolean;
  external?: boolean;
  children?: NavItem[];
}

// Dashboard Types
export interface DashboardStats {
  totalUsers: number;
  totalTenants: number;
  activeUsers: number;
  systemHealth: 'healthy' | 'warning' | 'error';
}

// Form Types
export interface FormFieldProps {
  name: string;
  label: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  type?: 'text' | 'email' | 'password' | 'number' | 'tel' | 'url';
}

// Module Types
export type ModuleName = 'Finance' | 'HR' | 'Sales' | 'Inventory' | 'Procurement' | 'Marketing';

export interface ModuleConfig {
  name: ModuleName;
  enabled: boolean;
  permissions: string[];
}

// Tenant Types
export interface Tenant {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  logoUrl?: string;
  modules?: ModuleConfig[];
}

// Error Types
export interface AppError {
  message: string;
  code?: string;
  details?: Record<string, unknown>;
}
