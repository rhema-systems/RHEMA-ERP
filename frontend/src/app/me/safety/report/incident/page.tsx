'use client';

import { IncidentReportForm } from '@/components/hr/safety/report/IncidentReportForm';

/**
 * The open employee reporting surface (FR-SHE-100 / FR-ENV-025). Area 25 slice 8 re-homed it
 * from /hr/safety/report-incident; 2026-09-03 moved the form itself into
 * `IncidentReportForm` so the SHE desk's own door (/hr/safety/incidents/new, which names the
 * reporter) shares every field with it.
 */
export default function ReportIncidentPage() {
  return <IncidentReportForm mode="self" />;
}
