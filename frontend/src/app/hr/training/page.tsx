'use client';

import {
  CalendarClock,
  ClipboardList,
  UserCheck,
  ListOrdered,
  CheckSquare,
  Award,
  MessageSquare,
  Settings,
  Stamp,
  IdCard,
  Scale,
  ShieldAlert,
  Route,
  Handshake,
  TrendingUp,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function TrainingHomePage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training & Learning"
        description="Scheduling, nominations and delivery. The catalog and planning live under Administration."
      />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Day to day</h2>
        <NavCardGrid
          items={[
            {
              title: 'Schedules',
              description: 'Planned runs of a programme — dates, trainer, venue and seats.',
              href: '/hr/training/schedules',
              icon: CalendarClock,
            },
            {
              title: 'Enrollments',
              description: 'Who is on which learning path, and how far along.',
              href: '/hr/training/enrollments',
              icon: Route,
            },
            {
              title: 'Mentoring',
              description: 'Active mentoring pairs across the organisation.',
              href: '/hr/training/mentoring',
              icon: Handshake,
            },
            {
              title: 'Training Requests',
              description: 'Ad-hoc asks for training that is not yet in the catalog.',
              href: '/hr/training/requests',
              icon: ClipboardList,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Approvals</h2>
        <NavCardGrid
          items={[
            {
              title: 'Nomination Approvals',
              description: 'Nominations waiting on a supervisor or on HR.',
              href: '/hr/training/approvals',
              icon: UserCheck,
            },
            {
              title: 'Request Approvals',
              description: 'Submitted training requests awaiting a decision.',
              href: '/hr/training/requests?view=pending',
              icon: ListOrdered,
            },
            {
              title: 'Completion Verification',
              description: 'Completions a manager still has to verify.',
              href: '/hr/training/completions',
              icon: CheckSquare,
            },
            {
              title: 'Compliance',
              description: 'Who is behind on mandatory training, and how far.',
              href: '/hr/training/compliance',
              icon: ShieldAlert,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Records</h2>
        <NavCardGrid
          items={[
            {
              title: 'Completions',
              description: 'Outcomes, scores and manager verification.',
              href: '/hr/training/completions',
              icon: Award,
            },
            {
              title: 'Certificates',
              description: 'Certificates we issued, and the codes third parties check them with.',
              href: '/hr/training/certificates',
              icon: Stamp,
            },
            {
              title: 'Employee Certificates',
              description: 'Qualifications staff hold from outside bodies, and HR verification.',
              href: '/hr/training/employee-certificates',
              icon: IdCard,
            },
            {
              title: 'Service Bonds',
              description: 'Service obligations attached to sponsored training.',
              href: '/hr/service-bonds',
              icon: Scale,
            },
            {
              title: 'Feedback & Follow-up',
              description: 'Reaction feedback and post-training application reviews, per schedule.',
              href: '/hr/training/schedules',
              icon: MessageSquare,
            },
            {
              title: 'Analytics',
              description: 'Completions, pass rates, effectiveness and spend across the organisation.',
              href: '/hr/training/analytics',
              icon: TrendingUp,
            },
            {
              title: 'Setup & Catalog',
              description: 'Programmes, trainers, vendors, plans and budgets.',
              href: '/administration/hr/training',
              icon: Settings,
            },
          ]}
        />
      </div>
    </div>
  );
}
