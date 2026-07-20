'use client';

import { usePathname } from 'next/navigation';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface AdministrationLayoutProps {
  children: React.ReactNode;
}

export default function AdministrationLayout({ children }: AdministrationLayoutProps) {
  const pathname = usePathname() ?? '';

  let requiredPermissions: string[] | undefined;
  if (pathname.startsWith('/administration/finance')) {
    requiredPermissions = ['Finance.Admin'];
  } else if (pathname.startsWith('/administration/project-management')) {
    requiredPermissions = ['admin.project-management'];
  } else if (
    pathname.startsWith('/administration/fleet-management') ||
    pathname.startsWith('/administration/maintenance/fleet-trip-destinations') ||
    pathname.startsWith('/administration/maintenance/fleet-compliance-templates')
  ) {
    requiredPermissions = ['admin.fleet-management'];
  } else if (pathname.startsWith('/administration/maintenance')) {
    requiredPermissions = ['admin.maintenance'];
  }

  return (
    <AuthGuard requiredPermissions={requiredPermissions}>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
