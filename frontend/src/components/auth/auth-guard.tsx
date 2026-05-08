'use client';

import { useEffect, ReactNode, useState } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '../../services/auth';
import { useAuth } from '../../hooks/use-auth';
import { buildLoginRedirectUrl, getCurrentRelativeUrl } from '../../lib/auth-redirect';
import { hasAnyPermissionAccess } from '../../lib/permissions';

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
  const [hasMounted, setHasMounted] = useState(false);
  const storedUser = hasMounted ? authService.getStoredUser() : null;
  const isAuthenticated = hasMounted && authService.isAuthenticated();
  const effectiveUser = user ?? storedUser;
  const permissionsResolved = !requiredPermissions?.length || !!effectiveUser || !isLoading;
  const hasRequiredPermission = requiredPermissions?.length
    ? hasAnyPermissionAccess(effectiveUser, requiredPermissions)
    : true;

  useEffect(() => {
    setHasMounted(true);
  }, []);

  useEffect(() => {
    if (!hasMounted) {
      return;
    }

    if (!isAuthenticated) {
      const loginTarget = redirectTo === '/login'
        ? buildLoginRedirectUrl(getCurrentRelativeUrl())
        : redirectTo;
      router.replace(loginTarget);
      return;
    }

    if (requiredPermissions?.length && permissionsResolved && !hasRequiredPermission) {
      router.replace('/dashboard');
    }
  }, [
    hasMounted,
    isAuthenticated,
    router,
    redirectTo,
    requiredPermissions,
    permissionsResolved,
    hasRequiredPermission,
  ]);

  if (!hasMounted) {
    return <>{fallback}</>;
  }

  if (!isAuthenticated) {
    return <>{fallback}</>;
  }

  if (requiredPermissions?.length && !permissionsResolved) {
    return <>{fallback}</>;
  }

  if (requiredPermissions?.length && !hasRequiredPermission) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
