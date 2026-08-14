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
  Handshake,
  Stethoscope,
  HeartPulse,
  Leaf,
  Recycle,
  Scale,
  Signpost,
  GraduationCap,
  BarChart3,
  OctagonX,
  FileSearch,
  FolderArchive,
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
              description:
                'KPI figures per period — hand-reported or computed from the live registers, with management review.',
              href: '/hr/safety/performance',
              icon: Gauge,
            },
            {
              title: 'Corrective Action Tracker',
              description:
                'One queue over every corrective action — incidents, inspections, equipment and committee meetings — with overdue escalation.',
              href: '/hr/safety/corrective-actions',
              icon: ListChecks,
            },
            {
              title: 'SHE Analytics',
              description:
                'Computed KPIs per period: departmental compliance, contractor ranking and the hazard heat-map.',
              href: '/hr/safety/performance/analytics',
              icon: BarChart3,
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
              title: 'Raise Stop-Work',
              description:
                'Open to everyone — stop work you believe is imminently dangerous and record why. No SHE role needed.',
              href: '/hr/safety/raise-stop-work',
              icon: OctagonX,
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
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Training</h2>
        <NavCardGrid
          items={[
            {
              title: 'Safety Training',
              description:
                'SHE training plans, toolbox talks to certified courses, and the attendance register — contractor and visitor sign-ins included. Separate from corporate Training.',
              href: '/hr/safety/training',
              icon: GraduationCap,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Contractors</h2>
        <NavCardGrid
          items={[
            {
              title: 'Contractor SHE',
              description:
                'The contractor register — pre-qualification, worker inductions, site inspections, non-compliance notices and documents.',
              href: '/hr/safety/contractors',
              icon: Handshake,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Health</h2>
        <NavCardGrid
          items={[
            {
              title: 'Occupational Health',
              description:
                'Health surveillance, first-aid stations and wellness programs. Requires medical permissions.',
              href: '/hr/safety/occupational-health',
              icon: Stethoscope,
            },
            {
              title: 'Return to Work',
              description:
                'Phased return plans with medical clearance, duties and periodic reviews. Requires medical permissions.',
              href: '/hr/safety/return-to-work',
              icon: HeartPulse,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Environment</h2>
        <NavCardGrid
          items={[
            {
              title: 'Environmental',
              description:
                'Spills, exceedances and contamination with EPA-notification tracking, plus monitoring readings with computed exceedances.',
              href: '/hr/safety/environmental',
              icon: Leaf,
            },
            {
              title: 'Waste Management',
              description:
                'Disposal records and the waste-type catalogue. Manifest-requiring types are gated on their certificate.',
              href: '/hr/safety/waste',
              icon: Recycle,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Governance</h2>
        <NavCardGrid
          items={[
            {
              title: 'SHE Audits',
              description:
                'Management-system audits — planning, execution, findings with CAPA, verification and closure.',
              href: '/hr/safety/audits',
              icon: FileSearch,
            },
            {
              title: 'Stop-Work Orders',
              description:
                'Work halted under stop-work authority — routing, resolution and cleared resumption.',
              href: '/hr/safety/stop-work',
              icon: OctagonX,
            },
            {
              title: 'Document Register',
              description:
                'Controlled SHE documents — policies, procedures, plans and reports — with version history, approval and review cycles.',
              href: '/hr/safety/documents',
              icon: FolderArchive,
            },
            {
              title: 'Safety Committees',
              description:
                'Committees with their rosters, meeting minutes and the action-item queues raised in meetings.',
              href: '/hr/safety/committees',
              icon: Users,
            },
            {
              title: 'Regulatory Compliance',
              description:
                'Statutory obligations with their bodies, owners and evidence — GNFS certifications, EPA duties and reviews.',
              href: '/hr/safety/regulatory',
              icon: Scale,
            },
            {
              title: 'Safety Signage',
              description:
                'The signage register — location, condition and inspection cycle per sign.',
              href: '/hr/safety/signs',
              icon: Signpost,
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
