import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { ModuleReportLandingPage } from '@/components/reports/ModuleReportLandingPage';
import { moduleReportLandings } from '@/components/reports/module-report-landings';

export default function EstateReportsLandingPage() {
  return (
    <TenantGuard>
      <DashboardLayout>
        <ModuleReportLandingPage definition={moduleReportLandings.estate} />
      </DashboardLayout>
    </TenantGuard>
  );
}
