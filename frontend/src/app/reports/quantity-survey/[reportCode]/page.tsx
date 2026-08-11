import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { StatutoryReportCataloguePage } from '@/components/reports/StatutoryReportCataloguePage';

export default async function QuantitySurveyReportPage({ params }: { params: Promise<{ reportCode: string }> }) {
  const { reportCode } = await params;
  return (
    <TenantGuard>
      <DashboardLayout defaultSidebarCollapsed>
        <StatutoryReportCataloguePage mode="quantity-survey" reportCode={reportCode} />
      </DashboardLayout>
    </TenantGuard>
  );
}
