'use client';

import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';

/**
 * What the signed-in user may do on the travel desk, in one place.
 *
 * `canWrite` is `HR.Travel.Write`; `canAdmin` is `HR.Travel.Admin` — deletes, approving a policy,
 * authorising a booking above a cap. Each also passes on the roles the API's role fallback grants
 * while a tenant's permission seed has not run, as the other HR screens do.
 *
 * ⚠ A button an endpoint will refuse should not render (travel final closure, lane 0 — the
 * attachment delete showed for HR and answered 403). Screens read this rather than hard-coding.
 */
export function useTravelAccess() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  return {
    canWrite: hasAnyPermission(['HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES),
    canAdmin: hasAnyPermission(['HR.Travel.Admin']) || hasAnyRole(HR_ADMIN_ROLES),
  };
}
