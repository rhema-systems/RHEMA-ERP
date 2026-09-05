'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
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
import { Textarea } from '../../../../components/ui/textarea';
import { Checkbox } from '../../../../components/ui/checkbox';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, Role } from '../../../../services/admin-api.service';
import { useToast } from '../../../../hooks/use-toast';
import { Shield, Users, Settings, FileText, BarChart3, Package, DollarSign, Briefcase, Wrench, LockKeyhole, Pencil, Trash2, Building2, Home, Search } from 'lucide-react';
import { FACILITIES_PERMISSIONS } from '@/lib/facilities-permissions';
import { PROPERTY_MANAGEMENT_PERMISSIONS } from '@/lib/property-management-permissions';

const PROTECTED_SYSTEM_ROLE_NAMES = new Set([
  'superadmin',
  'tenantadmin',
  'manager',
  'employee',
  'readonly',
  'externaluser',
  'helpdeskagent',
  'helpdesksupervisor',
  'helpdeskmanager',
  // 2026-09-03: the HR-family roles are code-anchored (see Constants.Roles.IsProtectedSystemRole);
  // the name and the row are protected, the permissions stay editable.
  'hr',
  'safety officer',
  'she manager',
]);

const normalizeRoleName = (roleName?: string | null) => roleName?.trim().toLowerCase() ?? '';
const isProtectedRoleName = (roleName?: string | null) => PROTECTED_SYSTEM_ROLE_NAMES.has(normalizeRoleName(roleName));
const isProtectedSystemRole = (role?: Pick<Role, 'name' | 'isSystemRole'> | null) =>
  Boolean(role?.isSystemRole || isProtectedRoleName(role?.name));

const roleSchema = z.object({
  name: z.string()
    .trim()
    .min(2, 'Role name must be at least 2 characters'),
  description: z.string().optional(),
  permissions: z.array(z.string()).min(1, 'At least one permission is required'),
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
  },
  'Module Access': {
    icon: Briefcase,
    permissions: [
      { id: 'project.access', name: 'Access Project Management', description: 'Access the project management module' },
      { id: 'maintenance.access', name: 'Access Maintenance Management', description: 'Access the maintenance management module' },
      { id: 'fleet.access', name: 'Access Fleet Management', description: 'Access the fleet management module' },
    ]
  },
  'Estate / Facilities': {
    icon: Building2,
    permissions: [...FACILITIES_PERMISSIONS],
  },
  'Estate / Property Management': {
    icon: Home,
    permissions: [...PROPERTY_MANAGEMENT_PERMISSIONS],
  },
  'Estate / Land Management': {
    icon: Building2,
    permissions: [
      {
        id: 'estate.land.project-readiness',
        name: 'Mark Land Project Ready',
        description:
          'Approve verified land demarcations for project management handoff',
      },
    ],
  },
  'Administration Modules': {
    icon: Wrench,
    permissions: [
      { id: 'admin.project-management', name: 'Admin Project Management', description: 'Manage project administration settings' },
      { id: 'admin.maintenance', name: 'Admin Maintenance Management', description: 'Manage maintenance administration settings' },
      { id: 'admin.fleet-management', name: 'Admin Fleet Management', description: 'Manage fleet administration settings' },
    ]
  },
  'Helpdesk Branch Access': {
    icon: FileText,
    permissions: [
      { id: 'enquiry.internal.access', name: 'Access Internal Enquiry', description: 'Access the internal enquiry backoffice branch' },
      { id: 'enquiry.external.access', name: 'Access External Enquiry', description: 'Access the external enquiry backoffice branch' },
      { id: 'support.internal.access', name: 'Access Internal Helpdesk & Complaints', description: 'Access the internal helpdesk and complaints backoffice branch' },
      { id: 'support.external.access', name: 'Access External Helpdesk & Complaints', description: 'Access the external helpdesk and complaints backoffice branch' },
    ]
  }
};

export default function RolesPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<Role | null>(null);
  const [deleteRole, setDeleteRole] = useState<Role | null>(null);
  const [permissionSearch, setPermissionSearch] = useState('');
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const form = useForm<RoleFormData>({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      name: '',
      description: '',
      permissions: [],
    },
  });

  // Fetch roles data
  const { data: roles = [], isLoading } = useQuery({
    queryKey: ['admin-roles'],
    queryFn: () => adminApiService.getRoles(),
  });

  const {
    data: availablePermissions = [],
    isLoading: permissionsLoading,
    isError: permissionsFailed,
  } = useQuery({
    queryKey: ['admin-permissions'],
    queryFn: () => adminApiService.getPermissions(),
  });

  const permissionCategories = React.useMemo(() => {
    const grouped: Record<string, {
      icon: React.ElementType;
      permissions: Array<{ id: string; name: string; description: string }>;
    }> = {};

    for (const permission of availablePermissions) {
      const category = permission.category?.trim() || 'Other';
      const configuredCategory = Object.values(PERMISSION_CATEGORIES).find(config =>
        config.permissions.some(item => item.id === permission.name));
      grouped[category] ??= {
        icon: configuredCategory?.icon ?? Shield,
        permissions: [],
      };
      grouped[category].permissions.push({
        id: permission.name,
        name: permission.displayName || permission.name,
        description: permission.description || permission.name,
      });
    }

    for (const category of Object.values(grouped)) {
      category.permissions.sort((left, right) => left.name.localeCompare(right.name));
    }

    return Object.fromEntries(
      Object.entries(grouped).sort(([left], [right]) => left.localeCompare(right))
    );
  }, [availablePermissions]);

  const filteredPermissionCategories = React.useMemo(() => {
    const query = permissionSearch.trim().toLowerCase();
    if (!query) return permissionCategories;

    return Object.fromEntries(
      Object.entries(permissionCategories)
        .map(([category, config]) => {
          const categoryMatches = category.toLowerCase().includes(query);
          const permissions = categoryMatches
            ? config.permissions
            : config.permissions.filter(permission =>
                [permission.id, permission.name, permission.description]
                  .some(value => value.toLowerCase().includes(query))
              );

          return [category, { ...config, permissions }] as const;
        })
        .filter(([, config]) => config.permissions.length > 0)
    );
  }, [permissionCategories, permissionSearch]);

  const permissionCount = React.useMemo(
    () => Object.values(permissionCategories)
      .reduce((total, category) => total + category.permissions.length, 0),
    [permissionCategories]
  );
  const visiblePermissionCount = React.useMemo(
    () => Object.values(filteredPermissionCategories)
      .reduce((total, category) => total + category.permissions.length, 0),
    [filteredPermissionCategories]
  );

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
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error?.message || `Failed to ${editingRole ? 'update' : 'create'} role`,
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
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete role',
        variant: 'destructive',
      });
    },
  });

  const handleAdd = () => {
    setEditingRole(null);
    setPermissionSearch('');
    form.reset();
    setIsDialogOpen(true);
  };

  const handleEdit = (role: Role) => {
    setEditingRole(role);
    setPermissionSearch('');
    form.reset({
      name: role.name,
      description: role.description || '',
      permissions: role.permissions,
    });
    setIsDialogOpen(true);
  };

  const handleRoleDialogOpenChange = (open: boolean) => {
    setIsDialogOpen(open);
    if (!open) setPermissionSearch('');
  };

  const handleDelete = (role: Role) => {
    if (isProtectedSystemRole(role)) {
      toast({
        title: 'Error',
        description: 'Protected system roles cannot be deleted',
        variant: 'destructive',
      });
      return;
    }
    setDeleteRole(role);
  };

  const onSubmit = (data: RoleFormData) => {
    if (!isProtectedSystemRole(editingRole) && isProtectedRoleName(data.name)) {
      form.setError('name', {
        message: 'This name is reserved for a protected system role',
      });
      return;
    }

    createRoleMutation.mutate(isProtectedSystemRole(editingRole)
      ? {
          ...data,
          name: editingRole!.name,
          description: editingRole!.description || '',
        }
      : data);
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
          {isProtectedSystemRole(role) && (
            <Badge variant="outline" className="text-xs">
              System role
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
          customActions={(role) => (
            <div className="flex items-center gap-1">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-8 w-8 p-0"
                title={isProtectedSystemRole(role) ? 'Edit role permissions' : 'Edit role'}
                onClick={() => handleEdit(role)}
              >
                <Pencil className="h-4 w-4" />
              </Button>
              {isProtectedSystemRole(role) ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-8 w-8 p-0"
                  disabled
                  title="System roles cannot be deleted"
                >
                  <LockKeyhole className="h-4 w-4" />
                </Button>
              ) : (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-8 w-8 p-0 text-destructive hover:text-destructive"
                  title="Delete role"
                  onClick={() => handleDelete(role)}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              )}
            </div>
          )}
        />

        {/* Add/Edit Role Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={handleRoleDialogOpenChange}>
          <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>
                {editingRole ? 'Edit Role' : 'Add New Role'}
              </DialogTitle>
              <DialogDescription>
                {isProtectedSystemRole(editingRole)
                  ? 'Update permission assignments. The system role name and description remain protected.'
                  : editingRole
                  ? 'Update role information and permissions'
                  : 'Create a new role with specific permissions'}
              </DialogDescription>
            </DialogHeader>

            <FormProvider {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                <div className="grid grid-cols-1 gap-4">
                  <FormField
                    control={form.control}
                    name="name"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Role Name *</FormLabel>
                        <FormControl>
                          <Input
                            placeholder="Enter role name"
                            disabled={isProtectedSystemRole(editingRole)}
                            {...field}
                          />
                        </FormControl>
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
                          placeholder="Enter role description" 
                          rows={3}
                          disabled={isProtectedSystemRole(editingRole)}
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
                          <div className="space-y-2">
                            <div className="relative">
                              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                              <Input
                                aria-label="Search permissions"
                                placeholder="Search by category, permission name, code or description..."
                                value={permissionSearch}
                                onChange={event => setPermissionSearch(event.target.value)}
                                className="pl-10"
                              />
                            </div>
                            <p className="text-xs text-muted-foreground" aria-live="polite">
                              {permissionSearch.trim()
                                ? `Showing ${visiblePermissionCount} of ${permissionCount} permissions`
                                : `${permissionCount} permissions available`}
                            </p>
                          </div>
                          {permissionsLoading && (
                            <p className="text-sm text-muted-foreground">Loading permissions...</p>
                          )}
                          {permissionsFailed && (
                            <p className="text-sm text-destructive">
                              Permissions could not be loaded. Close the dialog and retry.
                            </p>
                          )}
                          {!permissionsLoading && !permissionsFailed &&
                            Object.keys(filteredPermissionCategories).length === 0 && (
                              <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                                No permissions match &ldquo;{permissionSearch.trim()}&rdquo;.
                              </div>
                            )}
                          {Object.entries(filteredPermissionCategories).map(([category, config]) => {
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
                    disabled={createRoleMutation.isPending || permissionsLoading || permissionsFailed}
                  >
                    {createRoleMutation.isPending
                      ? 'Saving...'
                      : editingRole
                      ? isProtectedSystemRole(editingRole)
                        ? 'Update Permissions'
                        : 'Update Role'
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
  );
}
