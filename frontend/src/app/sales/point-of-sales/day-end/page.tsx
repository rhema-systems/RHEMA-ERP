'use client';

import { AuthGuard } from '@/components/auth/auth-guard';
import { MobilePosDayEndWorkspace } from '@/components/mobile-pos/MobilePosOperations';

export default function MobilePosDayEndPage() {
  return <AuthGuard requiredPermissions={['MobilePOS.Till.Review', 'Finance.CashTills.Closures.Review']} permissionMode="all">
    <MobilePosDayEndWorkspace />
  </AuthGuard>;
}
