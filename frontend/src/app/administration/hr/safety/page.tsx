'use client';

import { AlertTriangle, Bandage, PersonStanding, ClipboardList, Landmark } from 'lucide-react';
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
        ]}
      />
    </div>
  );
}
