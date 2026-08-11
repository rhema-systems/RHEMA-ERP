import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { StatutoryReportCataloguePage } from '@/components/reports/StatutoryReportCataloguePage';

export default function QuantitySurveyReportsPage() {
  return (
    <TenantGuard>
      <DashboardLayout>
        <StatutoryReportCataloguePage mode="quantity-survey" />
      </DashboardLayout>
    </TenantGuard>
  );
}
