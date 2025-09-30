'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';

interface SecurityLayoutProps {
  children: React.ReactNode;
}

export default function SecurityLayout({ children }: SecurityLayoutProps) {
  return <DashboardLayout>{children}</DashboardLayout>;
}