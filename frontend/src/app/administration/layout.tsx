'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';

interface AdministrationLayoutProps {
  children: React.ReactNode;
}

export default function AdministrationLayout({ children }: AdministrationLayoutProps) {
  return <DashboardLayout>{children}</DashboardLayout>;
}