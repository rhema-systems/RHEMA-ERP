'use client';

import { useAuth } from '@/hooks/use-auth';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';

/**
 * The leave module's three server tiers, as the screens see them.
 *
 * Leave authorizes on `HR.Leave.Read` / `Write` / `Admin`, and the HR role is seeded with Read
 * and Write only — Admin is TenantAdmin's. Every delete in the rulebook (sub-types, allocations,
 * eligibility rules, accrual policies) and retiring a leave type sit on the Admin policy, so an HR
 * officer who is offered those controls gets a 403 (closure plan L-11). These flags hide them.
 *
 * ⚠ Not a security boundary — the API authorizes every call regardless. This only stops the
 * product promising something it will refuse.
 */
export function useLeavePermissions() {
  const { hasAnyPermission, hasAnyRole } = useAuth();

  const canAdminister =
    hasAnyPermission(['HR.Leave.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const canWrite =
    canAdminister || hasAnyPermission(['HR.Leave.Write']) || hasAnyRole(['HR', 'HR User']);

  return { canAdminister, canWrite };
}
