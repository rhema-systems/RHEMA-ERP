'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { CompanyCalendar } from '@/components/hr/company-schedule/CompanyCalendar';

/**
 * The company calendar in the staff portal (company-schedule final closure lane 7, D-7): the events you are invited to
 * or that are for you, the closures that cover you, public holidays and milestones, your room bookings, and your own
 * leave, travel, interview panels and training. Answer an invitation from it (D-8); look at a room and book it (D-13).
 */
export default function MyCalendarPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Calendar"
        description="What is on — for the company and for you. Click anything to see it, or to answer an invitation."
      />
      <CompanyCalendar portal />
    </div>
  );
}
