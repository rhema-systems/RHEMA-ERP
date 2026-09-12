'use client';

import { Building2, SlidersHorizontal } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';

/**
 * HR settings landing page.
 *
 * Nav parents in the sidebar are clickable, so this group needs a landing rather than a 404.
 */
const items: NavCardItem[] = [
  {
    title: 'Company Profile',
    description:
      'Legal identity, statutory numbers, registered address and the letterhead used on offer and confirmation letters.',
    href: '/administration/hr/settings/company-profile',
    icon: Building2,
  },
  {
    title: 'Policy Settings',
    description:
      'Retirement ages, probation and notice defaults, reminder lead times, enforcement modes and succession weights.',
    href: '/administration/hr/settings/policy',
    icon: SlidersHorizontal,
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
