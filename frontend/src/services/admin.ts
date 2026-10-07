// Types for administration data
export interface User {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  roles: string[];
  isActive: boolean;
  lastLoginAt?: Date;
  createdAt: Date;
  tenant?: string;
}

export interface Role {
  id: string;
  name: string;
  description?: string;
  permissions: string[];
  isSystemRole: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface Tenant {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  createdAt: Date;
  updatedAt: Date;
  settings?: Record<string, unknown>;
}

export interface SecurityLog {
  id: string;
  userId?: string;
  username?: string;
  action: string;
  ipAddress: string;
  userAgent: string;
  success: boolean;
  details?: string;
  timestamp: Date;
}

export interface AuditLog {
  id: string;
  userId: string;
  username: string;
  action: string;
  resource: string;
  resourceId?: string;
  oldValues?: Record<string, unknown>;
  newValues?: Record<string, unknown>;
  ipAddress: string;
  timestamp: Date;
}

export interface PasswordPolicy {
  minLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireDigits: boolean;
  requireSpecialChars: boolean;
  maxAge?: number;
  preventReuse?: number;
}

export interface AccountSettings {
  enableCaptcha: boolean;
  captchaProvider?: 'recaptcha' | 'hcaptcha';
  captchaSiteKey?: string;
  sessionTimeout: number; // in minutes
  maxFailedAttempts: number;
  lockoutDuration: number; // in minutes
}

export interface EmailSettings {
  smtpHost: string;
  smtpPort: number;
  smtpUsername: string;
  smtpPassword: string;
  smtpPasswordConfigured?: boolean;
  useTLS: boolean;
  fromAddress: string;
  fromName: string;
}

// Mock data generators
const generateMockUsers = (): User[] => [
  {
    id: '1',
    username: 'admin',
    email: 'admin@company.com',
    firstName: 'System',
    lastName: 'Administrator',
    roles: ['admin', 'user'],
    isActive: true,
    lastLoginAt: new Date('2024-01-15T10:30:00'),
    createdAt: new Date('2023-01-01T00:00:00'),
    tenant: 'company-a'
  },
  {
    id: '2',
    username: 'john.doe',
    email: 'john.doe@company.com',
    firstName: 'John',
    lastName: 'Doe',
    roles: ['user'],
    isActive: true,
    lastLoginAt: new Date('2024-01-14T15:45:00'),
    createdAt: new Date('2023-06-15T00:00:00'),
    tenant: 'company-a'
  },
  {
    id: '3',
    username: 'jane.smith',
    email: 'jane.smith@company.com',
    firstName: 'Jane',
    lastName: 'Smith',
    roles: ['manager', 'user'],
    isActive: false,
    lastLoginAt: new Date('2024-01-10T09:15:00'),
    createdAt: new Date('2023-03-20T00:00:00'),
    tenant: 'company-b'
  }
];

const generateMockRoles = (): Role[] => [
  {
    id: '1',
    name: 'admin',
    description: 'Full system access',
    permissions: ['*'],
    isSystemRole: true,
    createdAt: new Date('2023-01-01T00:00:00'),
    updatedAt: new Date('2023-01-01T00:00:00')
  },
  {
    id: '2',
    name: 'manager',
    description: 'Management access',
    permissions: ['users.read', 'users.create', 'users.update', 'reports.read'],
    isSystemRole: false,
    createdAt: new Date('2023-01-01T00:00:00'),
    updatedAt: new Date('2024-01-10T00:00:00')
  },
  {
    id: '3',
    name: 'user',
    description: 'Standard user access',
    permissions: ['dashboard.read', 'profile.update'],
    isSystemRole: false,
    createdAt: new Date('2023-01-01T00:00:00'),
    updatedAt: new Date('2023-01-01T00:00:00')
  }
];

const generateMockTenants = (): Tenant[] => [
  {
    id: '1',
    name: 'Company A Corp',
    code: 'company-a',
    isActive: true,
    createdAt: new Date('2023-01-01T00:00:00'),
    updatedAt: new Date('2024-01-10T00:00:00'),
    settings: { theme: 'blue', locale: 'en-US' }
  },
  {
    id: '2',
    name: 'Company B Ltd',
    code: 'company-b',
    isActive: true,
    createdAt: new Date('2023-02-15T00:00:00'),
    updatedAt: new Date('2023-12-20T00:00:00'),
    settings: { theme: 'green', locale: 'en-GB' }
  },
  {
    id: '3',
    name: 'Inactive Corp',
    code: 'inactive-corp',
    isActive: false,
    createdAt: new Date('2023-01-01T00:00:00'),
    updatedAt: new Date('2023-06-01T00:00:00')
  }
];

const generateMockSecurityLogs = (): SecurityLog[] => [
  {
    id: '1',
    userId: '1',
    username: 'admin',
    action: 'LOGIN_SUCCESS',
    ipAddress: '192.168.1.100',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
    success: true,
    timestamp: new Date('2024-01-15T10:30:00')
  },
  {
    id: '2',
    username: 'unknown_user',
    action: 'LOGIN_FAILED',
    ipAddress: '192.168.1.200',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
    success: false,
    details: 'Invalid credentials',
    timestamp: new Date('2024-01-15T09:15:00')
  },
  {
    id: '3',
    userId: '2',
    username: 'john.doe',
    action: 'PASSWORD_CHANGE',
    ipAddress: '192.168.1.150',
    userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36',
    success: true,
    timestamp: new Date('2024-01-14T16:20:00')
  }
];

const generateMockAuditLogs = (): AuditLog[] => [
  {
    id: '1',
    userId: '1',
    username: 'admin',
    action: 'CREATE',
    resource: 'users',
    resourceId: '4',
    newValues: { username: 'new_user', email: 'new@company.com' },
    ipAddress: '192.168.1.100',
    timestamp: new Date('2024-01-15T11:00:00')
  },
  {
    id: '2',
    userId: '1',
    username: 'admin',
    action: 'UPDATE',
    resource: 'users',
    resourceId: '2',
    oldValues: { isActive: true },
    newValues: { isActive: false },
    ipAddress: '192.168.1.100',
    timestamp: new Date('2024-01-14T14:30:00')
  },
  {
    id: '3',
    userId: '2',
    username: 'john.doe',
    action: 'READ',
    resource: 'reports',
    resourceId: 'monthly-sales',
    ipAddress: '192.168.1.110',
    timestamp: new Date('2024-01-14T15:45:00')
  }
];

// API delay simulation
const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export const adminService = {
  // User Management
  async getUsers(): Promise<User[]> {
    await delay(500);
    return generateMockUsers();
  },

  async getUser(id: string): Promise<User | null> {
    await delay(300);
    const users = generateMockUsers();
    return users.find(user => user.id === id) || null;
  },

  async createUser(userData: Omit<User, 'id' | 'createdAt'>): Promise<User> {
    await delay(1000);
    const newUser: User = {
      ...userData,
      id: Date.now().toString(),
      createdAt: new Date(),
    };
    return newUser;
  },

  async updateUser(id: string, userData: Partial<User>): Promise<User> {
    await delay(1000);
    const users = generateMockUsers();
    const user = users.find(u => u.id === id);
    if (!user) throw new Error('User not found');
    return { ...user, ...userData };
  },

  async deleteUser(_id: string): Promise<void> {
    await delay(500);
    // In real implementation, this would delete the user
  },

  // Role Management
  async getRoles(): Promise<Role[]> {
    await delay(500);
    return generateMockRoles();
  },

  async getRole(id: string): Promise<Role | null> {
    await delay(300);
    const roles = generateMockRoles();
    return roles.find(role => role.id === id) || null;
  },

  async createRole(roleData: Omit<Role, 'id' | 'createdAt' | 'updatedAt'>): Promise<Role> {
    await delay(1000);
    const now = new Date();
    const newRole: Role = {
      ...roleData,
      id: Date.now().toString(),
      createdAt: now,
      updatedAt: now,
    };
    return newRole;
  },

  async updateRole(id: string, roleData: Partial<Role>): Promise<Role> {
    await delay(1000);
    const roles = generateMockRoles();
    const role = roles.find(r => r.id === id);
    if (!role) throw new Error('Role not found');
    return { ...role, ...roleData, updatedAt: new Date() };
  },

  async deleteRole(_id: string): Promise<void> {
    await delay(500);
    // In real implementation, this would delete the role
  },

  // Tenant Management
  async getTenants(): Promise<Tenant[]> {
    await delay(500);
    return generateMockTenants();
  },

  async getTenant(id: string): Promise<Tenant | null> {
    await delay(300);
    const tenants = generateMockTenants();
    return tenants.find(tenant => tenant.id === id) || null;
  },

  async createTenant(tenantData: Omit<Tenant, 'id' | 'createdAt' | 'updatedAt'>): Promise<Tenant> {
    await delay(1000);
    const now = new Date();
    const newTenant: Tenant = {
      ...tenantData,
      id: Date.now().toString(),
      createdAt: now,
      updatedAt: now,
    };
    return newTenant;
  },

  async updateTenant(id: string, tenantData: Partial<Tenant>): Promise<Tenant> {
    await delay(1000);
    const tenants = generateMockTenants();
    const tenant = tenants.find(t => t.id === id);
    if (!tenant) throw new Error('Tenant not found');
    return { ...tenant, ...tenantData, updatedAt: new Date() };
  },

  async deleteTenant(_id: string): Promise<void> {
    await delay(500);
    // In real implementation, this would delete the tenant
  },

  // Security Logs
  async getSecurityLogs(): Promise<SecurityLog[]> {
    await delay(700);
    return generateMockSecurityLogs();
  },

  // Audit Logs
  async getAuditLogs(): Promise<AuditLog[]> {
    await delay(800);
    return generateMockAuditLogs();
  },

  // Settings
  async getPasswordPolicy(): Promise<PasswordPolicy> {
    await delay(300);
    return {
      minLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireDigits: true,
      requireSpecialChars: true,
      maxAge: 90,
      preventReuse: 5
    };
  },

  async updatePasswordPolicy(policy: PasswordPolicy): Promise<PasswordPolicy> {
    await delay(1000);
    return policy;
  },

  async getAccountSettings(): Promise<AccountSettings> {
    await delay(300);
    return {
      enableCaptcha: false,
      captchaProvider: 'recaptcha',
      captchaSiteKey: '',
      sessionTimeout: 30,
      maxFailedAttempts: 5,
      lockoutDuration: 15
    };
  },

  async updateAccountSettings(settings: AccountSettings): Promise<AccountSettings> {
    await delay(1000);
    return settings;
  },

  async getEmailSettings(): Promise<EmailSettings> {
    await delay(300);
    return {
      smtpHost: 'smtp.gmail.com',
      smtpPort: 587,
      smtpUsername: '',
      smtpPassword: '',
      useTLS: true,
      fromAddress: 'noreply@company.com',
      fromName: 'ERP System'
    };
  },

  async updateEmailSettings(settings: EmailSettings): Promise<EmailSettings> {
    await delay(1000);
    return settings;
  },

  async testEmailSettings(settings: EmailSettings, testEmail: string): Promise<{ success: boolean; message: string }> {
    await delay(2000);
    // Simulate email test
    const success = Math.random() > 0.3; // 70% success rate for demo
    return {
      success,
      message: success 
        ? `Test email sent successfully to ${testEmail}` 
        : 'Failed to send test email. Please check your SMTP settings.'
    };
  },

  // Online Users Management
  async getOnlineUsers(): Promise<any[]> {
    await delay(300);
    // Mock implementation - replace with actual API call
    return [];
  },

  async terminateUserSession(sessionId: string): Promise<void> {
    await delay(500);
    // Mock implementation - replace with actual API call
    console.log('Terminating session:', sessionId);
  }
};
