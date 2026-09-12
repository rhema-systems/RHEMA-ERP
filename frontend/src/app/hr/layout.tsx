'use client';

import { AuthGuard } from '@/components/auth/auth-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';

/**
 * W3: the HR module gate. `hr.access` is seeded and granted broadly to internal roles
 * (DatabaseSeedingService — SuperAdmin/TenantAdmin/Admin/Manager/Employee/ReadOnly/HR/legacy
 * "HR User"/both MD spellings/Internal Audit) because non-HR staff legitimately use surfaces
 * under /hr (peer evaluations, team goals, acknowledgements, payroll screens). Its purpose is
 * excluding external accounts — the self-registering public of the candidate portal (area 26)
 * — not rationing HR between internal staff; per-screen gates and the API keep doing that.
 * SuperAdmin/TenantAdmin auto-pass inside hasAnyPermissionAccess.
 */
export default function HrLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard requiredPermissions={['hr.access']}>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
