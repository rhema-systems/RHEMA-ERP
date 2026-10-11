'use client';

import { AuthGuard } from '@/components/auth/auth-guard';
import { MobilePosDevicesWorkspace } from '@/components/mobile-pos/MobilePosOperations';

export default function MobilePosDevicesPage() {
  return <AuthGuard requiredPermissions={['MobilePOS.Device.Approve']}>
    <MobilePosDevicesWorkspace />
  </AuthGuard>;
}
