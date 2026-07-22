'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Building2, Loader2 } from 'lucide-react';
import { useQuery, useMutation } from '@tanstack/react-query';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '../ui/dialog';
import { Button } from '../ui/button';
import { useToast } from '../../hooks/use-toast';
import { tenantService } from '../../services/tenant';
import { apiService, type UserTenantInfo } from '../../services/api.service';
import { getAuthenticatedHomePath } from '../../lib/auth-routing';
import type { Tenant } from '../../types';

interface TenantSelectionDialogProps {
  isOpen: boolean;
  onClose: () => void;
}

export function TenantSelectionDialog({ isOpen, onClose }: TenantSelectionDialogProps) {
  const router = useRouter();
  const { toast } = useToast();

  // Fetch current user info including accessible tenants
  const { data: userInfo, isLoading } = useQuery({
    queryKey: ['currentUser'],
    queryFn: () => apiService.getCurrentUser(),
  });

  // Extract accessible tenants from user info
  const tenants = userInfo?.accessibleTenants || [];

  // Auto-select tenant if user only has access to one
  // useEffect(() => {
  //   if (tenants?.length === 1) {
  //     handleTenantSelect(tenants[0]);
  //   }
  // }, [tenants]);

  const selectTenantMutation = useMutation({
    mutationFn: (tenant: UserTenantInfo) => tenantService.selectTenant(tenant.tenantCode, false),
    onSuccess: () => {
      router.push(getAuthenticatedHomePath(userInfo));
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.message || 'Failed to select tenant.',
        variant: 'destructive',
      });
    },
  });

  const handleTenantSelect = (tenant: UserTenantInfo) => {
    selectTenantMutation.mutate(tenant);
  };

  if (isLoading) {
    return (
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-lg">
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
          </div>
        </DialogContent>
      </Dialog>
    );
  }

  if (!tenants || tenants.length === 0) {
    return (
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>No Access</DialogTitle>
            <DialogDescription>
              You don't have access to any tenants. Please contact your administrator.
            </DialogDescription>
          </DialogHeader>
          <div className="mt-6">
            <Button
              variant="outline"
              onClick={() => {
                onClose();
                router.push('/login');
              }}
              className="w-full"
            >
              Back to Login
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Select Tenant</DialogTitle>
          <DialogDescription>
            Choose a tenant to continue
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          {tenants.map((tenant) => (
            <Card
              key={tenant.tenantId}
              className="cursor-pointer transition-all hover:shadow-lg hover:scale-[1.02]"
              onClick={() => handleTenantSelect(tenant)}
            >
              <CardContent className="flex items-center gap-4 p-4">
                <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-gradient-to-r from-blue-600 to-indigo-600">
                  <Building2 className="h-6 w-6 text-white" />
                </div>
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-medium text-slate-900 dark:text-slate-100">
                      {tenant.tenantName}
                    </h3>
                    {tenant.isDefault && (
                      <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">
                        Default
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-slate-500 dark:text-slate-400">
                    {tenant.tenantCode} • {tenant.accessLevel} Access
                  </p>
                </div>
                {selectTenantMutation.isPending &&
                 selectTenantMutation.variables?.tenantCode === tenant.tenantCode && (
                  <Loader2 className="h-5 w-5 animate-spin text-blue-600" />
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
