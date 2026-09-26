'use client';

import { History, Network } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';
import { hrSetupGroup } from '@/config/hr-setup-nav';

/**
 * Setup for the org backbone.
 *
 * ⚠ Unit Change Log is no longer a peer card here. It is the audit trail OF the units configured
 * on this page, not a fourth thing to configure, so it belongs to the Units screen — where a
 * unit's own log has always been a tab. The org-wide route is unchanged and is linked below as a
 * read-only view, beside the organogram.
 *
 * Cards come from `config/hr-setup-nav.ts` — see the note on the HR hub page.
 */
const group = hrSetupGroup('Organization');

// The read-only counterparts to the editors above: the same records, drawn and logged.
const views: NavCardItem[] = [
  {
    title: 'Organogram',
    description: 'See the structure drawn — units, posts, reporting lines and sites.',
    href: '/hr/organogram',
    icon: Network,
  },
  {
    title: 'Unit Change Log',
    description: 'Who a unit reported to, who headed it, and when each changed.',
    href: '/administration/hr/organization/unit-history',
    icon: History,
  },
];

export default function OrganizationSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Organization"
        description={group.description}
        backHref="/administration/hr"
      />

      <NavCardGrid items={group.links} />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Views</h2>
        <NavCardGrid items={views} />
      </div>
    </div>
  );
}
