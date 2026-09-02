import { TenantGuard } from '../../../../components/auth/tenant-guard';
import { SettingsModulePage } from '../../../../components/settings/SettingsModulePage';

interface SettingsModuleRouteProps {
  params: Promise<{
    sectionKey: string;
    moduleKey: string;
  }>;
}
export default async function SettingsModuleRoute({ params }: SettingsModuleRouteProps) {
  const { sectionKey, moduleKey } = await params;

  return (
    <TenantGuard>
      <SettingsModulePage sectionKey={sectionKey} moduleKey={moduleKey} />
    </TenantGuard>
  );
}
