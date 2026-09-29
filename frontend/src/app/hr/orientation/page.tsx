'use client';

import {
  BookOpen,
  CalendarClock,
  Users,
  GraduationCap,
  ClipboardCheck,
  LayoutDashboard,
  ListChecks,
  FileStack,
  Zap,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function OrientationHomePage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation & Onboarding"
        description="Induction programmes and their scheduled sessions, plus the onboarding checklist a new hire works through. Catalogue authoring lives under Administration."
      />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Day to day</h2>
        <NavCardGrid
          items={[
            {
              title: 'Dashboard',
              description: 'Completion rates, overdue participants and expiring certificates.',
              href: '/hr/orientation/dashboard',
              icon: LayoutDashboard,
            },
            {
              title: 'Sessions',
              description: 'Scheduled runs of a programme — dates, venue, facilitators and seats.',
              href: '/hr/orientation/sessions',
              icon: CalendarClock,
            },
            {
              title: 'Enrollments',
              description: 'Who is on which programme, how far through, and what is overdue.',
              href: '/hr/orientation/enrollments',
              icon: Users,
            },
            {
              title: 'Enrollment Triggers',
              description: 'Why a rule did — or did not — enrol someone, and which onboarding template they get.',
              href: '/hr/orientation/triggers',
              icon: Zap,
            },
            // Area 25 slice 7: "My Orientation" re-homed to the portal (/me/orientation).
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Onboarding</h2>
        <NavCardGrid
          items={[
            {
              title: 'Onboarding Plans',
              description: 'A new hire’s checklist — tasks, owners, due dates and provisioned assets.',
              href: '/hr/orientation/onboarding',
              icon: ListChecks,
            },
            {
              title: 'Task Queues',
              description: 'Everything overdue or awaiting verification across every open plan.',
              href: '/hr/orientation/onboarding/queues',
              icon: ClipboardCheck,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Setup</h2>
        <NavCardGrid
          items={[
            {
              title: 'Programmes',
              description: 'The induction catalogue — modules, content, prerequisites and assessments.',
              href: '/administration/hr/orientation/programs',
              icon: BookOpen,
            },
            {
              title: 'Onboarding Templates',
              description: 'Reusable task checklists instantiated onto each new hire’s plan.',
              href: '/administration/hr/orientation/onboarding-templates',
              icon: FileStack,
            },
          ]}
        />
      </div>
    </div>
  );
}
