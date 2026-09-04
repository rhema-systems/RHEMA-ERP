'use client';

import { HazardReportForm } from '@/components/hr/safety/report/HazardReportForm';

/**
 * The open hazard-reporting surface. Area 25 slice 8 re-homed it from /hr/safety/report-hazard;
 * 2026-09-03 moved the form into `HazardReportForm`, shared with the desk door at
 * /hr/safety/hazards/new.
 */
export default function ReportHazardPage() {
  return <HazardReportForm mode="self" />;
}
