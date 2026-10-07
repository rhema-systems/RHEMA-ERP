'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Search, UserPlus, X, Check, Loader2, CalendarClock, ChevronsUpDown } from 'lucide-react';

import { Button } from '../../../components/ui/button';
import { Input } from '../../../components/ui/input';
import { Label } from '../../../components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/table';
import { Badge } from '../../../components/ui/badge';
import { Checkbox } from '../../../components/ui/checkbox';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '../../../components/ui/command';
import { ConfirmationDialog } from '../../../components/ui/confirmation-dialog';
import { Popover, PopoverContent, PopoverTrigger } from '../../../components/ui/popover';
import { cn } from '../../../lib/utils';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '../../../components/ui/dialog';
import { useToast } from '../../../hooks/use-toast';
import { tenantService } from '../../../services/tenant';
import { userService } from '../../../services/user';

// Import the types from API service
import type { TenantUserMapping } from '../../../services/api.service';

// Type alias for consistency with the rest of the component
type UserTenantMapping = TenantUserMapping;

const addUserToTenantSchema = z.object({
  userId: z.string().min(1, 'User is required'),
  tenantIds: z.array(z.string()).min(1, 'Select at least one tenant'),
  expiresAt: z.string().optional(),
});

type AddUserToTenantForm = z.infer<typeof addUserToTenantSchema>;

export default function UserTenantMappingPage() {
  const [selectedTenant, setSelectedTenant] = useState<string>('');
  const [searchQuery, setSearchQuery] = useState('');
  const [userSearchQuery, setUserSearchQuery] = useState('');
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [userPickerOpen, setUserPickerOpen] = useState(false);
  const [tenantPickerOpen, setTenantPickerOpen] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<UserTenantMapping | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch tenants and users
  const { data: tenants = [], isLoading: isLoadingTenants } = useQuery({
    queryKey: ['tenants'],
    queryFn: () => tenantService.getAllTenants(),
  });

  const { data: users = [], isLoading: isLoadingUsers } = useQuery({
    queryKey: ['tenant-mapping-users', userSearchQuery],
    queryFn: () => userService.searchUsers(userSearchQuery),
    enabled: showAddDialog,
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
    watch,
  } = useForm<AddUserToTenantForm>({
    resolver: zodResolver(addUserToTenantSchema),
    defaultValues: { userId: '', tenantIds: [], expiresAt: '' },
  });

  const selectedUserId = watch('userId');
  const selectedTenantIds = watch('tenantIds') ?? [];
  const selectedUser = users.find(user => user.id === selectedUserId);

  const addUserMutation = useMutation({
    mutationFn: async (data: AddUserToTenantForm) => {
      const expiresAt = data.expiresAt ? new Date(data.expiresAt).toISOString() : null;
      const results = await Promise.allSettled(data.tenantIds.map(tenantId =>
        tenantService.addUserToTenant({ userId: data.userId, tenantId, expiresAt })));
      const failures = results.filter(result => result.status === 'rejected');
      if (failures.length > 0) {
        const firstFailure = failures[0] as PromiseRejectedResult;
        throw new Error(`${results.length - failures.length} of ${results.length} assignments completed. ${firstFailure.reason?.message || 'One or more assignments failed.'}`);
      }
      return results.length;
    },
    onSuccess: (count) => {
      queryClient.invalidateQueries({ queryKey: ['userTenants'] });
      toast({
        title: 'Access granted',
        description: `User access was added to ${count} tenant${count === 1 ? '' : 's'}.`,
      });
      setShowAddDialog(false);
      setUserSearchQuery('');
      reset({ userId: '', tenantIds: [], expiresAt: '' });
    },
    onError: (error: any) => {
      toast({
        title: 'Assignment incomplete',
        description: error.message || 'Failed to add tenant access.',
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
        title: 'Access removed',
        description: 'The user can no longer access this tenant.',
      });
      setRemoveTarget(null);
    },
    onError: (error: any) => toast({
      title: 'Removal failed',
      description: error.message || 'The tenant access could not be removed.',
      variant: 'destructive',
    }),
  });

  const filteredMappings = (userTenants ?? []).filter((mapping: UserTenantMapping) => {
    const term = searchQuery.trim().toLowerCase();
    if (!term) return true;
    const name = mapping.user.fullName || `${mapping.user.firstName ?? ''} ${mapping.user.lastName ?? ''}`;
    return [name, mapping.user.username, mapping.user.email]
      .some(value => value?.toLowerCase().includes(term));
  });

  if (isLoadingTenants) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">User Tenant Access</h1>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Grant, review, expire, and revoke access from the authoritative tenant mapping register.
          </p>
        </div>

        <Dialog open={showAddDialog} onOpenChange={(open) => {
          setShowAddDialog(open);
          if (!open && !addUserMutation.isPending) {
            setUserSearchQuery('');
            reset({ userId: '', tenantIds: [], expiresAt: '' });
          }
        }}>
          <DialogTrigger asChild>
            <Button>
              <UserPlus className="h-4 w-4 mr-2" />
              Add tenant access
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-xl">
            <DialogHeader>
              <DialogTitle>Add user to tenants</DialogTitle>
              <DialogDescription>
                Select one user and one or more tenants. The optional expiry applies to every selection.
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={handleSubmit((data) => addUserMutation.mutate(data))} className="space-y-5">
              <div className="space-y-2">
                <Label>User</Label>
                <Popover open={userPickerOpen} onOpenChange={setUserPickerOpen}>
                  <PopoverTrigger asChild>
                    <Button type="button" variant="outline" role="combobox" className="w-full justify-between font-normal">
                      {selectedUser ? `${selectedUser.username} (${selectedUser.email})` : 'Search and select a user'}
                      <ChevronsUpDown className="ml-2 h-4 w-4 opacity-50" />
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
                    <Command shouldFilter={false}>
                      <CommandInput
                        placeholder="Search by name, username, or email..."
                        value={userSearchQuery}
                        onValueChange={setUserSearchQuery}
                      />
                      <CommandList>
                        {isLoadingUsers ? (
                          <div className="flex justify-center py-6"><Loader2 className="h-5 w-5 animate-spin" /></div>
                        ) : (
                          <>
                            <CommandEmpty>No matching users found.</CommandEmpty>
                            <CommandGroup>
                              {users.map((user) => (
                                <CommandItem
                                  key={user.id}
                                  value={`${user.username} ${user.email} ${user.firstName ?? ''} ${user.lastName ?? ''}`}
                                  onSelect={() => {
                                    setValue('userId', user.id, { shouldValidate: true });
                                    setUserPickerOpen(false);
                                  }}
                                >
                                  <Check className={cn('mr-2 h-4 w-4', selectedUserId === user.id ? 'opacity-100' : 'opacity-0')} />
                                  <div>
                                    <div className="font-medium">
                                      {user.firstName || user.lastName ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() : user.username}
                                    </div>
                                    <div className="text-xs text-muted-foreground">{user.username} - {user.email}</div>
                                  </div>
                                </CommandItem>
                              ))}
                            </CommandGroup>
                          </>
                        )}
                      </CommandList>
                    </Command>
                  </PopoverContent>
                </Popover>
                {errors.userId && <p className="text-sm text-red-500">{errors.userId.message}</p>}
              </div>

              <div className="space-y-2">
                <Label>Tenants</Label>
                <Popover open={tenantPickerOpen} onOpenChange={setTenantPickerOpen}>
                  <PopoverTrigger asChild>
                    <Button type="button" variant="outline" role="combobox" className="w-full justify-between font-normal">
                      {selectedTenantIds.length === 0
                        ? 'Select one or more tenants'
                        : selectedTenantIds.length === 1
                          ? tenants.find(tenant => tenant.id === selectedTenantIds[0])?.name
                          : `${selectedTenantIds.length} tenants selected`}
                      <ChevronsUpDown className="ml-2 h-4 w-4 opacity-50" />
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
                    <Command>
                      <CommandInput placeholder="Search tenants..." />
                      <CommandList>
                        <CommandEmpty>No tenants found.</CommandEmpty>
                        <CommandGroup>
                          {tenants.map((tenant) => {
                            const checked = selectedTenantIds.includes(tenant.id);
                            return (
                              <CommandItem
                                key={tenant.id}
                                value={`${tenant.name} ${tenant.code}`}
                                onSelect={() => setValue(
                                  'tenantIds',
                                  checked
                                    ? selectedTenantIds.filter(id => id !== tenant.id)
                                    : [...selectedTenantIds, tenant.id],
                                  { shouldValidate: true }
                                )}
                              >
                                <Checkbox checked={checked} className="mr-2" aria-label={`Select ${tenant.name}`} />
                                <span className="flex-1 truncate">{tenant.name}</span>
                                <span className="text-xs text-muted-foreground">{tenant.code}</span>
                              </CommandItem>
                            );
                          })}
                        </CommandGroup>
                      </CommandList>
                    </Command>
                  </PopoverContent>
                </Popover>
                {errors.tenantIds && <p className="text-sm text-red-500">{errors.tenantIds.message}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="expiresAt">Access Expiry (Optional)</Label>
                <Input
                  id="expiresAt"
                  type="datetime-local"
                  {...register('expiresAt')}
                />
                <p className="text-sm text-slate-500">
                  Leave empty for permanent access. Expired access cannot be used to log in or switch tenants.
                </p>
              </div>

              <div className="flex justify-end gap-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowAddDialog(false)}
                  disabled={addUserMutation.isPending}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={addUserMutation.isPending}>
                  {addUserMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Granting...
                    </>
                  ) : (
                    'Grant access'
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
              ) : filteredMappings.length === 0 ? (
                <div className="text-center py-8 text-slate-500">
                  {searchQuery ? 'No mapped users match your search.' : 'No users have active access to this tenant.'}
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
                      {filteredMappings.map((mapping: UserTenantMapping) => (
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
                                {new Date(mapping.expiresAt).toLocaleString()}
                              </div>
                            ) : (
                              <span className="text-slate-500">Never</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            <Button
                              variant="destructive"
                              size="sm"
                              aria-label={`Remove ${mapping.user.username} from tenant`}
                              onClick={() => setRemoveTarget(mapping)}
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
      <ConfirmationDialog
        open={Boolean(removeTarget)}
        onOpenChange={(open) => {
          if (!open && !removeUserMutation.isPending) setRemoveTarget(null);
        }}
        title="Remove tenant access?"
        description={removeTarget ? (
          <span>
            <strong>{removeTarget.user.fullName || removeTarget.user.username}</strong> will no longer be able to log in to or switch to this tenant.
          </span>
        ) : undefined}
        confirmText="Remove access"
        variant="destructive"
        isLoading={removeUserMutation.isPending}
        onConfirm={async () => {
          if (!removeTarget || !selectedTenant) return false;
          try {
            await removeUserMutation.mutateAsync({ userId: removeTarget.userId, tenantId: selectedTenant });
            return true;
          } catch {
            return false;
          }
        }}
      />
      </div>
  );
}
