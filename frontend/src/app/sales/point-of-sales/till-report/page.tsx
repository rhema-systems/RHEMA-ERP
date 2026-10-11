'use client';

import { AuthGuard } from '@/components/auth/auth-guard';
import { MobilePosTillReportWorkspace } from '@/components/mobile-pos/MobilePosOperations';

export default function MobilePosTillReportPage() {
  return <AuthGuard requiredPermissions={['MobilePOS.Reports.View', 'Finance.CashTills.Closures.Review']} permissionMode="all">
    <MobilePosTillReportWorkspace />
  </AuthGuard>;
}
