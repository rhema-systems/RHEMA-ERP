'use client';

import { Building2 } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';

/**
 * HR settings landing page.
 *
 * Nav parents in the sidebar are clickable, so this group needs a landing rather than a 404.
 * Policy settings joins it in the next slice — the tenant-wide rules (retirement ages, probation
 * length, notice periods, alert lead times, enforcement modes) that eight closed HR areas already
 * read and nobody can currently change.
 */
const items: NavCardItem[] = [
  {
    title: 'Company Profile',
    description:
      'Legal identity, statutory numbers, registered address and the letterhead used on offer and confirmation letters.',
    href: '/administration/hr/settings/company-profile',
    icon: Building2,
  },
];

export default function HrSettingsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="HR Settings"
        description="Tenant-wide HR configuration."
        backHref="/administration/hr"
      />
      <NavCardGrid items={items} />
    </div>
  );
}
