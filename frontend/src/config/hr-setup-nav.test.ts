import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

import {
  hrOperationalTrainingLinks,
  hrSetupGroup,
  hrSetupGroups,
  hrSetupLinkHrefs,
  hrSetupNavChildren,
} from './hr-setup-nav';
import { navigationItems, type NavItem } from '../components/layout/sidebar';

const APP_DIR = join(process.cwd(), 'src', 'app');

const routeExists = (href: string) =>
  existsSync(join(APP_DIR, ...href.split('/').filter(Boolean), 'page.tsx'));

const findNavItem = (items: NavItem[], title: string): NavItem | undefined => {
  for (const item of items) {
    if (item.title === title) return item;
    const found = item.children ? findNavItem(item.children, title) : undefined;
    if (found) return found;
  }
  return undefined;
};

describe('HR setup navigation manifest', () => {
  it('gives every link a group, so no settings tile renders without a caption', () => {
    // The regression this guards: /settings/modules/human-resources captions a tile with its
    // ancestor path, so a leaf hanging directly off the HR node rendered with an empty subtitle.
    // Twenty-two of them did.
    expect(hrSetupGroups.length).toBeGreaterThan(0);
    for (const group of hrSetupGroups) {
      expect(group.links.length, `${group.title} has no links`).toBeGreaterThan(0);
      for (const link of group.links) {
        expect(link.description.trim(), `${group.title} → ${link.title}`).not.toBe('');
      }
    }
  });

  it('points every link at a route that exists', () => {
    const missing = [...hrSetupLinkHrefs, ...hrOperationalTrainingLinks.map(l => l.href)]
      .filter(href => !routeExists(href));
    expect(missing).toEqual([]);
  });

  it('points every group at a route that exists', () => {
    expect(hrSetupGroups.filter(group => !routeExists(group.href)).map(g => g.href)).toEqual([]);
  });

  it('lists no screen twice', () => {
    expect(hrSetupLinkHrefs).toEqual([...new Set(hrSetupLinkHrefs)]);
  });

  it("is the sidebar's Administration → HR node, so the two cannot drift", () => {
    const hr = findNavItem(navigationItems, 'HR');
    expect(hr?.href).toBe('/administration/hr');
    expect(hr?.children).toBe(hrSetupNavChildren);
    // Every child of the HR node is a group, never a bare leaf.
    for (const child of hr?.children ?? []) {
      expect(child.children?.length, `${child.title} is a bare leaf`).toBeGreaterThan(0);
    }
  });

  it('keeps the training casework out of setup and in the operational menu', () => {
    // Needs Assessments, Plans and Budgets are per-cycle casework. They must appear under
    // HR → Training and NOT in the setup catalogue.
    for (const link of hrOperationalTrainingLinks) {
      expect(hrSetupLinkHrefs).not.toContain(link.href);
    }

    const training = findNavItem(navigationItems, 'Training');
    const hrefs = training?.children?.map(child => child.href) ?? [];
    for (const link of hrOperationalTrainingLinks) {
      expect(hrefs).toContain(link.href);

      // The pages moved under /hr, which is what let them gate on the training permission
      // instead of the admin.hr grant the /administration layout demands. A link left pointing
      // at the old tree would fail closed for an HR.Training.Read holder who followed it.
      expect(link.href.startsWith('/hr/training/'), link.title).toBe(true);
      const entry = training?.children?.find(child => child.href === link.href);
      expect(entry?.permissions, link.title).toEqual(['HR.Training.Read']);
    }
  });

  it('files destination alerts with the travel desk, not with travel policy', () => {
    // An advisory is raised against a destination, expires and is re-issued as conditions
    // change — the cadence of the trips it warns about. The policies it used to sit beside are
    // set once and stay in Administration.
    expect(hrSetupLinkHrefs).toContain('/administration/hr/travel/policies');
    expect(hrSetupLinkHrefs).not.toContain('/administration/hr/travel/alerts');

    const travel = findNavItem(navigationItems, 'Staff Travel');
    const alerts = travel?.children?.find(child => child.href === '/hr/travel/alerts');
    expect(alerts?.title).toBe('Destination Alerts');
    expect(alerts?.permissions).toEqual(['HR.Travel.Read']);
    expect(routeExists('/hr/travel/alerts')).toBe(true);
  });

  it('leaves nothing behind at the four routes that moved', () => {
    // ⚠ A copy left at the old path is not dead code — Next would route to it, so the screen
    // would still be reachable at an address gated on admin.hr, which is the requirement the
    // move exists to lift. The old tree must be empty, not merely unlinked.
    expect(
      [
        '/administration/hr/training/needs-assessments',
        '/administration/hr/training/plans',
        '/administration/hr/training/budgets',
        '/administration/hr/travel/alerts',
      ].filter(routeExists),
    ).toEqual([]);
  });

  it('surfaces the two groups that had no nav entry at all', () => {
    // Awards had a hub card and no sidebar entry; the Discipline Catalogue sat behind a
    // childless Discipline node. Neither could appear on the settings page.
    expect(hrSetupGroup('Awards').links.map(link => link.href)).toContain(
      '/administration/hr/awards/types',
    );
    expect(hrSetupGroup('Employee Lifecycle Setup').links.map(link => link.href)).toContain(
      '/administration/hr/discipline/catalogue',
    );
  });

  it('keeps the unit change log out of the setup lists', () => {
    // It is the audit trail OF the units configured here, not a thing to configure. It hangs
    // off the Units screen instead; the route itself is unchanged.
    expect(hrSetupLinkHrefs).not.toContain('/administration/hr/organization/unit-history');
    expect(routeExists('/administration/hr/organization/unit-history')).toBe(true);
  });
});
