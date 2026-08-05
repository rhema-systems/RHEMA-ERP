import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { StatutoryReportCataloguePage } from '@/components/reports/StatutoryReportCataloguePage';

export default function AuditComplianceReportsPage() {
  return (
    <TenantGuard>
      <DashboardLayout>
        <StatutoryReportCataloguePage mode="compliance" />
      </DashboardLayout>
    </TenantGuard>
  );
}
