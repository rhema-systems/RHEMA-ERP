'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface AdministrationLayoutProps {
  children: React.ReactNode;
}

export default function AdministrationLayout({ children }: AdministrationLayoutProps) {
  return (
    <AuthGuard>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}