'use client';

import type { ReactNode } from 'react';
import { useAuth } from '@/hooks/use-auth';

interface PermissionGateProps {
  /** Show the children when the user holds ANY of these permissions. */
  permissions?: string[];
  /** Show the children when the user holds ANY of these roles. */
  roles?: string[];
  /** Rendered instead of the children when access is denied. Defaults to nothing. */
  fallback?: ReactNode;
  children: ReactNode;
}

/**
 * Element-level access gate — hides buttons and sections the user cannot use. This
 * complements `AuthGuard`, which gates whole routes; it is not a security boundary on its
 * own (the API authorizes every call regardless).
 *
 * ⚠ HR permissions are not seeded yet. Only the occupational-health slice has real
 * permission names (`HR.Medical.*`); everything else in HR is still gated by role. Prefer
 * `roles` here until HR permissions exist, and note that the HR role is seeded as
 * "HR User" while several controllers authorize a bare "HR" — pass both spellings.
 *
 * With neither `permissions` nor `roles`, the children render: an unconfigured gate must
 * never silently hide functionality.
 */
export function PermissionGate({ permissions, roles, fallback = null, children }: PermissionGateProps) {
  const { hasAnyPermission, hasAnyRole } = useAuth();

  const permissionOk = permissions?.length ? hasAnyPermission(permissions) : false;
  const roleOk = roles?.length ? hasAnyRole(roles) : false;

  const gated = !!permissions?.length || !!roles?.length;
  const allowed = !gated || permissionOk || roleOk;

  return <>{allowed ? children : fallback}</>;
}

/** Roles that administer HR setup, matching the Administration nav. */
export const HR_ADMIN_ROLES = ['admin', 'SuperAdmin', 'TenantAdmin'];

/**
 * Roles with HR access. Both spellings are listed deliberately: the seeder creates
 * "HR User" but several HR controllers authorize a bare "HR".
 */
export const HR_ROLES = [...HR_ADMIN_ROLES, 'HR', 'HR User'];
