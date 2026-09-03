'use client';

import { AuthGuard } from '@/components/auth/auth-guard';

/**
 * The SHE desk's own route gate (DR-10, 2026-09-03). The parent /hr layout already requires
 * hr.access; this adds she.access so the Safety (SHE) screens can be shown or hidden per role
 * independently of Human Resources. Held by Safety Officer, SHE Manager, HR (read-only in SHE)
 * and the administrators. Staff self-service safety (report an incident, a hazard, a stop-work,
 * PPE) lives under /me/safety and is deliberately outside this gate.
 */
export default function SafetyLayout({ children }: { children: React.ReactNode }) {
  return <AuthGuard requiredPermissions={['she.access']}>{children}</AuthGuard>;
}
