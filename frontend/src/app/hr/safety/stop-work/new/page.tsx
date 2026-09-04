'use client';

import { StopWorkRaiseForm } from '@/components/hr/safety/report/StopWorkRaiseForm';

/** The SHE desk records a stop-work order on behalf of whoever stopped the work (2026-09-03). */
export default function RecordStopWorkPage() {
  return <StopWorkRaiseForm mode="desk" />;
}
