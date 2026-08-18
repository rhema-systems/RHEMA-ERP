import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { QuantitySurveyCostDashboardPage } from '@/components/quantity-survey/QuantitySurveyCostDashboardPage';

export default function QuantitySurveyCostDashboardRoute() {
  return (
    <TenantGuard>
      <DashboardLayout defaultSidebarCollapsed>
        <QuantitySurveyCostDashboardPage />
      </DashboardLayout>
    </TenantGuard>
  );
}
