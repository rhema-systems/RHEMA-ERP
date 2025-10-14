'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';

interface MaintenanceLayoutProps {
  children: React.ReactNode;
}

export default function MaintenanceLayout({ children }: MaintenanceLayoutProps) {
  return <DashboardLayout>{children}</DashboardLayout>;
}
