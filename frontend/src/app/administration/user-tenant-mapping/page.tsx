'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Building2, Search, UserPlus, X, Check, Loader2, CalendarClock } from 'lucide-react';

import { Button } from '../../../components/ui/button';
import { Input } from '../../../components/ui/input';
import { Label } from '../../../components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/table';
import { Badge } from '../../../components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '../../../components/ui/dialog';
import { useToast } from '../../../hooks/use-toast';
import { tenantService } from '../../../services/tenant';
import { userService } from '../../../services/user';
import { DashboardLayout } from '../../../components/layout/dashboard-layout';

// Import the types from API service
import type { TenantUserMapping } from '../../../services/api.service';

// Type alias for consistency with the rest of the component
type UserTenantMapping = TenantUserMapping;

const addUserToTenantSchema = z.object({
  userId: z.string().min(1, 'User is required'),
  tenantId: z.string().min(1, 'Tenant is required'),
  expiresAt: z.string().nullable(),
});

type AddUserToTenantForm = z.infer<typeof addUserToTenantSchema>;

export default function UserTenantMappingPage() {
  const [selectedTenant, setSelectedTenant] = useState<string>('');
  const [selectedUser, setSelectedUser] = useState<string>('');
  const [searchQuery, setSearchQuery] = useState('');
  const [showAddDialog, setShowAddDialog] = useState(false);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch tenants and users
  const { data: tenants, isLoading: isLoadingTenants } = useQuery({
    queryKey: ['tenants'],
    queryFn: () => tenantService.getAllTenants(),
  });

  const { data: users, isLoading: isLoadingUsers } = useQuery({
    queryKey: ['users', searchQuery],
    queryFn: () => userService.searchUsers(searchQuery),
  });

  // Fetch tenant-user mappings
  const { data: userTenants, isLoading: isLoadingMappings } = useQuery({
    queryKey: ['userTenants', selectedTenant],
    queryFn: () => tenantService.getTenantUsers(selectedTenant),
    enabled: !!selectedTenant,
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
    setValue,
  } = useForm<AddUserToTenantForm>({
    resolver: zodResolver(addUserToTenantSchema),
  });

  const addUserMutation = useMutation({
    mutationFn: (data: AddUserToTenantForm) => tenantService.addUserToTenant(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userTenants'] });
      toast({
        title: 'Success',
        description: 'User has been added to the tenant.',
      });
      setShowAddDialog(false);
      reset();
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.message || 'Failed to add user to tenant.',
        variant: 'destructive',
      });
    },
  });

  const removeUserMutation = useMutation({
    mutationFn: ({ userId, tenantId }: { userId: string; tenantId: string }) =>
      tenantService.removeUserFromTenant(userId, tenantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userTenants'] });
      toast({
        title: 'Success',
        description: 'User has been removed from the tenant.',
      });
    },
  });

  if (isLoadingTenants || isLoadingUsers) {
    return (
      <DashboardLayout>
        <div className="flex items-center justify-center min-h-screen">
          <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
        </div>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="container mx-auto py-6 space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">User-Tenant Mapping</h1>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Manage user access to tenants
          </p>
        </div>

        <Dialog open={showAddDialog} onOpenChange={setShowAddDialog}>
          <DialogTrigger asChild>
            <Button>
              <UserPlus className="h-4 w-4 mr-2" />
              Add User to Tenant
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Add User to Tenant</DialogTitle>
              <DialogDescription>
                Select a user and tenant to grant access
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={handleSubmit((data) => addUserMutation.mutate(data))} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="tenantId">Tenant</Label>
                <Select
                  onValueChange={(value) => setValue('tenantId', value)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select a tenant" />
                  </SelectTrigger>
                  <SelectContent>
                    {tenants?.map((tenant) => (
                      <SelectItem key={tenant.id} value={tenant.id}>
                        {tenant.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.tenantId && (
                  <p className="text-sm text-red-500">{errors.tenantId.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="userId">User</Label>
                <Select
                  onValueChange={(value) => setValue('userId', value)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select a user" />
                  </SelectTrigger>
                  <SelectContent>
                    {users?.map((user) => (
                      <SelectItem key={user.id} value={user.id}>
                        {user.username} ({user.email})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.userId && (
                  <p className="text-sm text-red-500">{errors.userId.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="expiresAt">Access Expiry (Optional)</Label>
                <Input
                  id="expiresAt"
                  type="datetime-local"
                  {...register('expiresAt')}
                />
                <p className="text-sm text-slate-500">
                  Leave empty for permanent access
                </p>
              </div>

              <div className="flex justify-end gap-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowAddDialog(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={addUserMutation.isPending}>
                  {addUserMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Adding...
                    </>
                  ) : (
                    'Add User'
                  )}
                </Button>
              </div>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>User Access Management</CardTitle>
          <CardDescription>
            View and manage user access to tenants
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {/* Search and Filter */}
            <div className="flex gap-4">
              <div className="flex-1">
                <Label>Select Tenant</Label>
                <Select
                  value={selectedTenant}
                  onValueChange={setSelectedTenant}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select a tenant to view users" />
                  </SelectTrigger>
                  <SelectContent>
                    {tenants?.map((tenant) => (
                      <SelectItem key={tenant.id} value={tenant.id}>
                        {tenant.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="flex-1">
                <Label>Search Users</Label>
                <div className="relative">
                  <Search className="absolute left-3 top-3 h-4 w-4 text-slate-400" />
                  <Input
                    className="pl-9"
                    placeholder="Search by name or email"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                  />
                </div>
              </div>
            </div>

            {/* Users Table */}
            {selectedTenant ? (
              isLoadingMappings ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
                </div>
              ) : userTenants?.length === 0 ? (
                <div className="text-center py-8 text-slate-500">
                  No users have access to this tenant
                </div>
              ) : (
                <div className="rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>User</TableHead>
                        <TableHead>Email</TableHead>
                        <TableHead>Access Type</TableHead>
                        <TableHead>Expires</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {userTenants?.map((mapping: UserTenantMapping) => (
                        <TableRow key={mapping.userId}>
                          <TableCell className="font-medium">
                            <div className="flex items-center gap-2">
                              <div>
                                <div className="font-medium">
                                  {mapping.user.fullName || `${mapping.user.firstName} ${mapping.user.lastName}`.trim() || mapping.user.username}
                                </div>
                                <div className="text-sm text-slate-500">
                                  @{mapping.user.username}
                                </div>
                              </div>
                              {mapping.isDefault && (
                                <Badge variant="outline" className="text-xs">
                                  Default
                                </Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell>{mapping.user.email}</TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              <Badge variant={mapping.isActive ? 'default' : 'secondary'}>
                                {mapping.isActive ? 'Active' : 'Inactive'}
                              </Badge>
                              <Badge variant="outline" className="text-xs">
                                {mapping.accessLevel}
                              </Badge>
                            </div>
                          </TableCell>
                          <TableCell>
                            {mapping.expiresAt ? (
                              <div className="flex items-center gap-1">
                                <CalendarClock className="h-4 w-4 text-slate-400" />
                                {new Date(mapping.expiresAt).toLocaleDateString()}
                              </div>
                            ) : (
                              <span className="text-slate-500">Never</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            <Button
                              variant="destructive"
                              size="sm"
                              onClick={() => removeUserMutation.mutate({
                                userId: mapping.userId,
                                tenantId: selectedTenant,
                              })}
                              disabled={removeUserMutation.isPending}
                            >
                              {removeUserMutation.isPending ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : (
                                <X className="h-4 w-4" />
                              )}
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )
            ) : (
              <div className="text-center py-8 text-slate-500">
                Select a tenant to view and manage user access
              </div>
            )}
          </div>
        </CardContent>
      </Card>
      </div>
    </DashboardLayout>
  );
}
