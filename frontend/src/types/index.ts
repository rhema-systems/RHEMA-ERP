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
  isActive: boolean;
  roles: string[];
}

// Authentication Types
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
  user: User;
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
