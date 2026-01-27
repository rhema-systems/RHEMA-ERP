'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface MaintenanceLayoutProps {
  children: React.ReactNode;
}

export default function MaintenanceLayout({ children }: MaintenanceLayoutProps) {
  return (
    <AuthGuard>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
