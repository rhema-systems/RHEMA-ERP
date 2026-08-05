'use client';

import { TenantGuard } from '../../components/auth/tenant-guard';
import { AllSettingsPage } from '../../components/settings/AllSettingsPage';

export default function SettingsPage() {
  return (
    <TenantGuard>
      <AllSettingsPage />
    </TenantGuard>
  );
}
