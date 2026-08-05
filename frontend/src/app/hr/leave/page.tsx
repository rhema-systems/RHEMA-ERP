'use client';

import {
  CalendarDays,
  CheckCircle2,
  Scale,
  SlidersHorizontal,
  CalendarRange,
  Banknote,
  ShieldCheck,
  CalendarCog,
  Settings,
} from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function LeaveHomePage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Management"
        description="Requests, approvals, balances and the year-end cycle."
      />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Day to day</h2>
        <NavCardGrid
          items={[
            {
              title: 'Requests',
              description: 'Raise leave and track it through approval.',
              href: '/hr/leave/requests',
              icon: CalendarDays,
            },
            {
              title: 'Approvals',
              description: "Requests waiting on a manager's decision.",
              href: '/hr/leave/approvals',
              icon: CheckCircle2,
            },
            {
              title: 'Plans',
              description: 'Leave planned ahead for the year.',
              href: '/hr/leave/plans',
              icon: CalendarRange,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Entitlement</h2>
        <NavCardGrid
          items={[
            {
              title: 'Balances',
              description: 'Entitlement, accrual, usage and what remains.',
              href: '/hr/leave/balances',
              icon: Scale,
            },
            {
              title: 'Adjustments',
              description: 'Manual corrections with an audit trail.',
              href: '/hr/leave/adjustments',
              icon: SlidersHorizontal,
            },
            {
              title: 'Encashments',
              description: 'Convert unused days into cash.',
              href: '/hr/leave/encashments',
              icon: Banknote,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Periodic</h2>
        <NavCardGrid
          items={[
            {
              title: 'Compliance',
              description: 'Who still owes mandatory leave.',
              href: '/hr/leave/compliance',
              icon: ShieldCheck,
            },
            {
              title: 'Year-end',
              description: 'Carry-over and forfeiture runs.',
              href: '/hr/leave/year-end',
              icon: CalendarCog,
            },
            {
              title: 'Leave Types',
              description: 'Setup: entitlement, carry-over and accrual rules.',
              href: '/administration/hr/leave-types',
              icon: Settings,
            },
          ]}
        />
      </div>
    </div>
  );
}
