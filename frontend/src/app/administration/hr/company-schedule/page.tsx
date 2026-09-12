'use client';

import { CalendarClock, CalendarRange, DoorOpen, Flag } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

/**
 * Company-schedule setup. Events and bookings are day-to-day work and live under `/hr`; what is
 * configured once lives here.
 */
export default function CompanyScheduleSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company Schedule"
        description="Meeting rooms, closures, milestones and fiscal years."
        backHref="/administration/hr"
      />
      <NavCardGrid
        items={[
          {
            title: 'Meeting Rooms',
            description: 'Rooms, their facilities, and the rules for booking them.',
            href: '/administration/hr/company-schedule/rooms',
            icon: DoorOpen,
          },
          {
            title: 'Business Closures',
            description: 'Days the organisation is shut, company-wide or per site.',
            href: '/administration/hr/company-schedule/closures',
            icon: CalendarClock,
          },
          {
            title: 'Milestones',
            description: 'Anniversaries, achievements and other calendar dates.',
            href: '/administration/hr/company-schedule/milestones',
            icon: Flag,
          },
          {
            title: 'Fiscal Years',
            description: 'Reporting windows and the periods inside them.',
            href: '/administration/hr/company-schedule/fiscal-years',
            icon: CalendarRange,
          },
        ]}
      />
    </div>
  );
}
