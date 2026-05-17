'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../../components/layout/dashboard-layout';
import { Button } from '../../../components/ui/button';
import { Badge } from '../../../components/ui/badge';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '../../../components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/dialog';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '../../../components/ui/form';
import { Input } from '../../../components/ui/input';
import { Switch } from '../../../components/ui/switch';
import { Textarea } from '../../../components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, Tenant } from '../../../services/admin-api.service';
import { fileUploadService } from '../../../services/file-upload.service';
import { useToast } from '../../../hooks/use-toast';
import { DataTable, Column } from '../../../components/admin/data-table';
import { TenantOverview } from '../../../components/admin/TenantOverview';
import { BulkTenantActions } from '../../../components/admin/BulkTenantActions';
import { ClientOnly } from '../../../components/ui/client-only';
import { 
  Building, 
  Settings, 
  Users, 
  Mail, 
  Phone, 
  MapPin, 
  Globe, 
  Palette,
  Image,
  Plus,
  Edit,
  Trash2,
  Search,
  Filter,
  Grid3X3,
  List,
  Upload,
  TestTube,
  HelpCircle,
  Shield
} from 'lucide-react';

const tenantSchema = z.object({
  name: z.string().min(1, 'Tenant name is required'),
  code: z.string().min(1, 'Tenant code is required'),
  description: z.string().optional(),
  status: z.enum(['Active', 'Inactive', 'Suspended']).optional(),
  
  // Branding
  logoUrl: z.string().optional(),
  primaryColor: z.string().optional(),
  secondaryColor: z.string().optional(),
  faviconUrl: z.string().optional(),
  coverImageUrl: z.string().optional(),
  
  // Contact Information
  domain: z.string().optional(),
  contactEmail: z.string().optional(),
  contactPhone: z.string().optional(),
  address: z.string().optional(),
  baseCurrency: z.string().optional(),
  baseCurrencyName: z.string().optional(),
  currencySymbol: z.string().optional(),
  currencyDecimalPlaces: z.number().optional(),
  
  // Subscription (optional for form)
  subscriptionStartDate: z.string().optional(),
  subscriptionEndDate: z.string().optional(),
  
  // Default tenant settings - IMPORTANT
  isDefaultForPublicUsers: z.boolean().optional(),
  isDefaultForInternalUsers: z.boolean().optional(),
  
  // Feature flags
  allowSelfRegistration: z.boolean().optional(),
  requireEmailVerification: z.boolean().optional(),
  userAudience: z.number().optional(),
  welcomeMessage: z.string().optional(),
  defaultPriority: z.number().optional(),
  enableAutoSelection: z.boolean().optional(),
  
  // LDAP Configuration (optional)
  ldapEnabled: z.boolean().optional(),
  ldapServer: z.string().optional(),
  ldapPort: z.number().optional(),
  ldapBaseDn: z.string().optional(),
  ldapBindDn: z.string().optional(),
  ldapBindPassword: z.string().optional(),
});

type TenantFormData = z.infer<typeof tenantSchema>;

type LdapDirectoryUser = {
  username: string;
  userPrincipalName: string;
  distinguishedName: string;
  displayName: string;
  email: string;
  firstName: string;
  lastName: string;
};

// Helper function to validate default tenant settings
const validateDefaultSettings = (formData: TenantFormData, tenants: Tenant[], editingTenant: Tenant | null): string | null => {
  if (formData.isDefaultForPublicUsers) {
    const existingPublicDefault = tenants.find(t => 
      t.isDefaultForPublicUsers && 
      (!editingTenant || t.id !== editingTenant.id)
    );
    if (existingPublicDefault) {
      return `Cannot set as default for public users. "${existingPublicDefault.name}" is already set as default for public users.`;
    }
  }
  
  if (formData.isDefaultForInternalUsers) {
    const existingInternalDefault = tenants.find(t => 
      t.isDefaultForInternalUsers && 
      (!editingTenant || t.id !== editingTenant.id)
    );
    if (existingInternalDefault) {
      return `Cannot set as default for internal users. "${existingInternalDefault.name}" is already set as default for internal users.`;
    }
  }
  
  return null;
};

export default function TenantManagementPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingTenant, setEditingTenant] = useState<Tenant | null>(null);
  const [deleteTenant, setDeleteTenant] = useState<Tenant | null>(null);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [searchQuery, setSearchQuery] = useState('');
  const [ldapEnabled, setLdapEnabled] = useState(false);
  const [isLdapUsersDialogOpen, setIsLdapUsersDialogOpen] = useState(false);
  const [ldapUsers, setLdapUsers] = useState<LdapDirectoryUser[]>([]);
  const [ldapUsersLoading, setLdapUsersLoading] = useState(false);
  const [ldapUsersQuery, setLdapUsersQuery] = useState('');
  const [ldapUsersMessage, setLdapUsersMessage] = useState<string>('');
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [faviconFile, setFaviconFile] = useState<File | null>(null);
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [selectedTenants, setSelectedTenants] = useState<Tenant[]>([]);
  const [viewingTenant, setViewingTenant] = useState<Tenant | null>(null);
  const [isOverviewDialogOpen, setIsOverviewDialogOpen] = useState(false);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const form = useForm<TenantFormData>({
    resolver: zodResolver(tenantSchema),
    defaultValues: {
      name: '',
      code: '',
      description: '',
      status: 'Active',
      
      // Branding
      logoUrl: '',
      primaryColor: '',
      secondaryColor: '',
      faviconUrl: '',
      coverImageUrl: '',
      
      // Contact Information
      domain: '',
      contactEmail: '',
      contactPhone: '',
      address: '',
      baseCurrency: 'GHS',
      baseCurrencyName: 'Ghana Cedi',
      currencySymbol: 'GH\u00a2',
      currencyDecimalPlaces: 2,
      
      // Subscription
      subscriptionStartDate: '',
      subscriptionEndDate: '',
      
      // Default tenant settings
      isDefaultForPublicUsers: false,
      isDefaultForInternalUsers: false,
      
      // Feature flags
      allowSelfRegistration: false,
      requireEmailVerification: false,
      userAudience: 2, // External by default
      welcomeMessage: '',
      defaultPriority: 10,
      enableAutoSelection: false,
      
      // LDAP Configuration
      ldapEnabled: false,
      ldapServer: '',
      ldapPort: 389,
      ldapBaseDn: '',
      ldapBindDn: '',
      ldapBindPassword: '',
    },
  });

  // Watch ldapEnabled changes to update field states
  useEffect(() => {
    const subscription = form.watch((value, { name }) => {
      if (name === 'ldapEnabled') {
        setLdapEnabled(Boolean(value.ldapEnabled));
      }
    });
    return () => subscription.unsubscribe();
  }, [form]);

  // Set initial LDAP enabled state
  useEffect(() => {
    setLdapEnabled(form.getValues('ldapEnabled') || false);
  }, [form, editingTenant]);

  // File upload handler
  const handleFileUpload = async (file: File | null, fieldName: 'logoUrl' | 'faviconUrl' | 'coverImageUrl') => {
    if (!file) return;
    
    try {
      // Validate file before upload
      const validation = fileUploadService.validateFile(file, 10); // 10MB max
      if (!validation.valid) {
        toast({
          title: 'Invalid File',
          description: validation.error,
          variant: 'destructive',
        });
        return;
      }
      
      // Show uploading toast
      toast({
        title: 'Uploading File',
        description: `Uploading ${file.name}...`,
      });
      
      // Determine image type and tenant ID
      const imageType = fieldName === 'logoUrl' ? 'logo' : 
                       fieldName === 'faviconUrl' ? 'favicon' : 'cover';
      const tenantId = editingTenant?.id;
      
      // Upload file to backend
      const uploadResult = await fileUploadService.uploadTenantBrandingImage(
        file,
        imageType,
        tenantId
      );
      
      // Set the permanent URL in the form
      form.setValue(fieldName, uploadResult.publicUrl);
      
      // Update file state for tracking
      if (fieldName === 'logoUrl') setLogoFile(file);
      else if (fieldName === 'faviconUrl') setFaviconFile(file);
      else if (fieldName === 'coverImageUrl') setCoverFile(file);
      
      // Show success toast
      toast({
        title: 'Upload Successful',
        description: `${file.name} uploaded successfully (${fileUploadService.formatFileSize(uploadResult.fileSize)})`,
      });
      
      // Trigger form validation
      form.trigger(fieldName);
      
    } catch (error) {
      console.error('File upload error:', error);
      toast({
        title: 'Upload Failed',
        description: `Failed to upload ${file.name}. Please try again.`,
        variant: 'destructive',
      });
    }
  };

  // Clear file upload
  const clearFileUpload = (fieldName: 'logoUrl' | 'faviconUrl' | 'coverImageUrl') => {
    // Clear file state
    if (fieldName === 'logoUrl') setLogoFile(null);
    else if (fieldName === 'faviconUrl') setFaviconFile(null);
    else if (fieldName === 'coverImageUrl') setCoverFile(null);
    
    // Clear form field
    form.setValue(fieldName, '');
    form.trigger(fieldName);
  };

  // Fetch tenants data
  const { data: tenants = [], isLoading } = useQuery({
    queryKey: ['admin-tenants'],
    queryFn: () => adminApiService.getTenants(),
  });

  // Create/Update tenant mutation
  const createTenantMutation = useMutation({
    mutationFn: (tenantData: TenantFormData) => {
      if (editingTenant) {
        const updateData = {
          name: tenantData.name,
          description: tenantData.description,
          status: tenantData.status as 'Active' | 'Inactive' | 'Suspended' | undefined,
          
          // Branding
          logoUrl: tenantData.logoUrl,
          primaryColor: tenantData.primaryColor,
          secondaryColor: tenantData.secondaryColor,
          faviconUrl: tenantData.faviconUrl,
          coverImageUrl: tenantData.coverImageUrl,
          
          // Contact Information
          domain: tenantData.domain,
          contactEmail: tenantData.contactEmail,
          contactPhone: tenantData.contactPhone,
          address: tenantData.address,
          baseCurrency: tenantData.baseCurrency,
          baseCurrencyName: tenantData.baseCurrencyName,
          currencySymbol: tenantData.currencySymbol,
          currencyDecimalPlaces: tenantData.currencyDecimalPlaces,
          
          // Subscription
          subscriptionStartDate: tenantData.subscriptionStartDate ? new Date(tenantData.subscriptionStartDate) : undefined,
          subscriptionEndDate: tenantData.subscriptionEndDate ? new Date(tenantData.subscriptionEndDate) : undefined,
          
          // Default tenant settings
          isDefaultForPublicUsers: tenantData.isDefaultForPublicUsers,
          isDefaultForInternalUsers: tenantData.isDefaultForInternalUsers,
          
          // Feature flags
          allowSelfRegistration: tenantData.allowSelfRegistration,
          requireEmailVerification: tenantData.requireEmailVerification,
          userAudience: tenantData.userAudience,
          welcomeMessage: tenantData.welcomeMessage,
          defaultPriority: tenantData.defaultPriority,
          enableAutoSelection: tenantData.enableAutoSelection,
          
          // LDAP Configuration
          ldapEnabled: tenantData.ldapEnabled,
          ldapServer: tenantData.ldapServer,
          ldapPort: tenantData.ldapPort,
          ldapBaseDn: tenantData.ldapBaseDn,
          ldapBindDn: tenantData.ldapBindDn,
          ldapBindPassword: tenantData.ldapBindPassword,
        };
        return adminApiService.updateTenant(editingTenant.id, updateData);
      } else {
        const createData = {
          name: tenantData.name,
          code: tenantData.code,
          description: tenantData.description,
          status: tenantData.status as 'Active' | 'Inactive' | 'Suspended' | undefined,
          
          // Branding
          logoUrl: tenantData.logoUrl,
          primaryColor: tenantData.primaryColor,
          secondaryColor: tenantData.secondaryColor,
          faviconUrl: tenantData.faviconUrl,
          coverImageUrl: tenantData.coverImageUrl,
          
          // Contact Information
          domain: tenantData.domain,
          contactEmail: tenantData.contactEmail,
          contactPhone: tenantData.contactPhone,
          address: tenantData.address,
          baseCurrency: tenantData.baseCurrency,
          baseCurrencyName: tenantData.baseCurrencyName,
          currencySymbol: tenantData.currencySymbol,
          currencyDecimalPlaces: tenantData.currencyDecimalPlaces,
          
          // Subscription
          subscriptionStartDate: tenantData.subscriptionStartDate ? new Date(tenantData.subscriptionStartDate) : undefined,
          subscriptionEndDate: tenantData.subscriptionEndDate ? new Date(tenantData.subscriptionEndDate) : undefined,
          
          // Default tenant settings
          isDefaultForPublicUsers: tenantData.isDefaultForPublicUsers,
          isDefaultForInternalUsers: tenantData.isDefaultForInternalUsers,
          
          // Feature flags
          allowSelfRegistration: tenantData.allowSelfRegistration,
          requireEmailVerification: tenantData.requireEmailVerification,
          userAudience: tenantData.userAudience,
          welcomeMessage: tenantData.welcomeMessage,
          defaultPriority: tenantData.defaultPriority,
          enableAutoSelection: tenantData.enableAutoSelection,
          
          // LDAP Configuration
          ldapEnabled: tenantData.ldapEnabled,
          ldapServer: tenantData.ldapServer,
          ldapPort: tenantData.ldapPort,
          ldapBaseDn: tenantData.ldapBaseDn,
          ldapBindDn: tenantData.ldapBindDn,
          ldapBindPassword: tenantData.ldapBindPassword,
        };
        return adminApiService.createTenant(createData);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-tenants'] });
      setIsDialogOpen(false);
      setEditingTenant(null);
      form.reset();
      toast({
        title: 'Success',
        description: `Tenant ${editingTenant ? 'updated' : 'created'} successfully`,
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: `Failed to ${editingTenant ? 'update' : 'create'} tenant`,
        variant: 'destructive',
      });
    },
  });

  // Delete tenant mutation
  const deleteTenantMutation = useMutation({
    mutationFn: (tenantId: string) => adminApiService.deleteTenant(tenantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-tenants'] });
      setDeleteTenant(null);
      toast({
        title: 'Success',
        description: 'Tenant deleted successfully',
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to delete tenant',
        variant: 'destructive',
      });
    },
  });

  const handleAdd = () => {
    setEditingTenant(null);
    form.reset();
    setIsDialogOpen(true);
  };

  const handleEdit = (tenant: Tenant) => {
    setEditingTenant(tenant);
    form.reset({
      name: tenant.name,
      code: tenant.code,
      description: tenant.description || '',
      status: tenant.status,
      
      // Branding
      logoUrl: tenant.logoUrl || '',
      primaryColor: tenant.primaryColor || '',
      secondaryColor: tenant.secondaryColor || '',
      faviconUrl: tenant.faviconUrl || '',
      coverImageUrl: tenant.coverImageUrl || '',
      
      // Contact Information
      domain: tenant.domain || '',
      contactEmail: tenant.contactEmail || '',
      contactPhone: tenant.contactPhone || '',
      address: tenant.address || '',
      baseCurrency: tenant.baseCurrency || 'GHS',
      baseCurrencyName: tenant.baseCurrencyName || '',
      currencySymbol: tenant.currencySymbol || '',
      currencyDecimalPlaces: tenant.currencyDecimalPlaces || 2,
      
      // Subscription
      subscriptionStartDate: tenant.subscriptionStartDate ? new Date(tenant.subscriptionStartDate).toISOString().slice(0, 16) : '',
      subscriptionEndDate: tenant.subscriptionEndDate ? new Date(tenant.subscriptionEndDate).toISOString().slice(0, 16) : '',
      
      // Default tenant settings
      isDefaultForPublicUsers: tenant.isDefaultForPublicUsers,
      isDefaultForInternalUsers: tenant.isDefaultForInternalUsers,
      
      // Feature flags
      allowSelfRegistration: tenant.allowSelfRegistration,
      requireEmailVerification: tenant.requireEmailVerification,
      userAudience: tenant.userAudience,
      welcomeMessage: tenant.welcomeMessage || '',
      defaultPriority: tenant.defaultPriority,
      enableAutoSelection: tenant.enableAutoSelection,
      
      // LDAP Configuration
      ldapEnabled: tenant.ldapEnabled,
      ldapServer: tenant.ldapServer || '',
      ldapPort: tenant.ldapPort || 389,
      ldapBaseDn: tenant.ldapBaseDn || '',
      ldapBindDn: tenant.ldapBindDn || '',
      ldapBindPassword: tenant.ldapBindPassword || '',
    });
    setIsDialogOpen(true);
  };

  const handleDelete = (tenant: Tenant) => {
    setDeleteTenant(tenant);
  };

  const handleView = (tenant: Tenant) => {
    setViewingTenant(tenant);
    setIsOverviewDialogOpen(true);
  };

  const handleSelectionChange = (selectedTenants: Tenant[]) => {
    console.log('Selected tenants:', selectedTenants.map(t => t.name));
    setSelectedTenants(selectedTenants);
  };

  const handleBulkActionComplete = () => {
    setSelectedTenants([]);
    queryClient.invalidateQueries({ queryKey: ['admin-tenants'] });
  };

  // Test LDAP connection
  const testLdapConnection = async () => {
    const formData = form.getValues();
    
    if (!formData.ldapServer) {
      toast({
        title: 'LDAP Test Failed',
        description: 'LDAP server must be specified',
        variant: 'destructive',
      });
      return;
    }
    
    try {
      toast({
        title: 'Testing LDAP Connection',
        description: 'Please wait...',
      });
      
      // Call the real backend LDAP test endpoint
      const testData = {
        ldapServer: formData.ldapServer,
        ldapPort: formData.ldapPort || 389,
        ldapBaseDn: formData.ldapBaseDn || '',
        ldapBindDn: formData.ldapBindDn || '',
        ldapBindPassword: formData.ldapBindPassword || ''
      };
      
      const response = await adminApiService.testLdapConnection(testData);
      
      if (response.success) {
        toast({
          title: 'LDAP Test Successful',
          description: response.message || 'Connection to LDAP server established successfully',
          variant: 'default',
        });
      } else {
        toast({
          title: 'LDAP Test Failed',
          description: response.message || 'Failed to connect to LDAP server. Please check your settings.',
          variant: 'destructive',
        });
      }
    } catch (error: any) {
      toast({
        title: 'LDAP Test Failed',
        description: error.message || 'Failed to test LDAP connection. Please check your settings.',
        variant: 'destructive',
      });
    }
  };

  const loadLdapUsers = async (query: string, limit = 200) => {
    const formData = form.getValues();

    if (!formData.ldapServer) {
      toast({
        title: 'LDAP Users',
        description: 'LDAP server must be specified',
        variant: 'destructive',
      });
      return;
    }

    if (!formData.ldapBaseDn) {
      toast({
        title: 'LDAP Users',
        description: 'Base DN must be specified',
        variant: 'destructive',
      });
      return;
    }

    try {
      setLdapUsersLoading(true);
      setLdapUsersMessage('Loading...');

      const response = await adminApiService.listLdapUsers({
        ldapServer: formData.ldapServer,
        ldapPort: formData.ldapPort || 389,
        ldapBaseDn: formData.ldapBaseDn,
        ldapBindDn: formData.ldapBindDn || '',
        ldapBindPassword: formData.ldapBindPassword || '',
        query,
        limit
      });

      setLdapUsers((response.users || []) as LdapDirectoryUser[]);
      setLdapUsersMessage(response.message || `Found ${(response.users || []).length} user(s)`);

      if (!response.success) {
        toast({
          title: 'LDAP Users',
          description: response.message || 'Failed to list LDAP users',
          variant: 'destructive',
        });
      }
    } catch (error: any) {
      setLdapUsers([]);
      setLdapUsersMessage('');
      toast({
        title: 'LDAP Users',
        description: error.message || 'Failed to list LDAP users',
        variant: 'destructive',
      });
    } finally {
      setLdapUsersLoading(false);
    }
  };

  const openLdapUsersDialog = async () => {
    setIsLdapUsersDialogOpen(true);
    setLdapUsersQuery('');
    await loadLdapUsers('', 200);
  };
  
  const onSubmit = (data: TenantFormData) => {
    console.log('Form submitted with data:', data);
    
    // Convert tenant code to lowercase on first save (creation)
    if (!editingTenant && data.code) {
      data.code = data.code.toLowerCase();
    }
    
    // Get all form values to ensure we capture everything
    const formValues = form.getValues();
    
    console.log('All form values:', formValues);
    console.log('🗺️ FORM STATUS VALUE:', formValues.status);
    console.log('🗺️ FORM LDAP ENABLED:', formValues.ldapEnabled);
    console.log('🗺️ FORM DEFAULT PUBLIC:', formValues.isDefaultForPublicUsers);
    console.log('🗺️ FORM USER AUDIENCE:', formValues.userAudience, typeof formValues.userAudience);
    
    // Process the data with proper defaults
    const processedData = {
      // Basic fields (required)
      name: formValues.name,
      code: formValues.code,
      description: formValues.description || '',
      status: formValues.status || 'Active',
      
      // Branding
      logoUrl: formValues.logoUrl || '',
      primaryColor: formValues.primaryColor || '',
      secondaryColor: formValues.secondaryColor || '',
      faviconUrl: formValues.faviconUrl || '',
      coverImageUrl: formValues.coverImageUrl || '',
      
      // Contact Information
      domain: formValues.domain || '',
      contactEmail: formValues.contactEmail || '',
      contactPhone: formValues.contactPhone || '',
      address: formValues.address || '',
      baseCurrency: (formValues.baseCurrency || 'GHS').toUpperCase(),
      baseCurrencyName: formValues.baseCurrencyName || '',
      currencySymbol: formValues.currencySymbol || '',
      currencyDecimalPlaces: Number(formValues.currencyDecimalPlaces) || 2,
      
      // Subscription - handle date conversion properly
      subscriptionStartDate: formValues.subscriptionStartDate ? new Date(formValues.subscriptionStartDate).toISOString() : undefined,
      subscriptionEndDate: formValues.subscriptionEndDate ? new Date(formValues.subscriptionEndDate).toISOString() : undefined,
      
      // Default tenant settings
      isDefaultForPublicUsers: Boolean(formValues.isDefaultForPublicUsers),
      isDefaultForInternalUsers: Boolean(formValues.isDefaultForInternalUsers),
      
      // Feature flags
      allowSelfRegistration: Boolean(formValues.allowSelfRegistration),
      requireEmailVerification: Boolean(formValues.requireEmailVerification),
      userAudience: Number(formValues.userAudience) || 2,
      welcomeMessage: formValues.welcomeMessage || '',
      defaultPriority: Number(formValues.defaultPriority) || 10,
      enableAutoSelection: Boolean(formValues.enableAutoSelection),
      
      // LDAP Configuration
      ldapEnabled: Boolean(formValues.ldapEnabled),
      ldapServer: formValues.ldapServer || '',
      ldapPort: Number(formValues.ldapPort) || 389,
      ldapBaseDn: formValues.ldapBaseDn || '',
      ldapBindDn: formValues.ldapBindDn || '',
      ldapBindPassword: formValues.ldapBindPassword || '',
    };
    
    console.log('Processed data:', processedData);
    createTenantMutation.mutate(processedData);
  };

  const columns: Column<Tenant>[] = [
    {
      key: 'name',
      label: 'Tenant Name',
      sortable: true,
      render: (name: string, tenant: Tenant) => (
        <div className="flex items-center gap-2">
          <Building className="h-4 w-4 text-muted-foreground" />
          <div>
            <div className="font-medium">{name}</div>
            <div className="text-sm text-muted-foreground">{tenant.code}</div>
          </div>
        </div>
      ),
    },
    {
      key: 'code',
      label: 'Tenant Code',
      sortable: true,
      render: (code: string) => (
        <Badge variant="outline" className="font-mono">
          {code}
        </Badge>
      ),
    },
    {
      key: 'isActive',
      label: 'Status',
      render: (isActive: boolean) => (
        <Badge variant={isActive ? 'default' : 'secondary'}>
          {isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      key: 'contactEmail',
      label: 'Contact',
      render: (contactEmail: string, tenant: Tenant) => (
        <div className="flex flex-col gap-1">
          {contactEmail && (
            <span className="text-sm">{contactEmail}</span>
          )}
          {tenant.contactPhone && (
            <span className="text-xs text-muted-foreground">{tenant.contactPhone}</span>
          )}
          {!contactEmail && !tenant.contactPhone && (
            <span className="text-xs text-muted-foreground">No contact info</span>
          )}
        </div>
      ),
    },
    {
      key: 'createdAt',
      label: 'Created',
      sortable: true,
      render: (createdAt: Date) => new Date(createdAt).toLocaleDateString(),
    },
    {
      key: 'updatedAt',
      label: 'Updated',
      sortable: true,
      render: (updatedAt: Date) => new Date(updatedAt).toLocaleDateString(),
    },
    {
      key: 'description',
      label: 'Description',
      render: (description: string) => (
        <span className="text-sm text-muted-foreground">
          {description || 'No description'}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Tenant & Company Settings</h1>
            <p className="text-muted-foreground">
              Manage enterprise company identity, tenant access, and payroll header settings
            </p>
          </div>
        </div>

        {selectedTenants.length > 0 && (
          <BulkTenantActions
            selectedTenantIds={selectedTenants.map(t => t.id)}
            tenants={tenants}
            onActionComplete={handleBulkActionComplete}
          />
        )}

        <DataTable
          title="Tenants"
          description="Manage enterprise tenant organizations and company profile settings"
          data={tenants}
          columns={columns}
          loading={isLoading}
          searchPlaceholder="Search tenants..."
          onAdd={handleAdd}
          onEdit={handleEdit}
          onDelete={handleDelete}
          onView={handleView}
          selectable={true}
          onSelectionChange={handleSelectionChange}
          exportable={true}
          exportFileName="tenants_export.csv"
        />

        {/* Add/Edit Tenant Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
          <DialogContent className="w-[1000px] h-[720px] max-w-[90vw] max-h-[90vh] overflow-hidden flex flex-col">
            <DialogHeader>
              <DialogTitle>
                {editingTenant ? 'Edit Tenant' : 'Add New Tenant'}
              </DialogTitle>
              <DialogDescription>
                {editingTenant 
                  ? 'Update tenant information and configuration' 
                  : 'Create a new tenant organization'}
              </DialogDescription>
            </DialogHeader>

            <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col h-full">
              <Tabs defaultValue="basic" className="flex flex-col flex-1 overflow-hidden">
                <TabsList className="grid w-full grid-cols-6 flex-shrink-0">
                  <TabsTrigger value="basic">Basic</TabsTrigger>
                  <TabsTrigger value="company">Company</TabsTrigger>
                  <TabsTrigger value="settings">Settings</TabsTrigger>
                  <TabsTrigger value="branding">Branding</TabsTrigger>
                  <TabsTrigger value="ldap">LDAP</TabsTrigger>
                  <TabsTrigger value="features">Features</TabsTrigger>
                </TabsList>

                <div className="flex-1 overflow-y-auto mt-6">

                  {/* Basic Information Tab */}
                  <TabsContent value="basic" className="space-y-6 mt-0">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="name"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Tenant Name *</FormLabel>
                          <FormControl>
                            <Input placeholder="Enter tenant name" {...field} />
                          </FormControl>
                          <FormDescription>
                            Display name for the organization
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    <FormField
                      control={form.control}
                      name="code"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Tenant Code *</FormLabel>
                          <FormControl>
                            <Input 
                              placeholder="tenant-code" 
                              {...field}
                              disabled={!!editingTenant}
                              onChange={(e) => {
                                if (!editingTenant) {
                                  const value = e.target.value
                                    .toLowerCase()
                                    .replace(/\s+/g, '-')
                                    .replace(/[^a-z0-9-]/g, '');
                                  field.onChange(value);
                                }
                              }}
                            />
                          </FormControl>
                          <FormDescription>
                            {editingTenant 
                              ? 'Code cannot be changed after creation' 
                              : 'Unique identifier (lowercase, no spaces)'}
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>

                  <FormField
                    control={form.control}
                    name="description"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Description</FormLabel>
                        <FormControl>
                          <Textarea
                            placeholder="Enter tenant description"
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="status"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Status</FormLabel>
                        <ClientOnly fallback={
                          <div className="h-10 bg-muted/50 rounded-md border" />
                        }>
                          <Select
                            onValueChange={field.onChange}
                            defaultValue={field.value}
                          >
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Select tenant status" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="Active">Active</SelectItem>
                              <SelectItem value="Inactive">Inactive</SelectItem>
                              <SelectItem value="Suspended">Suspended</SelectItem>
                            </SelectContent>
                          </Select>
                        </ClientOnly>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </TabsContent>

                  {/* Company Settings Tab */}
                  <TabsContent value="company" className="space-y-6 mt-0">
                    <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1.4fr)_minmax(280px,0.6fr)] gap-6">
                      <div className="space-y-5">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          <FormField
                            control={form.control}
                            name="contactEmail"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Company Email</FormLabel>
                                <FormControl>
                                  <Input placeholder="payroll@company.com" {...field} />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />

                          <FormField
                            control={form.control}
                            name="contactPhone"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Company Phone</FormLabel>
                                <FormControl>
                                  <Input placeholder="+233 00 000 0000" {...field} />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          <FormField
                            control={form.control}
                            name="domain"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Company Domain</FormLabel>
                                <FormControl>
                                  <Input placeholder="company.com" {...field} />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />

                          <FormField
                            control={form.control}
                            name="baseCurrency"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Base Currency</FormLabel>
                                <FormControl>
                                  <Input
                                    maxLength={3}
                                    placeholder="GHS"
                                    {...field}
                                    onChange={(event) => field.onChange(event.target.value.toUpperCase())}
                                  />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />
                        </div>

                        <FormField
                          control={form.control}
                          name="address"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>Company Address</FormLabel>
                              <FormControl>
                                <Textarea placeholder="Postal and physical address used on payroll documents" {...field} />
                              </FormControl>
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                          <FormField
                            control={form.control}
                            name="baseCurrencyName"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Currency Name</FormLabel>
                                <FormControl>
                                  <Input placeholder="Ghana Cedi" {...field} />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />

                          <FormField
                            control={form.control}
                            name="currencySymbol"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Currency Symbol</FormLabel>
                                <FormControl>
                                  <Input placeholder="GHS" {...field} />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />

                          <FormField
                            control={form.control}
                            name="currencyDecimalPlaces"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Decimal Places</FormLabel>
                                <FormControl>
                                  <Input
                                    type="number"
                                    min="0"
                                    max="6"
                                    {...field}
                                    onChange={(event) => field.onChange(Number(event.target.value))}
                                  />
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />
                        </div>
                      </div>

                      <div className="rounded-lg border bg-muted/30 p-4">
                        <div className="mb-3 flex items-center gap-2 text-sm font-medium">
                          <Building className="h-4 w-4" />
                          Payroll Header Preview
                        </div>
                        <div className="rounded-md border bg-background px-4 py-5 text-center text-sm">
                          <div className="font-semibold">{form.watch('name') || 'Company Name'}</div>
                          <div className="mt-1 text-xs text-muted-foreground">{form.watch('address') || 'Company address'}</div>
                          <div className="mt-1 text-xs text-muted-foreground">{form.watch('contactPhone') || 'Company phone'}</div>
                          <div className="mt-3 text-sm font-semibold underline">PAYSLIP</div>
                        </div>
                      </div>
                    </div>
                  </TabsContent>

                  {/* Settings Tab */}
                  <TabsContent value="settings" className="space-y-6 mt-0">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="isDefaultForPublicUsers"
                      render={({ field }) => (
                        <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                          <div className="space-y-0.5">
                            <FormLabel className="text-base">Default for Public Users</FormLabel>
                            <FormDescription>
                              Use as default tenant for public users
                            </FormDescription>
                          </div>
                          <FormControl>
                            <Switch
                              checked={field.value}
                              onCheckedChange={field.onChange}
                            />
                          </FormControl>
                        </FormItem>
                      )}
                    />

                    <FormField
                      control={form.control}
                      name="isDefaultForInternalUsers"
                      render={({ field }) => (
                        <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                          <div className="space-y-0.5">
                            <FormLabel className="text-base">Default for Internal Users</FormLabel>
                            <FormDescription>
                              Use as default tenant for internal users
                            </FormDescription>
                          </div>
                          <FormControl>
                            <Switch
                              checked={field.value}
                              onCheckedChange={field.onChange}
                            />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="domain"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Domain</FormLabel>
                          <FormControl>
                            <Input
                              placeholder="tenant.yourdomain.com"
                              {...field}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    
                    <FormField
                      control={form.control}
                      name="userAudience"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>User Audience</FormLabel>
                          <ClientOnly fallback={
                            <div className="h-10 bg-muted/50 rounded-md border" />
                          }>
                            <Select
                              onValueChange={(value) => field.onChange(parseInt(value))}
                              defaultValue={field.value?.toString() || '2'}
                            >
                              <FormControl>
                                <SelectTrigger>
                                  <SelectValue placeholder="Select user audience" />
                                </SelectTrigger>
                              </FormControl>
                              <SelectContent>
                                <SelectItem value="1">Internal</SelectItem>
                                <SelectItem value="2">External</SelectItem>
                                <SelectItem value="3">Both</SelectItem>
                              </SelectContent>
                            </Select>
                          </ClientOnly>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="subscriptionStartDate"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Subscription Start</FormLabel>
                          <FormControl>
                            <Input
                              type="datetime-local"
                              {...field}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    
                    <FormField
                      control={form.control}
                      name="subscriptionEndDate"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Subscription End</FormLabel>
                          <FormControl>
                            <Input
                              type="datetime-local"
                              {...field}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>
                </TabsContent>

                  {/* Branding Tab */}
                  <TabsContent value="branding" className="space-y-6 mt-0">
                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                      {/* Branding Form */}
                      <div className="lg:col-span-2 space-y-6">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          {/* Logo Upload */}
                          <FormField
                            control={form.control}
                            name="logoUrl"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Logo</FormLabel>
                                <FormControl>
                                  <div className="space-y-2">
                                    <div className="flex items-center gap-3">
                                      {field.value && !field.value.startsWith('file://') && (
                                        <div className="w-12 h-12 border rounded overflow-hidden bg-muted">
                                          <img 
                                            src={field.value} 
                                            alt="Logo preview" 
                                            className="w-full h-full object-contain"
                                            onError={(e) => {
                                              e.currentTarget.style.display = 'none';
                                            }}
                                          />
                                        </div>
                                      )}
                                      {field.value && field.value.startsWith('file://') && (
                                        <div className="w-12 h-12 border rounded overflow-hidden bg-muted flex items-center justify-center">
                                          <Image className="h-6 w-6 text-muted-foreground" />
                                        </div>
                                      )}
                                      <div className="flex-1 flex gap-2">
                                        <Input
                                          type="file"
                                          accept="image/*"
                                          onChange={(e) => handleFileUpload(e.target.files?.[0] || null, 'logoUrl')}
                                          className="hidden"
                                          id="logo-upload"
                                        />
                                        <Button 
                                          type="button" 
                                          variant="outline"
                                          size="sm"
                                          onClick={() => document.getElementById('logo-upload')?.click()}
                                        >
                                          <Upload className="h-4 w-4 mr-2" />
                                          {field.value ? 'Change' : 'Upload'}
                                        </Button>
                                        {field.value && (
                                          <Button 
                                            type="button" 
                                            variant="outline"
                                            size="sm"
                                            onClick={() => clearFileUpload('logoUrl')}
                                          >
                                            <Trash2 className="h-4 w-4" />
                                          </Button>
                                        )}
                                      </div>
                                    </div>
                                    <Input
                                      placeholder="https://example.com/logo.png"
                                      {...field}
                                    />
                                  </div>
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />

                          {/* Favicon Upload */}
                          <FormField
                            control={form.control}
                            name="faviconUrl"
                            render={({ field }) => (
                              <FormItem>
                                <FormLabel>Favicon</FormLabel>
                                <FormControl>
                                  <div className="space-y-2">
                                    <div className="flex items-center gap-3">
                                      {field.value && !field.value.startsWith('file://') && (
                                        <div className="w-8 h-8 border rounded overflow-hidden bg-muted">
                                          <img 
                                            src={field.value} 
                                            alt="Favicon preview" 
                                            className="w-full h-full object-contain"
                                            onError={(e) => {
                                              e.currentTarget.style.display = 'none';
                                            }}
                                          />
                                        </div>
                                      )}
                                      {field.value && field.value.startsWith('file://') && (
                                        <div className="w-8 h-8 border rounded overflow-hidden bg-muted flex items-center justify-center">
                                          <Image className="h-4 w-4 text-muted-foreground" />
                                        </div>
                                      )}
                                      <div className="flex-1 flex gap-2">
                                        <Input
                                          type="file"
                                          accept="image/*"
                                          onChange={(e) => handleFileUpload(e.target.files?.[0] || null, 'faviconUrl')}
                                          className="hidden"
                                          id="favicon-upload"
                                        />
                                        <Button 
                                          type="button" 
                                          variant="outline"
                                          size="sm"
                                          onClick={() => document.getElementById('favicon-upload')?.click()}
                                        >
                                          <Upload className="h-4 w-4 mr-2" />
                                          {field.value ? 'Change' : 'Upload'}
                                        </Button>
                                        {field.value && (
                                          <Button 
                                            type="button" 
                                            variant="outline"
                                            size="sm"
                                            onClick={() => clearFileUpload('faviconUrl')}
                                          >
                                            <Trash2 className="h-4 w-4" />
                                          </Button>
                                        )}
                                      </div>
                                    </div>
                                    <Input
                                      placeholder="https://example.com/favicon.ico"
                                      {...field}
                                    />
                                  </div>
                                </FormControl>
                                <FormMessage />
                              </FormItem>
                            )}
                          />
                        </div>

                        {/* Cover Image Upload */}
                        <FormField
                          control={form.control}
                          name="coverImageUrl"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>Cover Image</FormLabel>
                              <FormControl>
                                <div className="space-y-2">
                                  <div className="flex items-center gap-3">
                                    {field.value && !field.value.startsWith('file://') && (
                                      <div className="w-20 h-12 border rounded overflow-hidden bg-muted">
                                        <img 
                                          src={field.value} 
                                          alt="Cover preview" 
                                          className="w-full h-full object-cover"
                                          onError={(e) => {
                                            e.currentTarget.style.display = 'none';
                                          }}
                                        />
                                      </div>
                                    )}
                                    {field.value && field.value.startsWith('file://') && (
                                      <div className="w-20 h-12 border rounded overflow-hidden bg-muted flex items-center justify-center">
                                        <Image className="h-6 w-6 text-muted-foreground" />
                                      </div>
                                    )}
                                    <div className="flex-1 flex gap-2">
                                      <Input
                                        type="file"
                                        accept="image/*"
                                        onChange={(e) => handleFileUpload(e.target.files?.[0] || null, 'coverImageUrl')}
                                        className="hidden"
                                        id="cover-upload"
                                      />
                                      <Button 
                                        type="button" 
                                        variant="outline"
                                        size="sm"
                                        onClick={() => document.getElementById('cover-upload')?.click()}
                                      >
                                        <Upload className="h-4 w-4 mr-2" />
                                        {field.value ? 'Change' : 'Upload Cover'}
                                      </Button>
                                      {field.value && (
                                        <Button 
                                          type="button" 
                                          variant="outline"
                                          size="sm"
                                          onClick={() => clearFileUpload('coverImageUrl')}
                                        >
                                          <Trash2 className="h-4 w-4" />
                                        </Button>
                                      )}
                                    </div>
                                  </div>
                                  <Input
                                    placeholder="https://example.com/cover.jpg"
                                    {...field}
                                  />
                                </div>
                              </FormControl>
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        {/* Color Theme */}
                        <div className="space-y-4">
                          <div className="flex items-center gap-2">
                            <Palette className="h-4 w-4" />
                            <h3 className="text-lg font-medium">Brand Colors</h3>
                          </div>

                          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <FormField
                              control={form.control}
                              name="primaryColor"
                              render={({ field }) => (
                                <FormItem>
                                  <FormLabel>Primary Color</FormLabel>
                                  <FormControl>
                                    <div className="flex gap-2">
                                      <Input
                                        type="color"
                                        {...field}
                                        className="w-12 h-10 p-1 border rounded"
                                      />
                                      <Input
                                        placeholder="#3B82F6"
                                        {...field}
                                        className="flex-1"
                                      />
                                    </div>
                                  </FormControl>
                                  <FormMessage />
                                </FormItem>
                              )}
                            />

                            <FormField
                              control={form.control}
                              name="secondaryColor"
                              render={({ field }) => (
                                <FormItem>
                                  <FormLabel>Secondary Color</FormLabel>
                                  <FormControl>
                                    <div className="flex gap-2">
                                      <Input
                                        type="color"
                                        {...field}
                                        className="w-12 h-10 p-1 border rounded"
                                      />
                                      <Input
                                        placeholder="#6B7280"
                                        {...field}
                                        className="flex-1"
                                      />
                                    </div>
                                  </FormControl>
                                  <FormMessage />
                                </FormItem>
                              )}
                            />
                          </div>
                        </div>
                      </div>

                      {/* Help Panel */}
                      <div className="space-y-4">
                        <div className="bg-green-50 border border-green-200 rounded-lg p-3 space-y-3">
                          <h4 className="font-medium text-sm text-green-900">✓ How to Add Images</h4>
                          
                          <div className="space-y-2 text-xs text-green-700">
                            <div className="space-y-1">
                              <p><strong>Option 1:</strong> Enter URL in text field</p>
                              <p className="text-green-600">• Paste direct image URL (https://...)</p>
                            </div>
                            
                            <div className="space-y-1">
                              <p><strong>Option 2:</strong> Upload local file 🎆</p>
                              <p className="text-green-600">• Click "Upload" button and select file</p>
                              <p className="text-green-600">• File will be uploaded to server storage</p>
                              <p className="text-green-600">• Max size: 10MB per file</p>
                            </div>
                          </div>
                        </div>
                        
                        <div className="bg-muted/50 rounded-lg p-3 space-y-3">
                          <h4 className="font-medium text-sm text-foreground">Image Guidelines</h4>
                          
                          <div className="space-y-2 text-xs">
                            <div>
                              <h5 className="font-medium text-foreground mb-1">Logo</h5>
                              <div className="text-muted-foreground space-y-0.5">
                                <p>Recommended: 200x60px</p>
                                <p>Format: PNG/JPG with transparency</p>
                              </div>
                            </div>

                            <div>
                              <h5 className="font-medium text-foreground mb-1">Favicon</h5>
                              <div className="text-muted-foreground space-y-0.5">
                                <p>Recommended: 32x32px</p>
                                <p>Format: ICO/PNG</p>
                              </div>
                            </div>

                            <div>
                              <h5 className="font-medium text-foreground mb-1">Cover Image</h5>
                              <div className="text-muted-foreground space-y-0.5">
                                <p>Recommended: 1920x1080px</p>
                                <p>Format: JPG/PNG</p>
                              </div>
                            </div>
                          </div>
                        </div>

                      </div>
                    </div>
                  </TabsContent>

                  {/* LDAP Tab */}
                  <TabsContent value="ldap" className="space-y-6 mt-0">
                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                      {/* LDAP Configuration Form */}
                      <div className="lg:col-span-2">
                        <div className="space-y-6">
                          <FormField
                            control={form.control}
                            name="ldapEnabled"
                            render={({ field }) => (
                              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                                <div className="space-y-0.5">
                                  <FormLabel className="text-base">Enable LDAP Authentication</FormLabel>
                                  <FormDescription>
                                    Use LDAP directory service for user authentication
                                  </FormDescription>
                                </div>
                                <FormControl>
                                  <Switch
                                    checked={field.value}
                                    onCheckedChange={field.onChange}
                                  />
                                </FormControl>
                              </FormItem>
                            )}
                          />

                          {ldapEnabled && (
                            <div className="space-y-4">
                              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <FormField
                                  control={form.control}
                                  name="ldapServer"
                                  render={({ field }) => (
                                    <FormItem>
                                      <FormLabel>LDAP Server</FormLabel>
                                      <FormControl>
                                        <Input placeholder="ldap.company.com" {...field} />
                                      </FormControl>
                                      <FormMessage />
                                    </FormItem>
                                  )}
                                />
                                
                                <FormField
                                  control={form.control}
                                  name="ldapPort"
                                  render={({ field }) => (
                                    <FormItem>
                                      <FormLabel>Port</FormLabel>
                                      <FormControl>
                                        <Input
                                          type="number"
                                          placeholder="389"
                                          {...field}
                                          onChange={(e) => field.onChange(parseInt(e.target.value) || 389)}
                                        />
                                      </FormControl>
                                      <FormMessage />
                                    </FormItem>
                                  )}
                                />
                              </div>

                              <FormField
                                control={form.control}
                                name="ldapBaseDn"
                                render={({ field }) => (
                                  <FormItem>
                                    <FormLabel>Base DN</FormLabel>
                                    <FormControl>
                                      <Input placeholder="dc=company,dc=com" {...field} />
                                    </FormControl>
                                    <FormMessage />
                                  </FormItem>
                                )}
                              />

                              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <FormField
                                  control={form.control}
                                  name="ldapBindDn"
                                  render={({ field }) => (
                                    <FormItem>
                                      <FormLabel>Bind DN (Optional)</FormLabel>
                                      <FormControl>
                                        <Input placeholder="cn=admin,dc=company,dc=com" {...field} />
                                      </FormControl>
                                      <FormMessage />
                                    </FormItem>
                                  )}
                                />
                                
                                <FormField
                                  control={form.control}
                                  name="ldapBindPassword"
                                  render={({ field }) => (
                                    <FormItem>
                                      <FormLabel>Bind Password (Optional)</FormLabel>
                                      <FormControl>
                                        <Input type="password" placeholder="••••••••" {...field} />
                                      </FormControl>
                                      <FormMessage />
                                    </FormItem>
                                  )}
                                />
                              </div>

                              <div className="flex gap-2">
                                <Button 
                                  type="button" 
                                  variant="outline"
                                  size="sm"
                                  onClick={testLdapConnection}
                                  disabled={!form.getValues('ldapServer')}
                                >
                                  <TestTube className="h-4 w-4 mr-2" />
                                  Test Connection
                                </Button>

                                <Button
                                  type="button"
                                  variant="outline"
                                  size="sm"
                                  onClick={openLdapUsersDialog}
                                  disabled={!form.getValues('ldapServer') || !form.getValues('ldapBaseDn')}
                                >
                                  <Users className="h-4 w-4 mr-2" />
                                  Browse Users
                                </Button>
                              </div>

                              <Dialog open={isLdapUsersDialogOpen} onOpenChange={setIsLdapUsersDialogOpen}>
                                <DialogContent className="w-[1100px] max-w-[95vw] max-h-[90vh] overflow-hidden flex flex-col">
                                  <DialogHeader>
                                    <DialogTitle>Directory Users</DialogTitle>
                                    <DialogDescription>
                                      Lists users from the configured Active Directory/LDAP base DN. Showing up to 200 results per search.
                                    </DialogDescription>
                                  </DialogHeader>

                                  <div className="flex items-end gap-2">
                                    <div className="flex-1">
                                      <FormLabel>Search</FormLabel>
                                      <Input
                                        value={ldapUsersQuery}
                                        onChange={(e) => setLdapUsersQuery(e.target.value)}
                                        placeholder="Search by username, name, email..."
                                      />
                                    </div>
                                    <Button
                                      type="button"
                                      onClick={() => loadLdapUsers(ldapUsersQuery, 200)}
                                      disabled={ldapUsersLoading}
                                    >
                                      <Search className="h-4 w-4 mr-2" />
                                      Search
                                    </Button>
                                    <Button
                                      type="button"
                                      variant="outline"
                                      onClick={() => loadLdapUsers('', 200)}
                                      disabled={ldapUsersLoading}
                                    >
                                      Refresh
                                    </Button>
                                  </div>

                                  <div className="text-sm text-muted-foreground">
                                    {ldapUsersMessage}{ldapUsersMessage ? ' • ' : ''}{ldapUsers.length} user(s)
                                  </div>

                                  <div className="flex-1 border rounded-md overflow-auto">
                                    <table className="w-full text-sm">
                                      <thead className="sticky top-0 bg-background">
                                        <tr className="border-b">
                                          <th className="text-left p-2 w-[180px]">Username</th>
                                          <th className="text-left p-2 w-[260px]">Display Name</th>
                                          <th className="text-left p-2 w-[260px]">Email</th>
                                          <th className="text-left p-2">UPN</th>
                                        </tr>
                                      </thead>
                                      <tbody>
                                        {ldapUsers.length === 0 ? (
                                          <tr>
                                            <td colSpan={4} className="p-4 text-center text-muted-foreground">
                                              {ldapUsersLoading ? 'Loading users...' : 'No users found'}
                                            </td>
                                          </tr>
                                        ) : (
                                          ldapUsers.map((u, idx) => (
                                            <tr key={`${u.distinguishedName || u.username}-${idx}`} className="border-b hover:bg-muted/30">
                                              <td className="p-2 font-medium">{u.username || '-'}</td>
                                              <td className="p-2">{u.displayName || '-'}</td>
                                              <td className="p-2">{u.email || '-'}</td>
                                              <td className="p-2">{u.userPrincipalName || '-'}</td>
                                            </tr>
                                          ))
                                        )}
                                      </tbody>
                                    </table>
                                  </div>

                                  <DialogFooter>
                                    <Button type="button" variant="outline" onClick={() => setIsLdapUsersDialogOpen(false)}>
                                      Close
                                    </Button>
                                  </DialogFooter>
                                </DialogContent>
                              </Dialog>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Help Panel */}
                      <div className="space-y-4">
                        <div className="bg-muted/50 rounded-lg p-3 space-y-3">
                          <h4 className="font-medium text-sm text-foreground">Quick Setup</h4>
                          
                          <div className="space-y-2 text-xs">
                            <div>
                              <h5 className="font-medium text-foreground mb-1">Active Directory</h5>
                              <div className="text-muted-foreground space-y-0.5">
                                <p>Port: 389, Base: dc=domain,dc=local</p>
                              </div>
                            </div>

                            <div>
                              <h5 className="font-medium text-foreground mb-1">OpenLDAP</h5>
                              <div className="text-muted-foreground space-y-0.5">
                                <p>Port: 389, Base: ou=users,dc=company,dc=com</p>
                              </div>
                            </div>
                          </div>
                        </div>

                        <div className="bg-muted/50 rounded-lg p-3 space-y-2">
                          <h4 className="font-medium text-sm text-foreground">Security Tips</h4>
                          <div className="space-y-1.5 text-xs text-muted-foreground">
                            <div className="flex items-start gap-1.5">
                              <Shield className="h-3 w-3 mt-0.5 text-primary flex-shrink-0" />
                              <p>Use port 636 for SSL/TLS</p>
                            </div>
                            <div className="flex items-start gap-1.5">
                              <TestTube className="h-3 w-3 mt-0.5 text-green-500 flex-shrink-0" />
                              <p>Test before saving</p>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  </TabsContent>

                  {/* Features Tab */}
                  <TabsContent value="features" className="space-y-6 mt-0">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="allowSelfRegistration"
                      render={({ field }) => (
                        <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                          <div className="space-y-0.5">
                            <FormLabel className="text-base">Self Registration</FormLabel>
                            <FormDescription>
                              Allow users to self-register
                            </FormDescription>
                          </div>
                          <FormControl>
                            <Switch
                              checked={field.value}
                              onCheckedChange={field.onChange}
                            />
                          </FormControl>
                        </FormItem>
                      )}
                    />

                    <FormField
                      control={form.control}
                      name="requireEmailVerification"
                      render={({ field }) => (
                        <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                          <div className="space-y-0.5">
                            <FormLabel className="text-base">Email Verification</FormLabel>
                            <FormDescription>
                              Require email verification
                            </FormDescription>
                          </div>
                          <FormControl>
                            <Switch
                              checked={field.value}
                              onCheckedChange={field.onChange}
                            />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                  </div>

                  <FormField
                    control={form.control}
                    name="welcomeMessage"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Welcome Message</FormLabel>
                        <FormControl>
                          <Textarea
                            placeholder="Welcome message for users"
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="defaultPriority"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Default Priority</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              min="1"
                              max="100"
                              {...field}
                              onChange={(e) => field.onChange(parseInt(e.target.value))}
                            />
                          </FormControl>
                          <FormDescription>
                            Priority for tenant selection (1-100)
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    
                    <FormField
                      control={form.control}
                      name="enableAutoSelection"
                      render={({ field }) => (
                        <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                          <div className="space-y-0.5">
                            <FormLabel className="text-base">Auto Selection</FormLabel>
                            <FormDescription>
                              Enable automatic tenant selection
                            </FormDescription>
                          </div>
                          <FormControl>
                            <Switch
                              checked={field.value}
                              onCheckedChange={field.onChange}
                            />
                          </FormControl>
                        </FormItem>
                      )}
                    />
                  </div>
                  </TabsContent>
                </div>
              </Tabs>

              <DialogFooter className="flex-shrink-0 mt-6">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setIsDialogOpen(false)}
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  disabled={createTenantMutation.isPending}
                >
                  {createTenantMutation.isPending
                    ? 'Saving...'
                    : editingTenant
                    ? 'Update Tenant'
                    : 'Create Tenant'}
                </Button>
              </DialogFooter>
            </form>
            </Form>
          </DialogContent>
        </Dialog>

        {/* Delete Confirmation Dialog */}
        <Dialog open={!!deleteTenant} onOpenChange={() => setDeleteTenant(null)}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete Tenant</DialogTitle>
              <DialogDescription>
                Are you sure you want to delete tenant &quot;{deleteTenant?.name}&quot;?
                This will permanently delete all data associated with this tenant including:
                <ul className="mt-2 ml-4 list-disc text-sm">
                  <li>All tenant users and their data</li>
                  <li>Tenant-specific configurations</li>
                  <li>Any custom settings or preferences</li>
                </ul>
                <strong className="text-destructive">This action cannot be undone.</strong>
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setDeleteTenant(null)}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={() => deleteTenant && deleteTenantMutation.mutate(deleteTenant.id)}
                disabled={deleteTenantMutation.isPending}
              >
                {deleteTenantMutation.isPending ? 'Deleting...' : 'Delete Tenant'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Tenant Overview Dialog */}
        {viewingTenant && (
          <TenantOverview
            tenant={viewingTenant}
            isOpen={isOverviewDialogOpen}
            onClose={() => setIsOverviewDialogOpen(false)}
            onEdit={(tenant) => {
              setEditingTenant(tenant);
              setIsDialogOpen(true);
              setIsOverviewDialogOpen(false);
            }}
          />
        )}
      </div>
  );
}
