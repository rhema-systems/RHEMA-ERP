'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../../components/layout/dashboard-layout';
import { DataTable, Column } from '../../../components/admin/data-table';
import { Button } from '../../../components/ui/button';
import { Badge } from '../../../components/ui/badge';
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
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, Tenant } from '../../../services/admin-api.service';
import { useToast } from '../../../hooks/use-toast';
import { Building, Settings } from 'lucide-react';

const tenantSchema = z.object({
  name: z.string().min(2, 'Tenant name must be at least 2 characters'),
  code: z.string().min(2, 'Tenant code must be at least 2 characters')
    .regex(/^[a-z0-9-]+$/, 'Code must contain only lowercase letters, numbers, and hyphens'),
  isActive: z.boolean(),
  description: z.string().optional(),
  domain: z.string().optional(),
  contactEmail: z.string().email().optional().or(z.literal('')),
  contactPhone: z.string().optional(),
  address: z.string().optional(),
});

type TenantFormData = z.infer<typeof tenantSchema>;

export default function TenantManagementPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingTenant, setEditingTenant] = useState<Tenant | null>(null);
  const [deleteTenant, setDeleteTenant] = useState<Tenant | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const form = useForm<TenantFormData>({
    resolver: zodResolver(tenantSchema),
    defaultValues: {
      name: '',
      code: '',
      isActive: true,
      description: '',
      domain: '',
      contactEmail: '',
      contactPhone: '',
      address: '',
    },
  });

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
          domain: tenantData.domain,
          contactEmail: tenantData.contactEmail,
          contactPhone: tenantData.contactPhone,
          address: tenantData.address,
          isActive: tenantData.isActive,
        };
        return adminApiService.updateTenant(editingTenant.id, updateData);
      } else {
        const createData = {
          name: tenantData.name,
          code: tenantData.code,
          description: tenantData.description,
          domain: tenantData.domain,
          contactEmail: tenantData.contactEmail,
          contactPhone: tenantData.contactPhone,
          address: tenantData.address,
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
      isActive: tenant.isActive,
      description: tenant.description || '',
      domain: tenant.domain || '',
      contactEmail: tenant.contactEmail || '',
      contactPhone: tenant.contactPhone || '',
      address: tenant.address || '',
    });
    setIsDialogOpen(true);
  };

  const handleDelete = (tenant: Tenant) => {
    setDeleteTenant(tenant);
  };

  const onSubmit = (data: TenantFormData) => {
    createTenantMutation.mutate(data);
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
    <DashboardLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Tenant Management</h1>
            <p className="text-muted-foreground">
              Manage multi-tenant organizations and their configurations
            </p>
          </div>
        </div>

        <DataTable
          title="Tenants"
          description="Manage tenant organizations and their settings"
          data={tenants}
          columns={columns}
          loading={isLoading}
          searchPlaceholder="Search tenants..."
          onAdd={handleAdd}
          onEdit={handleEdit}
          onDelete={handleDelete}
        />

        {/* Add/Edit Tenant Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
          <DialogContent className="max-w-2xl">
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
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
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
                            onChange={(e) => {
                              // Auto-convert to lowercase and replace spaces with hyphens
                              const value = e.target.value
                                .toLowerCase()
                                .replace(/\s+/g, '-')
                                .replace(/[^a-z0-9-]/g, '');
                              field.onChange(value);
                            }}
                          />
                        </FormControl>
                        <FormDescription>
                          Unique identifier (lowercase, no spaces)
                        </FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                <FormField
                  control={form.control}
                  name="isActive"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                      <div className="space-y-0.5">
                        <FormLabel className="text-base">Active Tenant</FormLabel>
                        <FormDescription>
                          Whether this tenant can access the system
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

                {/* Additional Tenant Information */}
                <div className="space-y-4">
                  <div className="flex items-center gap-2">
                    <Settings className="h-4 w-4" />
                    <h3 className="text-lg font-medium">Tenant Details</h3>
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
                        <FormDescription>
                          Brief description of the tenant organization
                        </FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  
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
                          <FormDescription>
                            Custom domain for this tenant
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    
                    <FormField
                      control={form.control}
                      name="contactEmail"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Contact Email</FormLabel>
                          <FormControl>
                            <Input
                              type="email"
                              placeholder="contact@tenant.com"
                              {...field}
                            />
                          </FormControl>
                          <FormDescription>
                            Primary contact email
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>
                  
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="contactPhone"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Contact Phone</FormLabel>
                          <FormControl>
                            <Input
                              placeholder="+1-555-0123"
                              {...field}
                            />
                          </FormControl>
                          <FormDescription>
                            Primary contact phone number
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    
                    <FormField
                      control={form.control}
                      name="address"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Address</FormLabel>
                          <FormControl>
                            <Input
                              placeholder="123 Main St, City, State"
                              {...field}
                            />
                          </FormControl>
                          <FormDescription>
                            Physical address
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>
                </div>

                <DialogFooter>
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
      </div>
    </DashboardLayout>
  );
}