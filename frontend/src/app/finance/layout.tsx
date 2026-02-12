'use client';

import { DashboardLayout } from '../../components/layout/dashboard-layout';

interface FinanceLayoutProps {
    children: React.ReactNode;
}

export default function FinanceLayout({ children }: FinanceLayoutProps) {
    return <DashboardLayout>{children}</DashboardLayout>;
}
