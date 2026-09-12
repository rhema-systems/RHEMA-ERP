'use client';

import { Building2, History, ListTree, FolderTree, Network, Users2 } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';

export default function OrganizationSetupPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Organization"
        description="The HR org backbone: a structure defines its levels, and units are the actual nodes."
      />

      <NavCardGrid
        items={[
          {
            title: 'Structures',
            description: 'Templates that group a set of levels.',
            href: '/administration/hr/organization/structures',
            icon: Building2,
          },
          {
            title: 'Levels',
            description: 'The tiers within a structure — division, department, unit.',
            href: '/administration/hr/organization/levels',
            icon: ListTree,
          },
          {
            title: 'Units',
            description: 'The actual org nodes employees and positions belong to.',
            href: '/administration/hr/organization/units',
            icon: FolderTree,
          },
          {
            title: 'Teams',
            description:
              'Working groups — permanent, project, task force, committee — and who is in them.',
            href: '/administration/hr/organization/teams',
            icon: Users2,
          },
          {
            // Slice 5. The audit trail under the units editor: every restructure and change of head,
            // across the whole organisation. A unit's own log is a tab on the unit itself.
            title: 'Unit Change Log',
            description: 'Who a unit reported to, who headed it, and when each changed.',
            href: '/administration/hr/organization/unit-history',
            icon: History,
          },
          {
            // The read-only counterpart to the three editors above: the same records, drawn.
            // It lives under /hr because HR reads it daily; this card is the way in from the data.
            title: 'Organogram',
            description: 'See the structure drawn — units, posts, reporting lines and sites.',
            href: '/hr/organogram',
            icon: Network,
          },
        ]}
      />
    </div>
  );
}
