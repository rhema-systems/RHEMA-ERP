'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';
import { DataTable, Column } from '../../../../components/admin/data-table';
import { Button } from '../../../../components/ui/button';
import { Badge } from '../../../../components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../../components/ui/dialog';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '../../../../components/ui/form';
import { Input } from '../../../../components/ui/input';
import { Switch } from '../../../../components/ui/switch';
import { Textarea } from '../../../../components/ui/textarea';
import { Checkbox } from '../../../../components/ui/checkbox';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, Role } from '../../../../services/admin-api.service';
import { useToast } from '../../../../hooks/use-toast';
import { Shield, Users, Settings, FileText, BarChart3, Package, DollarSign } from 'lucide-react';

const roleSchema = z.object({
  name: z.string().min(2, 'Role name must be at least 2 characters'),
  description: z.string().optional(),
  permissions: z.array(z.string()).min(1, 'At least one permission is required'),
  isSystemRole: z.boolean(),
});

type RoleFormData = z.infer<typeof roleSchema>;

// Available permissions organized by category
const PERMISSION_CATEGORIES = {
  'User Management': {
    icon: Users,
    permissions: [
      { id: 'users.read', name: 'View Users', description: 'View user accounts and details' },
      { id: 'users.create', name: 'Create Users', description: 'Create new user accounts' },
      { id: 'users.update', name: 'Update Users', description: 'Edit existing user accounts' },
      { id: 'users.delete', name: 'Delete Users', description: 'Delete user accounts' },
    ]
  },
  'Role Management': {
    icon: Shield,
    permissions: [
      { id: 'roles.read', name: 'View Roles', description: 'View role definitions' },
      { id: 'roles.create', name: 'Create Roles', description: 'Create new roles' },
      { id: 'roles.update', name: 'Update Roles', description: 'Edit existing roles' },
      { id: 'roles.delete', name: 'Delete Roles', description: 'Delete roles' },
    ]
  },
  'Dashboard & Reports': {
    icon: BarChart3,
    permissions: [
      { id: 'dashboard.read', name: 'View Dashboard', description: 'Access main dashboard' },
      { id: 'reports.read', name: 'View Reports', description: 'Access reporting features' },
      { id: 'reports.create', name: 'Create Reports', description: 'Generate custom reports' },
      { id: 'analytics.read', name: 'View Analytics', description: 'Access analytics data' },
    ]
  },
  'Product Management': {
    icon: Package,
    permissions: [
      { id: 'products.read', name: 'View Products', description: 'View product catalog' },
      { id: 'products.create', name: 'Create Products', description: 'Add new products' },
      { id: 'products.update', name: 'Update Products', description: 'Edit product information' },
      { id: 'products.delete', name: 'Delete Products', description: 'Remove products' },
    ]
  },
  'Financial': {
    icon: DollarSign,
    permissions: [
      { id: 'finance.read', name: 'View Finance', description: 'Access financial data' },
      { id: 'invoices.read', name: 'View Invoices', description: 'View invoice data' },
      { id: 'invoices.create', name: 'Create Invoices', description: 'Generate invoices' },
      { id: 'payments.read', name: 'View Payments', description: 'View payment records' },
    ]
  },
  'System Administration': {
    icon: Settings,
    permissions: [
      { id: 'admin.read', name: 'View Admin', description: 'Access admin interface' },
      { id: 'settings.read', name: 'View Settings', description: 'View system settings' },
      { id: 'settings.update', name: 'Update Settings', description: 'Modify system settings' },
      { id: 'audit.read', name: 'View Audit Logs', description: 'Access audit trail' },
    ]
  }
};

export default function RolesPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<Role | null>(null);
  const [deleteRole, setDeleteRole] = useState<Role | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const form = useForm<RoleFormData>({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      name: '',
      description: '',
      permissions: [],
      isSystemRole: false,
    },
  });

  // Fetch roles data
  const { data: roles = [], isLoading } = useQuery({
    queryKey: ['admin-roles'],
    queryFn: () => adminApiService.getRoles(),
  });

  // Create/Update role mutation
  const createRoleMutation = useMutation({
    mutationFn: (roleData: RoleFormData) => {
      if (editingRole) {
        return adminApiService.updateRole(editingRole.id, roleData);
      } else {
        return adminApiService.createRole(roleData);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-roles'] });
      setIsDialogOpen(false);
      setEditingRole(null);
      form.reset();
      toast({
        title: 'Success',
        description: `Role ${editingRole ? 'updated' : 'created'} successfully`,
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: `Failed to ${editingRole ? 'update' : 'create'} role`,
        variant: 'destructive',
      });
    },
  });

  // Delete role mutation
  const deleteRoleMutation = useMutation({
    mutationFn: (roleId: string) => adminApiService.deleteRole(roleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-roles'] });
      setDeleteRole(null);
      toast({
        title: 'Success',
        description: 'Role deleted successfully',
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to delete role',
        variant: 'destructive',
      });
    },
  });

  const handleAdd = () => {
    setEditingRole(null);
    form.reset();
    setIsDialogOpen(true);
  };

  const handleEdit = (role: Role) => {
    setEditingRole(role);
    form.reset({
      name: role.name,
      description: role.description || '',
      permissions: role.permissions,
      isSystemRole: role.isSystemRole,
    });
    setIsDialogOpen(true);
  };

  const handleDelete = (role: Role) => {
    if (role.isSystemRole) {
      toast({
        title: 'Error',
        description: 'Cannot delete system roles',
        variant: 'destructive',
      });
      return;
    }
    setDeleteRole(role);
  };

  const onSubmit = (data: RoleFormData) => {
    createRoleMutation.mutate(data);
  };

  const columns: Column<Role>[] = [
    {
      key: 'name',
      label: 'Role Name',
      sortable: true,
      render: (name: string, role: Role) => (
        <div className="flex items-center gap-2">
          <Shield className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium">{name}</span>
          {role.isSystemRole && (
            <Badge variant="outline" className="text-xs">
              System
            </Badge>
          )}
        </div>
      ),
    },
    {
      key: 'description',
      label: 'Description',
      render: (description: string) => description || '-',
    },
    {
      key: 'permissions',
      label: 'Permissions',
      render: (permissions: string[]) => {
        if (permissions.includes('*')) {
          return <Badge variant="destructive">All Permissions</Badge>;
        }
        return (
          <div className="flex flex-wrap gap-1">
            {permissions.slice(0, 3).map((permission) => (
              <Badge key={permission} variant="secondary" className="text-xs">
                {permission}
              </Badge>
            ))}
            {permissions.length > 3 && (
              <Badge variant="outline" className="text-xs">
                +{permissions.length - 3} more
              </Badge>
            )}
          </div>
        );
      },
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
  ];

  return (
    <DashboardLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Role Management</h1>
            <p className="text-muted-foreground">
              Define roles and permissions for system access control
            </p>
          </div>
        </div>

        <DataTable
          title="Roles"
          description="Manage user roles and their associated permissions"
          data={roles}
          columns={columns}
          loading={isLoading}
          searchPlaceholder="Search roles..."
          onAdd={handleAdd}
          onEdit={handleEdit}
          onDelete={handleDelete}
        />

        {/* Add/Edit Role Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
          <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>
                {editingRole ? 'Edit Role' : 'Add New Role'}
              </DialogTitle>
              <DialogDescription>
                {editingRole 
                  ? 'Update role information and permissions' 
                  : 'Create a new role with specific permissions'}
              </DialogDescription>
            </DialogHeader>

            <FormProvider {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    control={form.control}
                    name="name"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Role Name *</FormLabel>
                        <FormControl>
                          <Input placeholder="Enter role name" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="isSystemRole"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                        <div className="space-y-0.5">
                          <FormLabel className="text-base">System Role</FormLabel>
                          <FormDescription>
                            System roles cannot be deleted
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
                  name="description"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Description</FormLabel>
                      <FormControl>
                        <Textarea 
                          placeholder="Enter role description" 
                          rows={3}
                          {...field} 
                        />
                      </FormControl>
                      <FormDescription>
                        Optional description explaining the role&rsquo;s purpose
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="permissions"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Permissions *</FormLabel>
                      <FormControl>
                        <div className="space-y-6">
                          {Object.entries(PERMISSION_CATEGORIES).map(([category, config]) => {
                            const Icon = config.icon;
                            return (
                              <div key={category} className="space-y-3">
                                <div className="flex items-center gap-2">
                                  <Icon className="h-4 w-4 text-primary" />
                                  <h4 className="font-medium text-sm">{category}</h4>
                                </div>
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-3 ml-6">
                                  {config.permissions.map((permission) => (
                                    <div
                                      key={permission.id}
                                      className="flex items-start space-x-2 p-2 rounded border"
                                    >
                                      <Checkbox
                                        id={permission.id}
                                        checked={field.value.includes(permission.id)}
                                        onCheckedChange={(checked) => {
                                          if (checked) {
                                            field.onChange([...field.value, permission.id]);
                                          } else {
                                            field.onChange(
                                              field.value.filter((p) => p !== permission.id)
                                            );
                                          }
                                        }}
                                      />
                                      <div className="space-y-1">
                                        <label
                                          htmlFor={permission.id}
                                          className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                                        >
                                          {permission.name}
                                        </label>
                                        <p className="text-xs text-muted-foreground">
                                          {permission.description}
                                        </p>
                                      </div>
                                    </div>
                                  ))}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      </FormControl>
                      <FormDescription>
                        Select the permissions this role should have
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

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
                    disabled={createRoleMutation.isPending}
                  >
                    {createRoleMutation.isPending
                      ? 'Saving...'
                      : editingRole
                      ? 'Update Role'
                      : 'Create Role'}
                  </Button>
                </DialogFooter>
              </form>
            </FormProvider>
          </DialogContent>
        </Dialog>

        {/* Delete Confirmation Dialog */}
        <Dialog open={!!deleteRole} onOpenChange={() => setDeleteRole(null)}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete Role</DialogTitle>
              <DialogDescription>
                Are you sure you want to delete role &ldquo;{deleteRole?.name}&rdquo;?
                Users with this role will lose their associated permissions.
                This action cannot be undone.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setDeleteRole(null)}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={() => deleteRole && deleteRoleMutation.mutate(deleteRole.id)}
                disabled={deleteRoleMutation.isPending}
              >
                {deleteRoleMutation.isPending ? 'Deleting...' : 'Delete'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </DashboardLayout>
  );
}