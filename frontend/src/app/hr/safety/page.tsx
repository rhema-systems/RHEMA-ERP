'use client';

import {
  LayoutDashboard,
  Gauge,
  ListChecks,
  AlertTriangle,
  Megaphone,
  ShieldAlert,
  FileWarning,
  ClipboardList,
  ClipboardCheck,
  FileCheck,
  HardHat,
  Package,
  Users,
  FireExtinguisher,
  Siren,
} from 'lucide-react';
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
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Prevention</h2>
        <NavCardGrid
          items={[
            {
              title: 'Report a Hazard',
              description:
                'Open to everyone — flag anything that could hurt someone before it does. No SHE role needed.',
              href: '/hr/safety/report-hazard',
              icon: FileWarning,
            },
            {
              title: 'Hazard Register',
              description:
                'Every identified hazard with its risk scoring, hierarchy of controls and review cycle.',
              href: '/hr/safety/hazards',
              icon: ShieldAlert,
            },
            {
              title: 'Risk Assessments',
              description:
                'Formal HIRA/JHA/pre-task assessments — hazard lines, approval and workforce sign-off.',
              href: '/hr/safety/risk-assessments',
              icon: ClipboardList,
            },
            {
              title: 'Inspections',
              description:
                'Scheduled and completed inspections — findings, discovered hazards and guarded close-out.',
              href: '/hr/safety/inspections',
              icon: ClipboardCheck,
            },
            {
              title: 'Permits to Work',
              description:
                'Authorisation for hazardous work — approval gated on hazards, controls and gas testing.',
              href: '/hr/safety/permits',
              icon: FileCheck,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">PPE</h2>
        <NavCardGrid
          items={[
            {
              title: 'My PPE',
              description:
                'Open to everyone — what has been issued to you, what you hold and what has expired.',
              href: '/hr/safety/my-ppe',
              icon: HardHat,
            },
            {
              title: 'PPE Stock',
              description:
                'Inventory per PPE type — stock on hand, reorder levels and restocking.',
              href: '/hr/safety/ppe',
              icon: Package,
            },
            {
              title: 'PPE Issuance',
              description: 'Who holds what, what is due back and what has come back.',
              href: '/hr/safety/ppe/issuances',
              icon: Users,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">
          Fire Safety &amp; Emergency
        </h2>
        <NavCardGrid
          items={[
            {
              title: 'Safety Equipment',
              description:
                'Extinguishers, AEDs, detectors and more — inspections, maintenance and certification tracking.',
              href: '/hr/safety/equipment',
              icon: FireExtinguisher,
            },
            {
              title: 'Emergency Plans',
              description:
                'Plans with assembly points, contact trees, drills and response teams — plus the review chase.',
              href: '/hr/safety/emergency',
              icon: Siren,
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
