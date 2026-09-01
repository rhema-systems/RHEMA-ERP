import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { StatutoryReportCataloguePage } from '@/components/reports/StatutoryReportCataloguePage';

export default function CivilEngineeringReportsPage() {
  return (
    <TenantGuard>
      <DashboardLayout>
        <StatutoryReportCataloguePage mode="civil-engineering" />
      </DashboardLayout>
    </TenantGuard>
  );
}
