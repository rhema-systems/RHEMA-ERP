'use client';

import { Suspense } from 'react';
import { usePathname } from 'next/navigation';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface MaintenanceLayoutProps {
  children: React.ReactNode;
}

function MaintenanceLayoutContent({ children }: MaintenanceLayoutProps) {
  const pathname = usePathname() ?? '';
  const requiredPermissions = pathname.startsWith('/maintenance/fleet')
    ? ['fleet.access']
    : ['maintenance.access'];

  return (
    <AuthGuard requiredPermissions={requiredPermissions}>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}

export default function MaintenanceLayout({ children }: MaintenanceLayoutProps) {
  return (
    <Suspense fallback={null}>
      <MaintenanceLayoutContent>{children}</MaintenanceLayoutContent>
    </Suspense>
  );
}
