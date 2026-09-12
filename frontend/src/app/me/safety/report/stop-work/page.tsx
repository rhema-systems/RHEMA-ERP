'use client';

import { StopWorkRaiseForm } from '@/components/hr/safety/report/StopWorkRaiseForm';

/**
 * Stop-work authority (FR-SHE-200), open to every employee. Area 25 slice 8 re-homed it from
 * /hr/safety/raise-stop-work; 2026-09-03 moved the form into `StopWorkRaiseForm`, shared with
 * the desk door at /hr/safety/stop-work/new (which names who raised it).
 */
export default function RaiseStopWorkPage() {
  return <StopWorkRaiseForm mode="self" />;
}
