'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface SecurityLayoutProps {
  children: React.ReactNode;
}

export default function SecurityLayout({ children }: SecurityLayoutProps) {
  return (
    <AuthGuard>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}