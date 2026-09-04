'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';
import { DataTable, Column } from '../../../../components/admin/data-table';
import { Button } from '../../../../components/ui/button';
import { Badge } from '../../../../components/ui/badge';
import { BulkUserActions } from '../../../../components/admin/BulkUserActions';
import { UserProfile } from '../../../../components/admin/UserProfile';
import { ClientOnly } from '../../../../components/ui/client-only';
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
import { Label } from '../../../../components/ui/label';
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
import PhoneInput from '../../../../components/ui/phone-input';

const userSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  email: z.string().email('Invalid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters').optional(),
  firstName: z.string().optional(),
  lastName: z.string().optional(),
  phoneNumber: z.string().refine((val) => !val || val.length >= 10, {
    message: 'Please enter a valid phone number'
  }).optional(),
  roles: z.array(z.string()).min(1, 'At least one role is required'),
  isActive: z.boolean(),
});

type UserFormData = z.infer<typeof userSchema>;

export default function UsersPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [deleteUser, setDeleteUser] = useState<User | null>(null);
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [lastLoginFromDate, setLastLoginFromDate] = useState<string>('');
  const [lastLoginToDate, setLastLoginToDate] = useState<string>('');
  const [phoneNumber, setPhoneNumber] = useState('+233'); // Default to Ghana country code
  const [selectedUsers, setSelectedUsers] = useState<User[]>([]);
  const [viewingUser, setViewingUser] = useState<User | null>(null);
  const [isProfileDialogOpen, setIsProfileDialogOpen] = useState(false);
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
      phoneNumber: '',
      roles: [],
      isActive: true,
    },
  });

  // Sync phoneNumber state with form field
  useEffect(() => {
    if (phoneNumber && phoneNumber !== form.getValues('phoneNumber')) {
      form.setValue('phoneNumber', phoneNumber, { shouldValidate: false });
    }
  }, [phoneNumber, form]);

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
          phoneNumber: userData.phoneNumber,
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
          phoneNumber: userData.phoneNumber,
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
      setPhoneNumber('+233'); // Reset phone number state
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
    setPhoneNumber('+233'); // Reset to Ghana default
    form.reset({
      username: '',
      email: '',
      password: '',
      firstName: '',
      lastName: '',
      phoneNumber: '',
      roles: [],
      isActive: true,
    });
    setIsDialogOpen(true);
  };

  const handleEdit = (user: User) => {
    setEditingUser(user);
    // Set phone number state from user data, fallback to Ghana default
    const userPhoneNumber = user.phoneNumber && user.phoneNumber.trim() !== '' 
      ? user.phoneNumber 
      : '+233';
    setPhoneNumber(userPhoneNumber);
    form.reset({
      username: user.username,
      email: user.email,
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      phoneNumber: user.phoneNumber || '',
      roles: user.roles,
      isActive: user.isActive,
    });
    setIsDialogOpen(true);
  };

  const handleDelete = (user: User) => {
    setDeleteUser(user);
  };

  const handleSelectionChange = (selectedUsers: User[]) => {
    console.log('Selected users:', selectedUsers.map(u => u.username));
    setSelectedUsers(selectedUsers);
  };

  const handleBulkActionComplete = () => {
    setSelectedUsers([]);
    queryClient.invalidateQueries({ queryKey: ['admin-users'] });
  };

  const handleViewProfile = (user: User) => {
    setViewingUser(user);
    setIsProfileDialogOpen(true);
  };

  const onSubmit = (data: UserFormData) => {
    createUserMutation.mutate(data);
  };

  // Filter users based on selected filters
  const filteredUsers = users.filter(user => {
    // Role filter
    if (roleFilter !== 'all' && !user.roles.includes(roleFilter)) {
      return false;
    }

    // Status filter
    if (statusFilter === 'active' && !user.isActive) {
      return false;
    }
    if (statusFilter === 'inactive' && user.isActive) {
      return false;
    }

    // Last login date range filter
    if (lastLoginFromDate || lastLoginToDate) {
      if (!user.lastLoginAt) {
        // If user never logged in, only include if we're not filtering by date
        if (lastLoginFromDate || lastLoginToDate) return false;
      } else {
        const loginDate = new Date(user.lastLoginAt);
        
        if (lastLoginFromDate) {
          const fromDate = new Date(lastLoginFromDate);
          if (loginDate < fromDate) return false;
        }
        
        if (lastLoginToDate) {
          const toDate = new Date(lastLoginToDate);
          toDate.setHours(23, 59, 59, 999); // End of day
          if (loginDate > toDate) return false;
        }
      }
    }

    return true;
  });

  // Get unique roles for the filter dropdown
  const availableRoles = Array.from(new Set(users.flatMap(user => user.roles)));

  const clearFilters = () => {
    setRoleFilter('all');
    setStatusFilter('all');
    setLastLoginFromDate('');
    setLastLoginToDate('');
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
      sortable: true,
      render: (_, user) => {
        const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ');
        return fullName || '-';
      },
    },
    {
      key: 'phoneNumber',
      label: 'Phone Number',
      sortable: true,
      render: (phoneNumber: string) => {
        return phoneNumber ? (
          <span className="text-sm">{phoneNumber}</span>
        ) : (
          <span className="text-muted-foreground text-xs">-</span>
        );
      },
    },
    {
      key: 'roles',
      label: 'Roles',
      render: (roles: string[]) => (
        <div className="flex flex-wrap gap-0.5">
          {roles.map((role) => (
            <Badge key={role} variant="secondary" className="text-xs px-1.5 py-0 h-4 leading-none">
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
        <Badge variant={isActive ? 'default' : 'destructive'} className="text-xs px-2 py-0 h-5">
          {isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      key: 'lastLoginAt',
      label: 'Last Login',
      render: (lastLoginAt: Date) => {
        if (!lastLoginAt) return <span className="text-muted-foreground text-xs">Never</span>;
        const date = new Date(lastLoginAt);
        const day = date.getDate().toString().padStart(2, '0');
        const month = date.toLocaleDateString('en-GB', { month: 'short' });
        const year = date.getFullYear().toString().slice(-2);
        const hours = date.getHours().toString().padStart(2, '0');
        const minutes = date.getMinutes().toString().padStart(2, '0');
        const seconds = date.getSeconds().toString().padStart(2, '0');
        return <span className="text-xs font-mono">{`${day}-${month}-${year} ${hours}:${minutes}:${seconds}`}</span>;
      },
    },
    {
      key: 'createdAt',
      label: 'Created',
      sortable: true,
      render: (createdAt: Date) => {
        const date = new Date(createdAt);
        const day = date.getDate().toString().padStart(2, '0');
        const month = date.toLocaleDateString('en-GB', { month: 'short' });
        const year = date.getFullYear().toString().slice(-2);
        const hours = date.getHours().toString().padStart(2, '0');
        const minutes = date.getMinutes().toString().padStart(2, '0');
        const seconds = date.getSeconds().toString().padStart(2, '0');
        return <span className="text-xs font-mono">{`${day}-${month}-${year} ${hours}:${minutes}:${seconds}`}</span>;
      },
    },
  ];

  return (
    <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">User Management</h1>
            <p className="text-muted-foreground">
              Manage system users, roles, and permissions
            </p>
          </div>
          {selectedUsers.length > 0 && (
            <div className="flex items-center gap-2">
              <Badge variant="outline" className="text-sm">
                {selectedUsers.length} selected
              </Badge>
              <BulkUserActions
                selectedUserIds={selectedUsers.map(u => u.id)}
                users={users}
                onActionComplete={handleBulkActionComplete}
              />
            </div>
          )}
        </div>

        {/* Filters */}
        <div className="flex flex-wrap gap-4 p-4 bg-muted/30 rounded-lg border">
          <div className="flex flex-wrap gap-4">
            {/* Role Filter */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Role</Label>
              <ClientOnly fallback={
                <div className="w-40 h-10 bg-muted/50 rounded-md border" />
              }>
                <Select value={roleFilter} onValueChange={setRoleFilter}>
                  <SelectTrigger className="w-40">
                    <SelectValue placeholder="All Roles" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Roles</SelectItem>
                    {availableRoles.map(role => (
                      <SelectItem key={role} value={role}>{role}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </ClientOnly>
            </div>

            {/* Status Filter */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Status</Label>
              <ClientOnly fallback={
                <div className="w-40 h-10 bg-muted/50 rounded-md border" />
              }>
                <Select value={statusFilter} onValueChange={setStatusFilter}>
                  <SelectTrigger className="w-40">
                    <SelectValue placeholder="All Status" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Status</SelectItem>
                    <SelectItem value="active">Active</SelectItem>
                    <SelectItem value="inactive">Inactive</SelectItem>
                  </SelectContent>
                </Select>
              </ClientOnly>
            </div>

            {/* Last Login Date Range */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Last Login From</Label>
              <Input
                type="date"
                value={lastLoginFromDate}
                onChange={(e) => setLastLoginFromDate(e.target.value)}
                className="w-40"
              />
            </div>

            <div className="space-y-2">
              <Label className="text-sm font-medium">Last Login To</Label>
              <Input
                type="date"
                value={lastLoginToDate}
                onChange={(e) => setLastLoginToDate(e.target.value)}
                className="w-40"
              />
            </div>

            {/* Clear Filters Button */}
            <div className="flex items-end">
              <Button
                variant="outline"
                onClick={clearFilters}
                className="h-11"
              >
                Clear Filters
              </Button>
            </div>
          </div>

          {/* Filter Summary */}
          <div className="flex items-center text-sm text-muted-foreground ml-auto">
            Showing {filteredUsers.length} of {users.length} users
          </div>
        </div>

        <DataTable
          title="Users"
          description="Manage user accounts and access levels"
          data={filteredUsers}
          columns={columns}
          loading={isLoading}
          searchPlaceholder="Search users..."
          onAdd={handleAdd}
          onEdit={handleEdit}
          onDelete={handleDelete}
          onView={handleViewProfile}
          exportable={true}
          exportFileName="users_export.csv"
          selectable={true}
          onSelectionChange={handleSelectionChange}
        />

        {/* Add/Edit User Dialog */}
        <Dialog open={isDialogOpen} onOpenChange={(open) => {
          setIsDialogOpen(open);
          if (!open) {
            setPhoneNumber('+233'); // Reset phone number state when dialog closes
          }
        }}>
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

                  <FormField
                    control={form.control}
                    name="phoneNumber"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Phone Number</FormLabel>
                        <FormControl>
                          <PhoneInput
                            value={phoneNumber}
                            onChange={(value) => {
                              setPhoneNumber(value);
                              field.onChange(value);
                            }}
                            placeholder="Enter phone number"
                            error={!!form.formState.errors.phoneNumber}
                          />
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
                        <ClientOnly fallback={
                          <div className="h-10 bg-muted/50 rounded-md border" />
                        }>
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
                        </ClientOnly>
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
                    onClick={() => {
                      setIsDialogOpen(false);
                      setPhoneNumber('+233'); // Reset phone number state
                    }}
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

        {/* User Profile Dialog */}
        <Dialog open={isProfileDialogOpen} onOpenChange={setIsProfileDialogOpen}>
          <DialogContent className="max-w-6xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>User Profile</DialogTitle>
              <DialogDescription>
                Detailed view and management of user information
              </DialogDescription>
            </DialogHeader>
            {viewingUser && (
              <UserProfile
                user={viewingUser}
                onClose={() => setIsProfileDialogOpen(false)}
                onResetPassword={async (userId, request) => {
                  await adminApiService.resetUserPassword(userId, request);
                  await queryClient.invalidateQueries({
                    queryKey: ['admin-users'],
                  });
                  toast({
                    title: 'Temporary password set',
                    description:
                      'The user must replace it at next sign-in before it expires.',
                  });
                }}
                onEdit={(user) => {
                  setEditingUser(user);
                  setIsDialogOpen(true);
                  setIsProfileDialogOpen(false);
                }}
              />
            )}
          </DialogContent>
        </Dialog>
      </div>
  );
}
