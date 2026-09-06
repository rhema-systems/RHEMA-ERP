'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { hrSetupGroups } from '@/config/hr-setup-nav';

/**
 * The HR setup hub.
 *
 * ⚠ **The card list is not written here.** It comes from `config/hr-setup-nav.ts`, the same
 * manifest the sidebar's Administration → HR node and `/settings/modules/human-resources` render.
 * This page used to keep its own copy, and the three lists had drifted: Awards had a card and no
 * nav entry, the Discipline Catalogue was unreachable from the nav, and thirteen nav entries had
 * no card. Add a screen to the manifest and all three surfaces pick it up.
 */
export default function HrAdministrationPage() {
  return (
    <div className="space-y-8 p-6">
      <PageHeader
        title="HR Setup"
        description="Structure, job architecture and reference data behind the HR module."
      />

      {hrSetupGroups.map(group => (
        <div key={group.title}>
          <h2 className="text-sm font-medium text-foreground">{group.title}</h2>
          <p className="mb-3 text-sm text-muted-foreground">{group.description}</p>
          <NavCardGrid items={group.links} />
        </div>
      ))}
    </div>
  );
}
