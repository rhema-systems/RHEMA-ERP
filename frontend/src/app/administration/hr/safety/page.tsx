'use client';

import {
  AlertTriangle,
  Bandage,
  PersonStanding,
  ClipboardList,
  Landmark,
  ClipboardCheck,
  HardHat,
  Grid3x3,
  AlarmClock,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

/**
 * SHE setup landing — the reference catalogue behind incident capture and compliance. Later
 * slices add their own setup screens (inspection checklists, PPE types, waste types, …).
 */
export default function SafetyAdminHomePage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety Setup"
        description="The SHE reference catalogue — what incident capture, injury recording and regulatory compliance classify against."
      />

      <NavCardGrid
        items={[
          {
            title: 'Incident Types',
            description:
              'The classification behind incident capture, with reportability and default corrective actions.',
            href: '/administration/hr/safety/incident-types',
            icon: AlertTriangle,
          },
          {
            title: 'Injury Types',
            description: 'How an injury is classified on an incident record.',
            href: '/administration/hr/safety/injury-types',
            icon: Bandage,
          },
          {
            title: 'Body Parts',
            description: 'The injured-body-part catalogue, grouped by region.',
            href: '/administration/hr/safety/body-parts',
            icon: PersonStanding,
          },
          {
            title: 'Corrective Action Templates',
            description: 'Reusable corrective actions, attachable as defaults to incident types.',
            href: '/administration/hr/safety/corrective-action-templates',
            icon: ClipboardList,
          },
          {
            title: 'Regulatory Bodies',
            description: 'EPA, GNFS, Labour Department and the rest — who reportable incidents go to.',
            href: '/administration/hr/safety/regulatory-bodies',
            icon: Landmark,
          },
          {
            title: 'Inspection Checklists',
            description: 'The reusable checklist templates inspections are conducted against.',
            href: '/administration/hr/safety/checklists',
            icon: ClipboardCheck,
          },
          {
            title: 'PPE Types',
            description:
              'The protective-equipment catalogue — categories, standards, lifespans and tracking flags.',
            href: '/administration/hr/safety/ppe-types',
            icon: HardHat,
          },
          {
            title: 'Job-Role PPE Requirements',
            description: 'The matrix of what each job role must be issued, and how often.',
            href: '/administration/hr/safety/ppe-requirements',
            icon: Grid3x3,
          },
          {
            title: 'Reminder Engine',
            description:
              'The hourly SHE sweep — permit auto-expiry, due-date reminder ladders and overdue escalation. Run it now, or read the dispatch history.',
            href: '/administration/hr/safety/reminders',
            icon: AlarmClock,
          },
        ]}
      />
    </div>
  );
}
