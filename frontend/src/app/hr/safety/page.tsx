'use client';

import { LayoutDashboard, Gauge, ListChecks, AlertTriangle, Megaphone } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

/**
 * SHE landing page. Slice 1 ships the dashboard and performance snapshots; each later slice adds
 * its register here (incidents, hazards, inspections, permits, PPE, …) — keep the groups in
 * lifecycle order as they land.
 */
export default function SafetyHomePage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety, Health & Environment"
        description="Incident reporting and prevention, inspections, permits and environmental compliance. Reference-data authoring lives under Administration."
      />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Overview</h2>
        <NavCardGrid
          items={[
            {
              title: 'Dashboard',
              description: 'Live counts across every SHE register — what is open, due or overdue.',
              href: '/hr/safety/dashboard',
              icon: LayoutDashboard,
            },
            {
              title: 'Performance Snapshots',
              description: 'Hand-reported KPI figures per period, with management review.',
              href: '/hr/safety/performance',
              icon: Gauge,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Incidents</h2>
        <NavCardGrid
          items={[
            {
              title: 'Report an Incident',
              description:
                'Open to everyone — accidents, near misses and environmental incidents. No SHE role needed.',
              href: '/hr/safety/report-incident',
              icon: Megaphone,
            },
            {
              title: 'Incident Register',
              description:
                'Every reported incident: triage, investigation, corrective actions and closure.',
              href: '/hr/safety/incidents',
              icon: AlertTriangle,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Setup</h2>
        <NavCardGrid
          items={[
            {
              title: 'Reference Data',
              description:
                'Incident types, injury types, body parts, corrective-action templates and regulatory bodies.',
              href: '/administration/hr/safety',
              icon: ListChecks,
            },
          ]}
        />
      </div>
    </div>
  );
}
