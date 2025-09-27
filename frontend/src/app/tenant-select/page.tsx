'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Building2, Loader2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { Button } from '../../components/ui/button';
import { useTenant } from '../../contexts/TenantContext';
import { tenantService } from '../../services/tenant';
import { apiService, type UserTenantInfo } from '../../services/api.service';
import type { Tenant } from '../../types';

export default function TenantSelectPage() {
  const router = useRouter();
  const { setCurrentTenantCode } = useTenant();
  const [isAutoSelecting, setIsAutoSelecting] = useState(false);

  // Fetch current user info including accessible tenants
  const { data: userInfo, isLoading, error } = useQuery({
    queryKey: ['currentUser'],
    queryFn: () => apiService.getCurrentUser(),
  });

  // Extract accessible tenants from user info
  const tenants = userInfo?.accessibleTenants || [];

  const selectTenantMutation = useMutation({
    mutationFn: (tenant: UserTenantInfo) => tenantService.selectTenant(tenant.tenantCode, false),
    onSuccess: (response, tenant) => {
      setCurrentTenantCode(tenant.tenantCode);
      router.push('/dashboard');
    },
    onError: (error) => {
      console.error('Error selecting tenant:', error);
      // Handle error - could show a toast notification
    },
  });

  const handleTenantSelect = (tenant: UserTenantInfo) => {
    selectTenantMutation.mutate(tenant);
  };

  // Auto-select if user has only one tenant (especially for public registration users)
  useEffect(() => {
    if (tenants?.length === 1) {
      // Set loading state to show "preparing workspace" message
      setIsAutoSelecting(true);
      // Add a delay to show the loading message
      const timer = setTimeout(() => {
        handleTenantSelect(tenants[0]);
      }, 3000); // 3 seconds delay for better UX
      
      return () => clearTimeout(timer);
    }
  }, [tenants]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
      </div>
    );
  }

  // Show preparing workspace loader when auto-selecting single tenant
  if (isAutoSelecting) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
        {/* Background Pattern */}
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-blue-100 via-transparent to-transparent dark:from-blue-900/20"></div>
        <div className="absolute inset-0 bg-grid-slate-100 dark:bg-grid-slate-800/25 bg-[bottom_1px_center] dark:border-slate-800/25"></div>
        
        <div className="relative text-center">
          {/* Logo */}
          <div className="flex justify-center mb-6">
            <div className="relative">
              <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-r from-blue-600 to-indigo-600 shadow-lg ring-1 ring-white/10">
                <Building2 className="h-10 w-10 text-white" />
              </div>
              <div className="absolute -inset-1 bg-gradient-to-r from-blue-600 to-indigo-600 rounded-2xl blur opacity-25"></div>
            </div>
          </div>
          
          {/* Loading Message */}
          <div className="space-y-4">
            <Loader2 className="h-8 w-8 animate-spin text-blue-600 mx-auto" />
            <div>
              <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
                Preparing your workspace
              </h2>
              <p className="text-slate-600 dark:text-slate-400">
                Setting up your account access...
              </p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (error || !tenants) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="text-red-600">Error</CardTitle>
            <CardDescription>
              Failed to load tenants. Please try logging in again.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button
              className="w-full"
              onClick={() => router.push('/login')}
            >
              Back to Login
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (tenants.length === 0) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="text-amber-600">No Access</CardTitle>
            <CardDescription>
              You don't have access to any tenants. Please contact your administrator.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button
              className="w-full"
              onClick={() => router.push('/login')}
            >
              Back to Login
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
      {/* Background Pattern */}
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-blue-100 via-transparent to-transparent dark:from-blue-900/20"></div>
      <div className="absolute inset-0 bg-grid-slate-100 dark:bg-grid-slate-800/25 bg-[bottom_1px_center] dark:border-slate-800/25"></div>

      <div className="relative w-full max-w-xl">
        {/* Logo and Header */}
        <div className="text-center mb-8">
          <div className="flex justify-center mb-6">
            <div className="relative">
              <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-r from-blue-600 to-indigo-600 shadow-lg ring-1 ring-white/10">
                <Building2 className="h-10 w-10 text-white" />
              </div>
              <div className="absolute -inset-1 bg-gradient-to-r from-blue-600 to-indigo-600 rounded-2xl blur opacity-25"></div>
            </div>
          </div>
          <h1 className="text-4xl font-bold tracking-tight bg-gradient-to-r from-slate-900 to-slate-700 dark:from-white dark:to-slate-300 bg-clip-text text-transparent">
            Select Tenant
          </h1>
          <p className="mt-2 text-slate-600 dark:text-slate-400">
            Choose a tenant to continue
          </p>
        </div>

        {/* Tenant Selection */}
        <div className="max-w-md mx-auto">
          <Card className="backdrop-blur-xl bg-white/80 dark:bg-slate-900/80 shadow-2xl border-white/20 dark:border-slate-800">
            <CardHeader className="text-center pb-4">
              <CardTitle className="text-xl font-semibold">Select Your Organization</CardTitle>
              <CardDescription>
                Choose the organization you'd like to access
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {tenants.map((tenant) => (
                <Button
                  key={tenant.tenantCode}
                  variant="outline"
                  className="w-full h-auto p-4 justify-start text-left hover:bg-blue-50 dark:hover:bg-blue-900/20 hover:border-blue-300 dark:hover:border-blue-700 transition-all"
                  onClick={() => handleTenantSelect(tenant)}
                  disabled={selectTenantMutation.isPending}
                >
                  <div className="flex items-center justify-between w-full">
                    <div className="flex items-center space-x-3">
                      <div className={`w-2 h-2 rounded-full ${
                        tenant.isDefault ? 'bg-blue-500' : 'bg-green-500'
                      }`} />
                      <div>
                        <div className="font-medium text-slate-900 dark:text-white flex items-center gap-2">
                          {tenant.tenantName}
                          {tenant.isDefault && (
                            <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">
                              Default
                            </span>
                          )}
                        </div>
                        <div className="text-xs text-slate-500 dark:text-slate-400">
                          {tenant.tenantCode} • {tenant.accessLevel} Access
                        </div>
                      </div>
                    </div>
                    {selectTenantMutation.isPending &&
                     selectTenantMutation.variables?.tenantCode === tenant.tenantCode ? (
                      <Loader2 className="h-4 w-4 animate-spin text-blue-600" />
                    ) : (
                      <div className="text-blue-600 text-sm font-medium">Select →</div>
                    )}
                  </div>
                </Button>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}