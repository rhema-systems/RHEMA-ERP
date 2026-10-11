'use client';

import { useEffect, ReactNode, useState } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '../../services/auth';
import { useAuth } from '../../hooks/use-auth';
import {
  buildLoginRedirectUrl,
  getCurrentRelativeUrl,
} from '../../lib/auth-redirect';
import {
  hasAllPermissionsAccess,
  hasAnyPermissionAccess,
  hasAnyRoleAccess,
} from '../../lib/permissions';

interface AuthGuardProps {
  children: ReactNode;
  fallback?: ReactNode;
  redirectTo?: string;
  requiredPermissions?: string[];
  requiredRoles?: string[];
  accessMode?: 'all' | 'any';
  permissionMode?: 'all' | 'any';
}

export function AuthGuard({
  children,
  fallback = null,
  redirectTo = '/login',
  requiredPermissions,
  requiredRoles,
  accessMode = 'all',
  permissionMode = 'any',
}: AuthGuardProps) {
  const router = useRouter();
  const { user, isLoading } = useAuth();
  const [hasMounted, setHasMounted] = useState(false);
  const storedUser = hasMounted ? authService.getStoredUser() : null;
  const isAuthenticated = hasMounted && authService.isAuthenticated();
  const effectiveUser = user ?? storedUser;
  const hasAccessRequirements =
    !!requiredPermissions?.length || !!requiredRoles?.length;
  const accessResolved =
    !hasAccessRequirements || !!effectiveUser || !isLoading;
  const hasRequiredPermission = requiredPermissions?.length
    ? permissionMode === 'all'
      ? hasAllPermissionsAccess(effectiveUser, requiredPermissions)
      : hasAnyPermissionAccess(effectiveUser, requiredPermissions)
    : true;
  const hasRequiredRole = requiredRoles?.length
    ? hasAnyRoleAccess(effectiveUser, requiredRoles)
    : true;
  const accessChecks = [
    ...(requiredPermissions?.length ? [hasRequiredPermission] : []),
    ...(requiredRoles?.length ? [hasRequiredRole] : []),
  ];
  const hasRequiredAccess =
    accessChecks.length === 0 ||
    (accessMode === 'any'
      ? accessChecks.some(Boolean)
      : accessChecks.every(Boolean));

  useEffect(() => {
    setHasMounted(true);
  }, []);

  useEffect(() => {
    if (!hasMounted) {
      return;
    }

    if (!isAuthenticated) {
      const loginTarget =
        redirectTo === '/login'
          ? buildLoginRedirectUrl(getCurrentRelativeUrl())
          : redirectTo;
      router.replace(loginTarget);
      return;
    }

    if (hasAccessRequirements && accessResolved && !hasRequiredAccess) {
      router.replace('/dashboard');
    }
  }, [
    hasMounted,
    isAuthenticated,
    router,
    redirectTo,
    requiredPermissions,
    requiredRoles,
    hasAccessRequirements,
    accessResolved,
    hasRequiredAccess,
  ]);

  if (!hasMounted) {
    return <>{fallback}</>;
  }

  if (!isAuthenticated) {
    return <>{fallback}</>;
  }

  if (hasAccessRequirements && !accessResolved) {
    return <>{fallback}</>;
  }

  if (hasAccessRequirements && !hasRequiredAccess) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
