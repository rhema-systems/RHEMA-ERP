'use client';

import { usePathname } from 'next/navigation';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface MaintenanceLayoutProps {
  children: React.ReactNode;
}

export default function MaintenanceLayout({ children }: MaintenanceLayoutProps) {
  const pathname = usePathname();
  const requiredPermissions = pathname.startsWith('/maintenance/fleet')
    ? ['fleet.access']
    : ['maintenance.access'];

  return (
    <AuthGuard requiredPermissions={requiredPermissions}>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
