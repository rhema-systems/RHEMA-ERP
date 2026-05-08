'use client';

import { Suspense } from 'react';
import { usePathname, useSearchParams } from 'next/navigation';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { AuthGuard } from '@/components/auth/auth-guard';
import { getRequiredPermissionForHelpdeskScope } from '@/lib/helpdesk-scope';

function HelpdeskLayoutContent({ children }: { children: React.ReactNode }) {
  const pathname = usePathname() ?? '';
  const searchParams = useSearchParams();

  let requiredPermissions: string[] | undefined;

  if (pathname.startsWith('/helpdesk/enquiry/internal')) {
    requiredPermissions = ['enquiry.internal.access'];
  } else if (pathname.startsWith('/helpdesk/enquiry/external')) {
    requiredPermissions = ['enquiry.external.access'];
  } else if (pathname.startsWith('/helpdesk/helpdesk-complaints/internal')) {
    requiredPermissions = ['support.internal.access'];
  } else if (pathname.startsWith('/helpdesk/helpdesk-complaints/external')) {
    requiredPermissions = ['support.external.access'];
  } else if (pathname.startsWith('/helpdesk')) {
    const scope = searchParams?.get('scope');
    if (scope) {
      requiredPermissions = [getRequiredPermissionForHelpdeskScope(scope)];
    }
  }

  return (
    <AuthGuard requiredPermissions={requiredPermissions}>
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}

export default function HelpdeskLayout({ children }: { children: React.ReactNode }) {
  return (
    <Suspense fallback={null}>
      <HelpdeskLayoutContent>{children}</HelpdeskLayoutContent>
    </Suspense>
  );
}

