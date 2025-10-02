'use client';

import { useEffect, ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '../../services/auth';
import { useTenant } from '../../contexts/TenantContext';
import { Loader2 } from 'lucide-react';

interface TenantGuardProps {
  children: ReactNode;
  fallback?: ReactNode;
  loadingFallback?: ReactNode;
  redirectToLogin?: string;
  redirectToTenantSelect?: string;
}

export function TenantGuard({ 
  children, 
  fallback = null,
  loadingFallback,
  redirectToLogin = '/login',
  redirectToTenantSelect = '/tenant-select'
}: TenantGuardProps) {
  const router = useRouter();
  const { currentTenant, isLoadingTenants } = useTenant();

  useEffect(() => {
    if (typeof window === 'undefined') return;

    // First check if user is authenticated
    if (!authService.isAuthenticated()) {
      console.log('TenantGuard: User not authenticated, redirecting to login');
      router.push(redirectToLogin);
      return;
    }

    // If user is authenticated but tenant data is loaded and no tenant is selected
    if (!isLoadingTenants && !currentTenant) {
      console.log('TenantGuard: User authenticated but no tenant selected, redirecting to tenant selection');
      router.push(redirectToTenantSelect);
      return;
    }
  }, [router, currentTenant, isLoadingTenants, redirectToLogin, redirectToTenantSelect]);

  // Show loading while tenant data is being fetched
  if (isLoadingTenants) {
    return loadingFallback || (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center space-y-4">
          <Loader2 className="h-8 w-8 animate-spin mx-auto" />
          <p className="text-muted-foreground">Loading workspace...</p>
        </div>
      </div>
    );
  }

  // Don't render anything if not authenticated
  if (typeof window !== 'undefined' && !authService.isAuthenticated()) {
    return <>{fallback}</>;
  }

  // Don't render anything if no tenant is selected
  if (typeof window !== 'undefined' && !currentTenant) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}