'use client';

import { useEffect, ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '../../services/auth';
import { useAuth } from '../../hooks/use-auth';

interface AuthGuardProps {
  children: ReactNode;
  fallback?: ReactNode;
  redirectTo?: string;
  requiredPermissions?: string[];
}

export function AuthGuard({ 
  children, 
  fallback = null, 
  redirectTo = '/login',
  requiredPermissions,
}: AuthGuardProps) {
  const router = useRouter();
  const { user, isLoading } = useAuth();
  const hasRequiredPermission = requiredPermissions?.length
    ? (user?.permissions?.some(permission => requiredPermissions.includes(permission)) ??
      authService.hasAnyPermission(requiredPermissions))
    : true;

  useEffect(() => {
    if (typeof window !== 'undefined' && !authService.isAuthenticated()) {
      router.push(redirectTo);
      return;
    }

    if (
      typeof window !== 'undefined' &&
      !isLoading &&
      requiredPermissions?.length &&
      !hasRequiredPermission
    ) {
      router.push('/dashboard');
    }
  }, [router, redirectTo, requiredPermissions, hasRequiredPermission, isLoading]);

  // Don't render anything if not authenticated
  if (typeof window !== 'undefined' && !authService.isAuthenticated()) {
    return <>{fallback}</>;
  }

  if (isLoading) {
    return <>{fallback}</>;
  }

  if (
    typeof window !== 'undefined' &&
    requiredPermissions?.length &&
    !hasRequiredPermission
  ) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
