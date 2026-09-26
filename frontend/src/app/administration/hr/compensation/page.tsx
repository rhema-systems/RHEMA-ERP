'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { hrSetupGroup } from '@/config/hr-setup-nav';

/**
 * Setup for Pay & Benefits. The operational side — an employee's resolved package and their
 * benefit enrolments — lives under `/hr`.
 *
 * Salary grades are deliberately absent: payroll owns the salary structure and HR only mirrors
 * it, so there is no HR grade editor to link to. Payroll Setup itself is listed, because a
 * lone "Payroll" card filed under its own heading on the HR hub told nobody where pay is
 * configured.
 *
 * Cards come from `config/hr-setup-nav.ts` — see the note on the HR hub page.
 */
const group = hrSetupGroup('Pay & Benefits');

export default function CompensationSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Pay & Benefits Setup"
        description={group.description}
        backHref="/administration/hr"
      />
      <NavCardGrid items={group.links} />
    </div>
  );
}
