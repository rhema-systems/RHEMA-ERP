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
 * ⚠ HR is part role-gated and part permission-gated, so pick per area rather than by habit.
 * `HR.Medical.*` is real, seeded, and enforced — the whole medical module authorizes on it, and
 * an HR-role user holds Read and Write but **not** Admin, so deletes there refuse by design. Every
 * other HR area is still gated by role, so `roles` remains correct for those until the W3
 * permission sweep lands.
 *
 * Two exceptions worth knowing inside medical: the facility, physician and facility-service
 * registers keep their READS open to any authenticated user (an employee filing a claim has to
 * name a facility), and the employee self-service surfaces are scoped by token rather than by
 * permission — neither needs a gate here at all.
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
 * Finance's roles that value the pay HR records — a leaver's settlement pay lines and leave cashed
 * in (leave settings audit 2, P2).
 *
 * Mirrors `HrPermissions.FinancePayValuerRoles`. The backend's role fallback grants them
 * `HR.Pay.Value` while a tenant's permission seed has not run — and login lists only seeded
 * permissions — so a screen checks these roles beside the permission, as the API does.
 */
export const PAY_VALUER_ROLES = ['Finance Officer', 'Senior Accountant', 'Chief Accountant'];

/**
 * Roles with HR access.
 *
 * "HR User" was the pre-rename name and no longer exists on a migrated tenant (the reference
 * database has only "HR"). It stays listed so a tenant that has not yet run
 * `MigrateLegacyHrRoleNameAsync` keeps access — the same reason the backend's role fallback still
 * carries it. Drop both together once every environment is known to be migrated.
 */
export const HR_ROLES = [...HR_ADMIN_ROLES, 'HR', 'HR User'];
