'use client';

import { AuthGuard } from '@/components/auth/auth-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';

export default function DevelopmentLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <AuthGuard requiredPermissions={['project.access']}>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
