'use client';

import { IncidentReportForm } from '@/components/hr/safety/report/IncidentReportForm';

/** The SHE desk records an incident on behalf of the person who reported it (2026-09-03). */
export default function RecordIncidentPage() {
  return <IncidentReportForm mode="desk" />;
}
