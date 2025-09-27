// Real admin API service that connects to the .NET backend
import { apiService } from './api.service';

// Types that match the backend DTOs
export interface User {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  roles: string[];
  isActive: boolean;
  lastLoginAt?: Date;
  createdAt: Date;
  tenantId?: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  isActive: boolean;
  roles: string[];
  tenantId?: string;
}

export interface UpdateUserRequest {
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  isActive: boolean;
  roles: string[];
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
  description?: string;
  status: 'Active' | 'Inactive' | 'Suspended';
  createdAt: Date;
  updatedAt: Date;
  
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
  subscriptionStartDate?: Date;
  subscriptionEndDate?: Date;
  
  // LDAP Configuration
  ldapServer?: string;
  ldapPort?: number;
  ldapBaseDn?: string;
  ldapBindDn?: string;
  ldapBindPassword?: string;
  ldapEnabled: boolean;
  
  // Default tenant settings
  isDefaultForPublicUsers: boolean;
  isDefaultForInternalUsers: boolean;
  
  // Feature flags
  allowSelfRegistration: boolean;
  publicRegistrationDomains?: string;
  requireEmailVerification: boolean;
  userAudience: number; // Internal (1), External (2), Both (3)
  welcomeMessage?: string;
  defaultPriority: number;
  enableAutoSelection: boolean;
  
  // Computed properties (for compatibility)
  isActive: boolean; // derived from status === 'Active'
}

export interface CreateTenantRequest {
  name: string;
  code: string;
  description?: string;
  status?: 'Active' | 'Inactive' | 'Suspended';
  
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
  subscriptionStartDate?: Date;
  subscriptionEndDate?: Date;
  
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

export interface EmailSettings {
  smtpHost: string;
  smtpPort: number;
  smtpUsername: string;
  smtpPassword: string;
  useTLS: boolean;
  fromAddress: string;
  fromName: string;
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

export interface TestEmailResult {
  success: boolean;
  message: string;
}

class AdminApiService {
  // User Management
  async getUsers(): Promise<User[]> {
    return apiService.request<User[]>('/user');
  }

  async getUser(id: string): Promise<User> {
    return apiService.request<User>(`/user/${id}`);
  }

  async createUser(userData: CreateUserRequest): Promise<User> {
    console.log('Creating user:', userData.username, 'with roles:', userData.roles);
    try {
      const result = await apiService.request<User>('/user', {
        method: 'POST',
        body: JSON.stringify(userData),
      });
      console.log('User created successfully:', result.username, 'ID:', result.id);
      return result;
    } catch (error) {
      console.error('Failed to create user:', userData.username, error);
      throw error;
    }
  }

  async updateUser(id: string, userData: UpdateUserRequest): Promise<User> {
    console.log('Updating user:', id, 'with data:', userData);
    try {
      const result = await apiService.request<User>(`/user/${id}`, {
        method: 'PUT',
        body: JSON.stringify(userData),
      });
      console.log('User updated successfully:', result.username, 'ID:', result.id);
      return result;
    } catch (error) {
      console.error('Failed to update user:', id, error);
      throw error;
    }
  }

  async deleteUser(id: string): Promise<void> {
    console.log('Deleting user:', id);
    try {
      await apiService.request(`/user/${id}`, {
        method: 'DELETE',
      });
      console.log('User deleted successfully:', id);
    } catch (error) {
      console.error('Failed to delete user:', id, error);
      throw error;
    }
  }

  // Tenant Management (Enhanced from existing TenantController)
  async getTenants(): Promise<Tenant[]> {
    const tenantDtos = await apiService.getTenants();
    return tenantDtos.map(dto => ({
      id: dto.id,
      name: dto.name,
      code: dto.code,
      description: dto.description,
      status: dto.status as 'Active' | 'Inactive' | 'Suspended',
      isActive: dto.status === 'Active', // computed property for compatibility
      createdAt: new Date(dto.createdAt),
      updatedAt: new Date(dto.updatedAt),
      
      // Branding
      logoUrl: dto.logoUrl,
      primaryColor: dto.primaryColor,
      secondaryColor: dto.secondaryColor,
      faviconUrl: dto.faviconUrl,
      coverImageUrl: dto.coverImageUrl,
      
      // Contact Information
      domain: dto.domain,
      contactEmail: dto.contactEmail,
      contactPhone: dto.contactPhone,
      address: dto.address,
      
      // Subscription
      subscriptionStartDate: dto.subscriptionStartDate ? new Date(dto.subscriptionStartDate) : undefined,
      subscriptionEndDate: dto.subscriptionEndDate ? new Date(dto.subscriptionEndDate) : undefined,
      
      // LDAP Configuration
      ldapServer: dto.ldapServer,
      ldapPort: dto.ldapPort,
      ldapBaseDn: dto.ldapBaseDn,
      ldapBindDn: dto.ldapBindDn,
      ldapBindPassword: dto.ldapBindPassword,
      ldapEnabled: dto.ldapEnabled || false,
      
      // Default tenant settings
      isDefaultForPublicUsers: dto.isDefaultForPublicUsers || false,
      isDefaultForInternalUsers: dto.isDefaultForInternalUsers || false,
      
      // Feature flags
      allowSelfRegistration: dto.allowSelfRegistration || false,
      publicRegistrationDomains: dto.publicRegistrationDomains,
      requireEmailVerification: dto.requireEmailVerification || false,
      userAudience: dto.userAudience || 2,
      welcomeMessage: dto.welcomeMessage,
      defaultPriority: dto.defaultPriority || 10,
      enableAutoSelection: dto.enableAutoSelection || false,
    }));
  }

  async getTenant(id: string): Promise<Tenant> {
    const dto = await apiService.getTenantById(id);
    return {
      id: dto.id,
      name: dto.name,
      code: dto.code,
      description: dto.description,
      status: dto.status as 'Active' | 'Inactive' | 'Suspended',
      isActive: dto.status === 'Active',
      createdAt: new Date(dto.createdAt),
      updatedAt: new Date(dto.updatedAt),
      
      // Branding
      logoUrl: dto.logoUrl,
      primaryColor: dto.primaryColor,
      secondaryColor: dto.secondaryColor,
      faviconUrl: dto.faviconUrl,
      coverImageUrl: dto.coverImageUrl,
      
      // Contact Information
      domain: dto.domain,
      contactEmail: dto.contactEmail,
      contactPhone: dto.contactPhone,
      address: dto.address,
      
      // Subscription
      subscriptionStartDate: dto.subscriptionStartDate ? new Date(dto.subscriptionStartDate) : undefined,
      subscriptionEndDate: dto.subscriptionEndDate ? new Date(dto.subscriptionEndDate) : undefined,
      
      // LDAP Configuration
      ldapServer: dto.ldapServer,
      ldapPort: dto.ldapPort,
      ldapBaseDn: dto.ldapBaseDn,
      ldapBindDn: dto.ldapBindDn,
      ldapBindPassword: dto.ldapBindPassword,
      ldapEnabled: dto.ldapEnabled || false,
      
      // Default tenant settings
      isDefaultForPublicUsers: dto.isDefaultForPublicUsers || false,
      isDefaultForInternalUsers: dto.isDefaultForInternalUsers || false,
      
      // Feature flags
      allowSelfRegistration: dto.allowSelfRegistration || false,
      publicRegistrationDomains: dto.publicRegistrationDomains,
      requireEmailVerification: dto.requireEmailVerification || false,
      userAudience: dto.userAudience || 2,
      welcomeMessage: dto.welcomeMessage,
      defaultPriority: dto.defaultPriority || 10,
      enableAutoSelection: dto.enableAutoSelection || false,
    };
  }

  async createTenant(tenantData: CreateTenantRequest): Promise<Tenant> {
    console.log('Creating tenant:', tenantData.name, 'with code:', tenantData.code);
    console.log('Full tenant data being sent:', tenantData);
    
    try {
      // Helper function to clean blob URLs and file:// URLs
      const cleanImageUrl = (url?: string) => {
        if (!url || url.startsWith('blob:') || url.startsWith('file://')) {
          return null; // Don't send blob URLs or file:// URLs to backend
        }
        return url;
      };
      
      // Map the data to match backend CreateTenantRequest structure (now supports all fields!)
      const backendData = {
        // Core fields
        name: tenantData.name,
        code: tenantData.code,
        description: tenantData.description || '',
        status: tenantData.status || 'Active',
        
        // Contact Information
        domain: tenantData.domain || null,
        contactEmail: tenantData.contactEmail || null,
        contactPhone: tenantData.contactPhone || null,
        address: tenantData.address || null,
        
        // Branding - Filter out blob URLs
        logoUrl: cleanImageUrl(tenantData.logoUrl),
        primaryColor: tenantData.primaryColor || null,
        secondaryColor: tenantData.secondaryColor || null,
        faviconUrl: cleanImageUrl(tenantData.faviconUrl),
        coverImageUrl: cleanImageUrl(tenantData.coverImageUrl),
        
        // Subscription
        subscriptionStartDate: tenantData.subscriptionStartDate || null,
        subscriptionEndDate: tenantData.subscriptionEndDate || null,
        
        // LDAP Configuration
        ldapServer: tenantData.ldapServer || null,
        ldapPort: tenantData.ldapPort || 389,
        ldapBaseDn: tenantData.ldapBaseDn || null,
        ldapBindDn: tenantData.ldapBindDn || null,
        ldapBindPassword: tenantData.ldapBindPassword || null,
        ldapEnabled: tenantData.ldapEnabled || false,
        
        // Default tenant settings
        isDefaultForPublicUsers: tenantData.isDefaultForPublicUsers || false,
        isDefaultForInternalUsers: tenantData.isDefaultForInternalUsers || false,
        
        // Feature flags
        allowSelfRegistration: tenantData.allowSelfRegistration || false,
        publicRegistrationDomains: tenantData.publicRegistrationDomains || null,
        requireEmailVerification: tenantData.requireEmailVerification || false,
        userAudience: tenantData.userAudience || 2,
        welcomeMessage: tenantData.welcomeMessage || null,
        defaultPriority: tenantData.defaultPriority || 10,
        enableAutoSelection: tenantData.enableAutoSelection || false,
      };
      
      console.log('Mapped backend create data:', backendData);
      
      const resultDto = await apiService.request<Record<string, unknown>>('/tenant', {
        method: 'POST',
        body: JSON.stringify(backendData),
      });
      
      console.log('Raw backend create response:', resultDto);
      
      // Map the response back to our Tenant interface
      // Backend now returns all fields from the expanded TenantDto!
      const result: Tenant = {
        id: resultDto.id,
        name: resultDto.name,
        code: resultDto.code,
        description: resultDto.description || '',
        status: (resultDto.status || 'Active') as 'Active' | 'Inactive' | 'Suspended',
        isActive: resultDto.isActive ?? (resultDto.status === 'Active'),
        createdAt: new Date(resultDto.createdAt),
        updatedAt: new Date(resultDto.updatedAt),
        
        // Contact Information
        domain: resultDto.domain,
        contactEmail: resultDto.contactEmail,
        contactPhone: resultDto.contactPhone,
        address: resultDto.address,
        
        // Branding
        logoUrl: resultDto.logoUrl,
        primaryColor: resultDto.primaryColor,
        secondaryColor: resultDto.secondaryColor,
        faviconUrl: resultDto.faviconUrl,
        coverImageUrl: resultDto.coverImageUrl,
        
        // Subscription
        subscriptionStartDate: resultDto.subscriptionStartDate ? new Date(resultDto.subscriptionStartDate) : undefined,
        subscriptionEndDate: resultDto.subscriptionEndDate ? new Date(resultDto.subscriptionEndDate) : undefined,
        
        // LDAP Configuration
        ldapServer: resultDto.ldapServer,
        ldapPort: resultDto.ldapPort ?? 389,
        ldapBaseDn: resultDto.ldapBaseDn,
        ldapBindDn: resultDto.ldapBindDn,
        ldapBindPassword: resultDto.ldapBindPassword,
        ldapEnabled: resultDto.ldapEnabled ?? false,
        
        // Default tenant settings
        isDefaultForPublicUsers: resultDto.isDefaultForPublicUsers ?? false,
        isDefaultForInternalUsers: resultDto.isDefaultForInternalUsers ?? false,
        
        // Feature flags
        allowSelfRegistration: resultDto.allowSelfRegistration ?? false,
        publicRegistrationDomains: resultDto.publicRegistrationDomains,
        requireEmailVerification: resultDto.requireEmailVerification ?? false,
        userAudience: resultDto.userAudience ?? 2,
        welcomeMessage: resultDto.welcomeMessage,
        defaultPriority: resultDto.defaultPriority ?? 10,
        enableAutoSelection: resultDto.enableAutoSelection ?? false,
      };
      
      console.log('Tenant created successfully:', result.name, 'ID:', result.id);
      return result;
    } catch (error) {
      console.error('Failed to create tenant:', tenantData.name, error);
      throw error;
    }
  }

  async updateTenant(id: string, tenantData: Partial<CreateTenantRequest>): Promise<Tenant> {
    console.log('Updating tenant:', id, 'with data:', tenantData);
    console.log('Full tenant update data being sent:', tenantData);
    
    try {
      // Helper function to clean blob URLs and file:// URLs
      const cleanImageUrl = (url?: string) => {
        if (!url || url.startsWith('blob:') || url.startsWith('file://')) {
          return null; // Don't send blob URLs or file:// URLs to backend
        }
        return url;
      };
      
      // Map the data to match backend UpdateTenantRequest structure (now supports all fields!)
      const backendData = {
        // Core fields
        name: tenantData.name,
        description: tenantData.description || '',
        status: tenantData.status || 'Active',
        isActive: tenantData.status === 'Active' || tenantData.status !== 'Inactive',
        
        // Contact Information
        domain: tenantData.domain || null,
        contactEmail: tenantData.contactEmail || null,
        contactPhone: tenantData.contactPhone || null,
        address: tenantData.address || null,
        
        // Branding
        logoUrl: cleanImageUrl(tenantData.logoUrl),
        primaryColor: tenantData.primaryColor || null,
        secondaryColor: tenantData.secondaryColor || null,
        faviconUrl: cleanImageUrl(tenantData.faviconUrl),
        coverImageUrl: cleanImageUrl(tenantData.coverImageUrl),
        
        // Subscription
        subscriptionStartDate: tenantData.subscriptionStartDate || null,
        subscriptionEndDate: tenantData.subscriptionEndDate || null,
        
        // LDAP Configuration
        ldapServer: tenantData.ldapServer || null,
        ldapPort: tenantData.ldapPort || 389,
        ldapBaseDn: tenantData.ldapBaseDn || null,
        ldapBindDn: tenantData.ldapBindDn || null,
        ldapBindPassword: tenantData.ldapBindPassword || null,
        ldapEnabled: tenantData.ldapEnabled || false,
        
        // Default tenant settings
        isDefaultForPublicUsers: tenantData.isDefaultForPublicUsers || false,
        isDefaultForInternalUsers: tenantData.isDefaultForInternalUsers || false,
        
        // Feature flags
        allowSelfRegistration: tenantData.allowSelfRegistration || false,
        publicRegistrationDomains: tenantData.publicRegistrationDomains || null,
        requireEmailVerification: tenantData.requireEmailVerification || false,
        userAudience: tenantData.userAudience || 2,
        welcomeMessage: tenantData.welcomeMessage || null,
        defaultPriority: tenantData.defaultPriority || 10,
        enableAutoSelection: tenantData.enableAutoSelection || false,
      };
      
      console.log('Mapped backend update data:', backendData);
      
      const resultDto = await apiService.request<Record<string, unknown>>(`/tenant/${id}`, {
        method: 'PUT',
        body: JSON.stringify(backendData),
      });
      
      console.log('Raw backend update response:', resultDto);
      
      // Map the response back to our Tenant interface
      // Backend now returns all fields from the expanded TenantDto!
      const result: Tenant = {
        id: resultDto.id,
        name: resultDto.name,
        code: resultDto.code,
        description: resultDto.description,
        status: resultDto.status as 'Active' | 'Inactive' | 'Suspended',
        isActive: resultDto.isActive ?? (resultDto.status === 'Active'),
        createdAt: new Date(resultDto.createdAt),
        updatedAt: new Date(resultDto.updatedAt),
        
        // Contact Information
        domain: resultDto.domain,
        contactEmail: resultDto.contactEmail,
        contactPhone: resultDto.contactPhone,
        address: resultDto.address,
        
        // Branding
        logoUrl: resultDto.logoUrl,
        primaryColor: resultDto.primaryColor,
        secondaryColor: resultDto.secondaryColor,
        faviconUrl: resultDto.faviconUrl,
        coverImageUrl: resultDto.coverImageUrl,
        
        // Subscription
        subscriptionStartDate: resultDto.subscriptionStartDate ? new Date(resultDto.subscriptionStartDate) : undefined,
        subscriptionEndDate: resultDto.subscriptionEndDate ? new Date(resultDto.subscriptionEndDate) : undefined,
        
        // LDAP Configuration
        ldapServer: resultDto.ldapServer,
        ldapPort: resultDto.ldapPort ?? 389,
        ldapBaseDn: resultDto.ldapBaseDn,
        ldapBindDn: resultDto.ldapBindDn,
        ldapBindPassword: resultDto.ldapBindPassword,
        ldapEnabled: resultDto.ldapEnabled ?? false,
        
        // Default tenant settings
        isDefaultForPublicUsers: resultDto.isDefaultForPublicUsers ?? false,
        isDefaultForInternalUsers: resultDto.isDefaultForInternalUsers ?? false,
        
        // Feature flags
        allowSelfRegistration: resultDto.allowSelfRegistration ?? false,
        publicRegistrationDomains: resultDto.publicRegistrationDomains,
        requireEmailVerification: resultDto.requireEmailVerification ?? false,
        userAudience: resultDto.userAudience ?? 2,
        welcomeMessage: resultDto.welcomeMessage,
        defaultPriority: resultDto.defaultPriority ?? 10,
        enableAutoSelection: resultDto.enableAutoSelection ?? false,
      };
      
      console.log('Tenant updated successfully:', result.name, 'ID:', result.id);
      return result;
    } catch (error) {
      console.error('Failed to update tenant:', id, error);
      throw error;
    }
  }

  async deleteTenant(id: string): Promise<void> {
    console.log('Deleting tenant:', id);
    try {
      await apiService.request(`/tenant/${id}`, {
        method: 'DELETE',
      });
      console.log('Tenant deleted successfully:', id);
    } catch (error) {
      console.error('Failed to delete tenant:', id, error);
      throw error;
    }
  }

  // Settings Management
  async getEmailSettings(): Promise<EmailSettings> {
    return apiService.request<EmailSettings>('/settings/email');
  }

  async saveEmailSettings(settings: EmailSettings): Promise<EmailSettings> {
    console.log('Saving email settings:', settings.smtpHost, settings.fromAddress);
    try {
      // First try to get existing settings
      const existing = await this.getEmailSettings();
      let result: EmailSettings;
      // If we get here without error and the settings have actual data (not defaults), use PUT
      if (existing && existing.smtpHost) {
        console.log('Updating existing email settings');
        result = await apiService.request<EmailSettings>('/settings/email', {
          method: 'PUT',
          body: JSON.stringify(settings),
        });
      } else {
        // No existing settings or only defaults, use POST to create
        console.log('Creating new email settings');
        result = await apiService.request<EmailSettings>('/settings/email', {
          method: 'POST',
          body: JSON.stringify(settings),
        });
      }
      console.log('Email settings saved successfully:', result.fromAddress);
      return result;
    } catch (error: unknown) {
      // If GET fails, assume no settings exist and use POST
      console.log('Creating email settings (fallback):', error instanceof Error ? error.message : 'Unknown error');
      try {
        const result = await apiService.request<EmailSettings>('/settings/email', {
          method: 'POST',
          body: JSON.stringify(settings),
        });
        console.log('Email settings created successfully (fallback):', result.fromAddress);
        return result;
      } catch (createError) {
        console.error('Failed to save email settings:', createError);
        throw createError;
      }
    }
  }

  // Keep the old method name for backward compatibility but make it smarter
  async updateEmailSettings(settings: EmailSettings): Promise<EmailSettings> {
    return this.saveEmailSettings(settings);
  }

  async testEmailSettings(settings: EmailSettings, testEmail: string): Promise<TestEmailResult> {
    console.log('Testing email settings to:', testEmail, 'via:', settings.smtpHost);
    try {
      const result = await apiService.request<TestEmailResult>('/settings/email/test', {
        method: 'POST',
        body: JSON.stringify({
          settings,
          testEmail,
        }),
      });
      console.log('Email test result:', result.success ? 'SUCCESS' : 'FAILED', result.message);
      return result;
    } catch (error) {
      console.error('Failed to test email settings:', error);
      throw error;
    }
  }

  async getPasswordPolicy(): Promise<PasswordPolicy> {
    return apiService.request<PasswordPolicy>('/settings/password-policy');
  }

  async savePasswordPolicy(policy: PasswordPolicy): Promise<PasswordPolicy> {
    console.log('Saving password policy:', policy.minLength, 'chars, require uppercase:', policy.requireUppercase);
    try {
      // First try to get existing policy
      const existing = await this.getPasswordPolicy();
      let result: PasswordPolicy;
      // If we get here without error and the policy has actual data (not defaults), use PUT
      if (existing && existing.minLength !== 8) { // Check if it's not the default
        console.log('Updating existing password policy');
        result = await apiService.request<PasswordPolicy>('/settings/password-policy', {
          method: 'PUT',
          body: JSON.stringify(policy),
        });
      } else {
        // No existing policy or only defaults, use POST to create
        console.log('Creating new password policy');
        result = await apiService.request<PasswordPolicy>('/settings/password-policy', {
          method: 'POST',
          body: JSON.stringify(policy),
        });
      }
      console.log('Password policy saved successfully with minimum length:', result.minLength);
      return result;
    } catch (error: unknown) {
      // If GET fails, assume no policy exists and use POST
      console.log('Creating password policy (fallback):', error instanceof Error ? error.message : 'Unknown error');
      try {
        const result = await apiService.request<PasswordPolicy>('/settings/password-policy', {
          method: 'POST',
          body: JSON.stringify(policy),
        });
        console.log('Password policy created successfully (fallback) with minimum length:', result.minLength);
        return result;
      } catch (createError) {
        console.error('Failed to save password policy:', createError);
        throw createError;
      }
    }
  }

  // Keep the old method name for backward compatibility but make it smarter
  async updatePasswordPolicy(policy: PasswordPolicy): Promise<PasswordPolicy> {
    return this.savePasswordPolicy(policy);
  }

  // Mock implementations for features not yet implemented in backend
  // These will be replaced with real API calls once the backend controllers are created

  async getRoles(): Promise<Role[]> {
    return apiService.request<Role[]>('/role');
  }

  async createRole(roleData: Partial<Role>): Promise<Role> {
    console.log('Creating role:', roleData.name, 'with permissions:', roleData.permissions?.length || 0);
    const createRequest = {
      name: roleData.name || '',
      description: roleData.description,
      permissions: roleData.permissions || []
    };
    try {
      const result = await apiService.request<Role>('/role', {
        method: 'POST',
        body: JSON.stringify(createRequest),
      });
      console.log('Role created successfully:', result.name, 'ID:', result.id);
      return result;
    } catch (error) {
      console.error('Failed to create role:', roleData.name, error);
      throw error;
    }
  }

  async updateRole(id: string, roleData: Partial<Role>): Promise<Role> {
    console.log('Updating role:', id, 'name:', roleData.name, 'permissions:', roleData.permissions?.length || 0);
    const updateRequest = {
      name: roleData.name || '',
      description: roleData.description,
      permissions: roleData.permissions || []
    };
    try {
      const result = await apiService.request<Role>(`/role/${id}`, {
        method: 'PUT',
        body: JSON.stringify(updateRequest),
      });
      console.log('Role updated successfully:', result.name, 'ID:', result.id);
      return result;
    } catch (error) {
      console.error('Failed to update role:', id, error);
      throw error;
    }
  }

  async deleteRole(id: string): Promise<void> {
    console.log('Deleting role:', id);
    try {
      await apiService.request(`/role/${id}`, {
        method: 'DELETE',
      });
      console.log('Role deleted successfully:', id);
    } catch (error) {
      console.error('Failed to delete role:', id, error);
      throw error;
    }
  }

  async getSecurityLogs(): Promise<SecurityLog[]> {
    return apiService.request<SecurityLog[]>('/securitylog');
  }

  async getAuditLogs(): Promise<AuditLog[]> {
    return apiService.request<AuditLog[]>('/auditlog');
  }

  // LDAP Testing
  async testLdapConnection(ldapSettings: {
    ldapServer?: string;
    ldapPort?: number;
    ldapBaseDn?: string;
    ldapBindDn?: string;
    ldapBindPassword?: string;
  }): Promise<{ success: boolean; message: string }> {
    console.log('Testing LDAP connection:', ldapSettings.ldapServer, 'port:', ldapSettings.ldapPort);
    try {
      const result = await apiService.request<{ success: boolean; message: string }>('/tenant/ldap/test', {
        method: 'POST',
        body: JSON.stringify({
          ldapServer: ldapSettings.ldapServer,
          ldapPort: ldapSettings.ldapPort,
          ldapBaseDn: ldapSettings.ldapBaseDn,
          ldapBindDn: ldapSettings.ldapBindDn,
          ldapBindPassword: ldapSettings.ldapBindPassword,
        }),
      });
      console.log('LDAP test result:', result.success ? 'SUCCESS' : 'FAILED', result.message);
      return result;
    } catch (error) {
      console.error('Failed to test LDAP connection:', error);
      throw error;
    }
  }
}

export const adminApiService = new AdminApiService();
