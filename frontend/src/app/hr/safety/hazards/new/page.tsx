'use client';

import { HazardReportForm } from '@/components/hr/safety/report/HazardReportForm';

/** The SHE desk puts a hazard on the register from an inspection or a report that reached it (2026-09-03). */
export default function RecordHazardPage() {
  return <HazardReportForm mode="desk" />;
}
