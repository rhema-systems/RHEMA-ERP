import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { StatutoryReportCataloguePage } from '@/components/reports/StatutoryReportCataloguePage';

export default async function InventoryReportPage({
  params,
}: {
  params: Promise<{ reportCode: string }>;
}) {
  const { reportCode } = await params;

  return (
    <TenantGuard>
      <DashboardLayout defaultSidebarCollapsed>
        <StatutoryReportCataloguePage mode="inventory" reportCode={reportCode} />
      </DashboardLayout>
    </TenantGuard>
  );
}
