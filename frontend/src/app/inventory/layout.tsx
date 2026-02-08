'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface InventoryLayoutProps {
  children: React.ReactNode;
}

export default function InventoryLayout({ children }: InventoryLayoutProps) {
  return (
    <AuthGuard>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}

