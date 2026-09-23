'use client';

import { BookOpen, Tags, FileStack, BellRing } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

/**
 * Setup landing for orientation and onboarding. The programmes and categories screens already
 * back-link here, and the sidebar's group parent points at it.
 */
export default function OrientationAdministrationPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation &amp; Onboarding Setup"
        description="The induction catalogue and the reusable onboarding checklists. Day-to-day delivery — sessions, enrollments and plans — lives under HR."
      />

      <NavCardGrid
        items={[
          {
            title: 'Programmes',
            description:
              'The induction catalogue — modules, content, prerequisites, audience rules and assessments.',
            href: '/administration/hr/orientation/programs',
            icon: BookOpen,
          },
          {
            title: 'Categories',
            description: 'Groups the catalogue — onboarding, compliance, health & safety.',
            href: '/administration/hr/orientation/categories',
            icon: Tags,
          },
          {
            title: 'Onboarding Templates',
            description:
              'Reusable task checklists, copied onto each new hire’s plan when it is created.',
            href: '/administration/hr/orientation/onboarding-templates',
            icon: FileStack,
          },
          {
            title: 'Reminders',
            description:
              'The daily sweep that tells people what is due — onboarding tasks, orientations, certificates — in-app and by email.',
            href: '/administration/hr/orientation/reminders',
            icon: BellRing,
          },
        ]}
      />
    </div>
  );
}
