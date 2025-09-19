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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../../components/ui/select';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, User, CreateUserRequest, UpdateUserRequest } from '../../../../services/admin-api.service';
import { useToast } from '../../../../hooks/use-toast';

const userSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  email: z.string().email('Invalid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters').optional(),
  firstName: z.string().optional(),
  lastName: z.string().optional(),
  roles: z.array(z.string()).min(1, 'At least one role is required'),
  isActive: z.boolean(),
  tenant: z.string().optional(),
});

type UserFormData = z.infer<typeof userSchema>;

export default function UsersPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [deleteUser, setDeleteUser] = useState<User | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const form = useForm<UserFormData>({
    resolver: zodResolver(userSchema),
    defaultValues: {
      username: '',
      email: '',
      password: '',
      firstName: '',
      lastName: '',
      roles: [],
      isActive: true,
      tenant: '',
    },
  });

  // Fetch users data
  const { data: users = [], isLoading } = useQuery({
    queryKey: ['admin-users'],
    queryFn: () => adminApiService.getUsers(),
  });

  // Fetch roles for the role selector
  const { data: roles = [] } = useQuery({
    queryKey: ['admin-roles'],
    queryFn: () => adminApiService.getRoles(),
  });

  // Fetch tenants for the tenant selector
  const { data: tenants = [] } = useQuery({
    queryKey: ['admin-tenants'],
    queryFn: () => adminApiService.getTenants(),
  });

  // Create/Update user mutation
  const createUserMutation = useMutation({
    mutationFn: (userData: UserFormData) => {
      if (editingUser) {
        const updateData: UpdateUserRequest = {
          username: userData.username,
          email: userData.email,
          firstName: userData.firstName,
          lastName: userData.lastName,
          isActive: userData.isActive,
          roles: userData.roles,
        };
        return adminApiService.updateUser(editingUser.id, updateData);
      } else {
        const createData: CreateUserRequest = {
          username: userData.username,
          email: userData.email,
          password: userData.password || 'TempPassword123!',
          firstName: userData.firstName,
          lastName: userData.lastName,
          isActive: userData.isActive,
          roles: userData.roles,
        };
        return adminApiService.createUser(createData);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      setIsDialogOpen(false);
      setEditingUser(null);
      form.reset();
      toast({
        title: 'Success',
        description: `User ${editingUser ? 'updated' : 'created'} successfully`,
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: `Failed to ${editingUser ? 'update' : 'create'} user`,
        variant: 'destructive',
      });
    },
  });

  // Delete user mutation
  const deleteUserMutation = useMutation({
    mutationFn: (userId: string) => adminApiService.deleteUser(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      setDeleteUser(null);
      toast({
        title: 'Success',
        description: 'User deleted successfully',
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to delete user',
        variant: 'destructive',
      });
    },
  });

  const handleAdd = () => {
    setEditingUser(null);
    form.reset();
    setIsDialogOpen(true);
  };

  const handleEdit = (user: User) => {
    setEditingUser(user);
    form.reset({
      username: user.username,
      email: user.email,
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      roles: user.roles,
      isActive: user.isActive,
      tenant: user.tenantId || '',
    });
    setIsDialogOpen(true);
  };

  const handleDelete = (user: User) => {
    setDeleteUser(user);
  };

  const onSubmit = (data: UserFormData) => {
    createUserMutation.mutate(data);
  };

  const columns: Column<User>[] = [
    {
      key: 'username',
      label: 'Username',
      sortable: true,
    },
    {
      key: 'email',
      label: 'Email',
      sortable: true,
    },
    {
      key: 'firstName',
      label: 'Full Name',
      render: (_, user) => {
        const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ');
        return fullName || '-';
      },
    },
    {
      key: 'roles',
      label: 'Roles',
      render: (roles: string[]) => (
        <div className="flex flex-wrap gap-1">
          {roles.map((role) => (
            <Badge key={role} variant="secondary" className="text-xs">
              {role}
            </Badge>
          ))}
        </div>
      ),
    },
    {
      key: 'isActive',
      label: 'Status',
      render: (isActive: boolean) => (
        <Badge variant={isActive ? 'default' : 'destructive'}>
          {isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      key: 'tenant',
      label: 'Tenant',
      render: (tenant: string) => tenant || '-',
    },
    {
      key: 'lastLoginAt',
      label: 'Last Login',
      render: (lastLoginAt: Date) => 
        lastLoginAt ? new Date(lastLoginAt).toLocaleDateString() : 'Never',
    },
    {
      key: 'createdAt',
      label: 'Created',
      sortable: true,
      render: (createdAt: Date) => new Date(createdAt).toLocaleDateString(),
    },
  ];

  return (
    <DashboardLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">User Management</h1>
            <p className="text-muted-foreground">
              Manage system users, roles, and permissions
            </p>
          </div>
        </div>

        <DataTable
          title="Users"
          description="Manage user accounts and access levels"
          data={users}
          columns={columns}
          loading={isLoading}
          searchPlaceholder="Search users..."
          onAdd={handleAdd}
          onEdit={handleEdit}
          onDelete={handleDelete}
        />

        {/* Add/Edit User Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>
                {editingUser ? 'Edit User' : 'Add New User'}
              </DialogTitle>
              <DialogDescription>
                {editingUser 
                  ? 'Update user information and permissions' 
                  : 'Create a new user account with appropriate roles'}
              </DialogDescription>
            </DialogHeader>

            <FormProvider {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    control={form.control}
                    name="username"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Username *</FormLabel>
                        <FormControl>
                          <Input placeholder="Enter username" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="email"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Email *</FormLabel>
                        <FormControl>
                          <Input placeholder="Enter email address" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="firstName"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>First Name</FormLabel>
                        <FormControl>
                          <Input placeholder="Enter first name" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="lastName"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Last Name</FormLabel>
                        <FormControl>
                          <Input placeholder="Enter last name" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                <FormField
                  control={form.control}
                  name="roles"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Roles *</FormLabel>
                      <FormControl>
                        <Select
                          value={field.value[0] || ''}
                          onValueChange={(value) => field.onChange([value])}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a role" />
                          </SelectTrigger>
                          <SelectContent>
                            {roles.map((role) => (
                              <SelectItem key={role.id} value={role.name}>
                                {role.name} - {role.description}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormControl>
                      <FormDescription>
                        Select the primary role for this user
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="tenant"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Tenant</FormLabel>
                      <FormControl>
                        <Select
                          value={field.value || 'none'}
                          onValueChange={(value) => field.onChange(value === 'none' ? '' : value)}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a tenant" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">
                              <span className="text-muted-foreground">No tenant (System-wide)</span>
                            </SelectItem>
                            {tenants.map((tenant) => (
                              <SelectItem key={tenant.id} value={tenant.code}>
                                {tenant.name} ({tenant.code})
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormControl>
                      <FormDescription>
                        Optional tenant assignment for multi-tenant systems
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="isActive"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                      <div className="space-y-0.5">
                        <FormLabel className="text-base">Active User</FormLabel>
                        <FormDescription>
                          Whether this user can log in to the system
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
                    disabled={createUserMutation.isPending}
                  >
                    {createUserMutation.isPending
                      ? 'Saving...'
                      : editingUser
                      ? 'Update User'
                      : 'Create User'}
                  </Button>
                </DialogFooter>
              </form>
            </FormProvider>
          </DialogContent>
        </Dialog>

        {/* Delete Confirmation Dialog */}
        <Dialog open={!!deleteUser} onOpenChange={() => setDeleteUser(null)}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete User</DialogTitle>
              <DialogDescription>
                Are you sure you want to delete user &ldquo;{deleteUser?.username}&rdquo;?
                This action cannot be undone.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => setDeleteUser(null)}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={() => deleteUser && deleteUserMutation.mutate(deleteUser.id)}
                disabled={deleteUserMutation.isPending}
              >
                {deleteUserMutation.isPending ? 'Deleting...' : 'Delete'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </DashboardLayout>
  );
}