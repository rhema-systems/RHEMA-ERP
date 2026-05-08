'use client';

import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { AuthGuard } from '@/components/auth/auth-guard';

export default function MarketingLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <AuthGuard>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
