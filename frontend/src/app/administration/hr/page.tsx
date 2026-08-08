'use client';

import {
  Building2,
  MapPin,
  Users,
  ListTree,
  Wrench,
  GraduationCap,
  IdCard,
  Tags,
  Globe,
  Building,
  CreditCard,
  Clock,
  CalendarDays,
  Coins,
  Target,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function HrAdministrationPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="HR Setup"
        description="Structure, job architecture and reference data behind the HR module."
      />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Structure</h2>
        <NavCardGrid
          items={[
            {
              title: 'Organization',
              description: 'Structures, levels and units.',
              href: '/administration/hr/organization',
              icon: Building2,
            },
            {
              title: 'Location',
              description: 'Location structures, levels and sites.',
              href: '/administration/hr/location',
              icon: MapPin,
            },
            {
              title: 'Job Positions',
              description: 'Positions, skill requirements and grades.',
              href: '/administration/hr/positions',
              icon: Users,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Reference data</h2>
        <NavCardGrid
          items={[
            {
              title: 'Staff Levels',
              description: 'Ranked staff tiers.',
              href: '/administration/hr/staff-levels',
              icon: ListTree,
            },
            {
              title: 'Skills',
              description: 'Skills positions require and employees hold.',
              href: '/administration/hr/skills',
              icon: Wrench,
            },
            {
              title: 'Qualifications',
              description: 'The qualification catalogue.',
              href: '/administration/hr/qualifications',
              icon: GraduationCap,
            },
            {
              title: 'Identification Types',
              description: 'Identity document types.',
              href: '/administration/hr/identification-types',
              icon: IdCard,
            },
            {
              title: 'Reason Codes',
              description: 'Standard reasons for HR actions.',
              href: '/administration/hr/reason-codes',
              icon: Tags,
            },
            {
              title: 'Countries',
              description: 'Countries used across HR records.',
              href: '/administration/hr/countries',
              icon: Globe,
            },
            {
              title: 'Departments',
              description: 'Read-only lookup shared with other modules.',
              href: '/administration/hr/departments',
              icon: Building,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Time &amp; leave</h2>
        <NavCardGrid
          items={[
            {
              title: 'Attendance & Time',
              description:
                'Work schedules, shifts, holidays, pay periods, devices and alert rules.',
              href: '/administration/hr/attendance',
              icon: Clock,
            },
            {
              title: 'Leave Types',
              description: 'Leave types with their sub-types, allocations and accrual policies.',
              href: '/administration/hr/leave-types',
              icon: CalendarDays,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Compensation</h2>
        <NavCardGrid
          items={[
            {
              title: 'Compensation & Benefits',
              description:
                'Pay components mirrored from Payroll, position emoluments and benefit policies.',
              href: '/administration/hr/compensation',
              icon: Coins,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Performance</h2>
        <NavCardGrid
          items={[
            {
              title: 'Performance Setup',
              description:
                'Strategic goals, the goal library, KPI definitions and the goal risk thresholds.',
              href: '/administration/hr/performance',
              icon: Target,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Payroll</h2>
        <NavCardGrid
          items={[
            {
              title: 'Payroll Setup',
              description: 'Grades, components, tax tables and parameters.',
              href: '/administration/hr/payroll',
              icon: CreditCard,
            },
          ]}
        />
      </div>
    </div>
  );
}
