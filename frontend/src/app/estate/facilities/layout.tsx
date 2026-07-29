'use client';

import { AuthGuard } from '@/components/auth/auth-guard';

export default function FacilitiesLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <AuthGuard requiredPermissions={['facilities.access']}>
      {children}
    </AuthGuard>
  );
}
