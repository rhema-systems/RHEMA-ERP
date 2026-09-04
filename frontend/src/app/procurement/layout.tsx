'use client';

import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { AuthGuard } from '@/components/auth/auth-guard';
import { ProcurementUatRouteGuide } from '@/components/procurement/ProcurementUatRouteGuide';

export default function ProcurementLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <AuthGuard>
      <DashboardLayout>
        <div className="flex min-w-0 items-start gap-4">
          <div className="min-w-0 flex-1">{children}</div>
          <div className="hidden shrink-0 xl:block">
            <ProcurementUatRouteGuide />
          </div>
        </div>
      </DashboardLayout>
    </AuthGuard>
  );
}
