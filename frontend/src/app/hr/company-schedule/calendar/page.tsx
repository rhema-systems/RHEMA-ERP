'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { CompanyCalendar } from '@/components/hr/company-schedule/CompanyCalendar';

/**
 * The Company Calendar in the HR menu (company-schedule final closure lane 7, D-7) — open to everybody (the sidebar test's
 * KEEP_OPEN): the server decides what each caller sees. The HR desk sees the whole company's events, closures, milestones,
 * holidays and bookings, each linking to its HR page; anyone else what is theirs. The same calendar is in the portal.
 */
export default function CompanyCalendarPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Company calendar"
        description="Events, closures, public holidays, milestones and room bookings — and your own leave, travel, panels and training."
        backHref="/hr/company-schedule"
      />
      <CompanyCalendar portal={false} />
    </div>
  );
}
