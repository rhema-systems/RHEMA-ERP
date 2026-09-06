'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { hrSetupGroup } from '@/config/hr-setup-nav';

/**
 * Setup for Time, Attendance & Leave. The operational screens — daily attendance, approvals,
 * alerts, exports — live under `/hr/attendance`; everything configured here shapes how those
 * behave.
 *
 * Leave Types is listed alongside the attendance settings rather than on its own: both answer
 * the same question — what time an employee is and is not expected to be at work.
 *
 * Cards come from `config/hr-setup-nav.ts` — see the note on the HR hub page.
 */
const group = hrSetupGroup('Time, Attendance & Leave');

export default function AttendanceSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Time, Attendance & Leave Setup"
        description={group.description}
        backHref="/administration/hr"
      />
      <NavCardGrid items={group.links} />
    </div>
  );
}
