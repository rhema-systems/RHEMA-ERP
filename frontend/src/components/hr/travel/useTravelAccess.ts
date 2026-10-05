'use client';

import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';

/**
 * What the signed-in user may do on the travel desk, in one place.
 *
 * `canWrite` is `HR.Travel.Write`; `canAdmin` is `HR.Travel.Admin` — deletes, approving a policy,
 * authorising a booking above a cap; `canApprove` is `HR.Travel.Approve`, which decides a request only
 * while no approval workflow is published (with one, the workflow names the approver). Read, Write and
 * Approve also pass on the roles the API's role fallback grants while a tenant's permission seed has not
 * run, as the other HR screens do. **`canAdmin` reads the permission alone** (travel final closure, lane 4,
 * D-3): the HR desk holds `HR.Travel.Admin` since then, seeded at every startup, and the role list it used
 * to fall back on named admin roles by guess — the permission is what the server checks.
 *
 * ⚠ A button an endpoint will refuse should not render (travel final closure, lane 0 — the
 * attachment delete showed for HR and answered 403). Screens read this rather than hard-coding.
 */
export function useTravelAccess() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  return {
    // The desk's view: the register, the tabs, the money. A line manager or another approver who holds
    // none of these still opens a request waiting for them — through the approver's door (lane 2).
    canRead: hasAnyPermission(['HR.Travel.Read', 'HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES),
    canWrite: hasAnyPermission(['HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES),
    canAdmin: hasAnyPermission(['HR.Travel.Admin']),
    canApprove: hasAnyPermission(['HR.Travel.Approve', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES),
  };
}
